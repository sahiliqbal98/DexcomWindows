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
    /// Create an icon showing "--" for no data state (red to draw attention)
    /// </summary>
    public static Icon CreateNoDataIcon()
    {
        return CreateTextIcon("--", "", Color.FromArgb(255, 59, 48)); // Red - needs attention
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
    /// Create an icon with glucose value text and dynamic color
    /// </summary>
    private static Icon CreateTextIcon(string value, string arrow, Color color)
    {
        // Use 48x48 - good balance of quality and compatibility
        const int size = 48;
        
        try
        {
            using var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bitmap);

            // High quality rendering
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.CompositingQuality = CompositingQuality.HighQuality;
            
            g.Clear(Color.Transparent);

            // Fill with glucose color - simple rectangle for max coverage
            using (var bgBrush = new SolidBrush(color))
            {
                g.FillRectangle(bgBrush, 0, 0, size, size);
            }

            // Determine font size based on text length
            float fontSize = value.Length switch
            {
                1 => 32f,
                2 => 28f,
                3 => 22f,
                _ => 18f
            };

            // Create font - use Arial which is always available
            using var font = new Font("Arial", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            using var whiteBrush = new SolidBrush(Color.White);
            using var shadowBrush = new SolidBrush(Color.FromArgb(80, 0, 0, 0));

            // Measure text to center it
            var textSize = g.MeasureString(value, font);
            float x = (size - textSize.Width) / 2;
            float y = (size - textSize.Height) / 2;

            // Draw shadow then white text
            g.DrawString(value, font, shadowBrush, x + 1, y + 1);
            g.DrawString(value, font, whiteBrush, x, y);

            // Create icon - need to handle the HICON properly
            IntPtr hIcon = bitmap.GetHicon();
            
            // Create a new icon from the handle
            using var tempIcon = Icon.FromHandle(hIcon);
            
            // Clone it so we own the memory
            var resultIcon = (Icon)tempIcon.Clone();
            
            // Destroy the original handle
            DestroyIcon(hIcon);
            
            return resultIcon;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CreateTextIcon error: {ex.Message}");
            // Return a simple colored square as fallback
            return CreateSimpleFallbackIcon(color);
        }
    }

    /// <summary>
    /// Simple fallback icon if text rendering fails
    /// </summary>
    private static Icon CreateSimpleFallbackIcon(Color color)
    {
        using var bitmap = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bitmap);
        
        g.Clear(color);
        
        IntPtr hIcon = bitmap.GetHicon();
        using var tempIcon = Icon.FromHandle(hIcon);
        var resultIcon = (Icon)tempIcon.Clone();
        DestroyIcon(hIcon);
        
        return resultIcon;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

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
