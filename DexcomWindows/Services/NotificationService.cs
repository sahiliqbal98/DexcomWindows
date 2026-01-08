using DexcomWindows.Models;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace DexcomWindows.Services;

/// <summary>
/// Windows Toast Notification manager - Complete port of NotificationManager.swift
/// </summary>
public class NotificationService
{
    private readonly Dictionary<AlertType, DateTime> _cooldowns = new();
    private readonly string _iconFolder;

    // Cooldown intervals (matching Mac app)
    private TimeSpan _alertCooldown = TimeSpan.FromMinutes(15);
    private TimeSpan _urgentAlertCooldown = TimeSpan.FromMinutes(5);

    // Notification style preferences
    public bool ShowBanners { get; set; } = true;
    public bool PlaySound { get; set; } = true;
    public bool ShowBadge { get; set; } = true;

    private enum AlertType
    {
        Low,
        High,
        UrgentLow,
        UrgentHigh,
        RapidChange,
        StaleData
    }

    public NotificationService()
    {
        // Create folder for notification icons
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _iconFolder = Path.Combine(appData, "DexcomWindows", "NotificationIcons");
        Directory.CreateDirectory(_iconFolder);

        try
        {
            // Initialize notification manager
            AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
            AppNotificationManager.Default.Register();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize notifications: {ex.Message}");
        }
    }

    /// <summary>
    /// Create a colored notification icon with the glucose value
    /// </summary>
    private Uri? CreateNotificationIcon(int value, Color color)
    {
        try
        {
            const int size = 64;
            using var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bitmap);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // Draw colored circle
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, 2, 2, size - 4, size - 4);

            // Draw value text
            var fontSize = value.ToString().Length switch
            {
                1 => 32f,
                2 => 28f,
                3 => 22f,
                _ => 18f
            };

            using var font = new Font("Arial", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            using var whiteBrush = new SolidBrush(Color.White);
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            g.DrawString(value.ToString(), font, whiteBrush, new RectangleF(0, 0, size, size), format);

            // Save to file
            var fileName = $"glucose_{value}_{color.ToArgb():X8}.png";
            var filePath = Path.Combine(_iconFolder, fileName);
            bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);

            return new Uri(filePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to create notification icon: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Get color for alert type
    /// </summary>
    private static Color GetAlertColor(AlertType type)
    {
        return type switch
        {
            AlertType.UrgentLow => Color.FromArgb(255, 59, 48),   // Red
            AlertType.Low => Color.FromArgb(255, 69, 58),         // Red
            AlertType.UrgentHigh => Color.FromArgb(255, 59, 48),  // Red
            AlertType.High => Color.FromArgb(255, 149, 0),        // Orange
            AlertType.RapidChange => Color.FromArgb(255, 149, 0), // Orange
            AlertType.StaleData => Color.FromArgb(128, 128, 128), // Gray
            _ => Color.FromArgb(52, 199, 89)                      // Green
        };
    }

    /// <summary>
    /// Check glucose reading and send appropriate alerts
    /// Matches Mac app logic exactly
    /// </summary>
    public void CheckAndAlert(GlucoseReading reading, AlertSettings settings)
    {
        if (!settings.AlertsEnabled) return;

        var value = reading.Value;
        var trend = reading.Trend;

        // Check urgent low (most critical)
        if (value <= settings.UrgentLowThreshold && settings.UrgentLowEnabled)
        {
            SendUrgentLowAlert(value, trend);
        }
        // Check urgent high
        else if (value >= settings.UrgentHighThreshold && settings.UrgentHighEnabled)
        {
            SendUrgentHighAlert(value, trend);
        }
        // Check low
        else if (value <= settings.LowThreshold && settings.LowEnabled)
        {
            SendLowAlert(value, trend);
        }
        // Check high
        else if (value >= settings.HighThreshold && settings.HighEnabled)
        {
            SendHighAlert(value, trend);
        }

        // Check for rapid changes
        if (settings.RapidChangeAlerts && trend.IsUrgent())
        {
            SendRapidChangeAlert(value, trend);
        }
    }

    /// <summary>
    /// Send alert for stale data
    /// </summary>
    public void SendStaleDataAlert(DateTime lastReading)
    {
        if (!CanSendAlert(AlertType.StaleData)) return;

        var minutesAgo = (int)(DateTime.Now - lastReading).TotalMinutes;

        var builder = new AppNotificationBuilder()
            .AddText("No Recent Data")
            .AddText($"Last reading {minutesAgo} minutes ago")
            .AddText("Check your Dexcom connection.");

        if (PlaySound)
        {
            builder.SetAudioUri(new Uri("ms-winsoundevent:Notification.Default"));
        }

        SendNotification(builder.BuildNotification());
        _cooldowns[AlertType.StaleData] = DateTime.Now;
    }

    /// <summary>
    /// Send test notification
    /// </summary>
    public void SendTestNotification()
    {
        var builder = new AppNotificationBuilder()
            .AddText("Test Notification")
            .AddText("Dexcom Windows")
            .AddText("Notifications are working! You'll receive alerts when glucose goes out of range.");

        if (PlaySound)
        {
            builder.SetAudioUri(new Uri("ms-winsoundevent:Notification.Default"));
        }

        SendNotification(builder.BuildNotification());
    }

    /// <summary>
    /// Update cooldown intervals
    /// </summary>
    public void UpdateCooldowns(int normalMinutes, int urgentMinutes)
    {
        _alertCooldown = TimeSpan.FromMinutes(normalMinutes);
        _urgentAlertCooldown = TimeSpan.FromMinutes(urgentMinutes);
    }

    /// <summary>
    /// Reset all cooldowns
    /// </summary>
    public void ResetCooldowns()
    {
        _cooldowns.Clear();
    }

    /// <summary>
    /// Clear all notifications
    /// </summary>
    public async Task ClearAllNotificationsAsync()
    {
        try
        {
            await AppNotificationManager.Default.RemoveAllAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to clear notifications: {ex.Message}");
        }
    }

    // Private alert methods

    private void SendUrgentLowAlert(int value, TrendArrow trend)
    {
        if (!CanSendAlert(AlertType.UrgentLow)) return;

        var builder = new AppNotificationBuilder()
            .AddText($"URGENT LOW {trend.Symbol()}")
            .AddText($"{value} mg/dL")
            .AddText("Take action immediately!")
            .SetScenario(AppNotificationScenario.Urgent);

        // Add custom icon
        var iconUri = CreateNotificationIcon(value, GetAlertColor(AlertType.UrgentLow));
        if (iconUri != null)
        {
            builder.SetAppLogoOverride(iconUri, AppNotificationImageCrop.Circle);
        }

        if (PlaySound)
        {
            builder.SetAudioUri(new Uri("ms-winsoundevent:Notification.Looping.Alarm"));
        }

        builder.AddButton(new AppNotificationButton("Snooze 15 min")
            .AddArgument("action", "snooze")
            .AddArgument("minutes", "15"));

        builder.AddButton(new AppNotificationButton("Dismiss")
            .AddArgument("action", "dismiss"));

        SendNotification(builder.BuildNotification());
        _cooldowns[AlertType.UrgentLow] = DateTime.Now;
    }

    private void SendUrgentHighAlert(int value, TrendArrow trend)
    {
        if (!CanSendAlert(AlertType.UrgentHigh)) return;

        var builder = new AppNotificationBuilder()
            .AddText($"URGENT HIGH {trend.Symbol()}")
            .AddText($"{value} mg/dL")
            .AddText("Consider taking action.")
            .SetScenario(AppNotificationScenario.Urgent);

        // Add custom icon
        var iconUri = CreateNotificationIcon(value, GetAlertColor(AlertType.UrgentHigh));
        if (iconUri != null)
        {
            builder.SetAppLogoOverride(iconUri, AppNotificationImageCrop.Circle);
        }

        if (PlaySound)
        {
            builder.SetAudioUri(new Uri("ms-winsoundevent:Notification.Looping.Alarm"));
        }

        builder.AddButton(new AppNotificationButton("Snooze 15 min")
            .AddArgument("action", "snooze")
            .AddArgument("minutes", "15"));

        builder.AddButton(new AppNotificationButton("Dismiss")
            .AddArgument("action", "dismiss"));

        SendNotification(builder.BuildNotification());
        _cooldowns[AlertType.UrgentHigh] = DateTime.Now;
    }

    private void SendLowAlert(int value, TrendArrow trend)
    {
        if (!CanSendAlert(AlertType.Low)) return;

        var builder = new AppNotificationBuilder()
            .AddText($"Low Glucose {trend.Symbol()}")
            .AddText($"{value} mg/dL")
            .AddText("Your glucose is below target range.");

        // Add custom icon
        var iconUri = CreateNotificationIcon(value, GetAlertColor(AlertType.Low));
        if (iconUri != null)
        {
            builder.SetAppLogoOverride(iconUri, AppNotificationImageCrop.Circle);
        }

        if (PlaySound)
        {
            builder.SetAudioUri(new Uri("ms-winsoundevent:Notification.Default"));
        }

        SendNotification(builder.BuildNotification());
        _cooldowns[AlertType.Low] = DateTime.Now;
    }

    private void SendHighAlert(int value, TrendArrow trend)
    {
        if (!CanSendAlert(AlertType.High)) return;

        var builder = new AppNotificationBuilder()
            .AddText($"High Glucose {trend.Symbol()}")
            .AddText($"{value} mg/dL")
            .AddText("Your glucose is above target range.");

        // Add custom icon
        var iconUri = CreateNotificationIcon(value, GetAlertColor(AlertType.High));
        if (iconUri != null)
        {
            builder.SetAppLogoOverride(iconUri, AppNotificationImageCrop.Circle);
        }

        if (PlaySound)
        {
            builder.SetAudioUri(new Uri("ms-winsoundevent:Notification.Default"));
        }

        SendNotification(builder.BuildNotification());
        _cooldowns[AlertType.High] = DateTime.Now;
    }

    private void SendRapidChangeAlert(int value, TrendArrow trend)
    {
        if (!CanSendAlert(AlertType.RapidChange)) return;

        var title = trend == TrendArrow.DoubleUp
            ? $"Rising Quickly {trend.Symbol()}"
            : $"Falling Quickly {trend.Symbol()}";

        var body = trend == TrendArrow.DoubleUp
            ? "Glucose is rising rapidly"
            : "Glucose is falling rapidly";

        var builder = new AppNotificationBuilder()
            .AddText(title)
            .AddText($"{value} mg/dL")
            .AddText(body);

        // Add custom icon
        var iconUri = CreateNotificationIcon(value, GetAlertColor(AlertType.RapidChange));
        if (iconUri != null)
        {
            builder.SetAppLogoOverride(iconUri, AppNotificationImageCrop.Circle);
        }

        if (PlaySound)
        {
            builder.SetAudioUri(new Uri("ms-winsoundevent:Notification.Default"));
        }

        SendNotification(builder.BuildNotification());
        _cooldowns[AlertType.RapidChange] = DateTime.Now;
    }

    // Cooldown checking

    private bool CanSendAlert(AlertType type)
    {
        if (!_cooldowns.TryGetValue(type, out var lastAlert))
            return true;

        var cooldown = type is AlertType.UrgentLow or AlertType.UrgentHigh
            ? _urgentAlertCooldown
            : _alertCooldown;

        return DateTime.Now - lastAlert > cooldown;
    }

    private void SendNotification(AppNotification notification)
    {
        try
        {
            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to send notification: {ex.Message}");
        }
    }

    private void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        // Handle notification interaction
        // Parse action and handle snooze, dismiss, etc.
        System.Diagnostics.Debug.WriteLine($"Notification invoked: {args.Argument}");
    }
}
