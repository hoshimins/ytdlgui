using System.Net.Http;
using YtdlGUI.Wpf.Models;
using YtdlGUI.Wpf.Services;

namespace YtdlGUI.Wpf.Tests;

[TestClass]
public sealed class YtDlpServiceTests
{
    [TestMethod]
    public async Task LatestVersion_ApiTimeoutReturnsUnknownVersion()
    {
        using var httpClient = new HttpClient(new TimeoutHandler());

        var latestVersion = await YtDlpService.GetLatestVersionAsync(
            httpClient,
            CancellationToken.None);

        Assert.IsNull(latestVersion);
    }

    [TestMethod]
    public async Task LatestVersion_CallerCancellationIsNotIgnored()
    {
        using var httpClient = new HttpClient(new CallerCancellationHandler());
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() =>
            YtDlpService.GetLatestVersionAsync(httpClient, cancellationSource.Token));
    }

    [TestMethod]
    public async Task Download_RequiresFfprobeAlongsideFfmpeg()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"YtdlGUI-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(Path.Combine(directory, "yt-dlp.exe"), string.Empty);
            File.WriteAllText(Path.Combine(directory, "ffmpeg.exe"), string.Empty);
            var service = new YtDlpService(directory);
            var request = new DownloadRequest(
                "https://example.com/video",
                DownloadPreset.AudioMp3,
                directory,
                false,
                false,
                false);

            var exception = await Assert.ThrowsExactlyAsync<FileNotFoundException>(() =>
                service.DownloadAsync(
                    request,
                    new Progress<DownloadProgress>(),
                    CancellationToken.None));

            StringAssert.Contains(exception.Message, "ffprobe.exe");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(
                new TaskCanceledException("GitHub API timeout", new TimeoutException()));
    }

    private sealed class CallerCancellationHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromCanceled<HttpResponseMessage>(cancellationToken);
    }
}
