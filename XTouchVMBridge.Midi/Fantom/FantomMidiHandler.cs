using Microsoft.Extensions.Logging;
using NAudio.Midi;

namespace XTouchVMBridge.Midi.Fantom;

/// <summary>
/// Filtert und leitet MIDI-Nachrichten vom Roland Fantom-06 Keyboard weiter.
/// Entspricht FantomMidiHandler aus dem Python-Original.
///
/// Nur Note On/Off und Control Change werden durchgelassen.
/// Program Changes werden zur Scene-Erkennung getrackt.
/// </summary>
public class FantomMidiHandler : IDisposable
{
    private readonly ILogger<FantomMidiHandler> _logger;

    private MidiIn? _inPort;
    private MidiOut? _outPort;
    private bool _isRunning;
    private bool _disposed;

    // Scene-Tracking
    private int _currentBankMsb;
    private int _currentBankLsb;
    private double _currentProgram;

    /// <summary>Name des Fantom MIDI Input-Geräts.</summary>
    public string? FantomDeviceName { get; private set; }

    /// <summary>Name des LoopMIDI Output-Ports.</summary>
    public string? OutputPortName { get; private set; }

    /// <summary>Ob der Handler aktiv ist.</summary>
    public bool IsRunning => _isRunning;

    /// <summary>Aktuell erkannte Scene-Nummer.</summary>
    public double CurrentProgram => _currentProgram;

    /// <summary>Wird ausgelöst wenn ein Fantom-Gerät gefunden/verbunden wird.</summary>
    public event EventHandler<string>? DeviceConnected;

    /// <summary>Wird ausgelöst wenn das Fantom-Gerät getrennt wird.</summary>
    public event EventHandler? DeviceDisconnected;

    public FantomMidiHandler(ILogger<FantomMidiHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Sucht nach dem Fantom-Gerät und dem LoopMIDI-Output.
    /// Gibt true zurück wenn beides gefunden wurde.
    /// </summary>
    public bool TryConnect()
    {
        FantomDeviceName = FindMidiDevice(isInput: true, "FANTOM-06");
        OutputPortName = FindMidiDevice(isInput: false, "FANTOM filterd");

        if (FantomDeviceName == null || OutputPortName == null)
        {
            _logger.LogDebug("Fantom oder LoopMIDI Port nicht gefunden.");
            return false;
        }

        try
        {
            int inputIndex = FindDeviceIndex(isInput: true, FantomDeviceName);
            int outputIndex = FindDeviceIndex(isInput: false, OutputPortName);

            if (inputIndex < 0 || outputIndex < 0) return false;

            _inPort = new MidiIn(inputIndex);
            _outPort = new MidiOut(outputIndex);

            _inPort.MessageReceived += OnMidiReceived;
            _inPort.ErrorReceived += OnMidiError;
            _inPort.Start();

            _isRunning = true;
            _logger.LogInformation("Fantom MIDI verbunden: {Input} → {Output}", FantomDeviceName, OutputPortName);
            DeviceConnected?.Invoke(this, FantomDeviceName);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Verbinden mit Fantom.");
            Stop();
            return false;
        }
    }

    /// <summary>
    /// Prüft ob das Fantom-Gerät noch verbunden ist.
    /// </summary>
    public bool CheckIfDisconnected()
    {
        if (!_isRunning) return true;

        var deviceName = FindMidiDevice(isInput: true, "FANTOM-06");
        if (deviceName != null) return false;

        _logger.LogInformation("Fantom wurde getrennt.");
        Stop();
        DeviceDisconnected?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Stoppt den Handler und gibt Ressourcen frei.</summary>
    public void Stop()
    {
        _isRunning = false;

        if (_inPort != null)
        {
            _inPort.Stop();
            _inPort.MessageReceived -= OnMidiReceived;
            _inPort.ErrorReceived -= OnMidiError;
            _inPort.Dispose();
            _inPort = null;
        }

        if (_outPort != null)
        {
            _outPort.Dispose();
            _outPort = null;
        }
    }

    // ─── MIDI Message Filtering ─────────────────────────────────────

    private void OnMidiReceived(object? sender, MidiInMessageEventArgs e)
    {
        try
        {
            var command = e.MidiEvent.CommandCode;

            switch (command)
            {
                // Whitelisted: durchleiten
                case MidiCommandCode.NoteOn:
                case MidiCommandCode.NoteOff:
                case MidiCommandCode.ControlChange:
                    ForwardMessage(e.RawMessage);
                    TrackControlChange(e);
                    break;

                // Program Change: Scene-Tracking (nicht weiterleiten)
                case MidiCommandCode.PatchChange:
                    TrackProgramChange(e);
                    break;

                // Alles andere: filtern (nicht weiterleiten)
                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler bei Fantom MIDI-Verarbeitung.");
        }
    }

    private void ForwardMessage(int rawMessage)
    {
        _outPort?.Send(rawMessage);
    }

    private void TrackControlChange(MidiInMessageEventArgs e)
    {
        byte data1 = (byte)((e.RawMessage >> 8) & 0xFF);
        byte data2 = (byte)((e.RawMessage >> 16) & 0xFF);

        // Bank Select MSB (CC 0)
        if (data1 == 0) _currentBankMsb = data2;

        // Bank Select LSB (CC 32)
        if (data1 == 32) _currentBankLsb = data2;
    }

    private void TrackProgramChange(MidiInMessageEventArgs e)
    {
        byte program = (byte)((e.RawMessage >> 8) & 0xFF);
        _currentProgram = _currentBankMsb * 128.0 + _currentBankLsb + (program + 1) / 1000.0;
        _logger.LogDebug("Fantom Scene: {Scene}", _currentProgram);
    }

    private void OnMidiError(object? sender, MidiInMessageEventArgs e)
    {
        _logger.LogWarning("Fantom MIDI Fehler: {Message}", e.RawMessage);
    }

    // ─── Geräte-Suche ───────────────────────────────────────────────

    private static string? FindMidiDevice(bool isInput, string searchTerm)
    {
        int count = isInput ? MidiIn.NumberOfDevices : MidiOut.NumberOfDevices;
        for (int i = 0; i < count; i++)
        {
            string name = isInput
                ? MidiIn.DeviceInfo(i).ProductName
                : MidiOut.DeviceInfo(i).ProductName;

            if (name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                return name;
        }
        return null;
    }

    private static int FindDeviceIndex(bool isInput, string name)
    {
        int count = isInput ? MidiIn.NumberOfDevices : MidiOut.NumberOfDevices;
        for (int i = 0; i < count; i++)
        {
            string deviceName = isInput
                ? MidiIn.DeviceInfo(i).ProductName
                : MidiOut.DeviceInfo(i).ProductName;

            if (deviceName == name) return i;
        }
        return -1;
    }

    // ─── IDisposable ────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        GC.SuppressFinalize(this);
    }
}
