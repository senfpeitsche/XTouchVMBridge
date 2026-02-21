using XTouchVMBridge.Core.Enums;

namespace XTouchVMBridge.Core.Hardware;

public class EncoderControl : HardwareControlBase
{
    public const int MinPosition = 0;
    public const int MaxPosition = 10;  // 11 LEDs: L5 L4 L3 L2 L1 M R1 R2 R3 R4 R5

    private int _ringPosition;
    private XTouchEncoderRingMode _ringMode;
    private bool _ringLed;
    private bool _isPressed;


    private readonly List<EncoderFunction> _functions = new();
    private int _activeFunctionIndex;

    public EncoderControl(int channel) : base(channel, $"Encoder_{channel}") { }

    public int RingPosition
    {
        get => _ringPosition;
        set => _ringPosition = Math.Clamp(value, MinPosition, MaxPosition);
    }

    public XTouchEncoderRingMode RingMode
    {
        get => _ringMode;
        set => _ringMode = value;
    }

    public bool RingLed
    {
        get => _ringLed;
        set => _ringLed = value;
    }

    public bool IsPressed
    {
        get => _isPressed;
        set => _isPressed = value;
    }


    public IReadOnlyList<EncoderFunction> Functions => _functions;

    public int ActiveFunctionIndex
    {
        get => _activeFunctionIndex;
        set
        {
            if (_functions.Count == 0) return;
            _activeFunctionIndex = ((value % _functions.Count) + _functions.Count) % _functions.Count;
        }
    }

    public EncoderFunction? ActiveFunction =>
        _functions.Count > 0 ? _functions[_activeFunctionIndex] : null;

    public bool HasFunctions => _functions.Count > 0;

    public void AddFunction(EncoderFunction function)
    {
        _functions.Add(function);
    }

    public void AddFunctions(IEnumerable<EncoderFunction> functions)
    {
        _functions.AddRange(functions);
    }

    public void ClearFunctions()
    {
        _functions.Clear();
        _activeFunctionIndex = 0;
    }

    public EncoderFunction? CycleFunction()
    {
        if (_functions.Count == 0) return null;
        _activeFunctionIndex = (_activeFunctionIndex + 1) % _functions.Count;
        return _functions[_activeFunctionIndex];
    }

    public EncoderFunction? CycleFunctionReverse()
    {
        if (_functions.Count == 0) return null;
        _activeFunctionIndex = (_activeFunctionIndex - 1 + _functions.Count) % _functions.Count;
        return _functions[_activeFunctionIndex];
    }

    public EncoderFunction? ApplyTicks(int ticks)
    {
        var fn = ActiveFunction;
        if (fn == null) return null;

        fn.ApplyTicks(ticks);
        RingPosition = fn.ToRingPosition();
        return fn;
    }

    public void SyncRingToActiveFunction()
    {
        var fn = ActiveFunction;
        if (fn == null) return;
        RingPosition = fn.ToRingPosition();
    }

    public byte CalculateCcValue()
    {
        int baseValue;

        switch (_ringMode)
        {
            case XTouchEncoderRingMode.Dot:
                baseValue = Math.Clamp(_ringPosition, 0, 10) + 1;
                break;

            case XTouchEncoderRingMode.Pan:
                baseValue = Math.Clamp(_ringPosition, 0, 10) + 17;
                break;

            case XTouchEncoderRingMode.Wrap:
                baseValue = Math.Clamp(_ringPosition, 0, 10) + 33;
                break;

            case XTouchEncoderRingMode.Spread:
                baseValue = Math.Clamp(_ringPosition, 0, 5) + 49;
                break;

            default:
                baseValue = 0;
                break;
        }

        if (_ringLed) baseValue += 64;

        return (byte)Math.Clamp(baseValue, 0, 127);
    }
}
