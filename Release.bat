@echo off
setlocal

echo === Build Publish Zip 開始 ===
echo.

set /p NEW_VERSION=バージョンを入力してください（例: 1.1.2）:
if "%NEW_VERSION%"=="" (
    echo [ERROR] バージョン未入力
    pause
    exit /b 1
)

set "SCRIPT_DIR=%~dp0"
set "PROJECT=%SCRIPT_DIR%StealthStockOverlay\StealthStockOverlay.csproj"
set "UPDATER_PROJECT=%SCRIPT_DIR%Updater\Updater.csproj"
set "VERSION_JSON=%SCRIPT_DIR%version.json"

set "PUBLISH_DIR=%SCRIPT_DIR%StealthStockOverlay\bin\Release\net8.0-windows\win-x64\publish"
set "UPDATER_PUBLISH_DIR=%SCRIPT_DIR%Updater\bin\Release\net8.0\win-x64\publish"
set "OUTPUT_ZIP=%SCRIPT_DIR%app.zip"
set "LOG_FILE=%SCRIPT_DIR%Release.log"

echo === Build Publish Zip 開始 === > "%LOG_FILE%"
echo [INFO] version=%NEW_VERSION% >> "%LOG_FILE%"
echo [INFO] project=%PROJECT% >> "%LOG_FILE%"
echo [INFO] updater=%UPDATER_PROJECT% >> "%LOG_FILE%"
echo. >> "%LOG_FILE%"

echo [0/6] version.json 更新
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$obj = @{ latest = '%NEW_VERSION%'; url = 'https://raw.githubusercontent.com/Chairman-bits/SystemMonitor/main/app.zip' }; $obj | ConvertTo-Json -Compress | Set-Content -Path '%VERSION_JSON%' -Encoding UTF8" >> "%LOG_FILE%" 2>&1

if errorlevel 1 (
    echo [ERROR] version.json 更新失敗
    pause
    exit /b 1
)

echo [1/6] Updater 発行
dotnet publish "%UPDATER_PROJECT%" -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:Version=%NEW_VERSION% /p:FileVersion=%NEW_VERSION%.0 /p:AssemblyVersion=1.0.0.0 /p:InformationalVersion=%NEW_VERSION% /p:DebugType=None /p:DebugSymbols=false >> "%LOG_FILE%" 2>&1
if errorlevel 1 (
    echo [ERROR] Updater 発行失敗
    pause
    exit /b 1
)

echo [2/6] 本体 発行
dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:Version=%NEW_VERSION% /p:FileVersion=%NEW_VERSION%.0 /p:AssemblyVersion=1.0.0.0 /p:InformationalVersion=%NEW_VERSION% /p:DebugType=None /p:DebugSymbols=false >> "%LOG_FILE%" 2>&1
if errorlevel 1 (
    echo [ERROR] 本体 発行失敗
    pause
    exit /b 1
)

echo [3/6] publish確認
if not exist "%PUBLISH_DIR%\SystemMonitorHelper.exe" (
    echo [ERROR] publish に SystemMonitorHelper.exe がありません
    pause
    exit /b 1
)

if not exist "%UPDATER_PUBLISH_DIR%\Updater.exe" (
    echo [ERROR] publish に Updater.exe がありません
    pause
    exit /b 1
)

echo [4/6] dist 作成
set "DIST_DIR=%SCRIPT_DIR%dist"

if exist "%DIST_DIR%" rmdir /s /q "%DIST_DIR%"
mkdir "%DIST_DIR%"

copy "%PUBLISH_DIR%\SystemMonitorHelper.exe" "%DIST_DIR%" >nul

echo [5/6] app.zip 作成
if exist "%OUTPUT_ZIP%" del /f /q "%OUTPUT_ZIP%"

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "Compress-Archive -Path '%PUBLISH_DIR%\SystemMonitorHelper.exe','%UPDATER_PUBLISH_DIR%\Updater.exe' -DestinationPath '%OUTPUT_ZIP%' -Force" >> "%LOG_FILE%" 2>&1

if not exist "%OUTPUT_ZIP%" (
    echo [ERROR] app.zip 作成失敗
    pause
    exit /b 1
)

echo [6/6] 完了
echo [OK] version=%NEW_VERSION%
echo [OK] app.zip=%OUTPUT_ZIP%
pause
endlocal