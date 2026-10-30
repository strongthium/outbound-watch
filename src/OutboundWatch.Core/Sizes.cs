using System.Globalization;

namespace Strongthium.OutboundWatch;

/// <summary>Sizes the way Windows writes them (in units of 1024, with one decimal, and "bytes" under 1 KB), and how
/// long connections lasted.</summary>
public static class Sizes
{
    private static readonly string[] Units = ["KB", "MB", "GB", "TB", "PB"];

    public static string Format(long bytes, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        if (bytes < 0) return "-" + Format(-bytes, culture);
        if (bytes < 1024) return bytes == 1 ? "1 byte" : bytes.ToString("N0", culture) + " bytes";
        double value = bytes;
        var unit = -1;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        // 1023.96 KB would round to "1024.0 KB": that's 1.0 MB.
        if (Math.Round(value, 1) >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return value.ToString("N1", culture) + " " + Units[unit];
    }

    public static string Count(long count, CultureInfo? culture = null) => count.ToString("N0", culture ?? CultureInfo.CurrentCulture);

    /// <summary>How long a connection lasted: "0.4 s", "42 s", "3 min 05 s", "2 h 07 min", "3 d 4 h".</summary>
    public static string Duration(TimeSpan time, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        if (time < TimeSpan.Zero) time = TimeSpan.Zero;
        if (time.TotalSeconds < 10) return time.TotalSeconds.ToString("0.0", culture) + " s";
        if (time.TotalMinutes < 1) return ((int)time.TotalSeconds).ToString(culture) + " s";
        if (time.TotalHours < 1) return $"{(int)time.TotalMinutes} min {time.Seconds:00} s";
        if (time.TotalDays < 1) return $"{(int)time.TotalHours} h {time.Minutes:00} min";
        return $"{(int)time.TotalDays} d {time.Hours} h";
    }

    /// <summary>A rate: "1.2 MB/s".</summary>
    public static string Rate(double bytesPerSecond, CultureInfo? culture = null) => Format((long)bytesPerSecond, culture) + "/s";
}
