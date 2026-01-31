using AudioManager.Core.Enums;
using AudioManager.Core.Events;
using AudioManager.Core.Models;

namespace AudioManager.Core.Interfaces;

/// <summary>
/// Abstraktion für ein MIDI-Controller-Gerät (z.B. X-Touch Extender).
/// Ermöglicht einfaches Austauschen oder Mocken des Geräts.
/// </summary>
public interface IMidiDevice : IDisposable
{
    /// <summary>Ob das Gerät verbunden ist.</summary>
    bool IsConnected { get; }

    /// <summary>Anzahl der Kanäle auf dem Gerät.</summary>
    int ChannelCount { get; }

    /// <summary>Zugriff auf die Kanal-Objekte.</summary>
    IReadOnlyList<XTouchChannel> Channels { get; }

    // ─── Events (Input vom Gerät) ───────────────────────────────────

    /// <summary>Fader wurde bewegt.</summary>
    event EventHandler<FaderEventArgs>? FaderChanged;

    /// <summary>Encoder wurde gedreht.</summary>
    event EventHandler<EncoderEventArgs>? EncoderRotated;

    /// <summary>Encoder wurde gedrückt/losgelassen.</summary>
    event EventHandler<EncoderPressEventArgs>? EncoderPressed;

    /// <summary>Button wurde gedrückt/losgelassen.</summary>
    event EventHandler<ButtonEventArgs>? ButtonChanged;

    /// <summary>Fader wurde berührt/losgelassen.</summary>
    event EventHandler<FaderTouchEventArgs>? FaderTouched;

    /// <summary>Rohe MIDI-Nachricht empfangen (Direct Hook).</summary>
    event EventHandler<MidiMessageEventArgs>? RawMidiReceived;

    // ─── Ausgabe (an das Gerät senden) ──────────────────────────────

    /// <summary>Setzt die Fader-Position eines Kanals.</summary>
    void SetFader(int channel, int position);

    /// <summary>Setzt die Fader-Position eines Kanals in dB.</summary>
    void SetFaderDb(int channel, double db);

    /// <summary>Setzt den LED-Status eines Buttons.</summary>
    void SetButtonLed(int channel, XTouchButtonType button, LedState state);

    /// <summary>Setzt den Encoder-Ring eines Kanals.</summary>
    void SetEncoderRing(int channel, int value, XTouchEncoderRingMode mode, bool led = false);

    /// <summary>Setzt das Level-Meter eines Kanals.</summary>
    void SetLevelMeter(int channel, int level);

    /// <summary>Setzt den Display-Text eines Kanals.</summary>
    void SetDisplayText(int channel, int row, string text);

    /// <summary>Setzt die Display-Farbe eines Kanals.</summary>
    void SetDisplayColor(int channel, XTouchColor color);

    /// <summary>Setzt alle Display-Farben auf einmal (effizienter).</summary>
    void SetAllDisplayColors(XTouchColor[] colors);

    /// <summary>Verbindung zum Gerät herstellen.</summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Verbindung trennen.</summary>
    void Disconnect();
}
