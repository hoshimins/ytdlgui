using YtdlGUI.Wpf.Models;
using YtdlGUI.Wpf.Services;

namespace YtdlGUI.Wpf.Tests;

[TestClass]
public sealed class YtDlpServiceTests
{
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
}
