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
    public void Duration_Over24HoursUsesTotalHours()
    {
        var duration = TimeSpan.FromHours(25) + TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(3);

        var label = MainViewModel.FormatDuration(duration);

        Assert.AreEqual("25:02:03", label);
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

    [TestMethod]
    public async Task Download_PreventsUrlChangeAndPendingInspection()
    {
        const string originalUrl = "https://example.com/video";
        var ytDlpService = new BlockingDownloadYtDlpService();
        using var viewModel = new MainViewModel(
            ytDlpService,
            new TestSettingsStore(),
            new TestFolderPicker())
        {
            OutputDirectory = Path.GetTempPath(),
            Url = originalUrl
        };

        viewModel.DownloadCommand.Execute(null);
        await ytDlpService.DownloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));

        try
        {
            viewModel.Url = "https://example.com/other";
            await Task.Delay(700);

            Assert.IsTrue(viewModel.IsDownloading);
            Assert.AreEqual(originalUrl, viewModel.Url);
            Assert.AreEqual(0, ytDlpService.InspectCallCount);
        }
        finally
        {
            ytDlpService.ReleaseDownload.TrySetResult();
        }

        await WaitUntilAsync(() => !viewModel.IsDownloading, TimeSpan.FromSeconds(3));
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var expiresAt = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= expiresAt)
            {
                Assert.Fail("条件が時間内に成立しませんでした。");
            }

            await Task.Delay(20);
        }
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

    private sealed class BlockingDownloadYtDlpService : IYtDlpService
    {
        private int _inspectCallCount;

        public TaskCompletionSource DownloadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseDownload { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int InspectCallCount => Volatile.Read(ref _inspectCallCount);

        public Task<VideoMetadata> InspectAsync(string url, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _inspectCallCount);
            return Task.FromResult(new VideoMetadata("テスト動画", null, null, null, null, null, null));
        }

        public async Task DownloadAsync(
            DownloadRequest request,
            IProgress<DownloadProgress> progress,
            CancellationToken cancellationToken)
        {
            DownloadStarted.TrySetResult();
            await ReleaseDownload.Task.WaitAsync(cancellationToken);
        }

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
