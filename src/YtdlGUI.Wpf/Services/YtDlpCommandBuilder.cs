using YtdlGUI.Wpf.Models;

namespace YtdlGUI.Wpf.Services;

public static class YtDlpCommandBuilder
{
    internal const string ProgressPrefix = "__YTDLGUI__";

    public static IReadOnlyList<string> BuildInspectArguments(string url) =>
    [
        "--dump-single-json",
        "--no-playlist",
        "--no-warnings",
        url
    ];

    public static IReadOnlyList<string> BuildDownloadArguments(
        DownloadRequest request,
        string ffmpegPath)
    {
        var outputTemplate = Path.Combine(
            request.OutputDirectory,
            "%(upload_date)s-%(title)s.%(ext)s");

        var arguments = new List<string>
        {
            request.Url,
            "--ffmpeg-location", ffmpegPath,
            "--socket-timeout", "30",
            "--retries", "3",
            "--newline",
            "--no-playlist",
            "--output", outputTemplate,
            "--progress-template",
            $"download:{ProgressPrefix}|%(progress._percent_str)s|%(progress._speed_str)s|%(progress._eta_str)s|%(progress.downloaded_bytes)s|%(progress.total_bytes_estimate)s"
        };

        if (request.EmbedThumbnail)
        {
            arguments.Add("--embed-thumbnail");
        }

        if (request.DownloadSubtitles && request.Preset == DownloadPreset.VideoMp4)
        {
            arguments.AddRange(["--write-subs", "--sub-langs", "ja,en", "--embed-subs"]);
        }

        switch (request.Preset)
        {
            case DownloadPreset.VideoMp4:
                arguments.AddRange(
                [
                    "-S", "vcodec:h264,res,acodec:m4a",
                    "--merge-output-format", "mp4",
                    "-f", "bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best"
                ]);
                break;
            case DownloadPreset.AudioM4a:
                AddAudioArguments(arguments, request.ConvertAudioToWav ? "wav" : "m4a");
                break;
            case DownloadPreset.AudioMp3:
                AddAudioArguments(arguments, request.ConvertAudioToWav ? "wav" : "mp3");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request.Preset));
        }

        return arguments;
    }

    private static void AddAudioArguments(List<string> arguments, string format)
    {
        arguments.AddRange(
        [
            "-f", "bestaudio[ext=m4a]/bestaudio/best",
            "--extract-audio",
            "--audio-format", format
        ]);
    }
}
