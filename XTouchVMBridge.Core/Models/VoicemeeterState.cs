namespace XTouchVMBridge.Core.Models;

public class VoicemeeterState
{
    public const int TotalChannels = 16;
    public const int StripCount = 8;
    public const int BusCount = 8;

    public bool[] Mutes { get; } = new bool[TotalChannels];

    public bool[] Solos { get; } = new bool[StripCount];

    public double[] Gains { get; } = new double[TotalChannels];

    public double[] Levels { get; } = new double[TotalChannels];
}
