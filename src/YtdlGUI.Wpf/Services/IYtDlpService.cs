using YtdlGUI.Wpf.Models;

namespace YtdlGUI.Wpf.Services;

public interface IYtDlpService
{
    Task<VideoMetadata> InspectAsync(string url, CancellationToken cancellationToken);
    Task DownloadAsync(
        DownloadRequest request,
        IProgress<DownloadProgress> progress,
        CancellationToken cancellationToken);
    Task<YtDlpVersionInfo> GetVersionInfoAsync(CancellationToken cancellationToken);
    Task<string> UpdateAsync(CancellationToken cancellationToken);
}
