@echo off
setlocal

echo === Build Publish Zip 開始 ===

set SCRIPT_DIR=%~dp0

set PROJECT=%SCRIPT_DIR%StealthStockOverlay\StealthStockOverlay.csproj
set UPDATER_PROJECT=%SCRIPT_DIR%Updater\Updater.csproj

set PUBLISH_DIR=%SCRIPT_DIR%StealthStockOverlay\bin\Release\net8.0-windows\win-x64\publish
set UPDATER_PUBLISH_DIR=%SCRIPT_DIR%Updater\bin\Release\net8.0\win-x64\publish

set OUTPUT_ZIP=%SCRIPT_DIR%app.zip

echo [1/4] Updater 発行
dotnet publish "%UPDATER_PROJECT%" -c Release -r win-x64 --self-contained false
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Updater 発行失敗
    pause
    exit /b 1
)

echo [2/4] 本体 発行
dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained true
if %ERRORLEVEL% neq 0 (
    echo [ERROR] 本体 発行失敗
    pause
    exit /b 1
)

echo [3/4] Updater.exe コピー
if not exist "%UPDATER_PUBLISH_DIR%\Updater.exe" (
    echo [ERROR] Updater.exe が見つかりません: %UPDATER_PUBLISH_DIR%\Updater.exe
    pause
    exit /b 1
)

copy /Y "%UPDATER_PUBLISH_DIR%\Updater.exe" "%PUBLISH_DIR%"
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Updater.exe コピー失敗
    pause
    exit /b 1
)

echo [4/4] app.zip 作成
if exist "%OUTPUT_ZIP%" del /f /q "%OUTPUT_ZIP%"

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
"Compress-Archive -Path '%PUBLISH_DIR%' -DestinationPath '%OUTPUT_ZIP%' -Force"

if exist "%OUTPUT_ZIP%" (
    echo [OK] app.zip 作成完了: %OUTPUT_ZIP%
) else (
    echo [ERROR] app.zip 作成失敗
    pause
    exit /b 1
)

echo === 完了 ===
pause
endlocal