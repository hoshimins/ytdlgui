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


class DownloaderApp:
    '''Movie Downloader アプリケーションクラス'''

    def __init__(self):
        self.window = tk.Tk()
        self.window.title(WINDOW_TITLE)
        self.window.geometry(WINDOW_GEOMETRY)

        # ウィジェット変数
        self.url_entry: Optional[tk.Entry] = None
        self.output_dir_entry: Optional[tk.Entry] = None
        self.radio_option: Optional[tk.StringVar] = None
        self.pulldown_option: Optional[tk.StringVar] = None
        self.pulldown_menu: Optional[tk.OptionMenu] = None
        self.start_dl_button: Optional[tk.Button] = None
        self.progress_bar: Optional[ttk.Progressbar] = None

        # 初期化
        self._setup_icon()
        self._setup_keyboard_shortcuts()
        self._update_ytdlp()
        self._create_widgets()

    def _setup_icon(self) -> None:
        '''アイコン設定'''
        try:
            if os.path.exists("icon.ico"):
                self.window.iconbitmap("icon.ico")
        except Exception:
            pass

    def _setup_keyboard_shortcuts(self) -> None:
        '''キーボードショートカット設定'''
        self.window.bind('<Return>', lambda e: self.on_download_button_clicked())
        self.window.bind('<Control-o>', lambda e: self.on_browse_button_clicked())
        self.window.bind('<Control-O>', lambda e: self.on_browse_button_clicked())

    def _update_ytdlp(self) -> None:
        '''yt-dlpの更新（起動時）'''
        try:
            subprocess.run("yt-dlp --rm-cache-dir", shell=True,
                          capture_output=True, text=True, timeout=30)
            result = subprocess.run("yt-dlp -U --no-check-certificate", shell=True,
                                  capture_output=True, text=True, timeout=60)
            if result.returncode != 0 and result.stderr:
                print(f"yt-dlp update info: {result.stderr}")
        except subprocess.TimeoutExpired:
            print("yt-dlp update timeout")
        except Exception as e:
            print(f"yt-dlp update error: {e}")

    def _create_widgets(self) -> None:
        '''ウィジェット作成'''
        # URL入力欄
        self._create_url_frame()

        # 保存先入力欄
        self._create_output_dir_frame()

        # オプションフレーム
        self._create_option_frame()

        # 進捗バー
        self.progress_bar = ttk.Progressbar(self.window, mode='indeterminate')
        self.progress_bar.pack(pady=5, padx=20, fill=tk.X)

        # ダウンロードボタン
        self.start_dl_button = tk.Button(
            self.window, text="ダウンロード", command=self.on_download_button_clicked)
        self.start_dl_button.pack(pady=10)

        # メニューバー
        self._create_menu()

    def _create_url_frame(self) -> None:
        '''URL入力欄を作成'''
        frame = tk.Frame(self.window)
        frame.pack(pady=10)

        label = tk.Label(frame, text="URL:")
        label.grid(row=0, column=0, sticky=tk.W)

        self.url_entry = tk.Entry(frame, width=50)
        self.url_entry.grid(row=0, column=1, padx=(0, 42))

    def _create_output_dir_frame(self) -> None:
        '''保存先入力欄を作成'''
        frame = tk.Frame(self.window)
        frame.pack(pady=10)

        label = tk.Label(frame, text="OUT:")
        label.grid(row=0, column=0, sticky=tk.W)

        self.output_dir_entry = tk.Entry(frame, width=50)
        self.output_dir_entry.grid(row=0, column=1)

        # デフォルト値の設定
        self._load_output_directory()

        # 参照ボタン
        browse_button = tk.Button(frame, text="参照",
                                 command=self.on_browse_button_clicked)
        browse_button.grid(row=0, column=2, padx=(5, 0))

    def _load_output_directory(self) -> None:
        '''出力先ディレクトリを読み込み'''
        config = self._load_config()
        saved_output = config.get('Settings', 'output_directory', fallback=None)

        if saved_output and os.path.exists(saved_output):
            self.output_dir_entry.insert(0, saved_output)
        else:
            default_output = os.path.join(os.path.expanduser("~"), "Downloads")
            if os.path.exists(default_output):
                self.output_dir_entry.insert(0, default_output)

    def _create_option_frame(self) -> None:
        '''オプションフレームを作成'''
        option_frame = tk.Frame(self.window)
        option_frame.pack(pady=10)

        # ラジオボタン
        self.radio_option = tk.StringVar(value=VIDEO_MODE)
        radio_options = [
            "動画をダウンロードする",
            "音声のみをダウンロードする",
        ]

        for option, option_text in enumerate(radio_options):
            radio_button = tk.Radiobutton(
                option_frame, text=option_text, variable=self.radio_option,
                value=str(option), command=self.update_pulldown_options, anchor=tk.W)
            radio_button.grid(row=option, column=0, sticky=tk.W)

        # プルダウンメニュー
        self.pulldown_option = tk.StringVar(value=OPTION_NONE)
        self.pulldown_menu = tk.OptionMenu(option_frame, self.pulldown_option, "")
        self.pulldown_menu.grid(row=1, column=1, padx=20)

        # プルダウン初期化
        self.update_pulldown_options()

    def _create_menu(self) -> None:
        '''メニューバーを作成'''
        menubar = tk.Menu(self.window)
        self.window.config(menu=menubar)

        tools_menu = tk.Menu(menubar, tearoff=0)
        menubar.add_cascade(label="ツール", menu=tools_menu)
        tools_menu.add_command(label="yt-dlp更新",
                              command=lambda: threading.Thread(
                                  target=self._update_ytdlp, daemon=True).start())
        tools_menu.add_command(label="バージョン情報", command=self.show_version_info)

    def update_pulldown_options(self) -> None:
        '''プルダウンリストのオプションを更新'''
        selected_radio_option = self.radio_option.get()

        # 既存オプションの削除
        self.pulldown_menu['menu'].delete(0, 'end')

        if selected_radio_option == VIDEO_MODE:
            self.pulldown_menu['menu'].add_command(
                label=OPTION_NONE, command=lambda: self.pulldown_option.set(OPTION_NONE))
        else:
            self.pulldown_menu['menu'].add_command(
                label=OPTION_NONE, command=lambda: self.pulldown_option.set(OPTION_NONE))
            for fmt in AUDIO_FORMATS:
                self.pulldown_menu['menu'].add_command(
                    label=fmt, command=lambda f=fmt: self.pulldown_option.set(f))

    def on_download_button_clicked(self) -> None:
        '''ダウンロードボタンが押された時の処理'''
        video_url = self.url_entry.get().strip()

        # URL入力のバリデーション
        if not video_url:
            messagebox.showerror("エラー", "URLを入力してください")
            return

        # 出力ディレクトリのバリデーション
        output_dir = self.output_dir_entry.get().strip()
        if not output_dir:
            messagebox.showerror("エラー", "保存先フォルダを指定してください")
            return

        if not os.path.exists(output_dir):
            messagebox.showerror("エラー", "指定された保存先フォルダが存在しません")
            return

        # ボタンを無効化
        self.start_dl_button.config(state=tk.DISABLED)
        self.progress_bar.start()

        # 出力先を設定ファイルに保存
        self._save_config(output_dir)

        # ログ記録
        log_info(f"ダウンロード開始: URL={video_url}, 出力先={output_dir}")

        # 別スレッドでダウンロード実行
        download_thread = threading.Thread(
            target=self._download_video,
            args=(video_url, output_dir),
            daemon=True
        )
        download_thread.start()

    def _download_video(self, video_url: str, output_dir: str) -> None:
        '''ダウンロード処理（別スレッド用）'''
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
        selected_pulldown_list_option = self.pulldown_option.get()
        if selected_pulldown_list_option == OPTION_NONE:
            ext = "m4a"
        else:
            ext = selected_pulldown_list_option

        # ラジオボタンの値によってオプションを変更
        selected_radio_option = self.radio_option.get()
        if selected_radio_option == VIDEO_MODE:
            command.append("-f")
            command.append(
                "bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best")
        else:
            command.append("-f")
            command.append(
                "bestaudio[ext=m4a]/bestaudio/best")
            command.append("--extract-audio")
            command.append("--audio-format")
            command.append(ext)

        # エラーハンドリング
        try:
            result = subprocess.run(command, timeout=DEFAULT_TIMEOUT,
                                  capture_output=True, text=True)

            if result.returncode == 0:
                # 成功時にURLをクリア
                self.url_entry.delete(0, tk.END)
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
            self.start_dl_button.config(state=tk.NORMAL)
            self.progress_bar.stop()

    def on_browse_button_clicked(self) -> None:
        '''参照ボタンが押された時の処理'''
        directory_path = filedialog.askdirectory()
        if directory_path:
            self.output_dir_entry.delete(0, tk.END)
            self.output_dir_entry.insert(tk.END, directory_path)
            # 選択した出力先を設定ファイルに保存
            self._save_config(directory_path)

    def _load_config(self) -> configparser.ConfigParser:
        '''設定ファイルを読み込む'''
        config = configparser.ConfigParser()
        if os.path.exists(CONFIG_FILE):
            config.read(CONFIG_FILE, encoding='utf-8')
        return config

    def _save_config(self, output_dir: str) -> None:
        '''設定ファイルに保存先を保存する'''
        config = configparser.ConfigParser()
        config['Settings'] = {
            'output_directory': output_dir
        }
        with open(CONFIG_FILE, 'w', encoding='utf-8') as f:
            config.write(f)

    def get_ytdlp_version(self) -> str:
        '''yt-dlpのバージョンを取得'''
        try:
            result = subprocess.run("yt-dlp --version", shell=True,
                                  capture_output=True, text=True, timeout=10)
            if result.returncode == 0:
                return result.stdout.strip()
            return "不明"
        except Exception:
            return "不明"

    def show_version_info(self) -> None:
        '''バージョン情報を表示'''
        version = self.get_ytdlp_version()
        messagebox.showinfo("バージョン情報",
                           f"Movie Downloader v1.0\n\nyt-dlp: {version}")

    def run(self) -> None:
        '''アプリケーションを実行'''
        self.window.mainloop()


if __name__ == "__main__":
    app = DownloaderApp()
    app.run()
