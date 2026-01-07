using System.Text.Json;
using System.Text.RegularExpressions;

namespace DexcomWindows.Models;

/// <summary>
/// Represents a single glucose reading from Dexcom - Port of GlucoseReading.swift
/// </summary>
public record GlucoseReading
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; }
    public int Value { get; init; }
    public TrendArrow Trend { get; init; }

    /// <summary>
    /// Color category based on glucose value - matches Mac app logic exactly
    /// </summary>
    public GlucoseColorCategory ColorCategory => Value switch
    {
        < 55 => GlucoseColorCategory.UrgentLow,   // Urgent low - Red
        < 70 => GlucoseColorCategory.Low,          // Low - Red
        < 80 => GlucoseColorCategory.Warning,      // Low warning - Yellow
        < 160 => GlucoseColorCategory.Normal,      // In range - Green
        < 180 => GlucoseColorCategory.Warning,     // High warning - Yellow
        < 250 => GlucoseColorCategory.High,        // High - Orange/Red
        _ => GlucoseColorCategory.UrgentHigh       // Urgent high - Red
    };

    /// <summary>
    /// Formatted value string
    /// </summary>
    public string FormattedValue => Value.ToString();

    /// <summary>
    /// Display string with trend arrow
    /// </summary>
    public string DisplayString => $"{Value} {Trend.Symbol()}";

    /// <summary>
    /// Short display for system tray
    /// </summary>
    public string TrayDisplayString => $"{Value}{Trend.Symbol()}";

    /// <summary>
    /// Time ago string - matches Mac app format exactly
    /// </summary>
    public string TimeAgoString
    {
        get
        {
            var interval = DateTime.Now - Timestamp;
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
    }

    /// <summary>
    /// Minutes since this reading
    /// </summary>
    public int MinutesAgo => (int)(DateTime.Now - Timestamp).TotalMinutes;

    /// <summary>
    /// Parse from Dexcom API response dictionary
    /// Handles multiple date formats: Date(ms), Date(ms-tz), /Date(ms)/
    /// </summary>
    public static GlucoseReading? FromDictionary(JsonElement element)
    {
        DateTime? timestamp = null;

        // Try WT first (wall time), then DT, then ST
        if (element.TryGetProperty("WT", out var wt))
            timestamp = ParseDexcomDate(wt);

        if (timestamp == null && element.TryGetProperty("DT", out var dt))
            timestamp = ParseDexcomDate(dt);

        if (timestamp == null && element.TryGetProperty("ST", out var st))
            timestamp = ParseDexcomDate(st);

        if (timestamp == null)
            return null;

        if (!element.TryGetProperty("Value", out var valueElement))
            return null;

        var value = valueElement.GetInt32();

        // Parse trend - can be Int or String
        TrendArrow trend = TrendArrow.Unknown;
        if (element.TryGetProperty("Trend", out var trendElement))
        {
            if (trendElement.ValueKind == JsonValueKind.Number)
            {
                trend = (TrendArrow)trendElement.GetInt32();
            }
            else if (trendElement.ValueKind == JsonValueKind.String)
            {
                trend = TrendArrowExtensions.FromString(trendElement.GetString() ?? "");
            }
        }

        return new GlucoseReading
        {
            Timestamp = timestamp.Value,
            Value = value,
            Trend = trend
        };
    }

    /// <summary>
    /// Parse Dexcom date from various formats
    /// </summary>
    private static DateTime? ParseDexcomDate(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            var ms = element.GetInt64();
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime;
        }

        if (element.ValueKind != JsonValueKind.String)
            return null;

        var str = element.GetString();
        if (string.IsNullOrEmpty(str))
            return null;

        // Remove optional slashes
        str = str.Trim().TrimStart('/').TrimEnd('/');

        // Extract milliseconds from Date(ms) or Date(ms-tz)
        var match = Regex.Match(str, @"Date\((\d+)");
        if (!match.Success)
            return null;

        if (!long.TryParse(match.Groups[1].Value, out var milliseconds))
            return null;

        return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).LocalDateTime;
    }
}

public enum GlucoseColorCategory
{
    UrgentLow,   // < 55 - Red, urgent
    Low,         // 55-70 - Red
    Warning,     // 70-80 or 160-180 - Yellow/Orange
    Normal,      // 80-160 - Green
    High,        // 180-250 - Orange
    UrgentHigh   // > 250 - Red, urgent
}
