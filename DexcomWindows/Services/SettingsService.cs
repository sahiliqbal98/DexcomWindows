using System.Text.Json;
using DexcomWindows.Models;
using Microsoft.Win32;

namespace DexcomWindows.Services;

/// <summary>
/// App settings persistence - Port of SettingsViewModel persistence logic
/// </summary>
public class SettingsService
{
    private const string RegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "DexcomWindows";

    private readonly string _settingsPath;

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var folder = Path.Combine(appData, "DexcomWindows");
        Directory.CreateDirectory(folder);
        _settingsPath = Path.Combine(folder, "settings.json");

        LoadSettings();
    }

    // General settings
    public ColorTheme ColorTheme { get; set; } = ColorTheme.System;
    public TimerBarStyle TimerBarStyle { get; set; } = TimerBarStyle.Elapsed;
    public int TargetLow { get; set; } = 80;
    public int TargetHigh { get; set; } = 160;

    // Alert settings
    public AlertSettings AlertSettings { get; set; } = AlertSettings.Default;
    public int NormalAlertCooldown { get; set; } = 15;
    public int UrgentAlertCooldown { get; set; } = 5;

    // Notification style
    public bool ShowBanners { get; set; } = true;
    public bool PlaySound { get; set; } = true;
    public bool ShowBadge { get; set; } = true;

    // Credential-related
    public DexcomShareAPI.Server Server { get; set; } = DexcomShareAPI.Server.US;
    public DexcomShareAPI.AuthMethod AuthMethod { get; set; } = DexcomShareAPI.AuthMethod.Username;
    public DateTime? SessionTimestamp { get; set; }

    // Chart settings
    public TimeRange DefaultTimeRange { get; set; } = TimeRange.ThreeHours;

    // UI hints
    public bool TrayPinHintDismissed { get; set; } = false;

    // Startup
    public bool StartWithWindows
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, false);
                return key?.GetValue(AppName) != null;
            }
            catch
            {
                return false;
            }
        }
        set
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
                if (value)
                {
                    var exePath = Environment.ProcessPath;
                    key?.SetValue(AppName, $"\"{exePath}\"");
                }
                else
                {
                    key?.DeleteValue(AppName, false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set startup: {ex.Message}");
            }
        }
    }

    public void SaveSettings()
    {
        try
        {
            var data = new Dictionary<string, object?>
            {
                ["ColorTheme"] = ColorTheme.ToString(),
                ["TimerBarStyle"] = TimerBarStyle.ToString(),
                ["TargetLow"] = TargetLow,
                ["TargetHigh"] = TargetHigh,
                ["AlertSettings"] = AlertSettings.ToJson(),
                ["NormalAlertCooldown"] = NormalAlertCooldown,
                ["UrgentAlertCooldown"] = UrgentAlertCooldown,
                ["ShowBanners"] = ShowBanners,
                ["PlaySound"] = PlaySound,
                ["ShowBadge"] = ShowBadge,
                ["Server"] = Server.ToString(),
                ["AuthMethod"] = AuthMethod.ToString(),
                ["SessionTimestamp"] = SessionTimestamp?.ToString("O") ?? "",
                ["DefaultTimeRange"] = DefaultTimeRange.ToString(),
                ["TrayPinHintDismissed"] = TrayPinHintDismissed
            };

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }

    public void LoadSettings()
    {
        if (!File.Exists(_settingsPath)) return;

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (data == null) return;

            if (data.TryGetValue("ColorTheme", out var ct) && Enum.TryParse<ColorTheme>(ct.GetString(), out var theme))
                ColorTheme = theme;

            if (data.TryGetValue("TimerBarStyle", out var ts) && Enum.TryParse<TimerBarStyle>(ts.GetString(), out var style))
                TimerBarStyle = style;

            if (data.TryGetValue("TargetLow", out var tl))
                TargetLow = tl.GetInt32();

            if (data.TryGetValue("TargetHigh", out var th))
                TargetHigh = th.GetInt32();

            if (data.TryGetValue("AlertSettings", out var als))
                AlertSettings = Models.AlertSettings.FromJson(als.GetString() ?? "");

            if (data.TryGetValue("NormalAlertCooldown", out var nac))
                NormalAlertCooldown = nac.GetInt32();

            if (data.TryGetValue("UrgentAlertCooldown", out var uac))
                UrgentAlertCooldown = uac.GetInt32();

            if (data.TryGetValue("ShowBanners", out var sb))
                ShowBanners = sb.GetBoolean();

            if (data.TryGetValue("PlaySound", out var ps))
                PlaySound = ps.GetBoolean();

            if (data.TryGetValue("ShowBadge", out var sba))
                ShowBadge = sba.GetBoolean();

            if (data.TryGetValue("Server", out var srv) && Enum.TryParse<DexcomShareAPI.Server>(srv.GetString(), out var server))
                Server = server;

            if (data.TryGetValue("AuthMethod", out var am) && Enum.TryParse<DexcomShareAPI.AuthMethod>(am.GetString(), out var method))
                AuthMethod = method;

            if (data.TryGetValue("SessionTimestamp", out var st))
            {
                var str = st.GetString();
                if (!string.IsNullOrEmpty(str) && DateTime.TryParse(str, out var timestamp))
                    SessionTimestamp = timestamp;
            }

            if (data.TryGetValue("DefaultTimeRange", out var dtr) && Enum.TryParse<TimeRange>(dtr.GetString(), out var timeRange))
                DefaultTimeRange = timeRange;

            if (data.TryGetValue("TrayPinHintDismissed", out var tph))
                TrayPinHintDismissed = tph.GetBoolean();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            // Use defaults on parse error
        }
    }

    public void ClearCredentialSettings()
    {
        SessionTimestamp = null;
        SaveSettings();
    }

    public void ResetToDefaults()
    {
        ColorTheme = ColorTheme.System;
        TimerBarStyle = TimerBarStyle.Elapsed;
        TargetLow = 80;
        TargetHigh = 160;
        AlertSettings = AlertSettings.Default;
        NormalAlertCooldown = 15;
        UrgentAlertCooldown = 5;
        ShowBanners = true;
        PlaySound = true;
        ShowBadge = true;
        DefaultTimeRange = TimeRange.ThreeHours;
        SaveSettings();
    }
}

/// <summary>
/// Timer bar style preference - Port from SettingsViewModel.swift
/// </summary>
public enum TimerBarStyle
{
    Elapsed,   // Bar fills up as time passes
    Remaining  // Bar empties as update approaches
}

/// <summary>
/// Color themes matching Mac app exactly
/// </summary>
public enum ColorTheme
{
    System,
    Light,
    Dark,
    Charcoal,
    Rainbow,
    DexcomGreen
}

/// <summary>
/// Time range options for chart display
/// </summary>
public enum TimeRange
{
    OneHour,
    ThreeHours,
    SixHours,
    TwelveHours,
    TwentyFourHours
}

public static class TimeRangeExtensions
{
    public static string DisplayName(this TimeRange range) => range switch
    {
        TimeRange.OneHour => "1 Hour",
        TimeRange.ThreeHours => "3 Hours",
        TimeRange.SixHours => "6 Hours",
        TimeRange.TwelveHours => "12 Hours",
        TimeRange.TwentyFourHours => "24 Hours",
        _ => "Unknown"
    };

    public static string ShortName(this TimeRange range) => range switch
    {
        TimeRange.OneHour => "1h",
        TimeRange.ThreeHours => "3h",
        TimeRange.SixHours => "6h",
        TimeRange.TwelveHours => "12h",
        TimeRange.TwentyFourHours => "24h",
        _ => "?"
    };

    public static double Seconds(this TimeRange range) => range switch
    {
        TimeRange.OneHour => 1 * 3600,
        TimeRange.ThreeHours => 3 * 3600,
        TimeRange.SixHours => 6 * 3600,
        TimeRange.TwelveHours => 12 * 3600,
        TimeRange.TwentyFourHours => 24 * 3600,
        _ => 3 * 3600
    };

    public static int Minutes(this TimeRange range) => range switch
    {
        TimeRange.OneHour => 60,
        TimeRange.ThreeHours => 180,
        TimeRange.SixHours => 360,
        TimeRange.TwelveHours => 720,
        TimeRange.TwentyFourHours => 1440,
        _ => 180
    };
}
