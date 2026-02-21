namespace XTouchVMBridge.Core.Hardware;

public class EncoderFunction
{
    public string Name { get; }

    public string VmParameter { get; }

    public double MinValue { get; }

    public double MaxValue { get; }

    public double StepSize { get; }

    public string Unit { get; }

    public double CurrentValue { get; set; }

    public EncoderFunction(
        string name,
        string vmParameter,
        double minValue = -12.0,
        double maxValue = 12.0,
        double stepSize = 0.5,
        string unit = "dB",
        double initialValue = 0.0)
    {
        Name = name;
        VmParameter = vmParameter;
        MinValue = minValue;
        MaxValue = maxValue;
        StepSize = stepSize;
        Unit = unit;
        CurrentValue = initialValue;
    }

    public double ApplyTicks(int ticks)
    {
        CurrentValue = Math.Clamp(CurrentValue + ticks * StepSize, MinValue, MaxValue);
        return CurrentValue;
    }

    public int ToRingPosition()
    {
        if (Math.Abs(MaxValue - MinValue) < 0.001) return 5; // Mitte
        double normalized = (CurrentValue - MinValue) / (MaxValue - MinValue);
        return (int)Math.Round(normalized * 10);
    }

    public string FormatValue() =>
        $"{CurrentValue.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}{Unit}";

    public override string ToString() => $"{Name}: {FormatValue()}";
}
