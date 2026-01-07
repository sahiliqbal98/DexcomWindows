using DexcomWindows.Models;
using DexcomWindows.Themes;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace DexcomWindows.Converters;

/// <summary>
/// Converts glucose reading to appropriate color brush
/// </summary>
public class GlucoseColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is GlucoseReading reading)
        {
            return ColorThemes.GetGlucoseBrush(reading.ColorCategory);
        }

        if (value is GlucoseColorCategory category)
        {
            return ColorThemes.GetGlucoseBrush(category);
        }

        if (value is int glucoseValue)
        {
            var cat = glucoseValue switch
            {
                < 55 => GlucoseColorCategory.UrgentLow,
                < 70 => GlucoseColorCategory.Low,
                < 80 => GlucoseColorCategory.Warning,
                < 160 => GlucoseColorCategory.Normal,
                < 180 => GlucoseColorCategory.Warning,
                < 250 => GlucoseColorCategory.High,
                _ => GlucoseColorCategory.UrgentHigh
            };
            return ColorThemes.GetGlucoseBrush(cat);
        }

        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts trend arrow to symbol string
/// </summary>
public class TrendSymbolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is TrendArrow trend)
        {
            return trend.Symbol();
        }

        if (value is GlucoseReading reading)
        {
            return reading.Trend.Symbol();
        }

        return "?";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts DateTime to "time ago" string
/// </summary>
public class TimeAgoConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DateTime timestamp)
        {
            var interval = DateTime.Now - timestamp;
            var minutes = (int)interval.TotalMinutes;

            return minutes switch
            {
                < 1 => "Just now",
                1 => "1 min ago",
                < 60 => $"{minutes} mins ago",
                _ => interval.Hours == 1
                    ? "1 hour ago"
                    : $"{interval.Hours} hours ago"
            };
        }

        if (value is GlucoseReading reading)
        {
            return reading.TimeAgoString;
        }

        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts boolean to visibility
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; } = false;

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var boolValue = value is bool b && b;
        if (Invert) boolValue = !boolValue;

        return boolValue
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts null to visibility (visible when not null)
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; } = false;

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var isNotNull = value != null;
        if (Invert) isNotNull = !isNotNull;

        return isNotNull
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts seconds to formatted time string
/// </summary>
public class SecondsToTimeStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int seconds)
        {
            if (seconds < 60)
            {
                return $"{seconds}s";
            }

            var minutes = seconds / 60;
            var remainingSeconds = seconds % 60;

            if (remainingSeconds == 0)
            {
                return $"{minutes}m";
            }

            return $"{minutes}m {remainingSeconds}s";
        }

        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts progress (0-1) to percentage string
/// </summary>
public class ProgressToPercentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double progress)
        {
            return $"{progress * 100:F0}%";
        }

        return "0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
