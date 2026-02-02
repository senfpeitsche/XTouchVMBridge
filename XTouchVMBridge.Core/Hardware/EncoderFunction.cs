namespace XTouchVMBridge.Core.Hardware;

/// <summary>
/// Definiert eine einzelne Funktion, die ein Encoder steuern kann.
/// Ein Encoder kann mehrere Funktionen haben, die durch Drücken durchgeschaltet werden.
/// Beispiel: High, Mid, Low EQ-Bänder.
/// </summary>
public class EncoderFunction
{
    /// <summary>Anzeigename (max 7 Zeichen, passend für X-Touch Display).</summary>
    public string Name { get; }

    /// <summary>Voicemeeter-Parametername (z.B. "Strip[0].EQGain1").</summary>
    public string VmParameter { get; }

    /// <summary>Minimaler Wert des Parameters.</summary>
    public double MinValue { get; }

    /// <summary>Maximaler Wert des Parameters.</summary>
    public double MaxValue { get; }

    /// <summary>Schrittweite pro Encoder-Tick.</summary>
    public double StepSize { get; }

    /// <summary>Einheit für die Anzeige (z.B. "dB", "%", "Hz").</summary>
    public string Unit { get; }

    /// <summary>Aktueller Wert des Parameters.</summary>
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

    /// <summary>
    /// Ändert den Wert um die angegebene Anzahl Ticks.
    /// Positive Ticks = Wert erhöhen, negative = Wert verringern.
    /// Der Wert wird auf [MinValue..MaxValue] begrenzt.
    /// </summary>
    /// <returns>Der neue Wert nach der Änderung.</returns>
    public double ApplyTicks(int ticks)
    {
        CurrentValue = Math.Clamp(CurrentValue + ticks * StepSize, MinValue, MaxValue);
        return CurrentValue;
    }

    /// <summary>
    /// Berechnet die Encoder-Ring-Position (0–15) für den aktuellen Wert.
    /// Mappt den Wertebereich linear auf 0–15.
    /// </summary>
    public int ToRingPosition()
    {
        if (Math.Abs(MaxValue - MinValue) < 0.001) return 0;
        double normalized = (CurrentValue - MinValue) / (MaxValue - MinValue);
        return (int)Math.Round(normalized * 15);
    }

    /// <summary>Formatierte Anzeige des aktuellen Werts (immer mit Punkt als Dezimaltrennzeichen).</summary>
    public string FormatValue() =>
        $"{CurrentValue.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}{Unit}";

    public override string ToString() => $"{Name}: {FormatValue()}";
}
