namespace XTouchVMBridge.Core.Models;

/// <summary>
/// Snapshot des Voicemeeter-Zustands.
/// Entspricht VMState aus dem Python-Original.
/// </summary>
public class VoicemeeterState
{
    public const int TotalChannels = 16;
    public const int StripCount = 8;
    public const int BusCount = 8;

    /// <summary>Mute-Status aller 16 Kanäle.</summary>
    public bool[] Mutes { get; } = new bool[TotalChannels];

    /// <summary>Solo-Status der 8 Input-Strips.</summary>
    public bool[] Solos { get; } = new bool[StripCount];

    /// <summary>Gain-Werte aller 16 Kanäle in dB.</summary>
    public double[] Gains { get; } = new double[TotalChannels];

    /// <summary>Level-Werte aller 16 Kanäle in dB (für Level-Meter).</summary>
    public double[] Levels { get; } = new double[TotalChannels];
}
