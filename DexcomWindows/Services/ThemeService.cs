using DexcomWindows.Models;
using DexcomWindows.Themes;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace DexcomWindows.Services;

/// <summary>
/// Service for applying color themes dynamically across the application
/// </summary>
public class ThemeService
{
    private static ThemeService? _instance;
    public static ThemeService Instance => _instance ??= new ThemeService();

    public event EventHandler<ColorTheme>? ThemeChanged;

    private ColorTheme _currentTheme = ColorTheme.System;
    private ThemeColors? _currentColors;
    private readonly UISettings _uiSettings;

    public ColorTheme CurrentTheme => _currentTheme;
    public ThemeColors CurrentColors => _currentColors ?? ColorThemes.GetColors(_currentTheme);

    private ThemeService()
    {
        _uiSettings = new UISettings();
        _uiSettings.ColorValuesChanged += OnSystemColorsChanged;
        UpdateColors();
    }

    /// <summary>
    /// Set the current theme
    /// </summary>
    public void SetTheme(ColorTheme theme)
    {
        if (_currentTheme == theme) return;

        _currentTheme = theme;
        UpdateColors();
        ThemeChanged?.Invoke(this, theme);
    }

    /// <summary>
    /// Update colors based on current theme
    /// </summary>
    private void UpdateColors()
    {
        if (_currentTheme == ColorTheme.System)
        {
            // Detect system theme
            var isDarkMode = IsSystemDarkMode();
            _currentColors = isDarkMode ? ColorThemes.GetColors(ColorTheme.Dark) : ColorThemes.GetColors(ColorTheme.Light);
        }
        else
        {
            _currentColors = ColorThemes.GetColors(_currentTheme);
        }
    }

    /// <summary>
    /// Check if system is in dark mode
    /// </summary>
    public bool IsSystemDarkMode()
    {
        try
        {
            var foreground = _uiSettings.GetColorValue(UIColorType.Foreground);
            // If foreground is light, we're in dark mode
            return foreground.R > 128 && foreground.G > 128 && foreground.B > 128;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Handle system color changes
    /// </summary>
    private void OnSystemColorsChanged(UISettings sender, object args)
    {
        if (_currentTheme == ColorTheme.System)
        {
            UpdateColors();
            ThemeChanged?.Invoke(this, _currentTheme);
        }
    }

    /// <summary>
    /// Get the ElementTheme for WinUI controls
    /// </summary>
    public ElementTheme GetElementTheme()
    {
        return _currentTheme switch
        {
            ColorTheme.System => ElementTheme.Default,
            ColorTheme.Light => ElementTheme.Light,
            ColorTheme.Dark => ElementTheme.Dark,
            ColorTheme.Charcoal => ElementTheme.Dark,
            ColorTheme.Rainbow => ElementTheme.Dark,
            ColorTheme.DexcomGreen => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    /// <summary>
    /// Apply theme to a FrameworkElement
    /// </summary>
    public void ApplyTheme(FrameworkElement element)
    {
        if (element == null) return;

        element.RequestedTheme = GetElementTheme();
    }

    /// <summary>
    /// Get color for glucose value based on current theme
    /// </summary>
    public Color GetGlucoseColor(GlucoseColorCategory category)
    {
        return category switch
        {
            GlucoseColorCategory.UrgentLow => CurrentColors.CriticalColor,
            GlucoseColorCategory.Low => CurrentColors.CriticalColor,
            GlucoseColorCategory.Warning => CurrentColors.WarningColor,
            GlucoseColorCategory.Normal => CurrentColors.NormalColor,
            GlucoseColorCategory.High => CurrentColors.WarningColor,
            GlucoseColorCategory.UrgentHigh => CurrentColors.CriticalColor,
            _ => Color.FromArgb(255, 128, 128, 128)
        };
    }

    /// <summary>
    /// Get a SolidColorBrush for a glucose category
    /// </summary>
    public SolidColorBrush GetGlucoseBrush(GlucoseColorCategory category)
    {
        return new SolidColorBrush(GetGlucoseColor(category));
    }

    /// <summary>
    /// Get the accent color
    /// </summary>
    public Color AccentColor => CurrentColors.AccentColor;

    /// <summary>
    /// Get the normal (in-range) color
    /// </summary>
    public Color NormalColor => CurrentColors.NormalColor;

    /// <summary>
    /// Get the warning color
    /// </summary>
    public Color WarningColor => CurrentColors.WarningColor;

    /// <summary>
    /// Get the critical color
    /// </summary>
    public Color CriticalColor => CurrentColors.CriticalColor;

    /// <summary>
    /// Get background color
    /// </summary>
    public Color BackgroundColor => CurrentColors.BackgroundColor;

    /// <summary>
    /// Get secondary background color
    /// </summary>
    public Color SecondaryBackgroundColor => CurrentColors.SecondaryBackgroundColor;

    /// <summary>
    /// Get primary text color
    /// </summary>
    public Color PrimaryTextColor => CurrentColors.PrimaryTextColor;

    /// <summary>
    /// Get secondary text color
    /// </summary>
    public Color SecondaryTextColor => CurrentColors.SecondaryTextColor;
}
