using System.Globalization;
using YtdlGUI.Wpf.Models;

namespace YtdlGUI.Wpf.Services;

public static class ProgressParser
{
    public static bool TryParse(string line, out DownloadProgress progress)
    {
        progress = new DownloadProgress(0, "—", "—", null, null, "ダウンロード中", line);
        var markerIndex = line.IndexOf(YtDlpCommandBuilder.ProgressPrefix, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return false;
        }

        var payload = line[markerIndex..];
        var parts = payload.Split('|');
        if (parts.Length < 6)
        {
            return false;
        }

        var percentText = parts[1].Trim().TrimEnd('%');
        _ = double.TryParse(percentText, NumberStyles.Float, CultureInfo.InvariantCulture, out var percentage);

        progress = new DownloadProgress(
            Math.Clamp(percentage, 0, 100),
            Normalize(parts[2]),
            Normalize(parts[3]),
            ParseLong(parts[4]),
            ParseLong(parts[5]),
            "ダウンロード中",
            line);
        return true;
    }

    private static string Normalize(string value)
    {
        var normalized = value.Trim();
        return string.IsNullOrWhiteSpace(normalized) || normalized == "NA" ? "—" : normalized;
    }

    private static long? ParseLong(string value) =>
        long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
}
