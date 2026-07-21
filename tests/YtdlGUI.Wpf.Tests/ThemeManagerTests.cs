using System.Windows;
using System.Windows.Media;
using YtdlGUI.Wpf.Services;

namespace YtdlGUI.Wpf.Tests;

[TestClass]
public sealed class ThemeManagerTests
{
    [TestMethod]
    public void DarkTheme_UsesDarkForegroundOnBrightAccent()
    {
        var resources = new ResourceDictionary();

        ThemeManager.ApplyTheme(resources, lightOverride: false);

        var foreground = (SolidColorBrush)resources["AccentForegroundBrush"];
        Assert.AreEqual(Color.FromRgb(0x00, 0x35, 0x4A), foreground.Color);
    }
}
