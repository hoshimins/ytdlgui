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

    [TestMethod]
    public async Task Update_IsMutuallyExclusiveWithInspectionAndDownload()
    {
        const string originalUrl = "https://example.com/video";
        var ytDlpService = new MutualExclusionYtDlpService();
        using var viewModel = new MainViewModel(
            ytDlpService,
            new TestSettingsStore(),
            new TestFolderPicker())
        {
            OutputDirectory = Path.GetTempPath(),
            Url = originalUrl
        };

        await ytDlpService.InspectStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        viewModel.UpdateYtDlpCommand.Execute(null);

        try
        {
            await WaitUntilAsync(() => viewModel.IsUpdating, TimeSpan.FromSeconds(3));
            viewModel.Url = "https://example.com/other";
            viewModel.DownloadCommand.Execute(null);
            await Task.Delay(100);

            Assert.AreEqual(originalUrl, viewModel.Url);
            Assert.AreEqual(0, ytDlpService.DownloadCallCount);
            Assert.IsFalse(ytDlpService.UpdateStarted.Task.IsCompleted);

            ytDlpService.ReleaseInspection.TrySetResult();
            await ytDlpService.UpdateStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }
        finally
        {
            ytDlpService.ReleaseInspection.TrySetResult();
            ytDlpService.ReleaseUpdate.TrySetResult();
        }

        await WaitUntilAsync(() => !viewModel.IsUpdating, TimeSpan.FromSeconds(3));
    }

    [TestMethod]
    public async Task Update_WaitsForStartupVersionCheck()
    {
        var ytDlpService = new BlockingVersionCheckYtDlpService();
        using var viewModel = new MainViewModel(
            ytDlpService,
            new TestSettingsStore(),
            new TestFolderPicker());

        await ytDlpService.VersionCheckStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        viewModel.UpdateYtDlpCommand.Execute(null);

        try
        {
            await WaitUntilAsync(() => viewModel.IsUpdating, TimeSpan.FromSeconds(3));
            await Task.Delay(100);
            Assert.IsFalse(ytDlpService.UpdateStarted.Task.IsCompleted);

            ytDlpService.ReleaseVersionCheck.TrySetResult();
            await ytDlpService.UpdateStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }
        finally
        {
            ytDlpService.ReleaseVersionCheck.TrySetResult();
            ytDlpService.ReleaseUpdate.TrySetResult();
        }

        await WaitUntilAsync(() => !viewModel.IsUpdating, TimeSpan.FromSeconds(3));
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

    private sealed class MutualExclusionYtDlpService : IYtDlpService
    {
        private int _downloadCallCount;

        public TaskCompletionSource InspectStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseInspection { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource UpdateStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseUpdate { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int DownloadCallCount => Volatile.Read(ref _downloadCallCount);

        public async Task<VideoMetadata> InspectAsync(string url, CancellationToken cancellationToken)
        {
            InspectStarted.TrySetResult();
            await ReleaseInspection.Task;
            return new VideoMetadata("テスト動画", null, null, null, null, null, null);
        }

        public Task DownloadAsync(
            DownloadRequest request,
            IProgress<DownloadProgress> progress,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _downloadCallCount);
            return Task.CompletedTask;
        }

        public Task<YtDlpVersionInfo> GetVersionInfoAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new YtDlpVersionInfo("test", "test"));

        public async Task<string> UpdateAsync(CancellationToken cancellationToken)
        {
            UpdateStarted.TrySetResult();
            await ReleaseUpdate.Task;
            return "更新済み";
        }
    }

    private sealed class BlockingVersionCheckYtDlpService : IYtDlpService
    {
        private int _versionCheckCount;

        public TaskCompletionSource VersionCheckStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseVersionCheck { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource UpdateStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseUpdate { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<VideoMetadata> InspectAsync(string url, CancellationToken cancellationToken) =>
            Task.FromResult(new VideoMetadata("テスト動画", null, null, null, null, null, null));

        public Task DownloadAsync(
            DownloadRequest request,
            IProgress<DownloadProgress> progress,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task<YtDlpVersionInfo> GetVersionInfoAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _versionCheckCount) == 1)
            {
                VersionCheckStarted.TrySetResult();
                await ReleaseVersionCheck.Task;
            }

            return new YtDlpVersionInfo("test", "test");
        }

        public async Task<string> UpdateAsync(CancellationToken cancellationToken)
        {
            UpdateStarted.TrySetResult();
            await ReleaseUpdate.Task;
            return "更新済み";
        }
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
