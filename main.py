import tkinter as tk
from tkinter import messagebox
from tkinter import filedialog
from tkinter import ttk
from tkinter import scrolledtext
import subprocess
import threading
import os
import configparser
import logging
import sys
from typing import Tuple, Optional
from datetime import datetime
from queue import Queue

# 定数定義
CONFIG_FILE = "config.ini"
LOG_FILE = "ytdlgui.log"

# UI定数
WINDOW_TITLE = "Movie Downloader"
WINDOW_GEOMETRY = "600x650"

# カラースキーム
COLOR_BG = "#f5f5f5"
COLOR_FRAME_BG = "#ffffff"
COLOR_PRIMARY = "#2196F3"
COLOR_PRIMARY_HOVER = "#1976D2"
COLOR_DANGER = "#f44336"
COLOR_TEXT = "#212121"
COLOR_TEXT_SECONDARY = "#757575"
COLOR_BORDER = "#e0e0e0"

# フォント設定
FONT_MAIN = ("Segoe UI", 10)
FONT_LABEL = ("Segoe UI", 9)
FONT_BUTTON = ("Segoe UI", 10, "bold")
FONT_CONSOLE = ("Consolas", 9)

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
        self.window.configure(bg=COLOR_BG)
        self.window.resizable(False, False)

        # ウィジェット変数
        self.url_entry: Optional[tk.Entry] = None
        self.output_dir_entry: Optional[tk.Entry] = None
        self.radio_option: Optional[tk.StringVar] = None
        self.pulldown_option: Optional[tk.StringVar] = None
        self.pulldown_menu: Optional[tk.OptionMenu] = None
        self.start_dl_button: Optional[tk.Button] = None
        self.progress_bar: Optional[ttk.Progressbar] = None
        self.console_output: Optional[scrolledtext.ScrolledText] = None

        # 出力キュー（スレッド間通信用）
        self.output_queue: Queue = Queue()

        # 初期化
        self._setup_icon()
        self._setup_style()
        self._setup_keyboard_shortcuts()
        self._update_ytdlp()
        self._create_widgets()

    def _setup_icon(self) -> None:
        '''アイコン設定'''
        try:
            if os.path.exists("ytdlp-icon.ico"):
                self.window.iconbitmap("ytdlp-icon.ico")
        except Exception:
            pass

    def _setup_style(self) -> None:
        '''スタイル設定'''
        style = ttk.Style()
        style.theme_use('clam')

        # プログレスバーのスタイル
        style.configure("Custom.Horizontal.TProgressbar",
                       troughcolor=COLOR_BORDER,
                       background=COLOR_PRIMARY,
                       borderwidth=0,
                       thickness=6)

    def _setup_keyboard_shortcuts(self) -> None:
        '''キーボードショートカット設定'''
        self.window.bind('<Return>', lambda e: self.on_download_button_clicked())
        self.window.bind('<Control-o>', lambda e: self.on_browse_button_clicked())
        self.window.bind('<Control-O>', lambda e: self.on_browse_button_clicked())

    def _update_ytdlp(self) -> None:
        '''yt-dlpの更新（起動時）'''
        try:
            # Windows用のコンソールウィンドウ非表示設定
            startupinfo = None
            creationflags = 0
            if sys.platform == 'win32':
                startupinfo = subprocess.STARTUPINFO()
                startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
                startupinfo.wShowWindow = subprocess.SW_HIDE
                creationflags = subprocess.CREATE_NO_WINDOW

            subprocess.run(["yt-dlp", "--rm-cache-dir"],
                          capture_output=True, text=True, timeout=30,
                          startupinfo=startupinfo, creationflags=creationflags)
            result = subprocess.run(["yt-dlp", "-U", "--no-check-certificate"],
                                  capture_output=True, text=True, timeout=60,
                                  startupinfo=startupinfo, creationflags=creationflags)
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
        progress_frame = tk.Frame(self.window, bg=COLOR_BG)
        progress_frame.pack(pady=(0, 15), padx=20, fill=tk.X)

        self.progress_bar = ttk.Progressbar(progress_frame, mode='indeterminate',
                                           style="Custom.Horizontal.TProgressbar")
        self.progress_bar.pack(fill=tk.X)

        # ダウンロードボタン
        button_frame = tk.Frame(self.window, bg=COLOR_BG)
        button_frame.pack(pady=(0, 20))

        self.start_dl_button = tk.Button(
            button_frame, text="ダウンロード", command=self.on_download_button_clicked,
            font=FONT_BUTTON, bg=COLOR_PRIMARY, fg="white",
            relief=tk.FLAT, padx=40, pady=12, cursor="hand2",
            activebackground=COLOR_PRIMARY_HOVER, activeforeground="white")
        self.start_dl_button.pack()

        # ホバー効果
        self.start_dl_button.bind("<Enter>", lambda e: self.start_dl_button.config(bg=COLOR_PRIMARY_HOVER))
        self.start_dl_button.bind("<Leave>", lambda e: self.start_dl_button.config(
            bg=COLOR_PRIMARY if self.start_dl_button['state'] == tk.NORMAL else COLOR_TEXT_SECONDARY))

        # コンソール出力エリア
        self._create_console_frame()

        # メニューバー
        self._create_menu()

        # 出力キューの監視を開始
        self._check_output_queue()

    def _create_url_frame(self) -> None:
        '''URL入力欄を作成'''
        frame = tk.Frame(self.window, bg=COLOR_FRAME_BG, padx=20, pady=15)
        frame.pack(pady=15, padx=20, fill=tk.X)

        label = tk.Label(frame, text="動画URL", font=FONT_LABEL,
                        bg=COLOR_FRAME_BG, fg=COLOR_TEXT)
        label.pack(anchor=tk.W, pady=(0, 5))

        self.url_entry = tk.Entry(frame, font=FONT_MAIN,
                                  relief=tk.FLAT, bd=1,
                                  highlightthickness=1,
                                  highlightbackground=COLOR_BORDER,
                                  highlightcolor=COLOR_PRIMARY)
        self.url_entry.pack(fill=tk.X, ipady=6)

    def _create_output_dir_frame(self) -> None:
        '''保存先入力欄を作成'''
        frame = tk.Frame(self.window, bg=COLOR_FRAME_BG, padx=20, pady=15)
        frame.pack(pady=(0, 15), padx=20, fill=tk.X)

        label = tk.Label(frame, text="保存先フォルダ", font=FONT_LABEL,
                        bg=COLOR_FRAME_BG, fg=COLOR_TEXT)
        label.pack(anchor=tk.W, pady=(0, 5))

        entry_frame = tk.Frame(frame, bg=COLOR_FRAME_BG)
        entry_frame.pack(fill=tk.X)

        self.output_dir_entry = tk.Entry(entry_frame, font=FONT_MAIN,
                                         relief=tk.FLAT, bd=1,
                                         highlightthickness=1,
                                         highlightbackground=COLOR_BORDER,
                                         highlightcolor=COLOR_PRIMARY)
        self.output_dir_entry.pack(side=tk.LEFT, fill=tk.X, expand=True, ipady=6)

        # デフォルト値の設定
        self._load_output_directory()

        # 参照ボタン
        browse_button = tk.Button(entry_frame, text="参照",
                                 command=self.on_browse_button_clicked,
                                 font=FONT_BUTTON, bg=COLOR_FRAME_BG,
                                 fg=COLOR_TEXT_SECONDARY, relief=tk.FLAT,
                                 padx=15, cursor="hand2", bd=1,
                                 highlightthickness=1,
                                 highlightbackground=COLOR_BORDER)
        browse_button.pack(side=tk.LEFT, padx=(10, 0), ipady=5)

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
        option_frame = tk.Frame(self.window, bg=COLOR_FRAME_BG, padx=20, pady=15)
        option_frame.pack(pady=(0, 15), padx=20, fill=tk.X)

        label = tk.Label(option_frame, text="ダウンロードオプション", font=FONT_LABEL,
                        bg=COLOR_FRAME_BG, fg=COLOR_TEXT)
        label.pack(anchor=tk.W, pady=(0, 10))

        # ラジオボタン
        self.radio_option = tk.StringVar(value=VIDEO_MODE)
        radio_options = [
            "動画をダウンロードする",
            "音声のみをダウンロードする",
        ]

        radio_frame = tk.Frame(option_frame, bg=COLOR_FRAME_BG)
        radio_frame.pack(fill=tk.X)

        for option, option_text in enumerate(radio_options):
            radio_button = tk.Radiobutton(
                radio_frame, text=option_text, variable=self.radio_option,
                value=str(option), command=self.update_pulldown_options,
                font=FONT_MAIN, bg=COLOR_FRAME_BG, fg=COLOR_TEXT,
                activebackground=COLOR_FRAME_BG, cursor="hand2",
                selectcolor=COLOR_FRAME_BG, anchor=tk.W)
            radio_button.pack(anchor=tk.W, pady=3)

        # プルダウンメニュー（音声フォーマット選択用）
        pulldown_frame = tk.Frame(option_frame, bg=COLOR_FRAME_BG)
        pulldown_frame.pack(anchor=tk.W, pady=(5, 0), padx=(20, 0))

        pulldown_label = tk.Label(pulldown_frame, text="音声フォーマット:",
                                 font=FONT_LABEL, bg=COLOR_FRAME_BG,
                                 fg=COLOR_TEXT_SECONDARY)
        pulldown_label.pack(side=tk.LEFT, padx=(0, 10))

        self.pulldown_option = tk.StringVar(value=OPTION_NONE)
        self.pulldown_menu = tk.OptionMenu(pulldown_frame, self.pulldown_option, "")
        self.pulldown_menu.config(font=FONT_MAIN, bg=COLOR_FRAME_BG,
                                 fg=COLOR_TEXT, relief=tk.FLAT, bd=1,
                                 highlightthickness=1,
                                 highlightbackground=COLOR_BORDER,
                                 cursor="hand2", padx=10)
        self.pulldown_menu.pack(side=tk.LEFT)

        # プルダウン初期化
        self.update_pulldown_options()

    def _create_console_frame(self) -> None:
        '''コンソール出力エリアを作成'''
        frame = tk.Frame(self.window, bg=COLOR_FRAME_BG, padx=20, pady=15)
        frame.pack(pady=(0, 20), padx=20, fill=tk.BOTH, expand=True)

        # ヘッダー（ラベルとクリアボタン）
        header_frame = tk.Frame(frame, bg=COLOR_FRAME_BG)
        header_frame.pack(fill=tk.X, pady=(0, 5))

        label = tk.Label(header_frame, text="ダウンロード状況", font=FONT_LABEL,
                        bg=COLOR_FRAME_BG, fg=COLOR_TEXT)
        label.pack(side=tk.LEFT)

        clear_button = tk.Button(header_frame, text="クリア",
                                command=self._clear_console,
                                font=FONT_LABEL, bg=COLOR_FRAME_BG,
                                fg=COLOR_TEXT_SECONDARY, relief=tk.FLAT,
                                cursor="hand2", padx=10, pady=2)
        clear_button.pack(side=tk.RIGHT)

        # スクロール付きテキストエリア
        self.console_output = scrolledtext.ScrolledText(
            frame, font=FONT_CONSOLE, bg="#1e1e1e", fg="#d4d4d4",
            relief=tk.FLAT, bd=1, highlightthickness=1,
            highlightbackground=COLOR_BORDER, wrap=tk.WORD,
            height=10, state=tk.DISABLED)
        self.console_output.pack(fill=tk.BOTH, expand=True)

    def _clear_console(self) -> None:
        '''コンソール出力をクリア'''
        self.console_output.config(state=tk.NORMAL)
        self.console_output.delete(1.0, tk.END)
        self.console_output.config(state=tk.DISABLED)

    def _append_to_console(self, text: str) -> None:
        '''コンソール出力に追記'''
        self.console_output.config(state=tk.NORMAL)
        self.console_output.insert(tk.END, text)
        self.console_output.see(tk.END)  # 自動スクロール
        self.console_output.config(state=tk.DISABLED)

    def _check_output_queue(self) -> None:
        '''出力キューを定期的にチェックしてUIに反映'''
        try:
            while True:
                message = self.output_queue.get_nowait()
                self._append_to_console(message)
        except:
            pass
        # 100msごとにチェック
        self.window.after(100, self._check_output_queue)

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
        self.start_dl_button.config(state=tk.DISABLED, bg=COLOR_TEXT_SECONDARY)
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
        # コンソールに開始メッセージを出力
        self.output_queue.put(f"ダウンロード開始: {video_url}\n")

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
            RETRY_COUNT,
            "--newline"  # 進捗を改行で出力
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
            # Windows用のコンソールウィンドウ非表示設定
            startupinfo = None
            if sys.platform == 'win32':
                startupinfo = subprocess.STARTUPINFO()
                startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
                startupinfo.wShowWindow = subprocess.SW_HIDE

            # プロセス起動（リアルタイム出力）
            process = subprocess.Popen(
                command,
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
                bufsize=1,
                startupinfo=startupinfo,
                creationflags=subprocess.CREATE_NO_WINDOW if sys.platform == 'win32' else 0
            )

            # リアルタイムで出力を読み込み
            for line in process.stdout:
                self.output_queue.put(line)

            # プロセス終了を待機
            return_code = process.wait(timeout=DEFAULT_TIMEOUT)

            if return_code == 0:
                # 成功時にURLをクリア
                self.url_entry.delete(0, tk.END)
                log_info(f"ダウンロード成功: {video_url}")
                self.output_queue.put("\n✓ ダウンロード完了しました！\n\n")
                messagebox.showinfo("ダウンロード完了", "ダウンロードが完了しました！")
            else:
                error_msg = f"エラーコード: {return_code}"
                log_error(f"ダウンロード失敗: {video_url} - {error_msg}")
                self.output_queue.put(f"\n✗ ダウンロードに失敗しました\n\n")
                messagebox.showerror("エラー", f"ダウンロードに失敗しました！\n\n{error_msg}")
        except subprocess.TimeoutExpired:
            log_error(f"ダウンロードタイムアウト: {video_url}")
            self.output_queue.put("\n✗ タイムアウト（10分以上経過）\n\n")
            messagebox.showerror("エラー", "ダウンロードがタイムアウトしました（10分以上経過）")
        except Exception as e:
            log_error(f"ダウンロードエラー: {video_url} - {str(e)}")
            self.output_queue.put(f"\n✗ エラー: {str(e)}\n\n")
            messagebox.showerror("エラー", f"ダウンロードに失敗しました！\n\n{str(e)}")
        finally:
            # ボタンを再度有効化
            self.start_dl_button.config(state=tk.NORMAL, bg=COLOR_PRIMARY)
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
            # Windows用のコンソールウィンドウ非表示設定
            startupinfo = None
            creationflags = 0
            if sys.platform == 'win32':
                startupinfo = subprocess.STARTUPINFO()
                startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
                startupinfo.wShowWindow = subprocess.SW_HIDE
                creationflags = subprocess.CREATE_NO_WINDOW

            result = subprocess.run(["yt-dlp", "--version"],
                                  capture_output=True, text=True, timeout=10,
                                  startupinfo=startupinfo, creationflags=creationflags)
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
