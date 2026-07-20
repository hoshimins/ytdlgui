using YtdlGUI.Wpf.Models;
using YtdlGUI.Wpf.Services;

namespace YtdlGUI.Wpf.Tests;

[TestClass]
public sealed class YtDlpCommandBuilderTests
{
    [TestMethod]
    public void Update_DoesNotDisableCertificateValidation()
    {
        var arguments = YtDlpCommandBuilder.BuildUpdateArguments();

        CollectionAssert.AreEqual(new[] { "-U" }, arguments.ToArray());
    }

    [TestMethod]
    public void VideoPreset_PreservesH264Mp4AndDatePrefixedOutput()
    {
        var request = new DownloadRequest(
            "https://example.com/video",
            DownloadPreset.VideoMp4,
            @"C:\Downloads",
            true,
            false,
            false);

        var arguments = YtDlpCommandBuilder.BuildDownloadArguments(request, @"C:\Tools\ffmpeg.exe");

        CollectionAssert.Contains(arguments.ToList(), "vcodec:h264,res,acodec:m4a");
        CollectionAssert.Contains(arguments.ToList(), "mp4");
        CollectionAssert.Contains(arguments.ToList(), "--embed-thumbnail");
        StringAssert.Contains(string.Join(' ', arguments), "%(upload_date)s-%(title)s.%(ext)s");
    }

    [TestMethod]
    [DataRow(DownloadPreset.AudioM4a, "m4a")]
    [DataRow(DownloadPreset.AudioMp3, "mp3")]
    public void AudioPreset_UsesExpectedExtractionFormat(DownloadPreset preset, string expectedFormat)
    {
        var request = new DownloadRequest(
            "https://example.com/video",
            preset,
            @"C:\Downloads",
            false,
            false,
            false);

        var arguments = YtDlpCommandBuilder.BuildDownloadArguments(request, @"C:\Tools\ffmpeg.exe");
        var audioFormatIndex = arguments.ToList().IndexOf("--audio-format");

        Assert.IsGreaterThanOrEqualTo(0, audioFormatIndex);
        Assert.AreEqual(expectedFormat, arguments[audioFormatIndex + 1]);
    }

    [TestMethod]
    public void AudioPreset_CanOverrideOutputToWav()
    {
        var request = new DownloadRequest(
            "https://example.com/video",
            DownloadPreset.AudioMp3,
            @"C:\Downloads",
            false,
            false,
            true);

        var arguments = YtDlpCommandBuilder.BuildDownloadArguments(request, @"C:\Tools\ffmpeg.exe");
        var audioFormatIndex = arguments.ToList().IndexOf("--audio-format");

        Assert.AreEqual("wav", arguments[audioFormatIndex + 1]);
    }
}
