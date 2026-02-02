namespace XTouchVMBridge.Core.Hardware;

/// <summary>
/// Repräsentiert eine Level-Meter-Anzeige auf dem X-Touch Extender.
/// Zeigt Pegel als 14-stufige LED-Kette an.
/// </summary>
public class LevelMeterControl : HardwareControlBase
{
    public const int MinLevel = 0;
    public const int MaxLevel = 13;

    /// <summary>
    /// dB-Schwellenwerte für die 14 Level-Meter-Stufen.
    /// Index 0 = kein Signal, Index 13 = Clipping/Peak.
    /// </summary>
    private static readonly double[] DbThresholds =
    {
        -200, -100, -50, -40, -35, -30, -25, -20, -15, -10, -5, 0, 5, double.MaxValue
    };

    private int _level;

    public LevelMeterControl(int channel) : base(channel, $"LevelMeter_{channel}") { }

    /// <summary>Aktueller Anzeigelevel (0–13).</summary>
    public int Level
    {
        get => _level;
        set => _level = Math.Clamp(value, MinLevel, MaxLevel);
    }

    /// <summary>
    /// Konvertiert einen dB-Wert in eine Level-Meter-Stufe (0–13).
    /// Entspricht level_interpolation() aus dem Python-Original.
    /// </summary>
    public static int DbToLevel(double db)
    {
        for (int i = 0; i < DbThresholds.Length - 1; i++)
        {
            if (db < DbThresholds[i + 1])
                return i;
        }
        return MaxLevel;
    }
}
