using YtdlGUI.Wpf.Models;
using YtdlGUI.Wpf.Services;
using YtdlGUI.Wpf.ViewModels;

namespace YtdlGUI.Wpf.Tests;

[TestClass]
public sealed class MainViewModelTests
{
    [TestMethod]
    public void OpenFolder_PassesSpacedPathAsSingleArgument()
    {
        const string outputDirectory = @"C:\Users\Jane Doe\Downloads";

        var startInfo = MainViewModel.CreateOpenFolderStartInfo(outputDirectory);

        Assert.AreEqual("explorer.exe", startInfo.FileName);
        Assert.IsTrue(startInfo.UseShellExecute);
        CollectionAssert.AreEqual(new[] { outputDirectory }, startInfo.ArgumentList.ToArray());
    }

    [TestMethod]
    public void AppVersion_UsesThreePartReleaseVersion()
    {
        var label = MainViewModel.FormatAppVersion(new Version(1, 0, 0, 0));

        Assert.AreEqual("Movie Downloader 1.0.0", label);
    }

    [TestMethod]
    public async Task Url_WithSurroundingWhitespace_InspectsNormalizedValue()
    {
        var ytDlpService = new RecordingYtDlpService();
        using var viewModel = new MainViewModel(
            ytDlpService,
            new TestSettingsStore(),
            new TestFolderPicker());

        viewModel.Url = " \r\nhttps://example.com/video\t ";
        var inspectedUrl = await ytDlpService.InspectedUrl.Task.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.AreEqual("https://example.com/video", inspectedUrl);
    }

    private sealed class RecordingYtDlpService : IYtDlpService
    {
        public TaskCompletionSource<string> InspectedUrl { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<VideoMetadata> InspectAsync(string url, CancellationToken cancellationToken)
        {
            InspectedUrl.TrySetResult(url);
            return Task.FromResult(new VideoMetadata("テスト動画", null, null, null, null, null, null));
        }

        public Task DownloadAsync(
            DownloadRequest request,
            IProgress<DownloadProgress> progress,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<YtDlpVersionInfo> GetVersionInfoAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new YtDlpVersionInfo("test", "test"));

        public Task<string> UpdateAsync(CancellationToken cancellationToken) =>
            Task.FromResult("更新済み");
    }

    private sealed class TestSettingsStore : ISettingsStore
    {
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AppSettings(Path.GetTempPath()));

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class TestFolderPicker : IFolderPicker
    {
        public string? PickFolder(string initialDirectory) => null;
    }
}
