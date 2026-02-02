namespace XTouchVMBridge.Core.Enums;

/// <summary>
/// Farben für die X-Touch Extender LCD-Displays.
/// Werte entsprechen dem Mackie Control SysEx-Protokoll (0–7).
/// </summary>
public enum XTouchColor : byte
{
    Off = 0,
    Red = 1,
    Green = 2,
    Yellow = 3,
    Blue = 4,
    Magenta = 5,
    Cyan = 6,
    White = 7
}
