import tkinter as tk
from tkinter import messagebox
from tkinter import filedialog
from tkinter import ttk
import subprocess
import threading
import os
import configparser
import logging
from typing import Tuple, Optional
from datetime import datetime

# 定数定義
CONFIG_FILE = "config.ini"
LOG_FILE = "ytdlgui.log"

# UI定数
WINDOW_TITLE = "Movie Downloader"
WINDOW_GEOMETRY = "400x280"

# オプション定数
OPTION_NONE = "オプションなし"
VIDEO_MODE = "0"
AUDIO_MODE = "1"

# オーディオフォーマット
AUDIO_FORMATS = ["m4a", "mp3", "wav"]

# yt-dlp定数
DEFAULT_TIMEOUT = 600  # 10分
SOCKET_TIMEOUT = "30"
RETRY_COUNT = "3"

# ロギング設定
logging.basicConfig(
    filename=LOG_FILE,
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    encoding='utf-8'
)


def log_info(message: str) -> None:
    '''情報ログを記録'''
    logging.info(message)


def log_error(message: str) -> None:
    '''エラーログを記録'''
    logging.error(message)


def load_config() -> configparser.ConfigParser:
    '''設定ファイルを読み込む'''
    config = configparser.ConfigParser()
    if os.path.exists(CONFIG_FILE):
        config.read(CONFIG_FILE, encoding='utf-8')
    return config


def save_config(output_dir: str) -> None:
    '''設定ファイルに保存先を保存する'''
    config = configparser.ConfigParser()
    config['Settings'] = {
        'output_directory': output_dir
    }
    with open(CONFIG_FILE, 'w', encoding='utf-8') as f:
        config.write(f)


def on_clicked_start_dl_button() -> None:
    '''DLボタンが押された時の処理'''
    global url_entry, output_dir_entry, radio_option, pulldown_option
    global start_dl_button, progress_bar

    video_url = url_entry.get().strip()

    # URL入力のバリデーション
    if not video_url:
        messagebox.showerror("エラー", "URLを入力してください")
        return

    # 出力ディレクトリのバリデーション
    output_dir = output_dir_entry.get().strip()
    if not output_dir:
        messagebox.showerror("エラー", "保存先フォルダを指定してください")
        return

    if not os.path.exists(output_dir):
        messagebox.showerror("エラー", "指定された保存先フォルダが存在しません")
        return

    # ボタンを無効化
    start_dl_button.config(state=tk.DISABLED)
    progress_bar.start()

    # 出力先を設定ファイルに保存
    save_config(output_dir)

    # ログ記録
    log_info(f"ダウンロード開始: URL={video_url}, 出力先={output_dir}")

    # 別スレッドでダウンロード実行
    download_thread = threading.Thread(
        target=download_video,
        args=(video_url, output_dir),
        daemon=True
    )
    download_thread.start()


def download_video(video_url: str, output_dir: str) -> None:
    '''ダウンロード処理（別スレッド用）'''
    global url_entry, radio_option, pulldown_option
    global start_dl_button, progress_bar

    # 出力パスの生成
    output_file_name = r"\%(upload_date)s-%(title)s.%(ext)s"
    output_path = os.path.join(output_dir, output_file_name.lstrip("\\"))

    # DLコマンドの生成
    command = [
        "yt-dlp",
        video_url,
        "--embed-thumbnail",
        "--socket-timeout",
        SOCKET_TIMEOUT,
        "--ignore-errors",
        "--output",
        output_path,
        "--retries",
        RETRY_COUNT
    ]

    # プルダウンリストの値によってオプションを変更
    selected_pulldown_list_option = pulldown_option.get()
    if selected_pulldown_list_option == OPTION_NONE:
        ext = "m4a"
    else:
        ext = selected_pulldown_list_option

    # ラジオボタンの値によってオプションを変更
    selected_radio_option = radio_option.get()
    if selected_radio_option == VIDEO_MODE:
        command.append("-f")
        command.append(
            "bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best")
    else:
        command.append("-f")
        command.append(
            "bestaudio[ext=m4a]/bestaudio/best")
        # 音声のみを抽出
        command.append("--extract-audio")
        command.append("--audio-format")
        command.append(ext)

    # エラーハンドリング
    try:
        result = subprocess.run(command, timeout=DEFAULT_TIMEOUT, capture_output=True, text=True)

        if result.returncode == 0:
            # 成功時にURLをクリア
            url_entry.delete(0, tk.END)
            log_info(f"ダウンロード成功: {video_url}")
            messagebox.showinfo("ダウンロード完了", "ダウンロードが完了しました！")
        else:
            error_msg = result.stderr if result.stderr else "不明なエラー"
            log_error(f"ダウンロード失敗: {video_url} - {error_msg}")
            messagebox.showerror("エラー", f"ダウンロードに失敗しました！\n\n{error_msg}")
    except subprocess.TimeoutExpired:
        log_error(f"ダウンロードタイムアウト: {video_url}")
        messagebox.showerror("エラー", "ダウンロードがタイムアウトしました（10分以上経過）")
    except Exception as e:
        log_error(f"ダウンロードエラー: {video_url} - {str(e)}")
        messagebox.showerror("エラー", f"ダウンロードに失敗しました！\n\n{str(e)}")
    finally:
        # ボタンを再度有効化
        start_dl_button.config(state=tk.NORMAL)
        progress_bar.stop()


def on_clicked_browse_button() -> None:
    '''参照ボタンが押された時の処理'''
    global output_dir_entry

    directory_path = filedialog.askdirectory()
    if directory_path:
        output_dir_entry.delete(0, tk.END)
        output_dir_entry.insert(tk.END, directory_path)
        # 選択した出力先を設定ファイルに保存
        save_config(directory_path)


def create_label_entry_frame(
    parent: tk.Widget,
    label_text: str,
    entry_width: int,
    padding_x: Tuple[int, int] = (0, 0)
) -> Tuple[tk.Frame, tk.Entry]:
    '''ラベルと入力欄を持つframeを作成する'''
    frame = tk.Frame(parent)
    frame.pack(pady=10)

    label = tk.Label(frame, text=label_text)
    label.grid(row=0, column=0, sticky=tk.W)

    entry = tk.Entry(frame, width=entry_width)
    entry.grid(row=0, column=1, padx=(padding_x[0], padding_x[1]))

    return frame, entry


def update_pulldown_options() -> None:
    '''pulldown listのオプションを更新する'''
    global radio_option, pulldown_option, pulldown_menu
    selected_radio_option = radio_option.get()

    # 既存オプションの削除
    pulldown_menu['menu'].delete(0, 'end')

    # movie
    if selected_radio_option == VIDEO_MODE:
        pulldown_menu['menu'].add_command(
            label=OPTION_NONE, command=lambda: pulldown_option.set(OPTION_NONE))
    # audio
    else:
        pulldown_menu['menu'].add_command(
            label=OPTION_NONE, command=lambda: pulldown_option.set(OPTION_NONE))
        for fmt in AUDIO_FORMATS:
            pulldown_menu['menu'].add_command(
                label=fmt, command=lambda f=fmt: pulldown_option.set(f))


def get_ytdlp_version() -> str:
    '''yt-dlpのバージョンを取得'''
    try:
        result = subprocess.run("yt-dlp --version", shell=True,
                              capture_output=True, text=True, timeout=10)
        if result.returncode == 0:
            return result.stdout.strip()
        return "不明"
    except Exception:
        return "不明"


def show_version_info() -> None:
    '''バージョン情報を表示'''
    version = get_ytdlp_version()
    messagebox.showinfo("バージョン情報",
                       f"Movie Downloader v1.0\n\nyt-dlp: {version}")


def update_ytdlp() -> None:
    '''yt-dlpの更新'''
    try:
        # キャッシュクリア
        result = subprocess.run("yt-dlp --rm-cache-dir", shell=True,
                              capture_output=True, text=True, timeout=30)

        # 更新実行
        result = subprocess.run("yt-dlp -U --no-check-certificate", shell=True,
                              capture_output=True, text=True, timeout=60)

        if result.returncode != 0 and result.stderr:
            # エラーがあっても継続（既に最新版の場合など）
            print(f"yt-dlp update info: {result.stderr}")
    except subprocess.TimeoutExpired:
        print("yt-dlp update timeout")
    except Exception as e:
        print(f"yt-dlp update error: {e}")


def main() -> None:
    global url_entry, output_dir_entry, radio_option, pulldown_option, pulldown_menu
    global start_dl_button, progress_bar

    # yt-dlpの更新（起動時に実行）
    update_ytdlp()

    # メインウィンドウの作成
    window = tk.Tk()
    window.title(WINDOW_TITLE)
    window.geometry(WINDOW_GEOMETRY)

    # アイコン設定（icon.icoが存在する場合）
    try:
        if os.path.exists("icon.ico"):
            window.iconbitmap("icon.ico")
    except Exception:
        pass  # アイコン設定失敗は無視

    # キーボードショートカット
    window.bind('<Return>', lambda e: on_clicked_start_dl_button())
    window.bind('<Control-o>', lambda e: on_clicked_browse_button())
    window.bind('<Control-O>', lambda e: on_clicked_browse_button())

    # URL入力欄のframe
    input_url_frame, url_entry = create_label_entry_frame(
        window, "URL:", 50, (0, 42))

    # 保存先入力欄のframe
    output_dir_frame, output_dir_entry = create_label_entry_frame(
        window, "OUT:", 50)

    # 設定ファイルから前回の出力先を読み込む
    config = load_config()
    saved_output = config.get('Settings', 'output_directory', fallback=None)

    if saved_output and os.path.exists(saved_output):
        # 前回の出力先が存在する場合
        output_dir_entry.insert(0, saved_output)
    else:
        # デフォルトの出力先を設定
        default_output = os.path.join(os.path.expanduser("~"), "Downloads")
        if os.path.exists(default_output):
            output_dir_entry.insert(0, default_output)

    # 参照ボタン
    browse_button = tk.Button(output_dir_frame, text="参照",
                              command=on_clicked_browse_button)
    browse_button.grid(row=0, column=2, padx=(5, 0))

    # * オプションのframe
    option_frame = tk.Frame(window)
    option_frame.pack(pady=10)

    radio_option = tk.StringVar(value=VIDEO_MODE)

    # radio button
    radio_options = [
        "動画をダウンロードする",
        "音声のみをダウンロードする",
    ]

    for option, option_text in enumerate(radio_options):
        radio_button = tk.Radiobutton(option_frame, text=option_text, variable=radio_option,
                                      value=str(option), command=update_pulldown_options, anchor=tk.W)
        radio_button.grid(row=option, column=0, sticky=tk.W)

    # pulldown list1
    pulldown_option = tk.StringVar(value=OPTION_NONE)
    pulldown_menu = tk.OptionMenu(option_frame, pulldown_option, "")
    pulldown_menu.grid(row=1, column=1, padx=20)

    # プルダウンメニューの初期化
    update_pulldown_options()

    # 進捗バー
    progress_bar = ttk.Progressbar(window, mode='indeterminate')
    progress_bar.pack(pady=5, padx=20, fill=tk.X)

    # DLボタン
    start_dl_button = tk.Button(
        window, text="ダウンロード", command=on_clicked_start_dl_button)
    start_dl_button.pack(pady=10)

    # メニューバー（更新機能）
    menubar = tk.Menu(window)
    window.config(menu=menubar)

    tools_menu = tk.Menu(menubar, tearoff=0)
    menubar.add_cascade(label="ツール", menu=tools_menu)
    tools_menu.add_command(label="yt-dlp更新", command=lambda: threading.Thread(target=update_ytdlp, daemon=True).start())
    tools_menu.add_command(label="バージョン情報", command=show_version_info)

    # イベントループの開始
    window.mainloop()


if __name__ == "__main__":
    main()
