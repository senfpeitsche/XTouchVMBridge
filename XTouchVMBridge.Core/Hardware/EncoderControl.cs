using XTouchVMBridge.Core.Enums;

namespace XTouchVMBridge.Core.Hardware;

/// <summary>
/// Repräsentiert einen Drehencoder mit Push-Funktion und LED-Ring.
/// Unterstützt eine konfigurierbare Liste von Funktionen, die durch
/// Drücken des Encoders durchgeschaltet werden (z.B. High → Mid → Low).
/// </summary>
public class EncoderControl : HardwareControlBase
{
    public const int MinPosition = 0;
    public const int MaxPosition = 10;  // 11 LEDs: L5 L4 L3 L2 L1 M R1 R2 R3 R4 R5

    private int _ringPosition;
    private XTouchEncoderRingMode _ringMode;
    private bool _ringLed;
    private bool _isPressed;

    // ─── Funktionsliste ──────────────────────────────────────────────

    private readonly List<EncoderFunction> _functions = new();
    private int _activeFunctionIndex;

    public EncoderControl(int channel) : base(channel, $"Encoder_{channel}") { }

    /// <summary>Position des LED-Rings (0–15).</summary>
    public int RingPosition
    {
        get => _ringPosition;
        set => _ringPosition = Math.Clamp(value, MinPosition, MaxPosition);
    }

    /// <summary>Anzeigemodus des LED-Rings.</summary>
    public XTouchEncoderRingMode RingMode
    {
        get => _ringMode;
        set => _ringMode = value;
    }

    /// <summary>Ob die Center-LED aktiv ist.</summary>
    public bool RingLed
    {
        get => _ringLed;
        set => _ringLed = value;
    }

    /// <summary>Ob der Encoder gerade gedrückt wird.</summary>
    public bool IsPressed
    {
        get => _isPressed;
        set => _isPressed = value;
    }

    // ─── Funktionsverwaltung ─────────────────────────────────────────

    /// <summary>Alle registrierten Funktionen (Read-Only).</summary>
    public IReadOnlyList<EncoderFunction> Functions => _functions;

    /// <summary>Index der aktuell aktiven Funktion.</summary>
    public int ActiveFunctionIndex
    {
        get => _activeFunctionIndex;
        set
        {
            if (_functions.Count == 0) return;
            _activeFunctionIndex = ((value % _functions.Count) + _functions.Count) % _functions.Count;
        }
    }

    /// <summary>
    /// Die aktuell aktive Funktion, oder null wenn keine Funktionen registriert sind.
    /// </summary>
    public EncoderFunction? ActiveFunction =>
        _functions.Count > 0 ? _functions[_activeFunctionIndex] : null;

    /// <summary>Ob mindestens eine Funktion registriert ist.</summary>
    public bool HasFunctions => _functions.Count > 0;

    /// <summary>
    /// Registriert eine neue Funktion für diesen Encoder.
    /// </summary>
    public void AddFunction(EncoderFunction function)
    {
        _functions.Add(function);
    }

    /// <summary>
    /// Registriert mehrere Funktionen auf einmal.
    /// </summary>
    public void AddFunctions(IEnumerable<EncoderFunction> functions)
    {
        _functions.AddRange(functions);
    }

    /// <summary>Entfernt alle registrierten Funktionen.</summary>
    public void ClearFunctions()
    {
        _functions.Clear();
        _activeFunctionIndex = 0;
    }

    /// <summary>
    /// Schaltet zur nächsten Funktion in der Liste (zyklisch).
    /// Gibt die neu aktive Funktion zurück, oder null wenn keine vorhanden.
    /// </summary>
    public EncoderFunction? CycleFunction()
    {
        if (_functions.Count == 0) return null;
        _activeFunctionIndex = (_activeFunctionIndex + 1) % _functions.Count;
        return _functions[_activeFunctionIndex];
    }

    /// <summary>
    /// Schaltet zur vorherigen Funktion in der Liste (zyklisch).
    /// </summary>
    public EncoderFunction? CycleFunctionReverse()
    {
        if (_functions.Count == 0) return null;
        _activeFunctionIndex = (_activeFunctionIndex - 1 + _functions.Count) % _functions.Count;
        return _functions[_activeFunctionIndex];
    }

    /// <summary>
    /// Wendet Encoder-Ticks auf die aktive Funktion an und aktualisiert die Ring-Position.
    /// Gibt die aktive Funktion zurück, oder null wenn keine vorhanden.
    /// </summary>
    public EncoderFunction? ApplyTicks(int ticks)
    {
        var fn = ActiveFunction;
        if (fn == null) return null;

        fn.ApplyTicks(ticks);
        RingPosition = fn.ToRingPosition();
        return fn;
    }

    /// <summary>
    /// Synchronisiert die Ring-Position mit dem aktuellen Wert der aktiven Funktion.
    /// Sollte nach einem Funktionswechsel aufgerufen werden.
    /// </summary>
    public void SyncRingToActiveFunction()
    {
        var fn = ActiveFunction;
        if (fn == null) return;
        RingPosition = fn.ToRingPosition();
    }

    /// <summary>
    /// Berechnet den MIDI CC-Wert für den Encoder-Ring.
    /// X-Touch Encoder Ring Mapping (basierend auf Tests):
    /// - Mode 0 (Dot):    1-11 = einzelne LED (L5..M..R5)
    /// - Mode 1 (Pan):    17-27 = von Mitte aus füllend (L5..M..R5)
    /// - Mode 2 (Wrap):   33-43 = von links füllend
    /// - Mode 3 (Spread): 49-54 = symmetrisch von Mitte
    /// - +64: L6 und R6 LEDs zusätzlich an
    /// </summary>
    public byte CalculateCcValue()
    {
        // RingPosition ist 0-15, wir mappen auf die gültigen Bereiche
        // Für Pan-Mode: Position 0 = Wert 17 (voll links), Position 5 = Wert 22 (Mitte), Position 10 = Wert 27 (voll rechts)
        int baseValue;

        switch (_ringMode)
        {
            case XTouchEncoderRingMode.Dot:
                // Position 0-10 → Wert 1-11
                baseValue = Math.Clamp(_ringPosition, 0, 10) + 1;
                break;

            case XTouchEncoderRingMode.Pan:
                // Position 0-10 → Wert 17-27
                baseValue = Math.Clamp(_ringPosition, 0, 10) + 17;
                break;

            case XTouchEncoderRingMode.Wrap:
                // Position 0-10 → Wert 33-43
                baseValue = Math.Clamp(_ringPosition, 0, 10) + 33;
                break;

            case XTouchEncoderRingMode.Spread:
                // Position 0-5 → Wert 49-54
                baseValue = Math.Clamp(_ringPosition, 0, 5) + 49;
                break;

            default:
                baseValue = 0;
                break;
        }

        // +64 für äußere LEDs (L6/R6)
        if (_ringLed) baseValue += 64;

        return (byte)Math.Clamp(baseValue, 0, 127);
    }
}
