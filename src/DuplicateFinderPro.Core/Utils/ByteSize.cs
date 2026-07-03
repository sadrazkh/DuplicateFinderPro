using System.Globalization;

namespace DuplicateFinderPro.Core.Utils;

/// <summary>Human-friendly byte formatting.</summary>
public static class ByteSize
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB", "PB" };

    public static string Humanize(long bytes, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        if (bytes < 0) return "-" + Humanize(-bytes, culture);

        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        var format = unit == 0 ? "0" : "0.##";
        return string.Create(culture, $"{value.ToString(format, culture)} {Units[unit]}");
    }
}
