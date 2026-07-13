using System.Windows;

namespace YtdlGUI.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var forceLight = e.Args.Contains("--qa-light", StringComparer.OrdinalIgnoreCase);
        if (forceLight)
        {
            this.ThemeMode = System.Windows.ThemeMode.Light;
        }
        base.OnStartup(e);
        Services.ThemeManager.ApplyTheme(Resources, forceLight ? true : null);
    }
}
