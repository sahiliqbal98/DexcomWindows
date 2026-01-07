using DexcomWindows.Models;
using DexcomWindows.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace DexcomWindows.Themes;

/// <summary>
/// Color theme definitions matching Mac app exactly
/// </summary>
public static class ColorThemes
{
    public static ThemeColors GetColors(ColorTheme theme) => theme switch
    {
        ColorTheme.System => SystemTheme,
        ColorTheme.Light => LightTheme,
        ColorTheme.Dark => DarkTheme,
        ColorTheme.Charcoal => CharcoalTheme,
        ColorTheme.Rainbow => RainbowTheme,
        ColorTheme.DexcomGreen => DexcomGreenTheme,
        _ => SystemTheme
    };

    public static Color GetGlucoseColor(GlucoseColorCategory category) => category switch
    {
        GlucoseColorCategory.UrgentLow => Color.FromArgb(255, 255, 59, 48),   // Bright Red
        GlucoseColorCategory.Low => Color.FromArgb(255, 255, 69, 58),         // Red
        GlucoseColorCategory.Warning => Color.FromArgb(255, 255, 149, 0),      // Orange
        GlucoseColorCategory.Normal => Color.FromArgb(255, 52, 199, 89),       // Green
        GlucoseColorCategory.High => Color.FromArgb(255, 255, 149, 0),         // Orange
        GlucoseColorCategory.UrgentHigh => Color.FromArgb(255, 255, 59, 48),  // Bright Red
        _ => Color.FromArgb(255, 128, 128, 128)
    };

    public static SolidColorBrush GetGlucoseBrush(GlucoseColorCategory category) =>
        new(GetGlucoseColor(category));

    private static ThemeColors SystemTheme => new()
    {
        BackgroundColor = Color.FromArgb(255, 249, 249, 249),
        SecondaryBackgroundColor = Color.FromArgb(255, 243, 243, 243),
        TertiaryBackgroundColor = Color.FromArgb(255, 235, 235, 235),
        PrimaryTextColor = Color.FromArgb(255, 0, 0, 0),
        SecondaryTextColor = Color.FromArgb(255, 128, 128, 128),
        IconColor = Color.FromArgb(255, 0, 0, 0),
        AccentColor = Color.FromArgb(255, 0, 120, 215),
        NormalColor = Color.FromArgb(255, 52, 199, 89),
        WarningColor = Color.FromArgb(255, 255, 149, 0),
        CriticalColor = Color.FromArgb(255, 255, 59, 48),
        ChartLineColor = Color.FromArgb(255, 0, 122, 255),
        ChartGridColor = Color.FromArgb(50, 128, 128, 128),
        TargetRangeColor = Color.FromArgb(30, 52, 199, 89)
    };

    private static ThemeColors LightTheme => new()
    {
        BackgroundColor = Colors.White,
        SecondaryBackgroundColor = Color.FromArgb(255, 245, 245, 245),
        TertiaryBackgroundColor = Color.FromArgb(255, 235, 235, 235),
        PrimaryTextColor = Color.FromArgb(255, 26, 26, 26),
        SecondaryTextColor = Color.FromArgb(255, 115, 115, 120),
        IconColor = Color.FromArgb(255, 64, 64, 71),
        AccentColor = Color.FromArgb(255, 0, 122, 255),
        NormalColor = Color.FromArgb(255, 52, 199, 89),
        WarningColor = Color.FromArgb(255, 255, 149, 0),
        CriticalColor = Color.FromArgb(255, 255, 59, 48),
        ChartLineColor = Color.FromArgb(255, 0, 122, 255),
        ChartGridColor = Color.FromArgb(50, 128, 128, 128),
        TargetRangeColor = Color.FromArgb(30, 52, 199, 89)
    };

    private static ThemeColors DarkTheme => new()
    {
        BackgroundColor = Color.FromArgb(255, 26, 26, 26),
        SecondaryBackgroundColor = Color.FromArgb(255, 38, 38, 38),
        TertiaryBackgroundColor = Color.FromArgb(255, 50, 50, 50),
        PrimaryTextColor = Colors.White,
        SecondaryTextColor = Color.FromArgb(255, 166, 166, 166),
        IconColor = Color.FromArgb(255, 204, 204, 204),
        AccentColor = Color.FromArgb(255, 0, 188, 212),
        NormalColor = Color.FromArgb(255, 52, 199, 89),
        WarningColor = Color.FromArgb(255, 255, 149, 0),
        CriticalColor = Color.FromArgb(255, 255, 59, 48),
        ChartLineColor = Color.FromArgb(255, 64, 156, 255),
        ChartGridColor = Color.FromArgb(50, 255, 255, 255),
        TargetRangeColor = Color.FromArgb(40, 52, 199, 89)
    };

    private static ThemeColors CharcoalTheme => new()
    {
        BackgroundColor = Color.FromArgb(255, 46, 46, 48),
        SecondaryBackgroundColor = Color.FromArgb(255, 56, 56, 61),
        TertiaryBackgroundColor = Color.FromArgb(255, 66, 66, 71),
        PrimaryTextColor = Colors.White,
        SecondaryTextColor = Color.FromArgb(255, 166, 166, 166),
        IconColor = Color.FromArgb(255, 204, 204, 204),
        AccentColor = Color.FromArgb(255, 0, 188, 212),
        NormalColor = Color.FromArgb(255, 52, 199, 89),
        WarningColor = Color.FromArgb(255, 255, 149, 0),
        CriticalColor = Color.FromArgb(255, 255, 59, 48),
        ChartLineColor = Color.FromArgb(255, 100, 180, 255),
        ChartGridColor = Color.FromArgb(50, 255, 255, 255),
        TargetRangeColor = Color.FromArgb(40, 52, 199, 89)
    };

    private static ThemeColors RainbowTheme => new()
    {
        BackgroundColor = Color.FromArgb(255, 38, 26, 51),
        SecondaryBackgroundColor = Color.FromArgb(255, 51, 38, 64),
        TertiaryBackgroundColor = Color.FromArgb(255, 64, 51, 77),
        PrimaryTextColor = Colors.White,
        SecondaryTextColor = Color.FromArgb(255, 204, 179, 230),
        IconColor = Color.FromArgb(255, 230, 204, 255),
        AccentColor = Color.FromArgb(255, 255, 102, 178),
        NormalColor = Color.FromArgb(255, 77, 255, 128),
        WarningColor = Color.FromArgb(255, 255, 204, 51),
        CriticalColor = Color.FromArgb(255, 255, 77, 128),
        ChartLineColor = Color.FromArgb(255, 178, 102, 255),
        ChartGridColor = Color.FromArgb(50, 255, 255, 255),
        TargetRangeColor = Color.FromArgb(40, 77, 255, 128)
    };

    private static ThemeColors DexcomGreenTheme => new()
    {
        BackgroundColor = Color.FromArgb(255, 13, 38, 26),
        SecondaryBackgroundColor = Color.FromArgb(255, 20, 51, 31),
        TertiaryBackgroundColor = Color.FromArgb(255, 27, 64, 40),
        PrimaryTextColor = Colors.White,
        SecondaryTextColor = Color.FromArgb(255, 153, 204, 166),
        IconColor = Color.FromArgb(255, 179, 230, 191),
        AccentColor = Color.FromArgb(255, 102, 230, 128),
        NormalColor = Color.FromArgb(255, 102, 242, 128),
        WarningColor = Color.FromArgb(255, 255, 149, 0),
        CriticalColor = Color.FromArgb(255, 255, 59, 48),
        ChartLineColor = Color.FromArgb(255, 102, 230, 128),
        ChartGridColor = Color.FromArgb(50, 255, 255, 255),
        TargetRangeColor = Color.FromArgb(40, 102, 242, 128)
    };
}

public record ThemeColors
{
    public Color BackgroundColor { get; init; }
    public Color SecondaryBackgroundColor { get; init; }
    public Color TertiaryBackgroundColor { get; init; }
    public Color PrimaryTextColor { get; init; }
    public Color SecondaryTextColor { get; init; }
    public Color IconColor { get; init; }
    public Color AccentColor { get; init; }
    public Color NormalColor { get; init; }
    public Color WarningColor { get; init; }
    public Color CriticalColor { get; init; }
    public Color ChartLineColor { get; init; }
    public Color ChartGridColor { get; init; }
    public Color TargetRangeColor { get; init; }

    public SolidColorBrush BackgroundBrush => new(BackgroundColor);
    public SolidColorBrush SecondaryBackgroundBrush => new(SecondaryBackgroundColor);
    public SolidColorBrush TertiaryBackgroundBrush => new(TertiaryBackgroundColor);
    public SolidColorBrush PrimaryTextBrush => new(PrimaryTextColor);
    public SolidColorBrush SecondaryTextBrush => new(SecondaryTextColor);
    public SolidColorBrush IconBrush => new(IconColor);
    public SolidColorBrush AccentBrush => new(AccentColor);
    public SolidColorBrush NormalBrush => new(NormalColor);
    public SolidColorBrush WarningBrush => new(WarningColor);
    public SolidColorBrush CriticalBrush => new(CriticalColor);
    public SolidColorBrush ChartLineBrush => new(ChartLineColor);
    public SolidColorBrush ChartGridBrush => new(ChartGridColor);
    public SolidColorBrush TargetRangeBrush => new(TargetRangeColor);
}
