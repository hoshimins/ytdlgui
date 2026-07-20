using System.Windows;

namespace YtdlGUI.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var forceLight = e.Args.Contains("--qa-light", StringComparer.OrdinalIgnoreCase);
        var forceDark = e.Args.Contains("--qa-dark", StringComparer.OrdinalIgnoreCase);
        if (forceLight)
        {
            this.ThemeMode = System.Windows.ThemeMode.Light;
        }
        else if (forceDark)
        {
            this.ThemeMode = System.Windows.ThemeMode.Dark;
        }
        base.OnStartup(e);
        Services.ThemeManager.ApplyTheme(Resources, forceLight ? true : forceDark ? false : null);
    }
}
