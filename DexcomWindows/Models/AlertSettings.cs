using System.Text.Json;

namespace DexcomWindows.Models;

/// <summary>
/// User-configurable alert thresholds and preferences - Complete port from Mac app
/// </summary>
public record AlertSettings
{
    // Master toggle
    public bool AlertsEnabled { get; init; } = true;

    // Low alerts
    public bool LowEnabled { get; init; } = true;
    public int LowThreshold { get; init; } = 70;  // mg/dL

    // High alerts
    public bool HighEnabled { get; init; } = true;
    public int HighThreshold { get; init; } = 180;  // mg/dL

    // Urgent low alerts
    public bool UrgentLowEnabled { get; init; } = true;
    public int UrgentLowThreshold { get; init; } = 55;  // mg/dL

    // Urgent high alerts
    public bool UrgentHighEnabled { get; init; } = true;
    public int UrgentHighThreshold { get; init; } = 250;  // mg/dL

    // Rapid change alerts
    public bool RapidChangeAlerts { get; init; } = true;

    // Sound preferences
    public bool UseCustomSounds { get; init; } = false;

    // Snooze duration (minutes)
    public int SnoozeDuration { get; init; } = 30;

    // Repeat alerts
    public bool RepeatAlerts { get; init; } = true;
    public int RepeatInterval { get; init; } = 15;  // minutes

    // Stale data alert
    public bool StaleDataAlertEnabled { get; init; } = true;
    public int StaleDataThreshold { get; init; } = 15;  // minutes without new data

    // Validation
    public bool IsValid =>
        UrgentLowThreshold < LowThreshold &&
        LowThreshold < HighThreshold &&
        HighThreshold < UrgentHighThreshold;

    // Default settings
    public static AlertSettings Default => new();

    // Serialization
    public string ToJson() => JsonSerializer.Serialize(this);

    public static AlertSettings FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<AlertSettings>(json) ?? Default;
        }
        catch
        {
            return Default;
        }
    }

    // Create a copy with modified values
    public AlertSettings With(
        bool? alertsEnabled = null,
        bool? lowEnabled = null,
        int? lowThreshold = null,
        bool? highEnabled = null,
        int? highThreshold = null,
        bool? urgentLowEnabled = null,
        int? urgentLowThreshold = null,
        bool? urgentHighEnabled = null,
        int? urgentHighThreshold = null,
        bool? rapidChangeAlerts = null,
        bool? useCustomSounds = null,
        int? snoozeDuration = null,
        bool? repeatAlerts = null,
        int? repeatInterval = null,
        bool? staleDataAlertEnabled = null,
        int? staleDataThreshold = null)
    {
        return new AlertSettings
        {
            AlertsEnabled = alertsEnabled ?? AlertsEnabled,
            LowEnabled = lowEnabled ?? LowEnabled,
            LowThreshold = lowThreshold ?? LowThreshold,
            HighEnabled = highEnabled ?? HighEnabled,
            HighThreshold = highThreshold ?? HighThreshold,
            UrgentLowEnabled = urgentLowEnabled ?? UrgentLowEnabled,
            UrgentLowThreshold = urgentLowThreshold ?? UrgentLowThreshold,
            UrgentHighEnabled = urgentHighEnabled ?? UrgentHighEnabled,
            UrgentHighThreshold = urgentHighThreshold ?? UrgentHighThreshold,
            RapidChangeAlerts = rapidChangeAlerts ?? RapidChangeAlerts,
            UseCustomSounds = useCustomSounds ?? UseCustomSounds,
            SnoozeDuration = snoozeDuration ?? SnoozeDuration,
            RepeatAlerts = repeatAlerts ?? RepeatAlerts,
            RepeatInterval = repeatInterval ?? RepeatInterval,
            StaleDataAlertEnabled = staleDataAlertEnabled ?? StaleDataAlertEnabled,
            StaleDataThreshold = staleDataThreshold ?? StaleDataThreshold
        };
    }
}
