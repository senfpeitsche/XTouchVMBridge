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
    public const int MaxPosition = 15;

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
    /// Format: (mode * 16 + position) [+ 64 wenn LED an].
    /// </summary>
    public byte CalculateCcValue()
    {
        int value = (int)_ringMode * 16 + _ringPosition;
        if (_ringLed) value += 64;
        return (byte)Math.Clamp(value, 0, 127);
    }
}
