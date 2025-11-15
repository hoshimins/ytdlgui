import tkinter as tk
from tkinter import messagebox
from tkinter import filedialog
from tkinter import ttk
import subprocess
import threading
import os
import configparser
from typing import Tuple, Optional

# 設定ファイルのパス
CONFIG_FILE = "config.ini"


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
        "30",
        "--ignore-errors",
        "--output",
        output_path,
        "--retries",
        "3"
    ]

    # プルダウンリストの値によってオプションを変更
    selected_pulldown_list_option = pulldown_option.get()
    if selected_pulldown_list_option == 'オプションなし':
        ext = "m4a"
    else:
        ext = selected_pulldown_list_option

    # ラジオボタンの値によってオプションを変更
    selected_radio_option = radio_option.get()
    if selected_radio_option == '0':
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
        result = subprocess.run(command, timeout=600, capture_output=True, text=True)

        if result.returncode == 0:
            # 成功時にURLをクリア
            url_entry.delete(0, tk.END)
            messagebox.showinfo("ダウンロード完了", "ダウンロードが完了しました！")
        else:
            error_msg = result.stderr if result.stderr else "不明なエラー"
            messagebox.showerror("エラー", f"ダウンロードに失敗しました！\n\n{error_msg}")
    except subprocess.TimeoutExpired:
        messagebox.showerror("エラー", "ダウンロードがタイムアウトしました（10分以上経過）")
    except Exception as e:
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
    if selected_radio_option == '0':
        pulldown_menu['menu'].add_command(
            label='オプションなし', command=lambda: pulldown_option.set('オプションなし'))
    # audio
    else:
        pulldown_menu['menu'].add_command(
            label='オプションなし', command=lambda: pulldown_option.set('オプションなし'))
        pulldown_menu['menu'].add_command(
            label='m4a', command=lambda: pulldown_option.set('m4a'))
        pulldown_menu['menu'].add_command(
            label='mp3', command=lambda: pulldown_option.set('mp3'))
        pulldown_menu['menu'].add_command(
            label='wav', command=lambda: pulldown_option.set('wav'))


def update_ytdlp() -> None:
    '''yt-dlpの更新'''
    subprocess.run("yt-dlp --rm-cache-dir", shell=True)
    subprocess.run("yt-dlp -U --no-check-certificate", shell=True)


def main() -> None:
    global url_entry, output_dir_entry, radio_option, pulldown_option, pulldown_menu
    global start_dl_button, progress_bar

    # yt-dlpの更新（起動時に実行）
    update_ytdlp()

    # メインウィンドウの作成
    window = tk.Tk()
    window.title("Movie Downloader")
    window.geometry("400x280")

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

    radio_option = tk.StringVar(value="動画をダウンロードする")

    # radio button
    radio_options = [
        "動画をダウンロードする",
        "音声のみをダウンロードする",
    ]

    for option, option_text in enumerate(radio_options):
        radio_button = tk.Radiobutton(option_frame, text=option_text, variable=radio_option,
                                      value=option, command=update_pulldown_options, anchor=tk.W)
        radio_button.grid(row=option, column=0, sticky=tk.W)

    # pulldown list1
    pulldown_option = tk.StringVar(value="オプションなし")
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

    # イベントループの開始
    window.mainloop()


if __name__ == "__main__":
    main()
