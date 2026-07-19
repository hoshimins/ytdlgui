namespace YtdlGUI.Wpf.Models;

public enum DownloadPreset
{
    VideoMp4,
    AudioM4a,
    AudioMp3
}

public sealed record DownloadRequest(
    string Url,
    DownloadPreset Preset,
    string OutputDirectory,
    bool EmbedThumbnail,
    bool DownloadSubtitles,
    bool ConvertAudioToWav);

public sealed record DownloadProgress(
    double Percentage,
    string Speed,
    string Eta,
    long? DownloadedBytes,
    long? TotalBytes,
    string Status,
    string? LogLine = null);

public sealed record VideoMetadata(
    string Title,
    string? Uploader,
    TimeSpan? Duration,
    string? ThumbnailUrl,
    int? Width,
    int? Height,
    DateOnly? UploadDate);

public sealed record YtDlpVersionInfo(string LocalVersion, string? LatestVersion)
{
    public bool IsUpdateAvailable =>
        !string.IsNullOrWhiteSpace(LatestVersion) &&
        !string.Equals(LocalVersion, LatestVersion, StringComparison.OrdinalIgnoreCase);
}

public sealed record AppSettings(
    string OutputDirectory,
    bool EmbedThumbnail = true,
    bool DownloadSubtitles = false);
