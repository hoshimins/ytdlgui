using YtdlGUI.Wpf.Services;

namespace YtdlGUI.Wpf.Tests;

[TestClass]
public sealed class ProgressParserTests
{
    [TestMethod]
    public void TryParse_ParsesStructuredProgressLine()
    {
        var line = "__YTDLGUI__| 67.2%|18.7MiB/s|00:18|2390000000|3510000000";

        var parsed = ProgressParser.TryParse(line, out var progress);

        Assert.IsTrue(parsed);
        Assert.AreEqual(67.2, progress.Percentage, 0.001);
        Assert.AreEqual("18.7MiB/s", progress.Speed);
        Assert.AreEqual("00:18", progress.Eta);
        Assert.AreEqual(2_390_000_000, progress.DownloadedBytes);
        Assert.AreEqual(3_510_000_000, progress.TotalBytes);
    }

    [TestMethod]
    public void TryParse_IgnoresOrdinaryLogLine()
    {
        Assert.IsFalse(ProgressParser.TryParse("[download] Destination: movie.mp4", out _));
    }
}
