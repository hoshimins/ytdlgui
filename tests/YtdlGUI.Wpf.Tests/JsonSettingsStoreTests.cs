using YtdlGUI.Wpf.Services;

namespace YtdlGUI.Wpf.Tests;

[TestClass]
public sealed class JsonSettingsStoreTests
{
    private string _temporaryDirectory = null!;

    [TestInitialize]
    public void SetUp()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), "YtdlGUI.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temporaryDirectory);
    }

    [TestCleanup]
    public void TearDown()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task LoadAsync_MigratesLegacyOutputDirectory()
    {
        var outputDirectory = Path.Combine(_temporaryDirectory, "downloads");
        Directory.CreateDirectory(outputDirectory);
        var legacyPath = Path.Combine(_temporaryDirectory, "config.ini");
        var settingsPath = Path.Combine(_temporaryDirectory, "settings", "settings.json");
        await File.WriteAllTextAsync(
            legacyPath,
            $"[Settings]{Environment.NewLine}output_directory = {outputDirectory}{Environment.NewLine}");
        var store = new JsonSettingsStore(settingsPath, legacyPath);

        var settings = await store.LoadAsync();

        Assert.AreEqual(outputDirectory, settings.OutputDirectory);
        Assert.IsTrue(File.Exists(settingsPath));
    }

    [TestMethod]
    public async Task SaveAsync_RoundTripsSettings()
    {
        var outputDirectory = Path.Combine(_temporaryDirectory, "downloads");
        Directory.CreateDirectory(outputDirectory);
        var settingsPath = Path.Combine(_temporaryDirectory, "settings.json");
        var store = new JsonSettingsStore(settingsPath, Path.Combine(_temporaryDirectory, "missing.ini"));

        await store.SaveAsync(new(outputDirectory, false, true));
        var settings = await store.LoadAsync();

        Assert.AreEqual(outputDirectory, settings.OutputDirectory);
        Assert.IsFalse(settings.EmbedThumbnail);
        Assert.IsTrue(settings.DownloadSubtitles);
    }
}
