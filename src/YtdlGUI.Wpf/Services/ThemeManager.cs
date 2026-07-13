using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;

namespace YtdlGUI.Wpf.Services;

public static class ThemeManager
{
    public static void ApplyTheme(ResourceDictionary resources, bool? lightOverride = null)
    {
        var isLight = lightOverride ?? IsSystemLightTheme();
        var palette = isLight
            ? new Dictionary<string, string>
            {
                ["AppBackgroundBrush"] = "#F5F8FC",
                ["SurfaceBrush"] = "#F9FBFE",
                ["SurfaceStrongBrush"] = "#FFFFFF",
                ["MutedSurfaceBrush"] = "#E7EBF0",
                ["SelectedSurfaceBrush"] = "#E7F3FF",
                ["FooterBrush"] = "#EEF4FA",
                ["ErrorSurfaceBrush"] = "#FFF4F2",
                ["ErrorBorderBrush"] = "#F3B7AE",
                ["BorderBrush"] = "#DCE3EC",
                ["TextPrimaryBrush"] = "#1B1B1F",
                ["TextSecondaryBrush"] = "#5F6570",
                ["AccentBrush"] = "#0F6CBD",
                ["AccentHoverBrush"] = "#115EA3",
                ["SuccessBrush"] = "#D86D32",
                ["ErrorBrush"] = "#C42B1C"
            }
            : new Dictionary<string, string>
            {
                ["AppBackgroundBrush"] = "#202020",
                ["SurfaceBrush"] = "#282828",
                ["SurfaceStrongBrush"] = "#2D2D2D",
                ["MutedSurfaceBrush"] = "#33383D",
                ["SelectedSurfaceBrush"] = "#173A4C",
                ["FooterBrush"] = "#232B33",
                ["ErrorSurfaceBrush"] = "#442226",
                ["ErrorBorderBrush"] = "#8D3A43",
                ["BorderBrush"] = "#3D4248",
                ["TextPrimaryBrush"] = "#F5F5F5",
                ["TextSecondaryBrush"] = "#B8BDC5",
                ["AccentBrush"] = "#60CDFF",
                ["AccentHoverBrush"] = "#75D5FF",
                ["SuccessBrush"] = "#FF9F6E",
                ["ErrorBrush"] = "#FF99A4"
            };

        foreach (var (key, colorText) in palette)
        {
            resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorText));
        }
    }

    private static bool IsSystemLightTheme()
    {
        try
        {
            var value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",
                1);
            return value is not int intValue || intValue != 0;
        }
        catch
        {
            return true;
        }
    }
}
