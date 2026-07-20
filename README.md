# Movie Downloader

yt-dlp と ffmpeg を使う、Windows 10/11 向けの C# / WPF ダウンローダーです。旧 Python / Tkinter 版を置き換えるため、.NET 10 LTS で開発しています。

## 開発とビルド

言語は Python から C# に変わっています。Visual Studio は必須ではありません。.NET 10 SDK があれば、PowerShell からビルドできます。

```powershell
.\build-wpf.ps1
```

スクリプトはテストを実行した後、自己完結型 Windows x64 版を `artifacts\publish\win-x64` に作成します。利用者側には .NET や Visual Studio は不要です。

アプリ本体のビルド後、ローカル実行に必要な外部ツールを各公式配布元から取得します。

```powershell
.\setup-tools.ps1
```

## 配布

アプリ本体の標準成果物は次のファイルです。

- `MovieDownloader.exe`

`yt-dlp.exe`、`ffmpeg.exe`、`ffprobe.exe` はライセンス対応を曖昧にしないため、リポジトリおよび標準配布物へ同梱しません。`setup-tools.ps1` が公式配布元から直接取得し、`MovieDownloader.exe` と同じフォルダへ配置します。

外部ツール取得後は `MovieDownloader.exe` を直接起動できます。設定は `%AppData%\YtdlGUI\settings.json` に保存され、初回のみ同じフォルダの旧 `config.ini` から保存先を移行します。第三者ライセンスと再配布時の注意は `THIRD_PARTY_NOTICES.md` を参照してください。

## 主な仕様

- 動画 MP4（H.264 優先）、音声 M4A、音声 MP3
- `YYYYMMDD-title.ext` 形式のファイル名
- URL 入力後の非同期メタデータ取得
- 実進捗、速度、残り時間、キャンセル
- 字幕、サムネイル埋め込み、WAV 変換、ログを詳細設定へ格納（WAV はサムネイル埋め込み対象外）
- 起動時は更新確認のみ。yt-dlp の更新は利用者が明示的に実行
