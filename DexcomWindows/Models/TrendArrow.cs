namespace DexcomWindows.Models;

/// <summary>
/// Dexcom trend arrow values - Complete port of TrendArrow.swift with all 10 cases
/// </summary>
public enum TrendArrow
{
    Unknown = 0,
    DoubleUp = 1,       // Rising quickly (>3 mg/dL per minute)
    SingleUp = 2,       // Rising (2-3 mg/dL per minute)
    FortyFiveUp = 3,    // Rising slowly (1-2 mg/dL per minute)
    Flat = 4,           // Stable (<1 mg/dL per minute)
    FortyFiveDown = 5,  // Falling slowly (1-2 mg/dL per minute)
    SingleDown = 6,     // Falling (2-3 mg/dL per minute)
    DoubleDown = 7,     // Falling quickly (>3 mg/dL per minute)
    NotComputable = 8,  // Cannot be computed
    RateOutOfRange = 9  // Rate out of range
}

public static class TrendArrowExtensions
{
    /// <summary>
    /// Unicode symbol for the trend - matches Mac app exactly
    /// </summary>
    public static string Symbol(this TrendArrow trend) => trend switch
    {
        TrendArrow.Unknown => "?",
        TrendArrow.DoubleUp => "⇈",
        TrendArrow.SingleUp => "↑",
        TrendArrow.FortyFiveUp => "↗",
        TrendArrow.Flat => "→",
        TrendArrow.FortyFiveDown => "↘",
        TrendArrow.SingleDown => "↓",
        TrendArrow.DoubleDown => "⇊",
        TrendArrow.NotComputable => "?",
        TrendArrow.RateOutOfRange => "?",
        _ => "?"
    };

    /// <summary>
    /// Human-readable description - matches Mac app exactly
    /// </summary>
    public static string Description(this TrendArrow trend) => trend switch
    {
        TrendArrow.Unknown => "Unknown",
        TrendArrow.DoubleUp => "Rising quickly",
        TrendArrow.SingleUp => "Rising",
        TrendArrow.FortyFiveUp => "Rising slowly",
        TrendArrow.Flat => "Stable",
        TrendArrow.FortyFiveDown => "Falling slowly",
        TrendArrow.SingleDown => "Falling",
        TrendArrow.DoubleDown => "Falling quickly",
        TrendArrow.NotComputable => "Cannot compute",
        TrendArrow.RateOutOfRange => "Rate out of range",
        _ => "Unknown"
    };

    /// <summary>
    /// Whether this trend indicates urgency
    /// </summary>
    public static bool IsUrgent(this TrendArrow trend) =>
        trend == TrendArrow.DoubleUp || trend == TrendArrow.DoubleDown;

    /// <summary>
    /// Segoe Fluent Icons glyph for Windows
    /// </summary>
    public static string FluentIcon(this TrendArrow trend) => trend switch
    {
        TrendArrow.Unknown => "\uE9CE",           // QuestionMark
        TrendArrow.DoubleUp => "\uE74A",          // Up
        TrendArrow.SingleUp => "\uE74A",          // Up
        TrendArrow.FortyFiveUp => "\uE76C",       // UpRight
        TrendArrow.Flat => "\uE72A",              // Forward
        TrendArrow.FortyFiveDown => "\uE76B",     // DownRight
        TrendArrow.SingleDown => "\uE74B",        // Down
        TrendArrow.DoubleDown => "\uE74B",        // Down
        TrendArrow.NotComputable => "\uE9CE",     // QuestionMark
        TrendArrow.RateOutOfRange => "\uE9CE",    // QuestionMark
        _ => "\uE9CE"
    };

    /// <summary>
    /// Initialize from string trend name (Dexcom API format)
    /// </summary>
    public static TrendArrow FromString(string str) => str.ToLowerInvariant() switch
    {
        "none" or "unknown" => TrendArrow.Unknown,
        "doubleup" => TrendArrow.DoubleUp,
        "singleup" => TrendArrow.SingleUp,
        "fortyfiveup" => TrendArrow.FortyFiveUp,
        "flat" => TrendArrow.Flat,
        "fortyfivedown" => TrendArrow.FortyFiveDown,
        "singledown" => TrendArrow.SingleDown,
        "doubledown" => TrendArrow.DoubleDown,
        "notcomputable" => TrendArrow.NotComputable,
        "rateoutofrange" => TrendArrow.RateOutOfRange,
        _ => TrendArrow.Unknown
    };
}
