using XTouchVMBridge.Core.Enums;

namespace XTouchVMBridge.Core.Hardware;

public class DisplayControl : HardwareControlBase
{
    public const int MaxTextLength = 7;
    public const int RowCount = 2;

    private readonly string[] _rows = { new(' ', MaxTextLength), new(' ', MaxTextLength) };
    private XTouchColor _color = XTouchColor.Off;

    public DisplayControl(int channel) : base(channel, $"Display_{channel}") { }

    public XTouchColor Color
    {
        get => _color;
        set => _color = value;
    }

    public string TopRow
    {
        get => _rows[0];
        set => _rows[0] = SanitizeText(value);
    }

    public string BottomRow
    {
        get => _rows[1];
        set => _rows[1] = SanitizeText(value);
    }

    public string GetRow(int row) => _rows[Math.Clamp(row, 0, RowCount - 1)];

    public void SetRow(int row, string text)
    {
        _rows[Math.Clamp(row, 0, RowCount - 1)] = SanitizeText(text);
    }

    private static string SanitizeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new string(' ', MaxTextLength);

        var sanitized = new string(text.Where(c => c >= 0x20 && c <= 0x7E).ToArray());
        return sanitized.Length >= MaxTextLength
            ? sanitized[..MaxTextLength]
            : sanitized.PadRight(MaxTextLength);
    }
}
