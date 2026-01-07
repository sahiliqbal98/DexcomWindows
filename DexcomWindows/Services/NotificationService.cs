using DexcomWindows.Models;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace DexcomWindows.Services;

/// <summary>
/// Windows Toast Notification manager - Complete port of NotificationManager.swift
/// </summary>
public class NotificationService
{
    private readonly Dictionary<AlertType, DateTime> _cooldowns = new();

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
