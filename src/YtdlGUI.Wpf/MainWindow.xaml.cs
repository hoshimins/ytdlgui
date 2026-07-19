using System.Windows;
using YtdlGUI.Wpf.Services;
using YtdlGUI.Wpf.ViewModels;

namespace YtdlGUI.Wpf;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        var baseDirectory = AppContext.BaseDirectory;
        _viewModel = new MainViewModel(
            new YtDlpService(baseDirectory),
            new JsonSettingsStore(
                legacyConfigPath: Path.Combine(baseDirectory, "config.ini")),
            new WindowsFolderPicker());
        DataContext = _viewModel;
        if (Environment.GetCommandLineArgs().Contains("--qa-light", StringComparer.OrdinalIgnoreCase))
        {
            Width = 1440;
            Height = 1024;
        }
        Closed += (_, _) => _viewModel.Dispose();
    }
}
