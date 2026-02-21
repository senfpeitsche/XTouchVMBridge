namespace XTouchVMBridge.Core.Hardware;

public class LevelMeterControl : HardwareControlBase
{
    public const int MinLevel = 0;
    public const int MaxLevel = 13;

    private static readonly double[] DbThresholds =
    {
        -200, -100, -50, -40, -35, -30, -25, -20, -15, -10, -5, 0, 5, double.MaxValue
    };

    private int _level;

    public LevelMeterControl(int channel) : base(channel, $"LevelMeter_{channel}") { }

    public int Level
    {
        get => _level;
        set => _level = Math.Clamp(value, MinLevel, MaxLevel);
    }

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
