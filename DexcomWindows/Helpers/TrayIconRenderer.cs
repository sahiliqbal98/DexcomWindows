using DexcomWindows.Models;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace DexcomWindows.Helpers;

/// <summary>
/// Renders glucose values and trend arrows as tray icons
/// </summary>
public static class TrayIconRenderer
{
    // Icon size (standard Windows tray icon)
    private const int IconSize = 16;

    // For high-DPI we create larger icons and let Windows scale
    private const int LargeIconSize = 32;

    /// <summary>
    /// Create an icon showing the glucose value and trend arrow
    /// </summary>
    public static Icon CreateGlucoseTextIcon(GlucoseReading? reading)
    {
        if (reading == null)
        {
            return CreateNoDataIcon();
        }

        return CreateTextIcon(
            reading.Value.ToString(),
            reading.Trend.Symbol(),
            GetGlucoseColor(reading.ColorCategory));
    }

    /// <summary>
    /// Create an icon showing "-- ●" for no data state
    /// </summary>
    public static Icon CreateNoDataIcon()
    {
        return CreateTextIcon("--", "●", Color.FromArgb(128, 128, 128));
    }

    /// <summary>
    /// Create a loading/refresh icon (simple spinner representation)
    /// </summary>
    public static Icon CreateLoadingIcon()
    {
        using var bitmap = new Bitmap(LargeIconSize, LargeIconSize);
        using var g = Graphics.FromImage(bitmap);

        SetupGraphics(g);
        g.Clear(Color.Transparent);

        // Draw a circular loading indicator
        using var pen = new Pen(Color.FromArgb(0, 120, 215), 2);
        pen.StartCap = LineCap.Round;
        pen.EndCap = LineCap.Round;

        // Draw arc
        g.DrawArc(pen, 4, 4, 24, 24, 0, 270);

        return Icon.FromHandle(bitmap.GetHicon());
    }

    /// <summary>
    /// Create an icon with text and arrow
    /// </summary>
    private static Icon CreateTextIcon(string value, string arrow, Color color)
    {
        using var bitmap = new Bitmap(LargeIconSize, LargeIconSize);
        using var g = Graphics.FromImage(bitmap);

        SetupGraphics(g);
        g.Clear(Color.Transparent);

        // Draw background circle for better visibility
        using var bgBrush = new SolidBrush(Color.FromArgb(40, color));
        g.FillEllipse(bgBrush, 0, 0, LargeIconSize - 1, LargeIconSize - 1);

        // Draw border
        using var borderPen = new Pen(Color.FromArgb(80, color), 1.5f);
        g.DrawEllipse(borderPen, 1, 1, LargeIconSize - 3, LargeIconSize - 3);

        // Draw center fill with glucose color
        using var centerBrush = new SolidBrush(color);
        g.FillEllipse(centerBrush, 4, 4, LargeIconSize - 9, LargeIconSize - 9);

        // Draw a small highlight for 3D effect
        using var highlightBrush = new SolidBrush(Color.FromArgb(60, 255, 255, 255));
        g.FillEllipse(highlightBrush, 6, 5, 8, 6);

        return Icon.FromHandle(bitmap.GetHicon());
    }

    /// <summary>
    /// Create a detailed text icon (for larger displays or tooltips)
    /// This creates a wider icon with actual text - useful for some taskbar modes
    /// </summary>
    public static Icon CreateDetailedTextIcon(GlucoseReading? reading, int width = 48, int height = 16)
    {
        if (reading == null)
        {
            return CreateDetailedNoDataIcon(width, height);
        }

        using var bitmap = new Bitmap(width, height);
        using var g = Graphics.FromImage(bitmap);

        SetupGraphics(g);
        g.Clear(Color.Transparent);

        var color = GetGlucoseColor(reading.ColorCategory);
        var text = $"{reading.Value}{reading.Trend.Symbol()}";

        // Use a nice rounded font
        using var font = GetBestFont(9f, FontStyle.Bold);
        using var brush = new SolidBrush(color);

        // Measure text
        var textSize = g.MeasureString(text, font);

        // Center the text
        float x = (width - textSize.Width) / 2;
        float y = (height - textSize.Height) / 2;

        // Draw text shadow for better visibility
        using var shadowBrush = new SolidBrush(Color.FromArgb(100, 0, 0, 0));
        g.DrawString(text, font, shadowBrush, x + 1, y + 1);

        // Draw main text
        g.DrawString(text, font, brush, x, y);

        return Icon.FromHandle(bitmap.GetHicon());
    }

    private static Icon CreateDetailedNoDataIcon(int width, int height)
    {
        using var bitmap = new Bitmap(width, height);
        using var g = Graphics.FromImage(bitmap);

        SetupGraphics(g);
        g.Clear(Color.Transparent);

        using var font = GetBestFont(9f, FontStyle.Bold);
        using var brush = new SolidBrush(Color.FromArgb(128, 128, 128));

        var text = "-- ●";
        var textSize = g.MeasureString(text, font);
        float x = (width - textSize.Width) / 2;
        float y = (height - textSize.Height) / 2;

        g.DrawString(text, font, brush, x, y);

        return Icon.FromHandle(bitmap.GetHicon());
    }

    /// <summary>
    /// Get the best available font for tray icon rendering
    /// Prefers Segoe UI Variable, falls back to Segoe UI, then Arial
    /// </summary>
    private static Font GetBestFont(float size, FontStyle style)
    {
        var fontFamilies = new[]
        {
            "Segoe UI Variable",
            "Segoe UI",
            "Segoe UI Semibold",
            "Arial"
        };

        foreach (var family in fontFamilies)
        {
            try
            {
                var font = new Font(family, size, style, GraphicsUnit.Point);
                if (font.Name.Equals(family, StringComparison.OrdinalIgnoreCase) ||
                    font.OriginalFontName?.Equals(family, StringComparison.OrdinalIgnoreCase) == true)
                {
                    return font;
                }
                font.Dispose();
            }
            catch
            {
                // Font not available, try next
            }
        }

        // Fallback
        return new Font(FontFamily.GenericSansSerif, size, style, GraphicsUnit.Point);
    }

    private static void SetupGraphics(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;
    }

    /// <summary>
    /// Get color for glucose category
    /// </summary>
    public static Color GetGlucoseColor(GlucoseColorCategory category)
    {
        return category switch
        {
            GlucoseColorCategory.UrgentLow => Color.FromArgb(255, 59, 48),   // Bright Red
            GlucoseColorCategory.Low => Color.FromArgb(255, 69, 58),         // Red
            GlucoseColorCategory.Warning => Color.FromArgb(255, 149, 0),      // Orange
            GlucoseColorCategory.Normal => Color.FromArgb(52, 199, 89),       // Green
            GlucoseColorCategory.High => Color.FromArgb(255, 149, 0),         // Orange
            GlucoseColorCategory.UrgentHigh => Color.FromArgb(255, 59, 48),  // Bright Red
            _ => Color.FromArgb(128, 128, 128)                                // Gray
        };
    }

    /// <summary>
    /// Create a simple colored circle icon
    /// </summary>
    public static Icon CreateColoredCircleIcon(Color color, int size = 16)
    {
        using var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);

        SetupGraphics(g);
        g.Clear(Color.Transparent);

        // Draw filled circle
        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, 1, 1, size - 2, size - 2);

        // Draw border
        var borderColor = Color.FromArgb(
            Math.Max(0, color.R - 40),
            Math.Max(0, color.G - 40),
            Math.Max(0, color.B - 40));
        using var pen = new Pen(borderColor, 1);
        g.DrawEllipse(pen, 1, 1, size - 3, size - 3);

        return Icon.FromHandle(bitmap.GetHicon());
    }
}
