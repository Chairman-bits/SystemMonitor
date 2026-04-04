# 🚀 SystemMonitor / StealthStockOverlay リリース手順

このドキュメントでは、アプリのビルド〜配布（GitHub公開）〜自動アップデートまでの一連の手順を説明します。

---

# 🎯 全体の流れ

```text
① バッチ実行（ビルド＋発行＋zip作成）
② app.zip を GitHub にアップロード
③ version.json を更新
④ アプリ起動で自動更新確認
```

---

# 🟢 事前準備（初回のみ）

## ① フォルダ構成

```text
同一階層
├ CreateAppZip.bat
├ StealthStockOverlay
│  └ StealthStockOverlay.csproj
└ Updater
   └ Updater.csproj
```

---

## ② version.txt を作成

配置場所：

```text
StealthStockOverlay プロジェクト内
```

中身：

```text
1.0.1
```

プロパティ設定：

```text
ビルド アクション: Content
出力ディレクトリにコピー: 常にコピー
```

---

## ③ AutoUpdater 実装済みであること

起動時に以下が呼ばれていること：

```csharp
await AutoUpdater.CheckAsync();
```

---

# 🟢 手順①：ビルド＋zip作成

## 実行方法

```text
CreateAppZip.bat をダブルクリック
```

---

## 内部処理

```text
① Updater 発行
② 本体 発行
③ Updater.exe 自動コピー
④ app.zip 自動作成
```

---

## 成果物

```text
app.zip
```

---

# 🟢 手順②：GitHub にアップロード

対象リポジトリ：

```text
Chairman-bits/SystemMonitor
```

---

## 配置（main ブランチ）

```text
main
├ app.zip
└ version.json
```

---

## version.json 作成

```json
{
  "version": "1.0.1",
  "url": "https://raw.githubusercontent.com/Chairman-bits/SystemMonitor/main/app.zip"
}
```

---

# 🟢 手順③：動作確認

## 確認①（ブラウザ）

```text
https://raw.githubusercontent.com/Chairman-bits/SystemMonitor/main/version.json
```

正常なら JSON が表示される

---

## 確認②（アプリ）

1. アプリ起動
2. 更新ダイアログ表示

```text
新しいバージョンがあります
```

---

# 🟢 手順④：アップデートテスト方法

## GitHub 側

```json
"version": "1.0.2"
```

## ローカル

```text
version.txt = 1.0.1
```

👉 必ず更新が走る

---

# 🔁 次回以降のリリース手順

```text
① version.txt を更新（例：1.0.2）
② CreateAppZip.bat 実行
③ app.zip を GitHub に上書き
④ version.json の version を更新
```

---

# ❗注意点

## ① zip構造

OK：

```text
app.zip
└ win-x64
   ├ exe
```

（Updaterが対応済み）

---

## ② version不一致

```text
version.txt ≠ version.json
```

でないと更新されない

---

## ③ raw URL 必須

```text
raw.githubusercontent.com
```

---

# 🎯 最終状態

* 自動ビルド
* 自動zip生成
* GitHub配布
* 自動アップデート

👉 完全自動リリース環境

---

# 🚀 今後の改善（任意）

* バージョン表示UI
* 更新履歴表示
* 強制アップデート
* GitHub自動アップロード

---
