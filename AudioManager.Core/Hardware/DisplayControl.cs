using AudioManager.Core.Enums;

namespace AudioManager.Core.Hardware;

/// <summary>
/// Repräsentiert das LCD-Display eines Kanals (7 Zeichen × 2 Zeilen + Hintergrundfarbe).
/// </summary>
public class DisplayControl : HardwareControlBase
{
    public const int MaxTextLength = 7;
    public const int RowCount = 2;

    private readonly string[] _rows = { new(' ', MaxTextLength), new(' ', MaxTextLength) };
    private XTouchColor _color = XTouchColor.Off;

    public DisplayControl(int channel) : base(channel, $"Display_{channel}") { }

    /// <summary>Hintergrundfarbe des Displays.</summary>
    public XTouchColor Color
    {
        get => _color;
        set => _color = value;
    }

    /// <summary>Text der oberen Zeile (max 7 Zeichen, ASCII).</summary>
    public string TopRow
    {
        get => _rows[0];
        set => _rows[0] = SanitizeText(value);
    }

    /// <summary>Text der unteren Zeile (max 7 Zeichen, ASCII).</summary>
    public string BottomRow
    {
        get => _rows[1];
        set => _rows[1] = SanitizeText(value);
    }

    /// <summary>Zugriff auf eine Zeile per Index (0 = oben, 1 = unten).</summary>
    public string GetRow(int row) => _rows[Math.Clamp(row, 0, RowCount - 1)];

    /// <summary>Setzt eine Zeile per Index.</summary>
    public void SetRow(int row, string text)
    {
        _rows[Math.Clamp(row, 0, RowCount - 1)] = SanitizeText(text);
    }

    private static string SanitizeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new string(' ', MaxTextLength);

        // Nur ASCII erlaubt, auf MaxTextLength kürzen/padden
        var sanitized = new string(text.Where(c => c >= 0x20 && c <= 0x7E).ToArray());
        return sanitized.Length >= MaxTextLength
            ? sanitized[..MaxTextLength]
            : sanitized.PadRight(MaxTextLength);
    }
}
