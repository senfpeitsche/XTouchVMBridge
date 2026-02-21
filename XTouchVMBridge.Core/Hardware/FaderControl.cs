namespace XTouchVMBridge.Core.Hardware;

public class FaderControl : HardwareControlBase
{
    public const int MinPosition = -8192;
    public const int MaxPosition = 8188;
    public const double MinDb = -70.0;
    public const double MaxDb = 8.0;

    private int _position;
    private bool _isTouched;

    private static readonly double[] DbTable = { -70, -60, -30, -10, 0, 8 };
    private static readonly int[] PosTable = { -8192, -7700, -4340, 245, 4720, 8188 };

    public FaderControl(int channel) : base(channel, $"Fader_{channel}") { }

    public int Position
    {
        get => _position;
        set => _position = Math.Clamp(value, MinPosition, MaxPosition);
    }

    public double PositionDb
    {
        get => PositionToDb(_position);
        set => _position = DbToPosition(Math.Clamp(value, MinDb, MaxDb));
    }

    public bool IsTouched
    {
        get => _isTouched;
        set => _isTouched = value;
    }

    public static double PositionToDb(int position)
    {
        if (position <= PosTable[0]) return DbTable[0];
        if (position >= PosTable[^1]) return DbTable[^1];

        for (int i = 0; i < PosTable.Length - 1; i++)
        {
            if (position <= PosTable[i + 1])
            {
                double ratio = (double)(position - PosTable[i]) / (PosTable[i + 1] - PosTable[i]);
                return DbTable[i] + ratio * (DbTable[i + 1] - DbTable[i]);
            }
        }

        return DbTable[^1];
    }

    public static int DbToPosition(double db)
    {
        if (db <= DbTable[0]) return PosTable[0];
        if (db >= DbTable[^1]) return PosTable[^1];

        for (int i = 0; i < DbTable.Length - 1; i++)
        {
            if (db <= DbTable[i + 1])
            {
                double ratio = (db - DbTable[i]) / (DbTable[i + 1] - DbTable[i]);
                return (int)(PosTable[i] + ratio * (PosTable[i + 1] - PosTable[i]));
            }
        }

        return PosTable[^1];
    }
}
