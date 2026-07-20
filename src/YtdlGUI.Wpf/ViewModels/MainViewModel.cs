using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using YtdlGUI.Wpf.Models;
using YtdlGUI.Wpf.Services;

namespace YtdlGUI.Wpf.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly IYtDlpService _ytDlpService;
    private readonly ISettingsStore _settingsStore;
    private readonly IFolderPicker _folderPicker;
    private CancellationTokenSource? _inspectCancellation;
    private CancellationTokenSource? _downloadCancellation;
    private string _url = string.Empty;
    private string _outputDirectory = string.Empty;
    private DownloadPreset _selectedPreset = DownloadPreset.VideoMp4;
    private VideoMetadata? _metadata;
    private bool _isInspecting;
    private bool _isDownloading;
    private bool _embedThumbnail = true;
    private bool _downloadSubtitles;
    private bool _convertAudioToWav;
    private double _progressPercentage;
    private string _speed = "—";
    private string _eta = "—";
    private string _statusText = "準備しています…";
    private string _errorMessage = string.Empty;
    private string _versionText = "yt-dlpを確認中…";

    public MainViewModel(
        IYtDlpService ytDlpService,
        ISettingsStore settingsStore,
        IFolderPicker folderPicker)
    {
        _ytDlpService = ytDlpService;
        _settingsStore = settingsStore;
        _folderPicker = folderPicker;
        DownloadCommand = new AsyncRelayCommand(DownloadAsync, CanDownload);
        CancelCommand = new RelayCommand(CancelDownload, () => IsDownloading);
        BrowseCommand = new RelayCommand(Browse, () => !IsDownloading);
        ClearUrlCommand = new RelayCommand(() => Url = string.Empty, () => !IsDownloading);
        OpenFolderCommand = new RelayCommand(OpenOutputFolder, () => Directory.Exists(OutputDirectory));
        UpdateYtDlpCommand = new AsyncRelayCommand(UpdateYtDlpAsync, () => !IsDownloading);
        _ = InitializeAsync();
    }

    public string Url
    {
        get => _url;
        set
        {
            if (!SetProperty(ref _url, value))
            {
                return;
            }

            Metadata = null;
            ErrorMessage = string.Empty;
            NotifyCommands();
            ScheduleInspection(value);
        }
    }

    public string OutputDirectory
    {
        get => _outputDirectory;
        set
        {
            if (SetProperty(ref _outputDirectory, value))
            {
                NotifyCommands();
            }
        }
    }

    public DownloadPreset SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value))
            {
                OnPropertyChanged(nameof(IsVideoSelected));
                OnPropertyChanged(nameof(IsM4aSelected));
                OnPropertyChanged(nameof(IsMp3Selected));
                OnPropertyChanged(nameof(IsAudioSelected));
            }
        }
    }

    public bool IsVideoSelected
    {
        get => SelectedPreset == DownloadPreset.VideoMp4;
        set { if (value) SelectedPreset = DownloadPreset.VideoMp4; }
    }

    public bool IsM4aSelected
    {
        get => SelectedPreset == DownloadPreset.AudioM4a;
        set { if (value) SelectedPreset = DownloadPreset.AudioM4a; }
    }

    public bool IsMp3Selected
    {
        get => SelectedPreset == DownloadPreset.AudioMp3;
        set { if (value) SelectedPreset = DownloadPreset.AudioMp3; }
    }

    public bool IsAudioSelected => SelectedPreset != DownloadPreset.VideoMp4;

    public VideoMetadata? Metadata
    {
        get => _metadata;
        private set
        {
            if (SetProperty(ref _metadata, value))
            {
                OnPropertyChanged(nameof(HasMetadata));
                OnPropertyChanged(nameof(DurationText));
                OnPropertyChanged(nameof(ResolutionText));
                OnPropertyChanged(nameof(UploadDateText));
            }
        }
    }

    public bool HasMetadata => Metadata is not null;
    public string DurationText => Metadata?.Duration is { } duration
        ? duration.TotalHours >= 1 ? duration.ToString(@"h\:mm\:ss") : duration.ToString(@"m\:ss")
        : "—";
    public string ResolutionText => Metadata is { Width: { } width, Height: { } height }
        ? $"{width} × {height}"
        : "—";
    public string UploadDateText => Metadata?.UploadDate?.ToString("yyyy/MM/dd") ?? "—";

    public bool IsInspecting
    {
        get => _isInspecting;
        private set => SetProperty(ref _isInspecting, value);
    }

    public bool IsDownloading
    {
        get => _isDownloading;
        private set
        {
            if (SetProperty(ref _isDownloading, value))
            {
                OnPropertyChanged(nameof(IsNotDownloading));
                NotifyCommands();
            }
        }
    }

    public bool IsNotDownloading => !IsDownloading;

    public bool EmbedThumbnail
    {
        get => _embedThumbnail;
        set => SetProperty(ref _embedThumbnail, value);
    }

    public bool DownloadSubtitles
    {
        get => _downloadSubtitles;
        set => SetProperty(ref _downloadSubtitles, value);
    }

    public bool ConvertAudioToWav
    {
        get => _convertAudioToWav;
        set => SetProperty(ref _convertAudioToWav, value);
    }

    public double ProgressPercentage
    {
        get => _progressPercentage;
        private set => SetProperty(ref _progressPercentage, value);
    }

    public string Speed
    {
        get => _speed;
        private set => SetProperty(ref _speed, value);
    }

    public string Eta
    {
        get => _eta;
        private set => SetProperty(ref _eta, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public string VersionText
    {
        get => _versionText;
        private set => SetProperty(ref _versionText, value);
    }

    public string AppVersionText { get; } = FormatAppVersion(
        typeof(MainViewModel).Assembly.GetName().Version);

    public ObservableCollection<string> LogLines { get; } = [];
    public ICommand DownloadCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand BrowseCommand { get; }
    public ICommand ClearUrlCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand UpdateYtDlpCommand { get; }

    private async Task InitializeAsync()
    {
        try
        {
            var settings = await _settingsStore.LoadAsync();
            OutputDirectory = settings.OutputDirectory;
            EmbedThumbnail = settings.EmbedThumbnail;
            DownloadSubtitles = settings.DownloadSubtitles;
            StatusText = "準備完了";
            NotifyCommands();
        }
        catch (Exception exception)
        {
            ErrorMessage = $"設定を読み込めませんでした。{Environment.NewLine}{exception.Message}";
            StatusText = "設定エラー";
        }

        try
        {
            var version = await _ytDlpService.GetVersionInfoAsync(CancellationToken.None);
            VersionText = version.IsUpdateAvailable
                ? $"yt-dlp {version.LocalVersion}（{version.LatestVersion}へ更新可能）"
                : $"yt-dlp {version.LocalVersion}";
        }
        catch (Exception exception)
        {
            VersionText = "yt-dlpを確認できません";
            AddLog(exception.Message);
        }
    }

    private void ScheduleInspection(string value)
    {
        _inspectCancellation?.Cancel();
        _inspectCancellation?.Dispose();
        _inspectCancellation = null;

        var normalizedValue = value.Trim();

        if (!IsValidHttpUrl(normalizedValue))
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                StatusText = "URLを入力してください";
            }
            return;
        }

        var source = new CancellationTokenSource();
        _inspectCancellation = source;
        _ = InspectAfterDelayAsync(normalizedValue, source.Token);
    }

    private async Task InspectAfterDelayAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(600, cancellationToken);
            IsInspecting = true;
            StatusText = "URLを解析しています…";
            var metadata = await _ytDlpService.InspectAsync(url, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.Equals(Url.Trim(), url, StringComparison.Ordinal))
            {
                return;
            }
            Metadata = metadata;
            StatusText = "ダウンロードできます";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            if (!string.Equals(Url.Trim(), url, StringComparison.Ordinal))
            {
                return;
            }
            ErrorMessage = exception.Message;
            StatusText = "URLを解析できません";
            AddLog(exception.ToString());
        }
        finally
        {
            IsInspecting = false;
            NotifyCommands();
        }
    }

    private async Task DownloadAsync()
    {
        ErrorMessage = string.Empty;
        if (!Validate(out var validationError))
        {
            ErrorMessage = validationError;
            return;
        }

        _downloadCancellation = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        IsDownloading = true;
        ProgressPercentage = 0;
        Speed = "—";
        Eta = "—";
        StatusText = "ダウンロードを開始しています…";
        LogLines.Clear();

        try
        {
            await SaveSettingsAsync();
            var request = new DownloadRequest(
                Url.Trim(),
                SelectedPreset,
                OutputDirectory.Trim(),
                EmbedThumbnail,
                DownloadSubtitles,
                ConvertAudioToWav);
            var progress = new Progress<DownloadProgress>(ApplyProgress);
            await _ytDlpService.DownloadAsync(request, progress, _downloadCancellation.Token);
            ProgressPercentage = 100;
            StatusText = "ダウンロードが完了しました";
        }
        catch (OperationCanceledException)
        {
            StatusText = "ダウンロードをキャンセルしました";
            AddLog("ユーザー操作またはタイムアウトにより処理を中止しました。");
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            StatusText = "ダウンロードに失敗しました";
            AddLog(exception.ToString());
        }
        finally
        {
            _downloadCancellation.Dispose();
            _downloadCancellation = null;
            IsDownloading = false;
        }
    }

    private void ApplyProgress(DownloadProgress progress)
    {
        if (progress.Percentage > 0 || progress.Status == "完了")
        {
            ProgressPercentage = progress.Percentage;
        }
        Speed = progress.Speed;
        Eta = progress.Eta;
        StatusText = progress.Status;
        if (!string.IsNullOrWhiteSpace(progress.LogLine) &&
            !progress.LogLine.Contains(YtDlpCommandBuilder.ProgressPrefix, StringComparison.Ordinal))
        {
            AddLog(progress.LogLine);
        }
    }

    private void CancelDownload() => _downloadCancellation?.Cancel();

    private void Browse()
    {
        var selected = _folderPicker.PickFolder(OutputDirectory);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            OutputDirectory = selected;
            _ = SaveSettingsAsync();
        }
    }

    private void OpenOutputFolder()
    {
        if (Directory.Exists(OutputDirectory))
        {
            Process.Start(CreateOpenFolderStartInfo(OutputDirectory));
        }
    }

    internal static ProcessStartInfo CreateOpenFolderStartInfo(string outputDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            UseShellExecute = true
        };
        startInfo.ArgumentList.Add(outputDirectory);
        return startInfo;
    }

    internal static string FormatAppVersion(Version? version) =>
        version is null
            ? "Movie Downloader"
            : $"Movie Downloader {version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";

    private async Task UpdateYtDlpAsync()
    {
        ErrorMessage = string.Empty;
        StatusText = "yt-dlpを更新しています…";
        try
        {
            var result = await _ytDlpService.UpdateAsync(CancellationToken.None);
            AddLog(result);
            var version = await _ytDlpService.GetVersionInfoAsync(CancellationToken.None);
            VersionText = $"yt-dlp {version.LocalVersion}";
            StatusText = "yt-dlpを更新しました";
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            StatusText = "yt-dlpを更新できません";
            AddLog(exception.ToString());
        }
    }

    private bool Validate(out string error)
    {
        if (!IsValidHttpUrl(Url))
        {
            error = "有効な動画URLを入力してください。";
            return false;
        }
        if (string.IsNullOrWhiteSpace(OutputDirectory) || !Directory.Exists(OutputDirectory))
        {
            error = "存在する保存先フォルダを選択してください。";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool CanDownload() => IsNotDownloading && IsValidHttpUrl(Url) && Directory.Exists(OutputDirectory);

    private async Task SaveSettingsAsync() =>
        await _settingsStore.SaveAsync(new AppSettings(OutputDirectory, EmbedThumbnail, DownloadSubtitles));

    private static bool IsValidHttpUrl(string value) =>
        Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private void AddLog(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }
        LogLines.Add(line.TrimEnd());
        while (LogLines.Count > 500)
        {
            LogLines.RemoveAt(0);
        }
    }

    private void NotifyCommands()
    {
        (DownloadCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (CancelCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (BrowseCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (ClearUrlCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (OpenFolderCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (UpdateYtDlpCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        _inspectCancellation?.Cancel();
        _inspectCancellation?.Dispose();
        _downloadCancellation?.Cancel();
        _downloadCancellation?.Dispose();
    }
}
