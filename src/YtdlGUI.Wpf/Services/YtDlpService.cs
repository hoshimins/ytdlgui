using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using YtdlGUI.Wpf.Models;

namespace YtdlGUI.Wpf.Services;

public sealed class YtDlpService : IYtDlpService
{
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private readonly string _ytDlpPath;
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;

    public YtDlpService(string? baseDirectory = null)
    {
        var directory = baseDirectory ?? AppContext.BaseDirectory;
        _ytDlpPath = Path.Combine(directory, "yt-dlp.exe");
        _ffmpegPath = Path.Combine(directory, "ffmpeg.exe");
        _ffprobePath = Path.Combine(directory, "ffprobe.exe");
    }

    public async Task<VideoMetadata> InspectAsync(string url, CancellationToken cancellationToken)
    {
        EnsureToolsAvailable(requireFfmpeg: false);
        var result = await RunCaptureAsync(
            YtDlpCommandBuilder.BuildInspectArguments(url),
            cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(BuildFailureMessage("URLを解析できませんでした。", result.Error));
        }

        using var document = JsonDocument.Parse(result.Output);
        var root = document.RootElement;
        return new VideoMetadata(
            GetString(root, "title") ?? "タイトル不明",
            GetString(root, "uploader") ?? GetString(root, "channel"),
            GetDuration(root),
            GetString(root, "thumbnail"),
            GetInt(root, "width"),
            GetInt(root, "height"),
            GetUploadDate(root));
    }

    public async Task DownloadAsync(
        DownloadRequest request,
        IProgress<DownloadProgress> progress,
        CancellationToken cancellationToken)
    {
        EnsureToolsAvailable(requireFfmpeg: true);
        var arguments = YtDlpCommandBuilder.BuildDownloadArguments(request, _ffmpegPath);
        var startInfo = CreateStartInfo(arguments);
        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var recentErrors = new ConcurrentQueue<string>();

        if (!process.Start())
        {
            throw new InvalidOperationException("yt-dlpを開始できませんでした。");
        }

        using var registration = cancellationToken.Register(() => TryKill(process));
        var stdoutTask = PumpAsync(process.StandardOutput, progress, recentErrors, false, cancellationToken);
        var stderrTask = PumpAsync(process.StandardError, progress, recentErrors, true, cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(stdoutTask, stderrTask);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        if (process.ExitCode != 0)
        {
            var details = string.Join(Environment.NewLine, recentErrors.TakeLast(8));
            throw new InvalidOperationException(BuildFailureMessage("ダウンロードに失敗しました。", details));
        }

        progress.Report(new DownloadProgress(100, "—", "0秒", null, null, "完了"));
    }

    public async Task<YtDlpVersionInfo> GetVersionInfoAsync(CancellationToken cancellationToken)
    {
        EnsureToolsAvailable(requireFfmpeg: false);
        var local = await RunCaptureAsync(["--version"], cancellationToken);
        var localVersion = local.Output.Trim();
        string? latestVersion = null;

        try
        {
            using var response = await HttpClient.GetAsync(
                "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest",
                cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            latestVersion = GetString(document.RootElement, "tag_name");
        }
        catch (HttpRequestException)
        {
            // オフラインでもローカルバージョンは表示する。
        }

        return new YtDlpVersionInfo(localVersion, latestVersion);
    }

    public async Task<string> UpdateAsync(CancellationToken cancellationToken)
    {
        EnsureToolsAvailable(requireFfmpeg: false);
        var result = await RunCaptureAsync(YtDlpCommandBuilder.BuildUpdateArguments(), cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(BuildFailureMessage("yt-dlpを更新できませんでした。", result.Error));
        }

        return string.IsNullOrWhiteSpace(result.Output) ? "更新が完了しました。" : result.Output.Trim();
    }

    private async Task<(int ExitCode, string Output, string Error)> RunCaptureAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = CreateStartInfo(arguments);
        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("yt-dlpを開始できませんでした。");
        }

        using var registration = cancellationToken.Register(() => TryKill(process));
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
            return (process.ExitCode, await outputTask, await errorTask);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
    }

    private ProcessStartInfo CreateStartInfo(IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _ytDlpPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static async Task PumpAsync(
        StreamReader reader,
        IProgress<DownloadProgress> progress,
        ConcurrentQueue<string> recentErrors,
        bool isError,
        CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (ProgressParser.TryParse(line, out var parsed))
            {
                progress.Report(parsed);
                continue;
            }

            progress.Report(new DownloadProgress(0, "—", "—", null, null, "処理中", line));
            if (isError || line.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
            {
                recentErrors.Enqueue(line);
                while (recentErrors.Count > 40)
                {
                    recentErrors.TryDequeue(out _);
                }
            }
        }
    }

    private void EnsureToolsAvailable(bool requireFfmpeg)
    {
        if (!File.Exists(_ytDlpPath))
        {
            throw new FileNotFoundException(
                "yt-dlp.exeがアプリと同じフォルダにありません。リポジトリのsetup-tools.ps1で取得できます。",
                _ytDlpPath);
        }

        if (requireFfmpeg && !File.Exists(_ffmpegPath))
        {
            throw new FileNotFoundException(
                "ffmpeg.exeがアプリと同じフォルダにありません。リポジトリのsetup-tools.ps1で取得できます。",
                _ffmpegPath);
        }

        if (requireFfmpeg && !File.Exists(_ffprobePath))
        {
            throw new FileNotFoundException(
                "ffprobe.exeがアプリと同じフォルダにありません。リポジトリのsetup-tools.ps1で取得できます。",
                _ffprobePath);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("YtdlGUI", "2.0"));
        return client;
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static int? GetInt(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.Number &&
        property.TryGetInt32(out var value)
            ? value
            : null;

    private static TimeSpan? GetDuration(JsonElement element) =>
        element.TryGetProperty("duration", out var property) &&
        property.ValueKind == JsonValueKind.Number &&
        property.TryGetDouble(out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : null;

    private static DateOnly? GetUploadDate(JsonElement element)
    {
        var value = GetString(element, "upload_date");
        return DateOnly.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    private static string BuildFailureMessage(string summary, string details) =>
        string.IsNullOrWhiteSpace(details) ? summary : $"{summary}{Environment.NewLine}{details.Trim()}";
}
