using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Events;
using XTouchVMBridge.Core.Hardware;
using XTouchVMBridge.Core.Interfaces;
using XTouchVMBridge.Core.Models;
using Microsoft.Extensions.Logging;
using NAudio.Midi;

namespace XTouchVMBridge.Midi.XTouch;

/// <summary>
/// MIDI transport adapter for Behringer X-Touch/X-Touch Extender devices.
/// </summary>
public partial class XTouchDevice : IMidiDevice
{
    private readonly ILogger<XTouchDevice> _logger;
    private readonly XTouchChannel[] _channels;
    private readonly Dictionary<int, DateTime> _noteTimers = new();

    private MidiIn? _midiIn;
    private MidiOut? _midiOut;
    private bool _isConnected;
    private bool _disposed;
    private string? _selectedDeviceName;
    private bool _mainFaderTouched;
    private readonly object _connectionLock = new();
    private int _consecutiveErrors;


    public event EventHandler<FaderEventArgs>? FaderChanged;
    public event EventHandler<EncoderEventArgs>? EncoderRotated;
    public event EventHandler<EncoderPressEventArgs>? EncoderPressed;
    public event EventHandler<ButtonEventArgs>? ButtonChanged;
    public event EventHandler<FaderTouchEventArgs>? FaderTouched;
    public event EventHandler<MidiMessageEventArgs>? RawMidiReceived;
    public event EventHandler<MasterButtonEventArgs>? MasterButtonChanged;
    public event EventHandler<bool>? ConnectionStateChanged;


    public bool IsConnected => _isConnected;
    public int ChannelCount => MackieProtocol.ChannelCount;
    public IReadOnlyList<XTouchChannel> Channels => _channels;
    public bool IsMainFaderTouched => _mainFaderTouched;

    public string? SelectedDeviceName
    {
        get => _selectedDeviceName;
        set => _selectedDeviceName = value;
    }

    public XTouchDevice(ILogger<XTouchDevice> logger)
    {
        _logger = logger;
        _channels = new XTouchChannel[MackieProtocol.ChannelCount];
        for (int i = 0; i < MackieProtocol.ChannelCount; i++)
            _channels[i] = new XTouchChannel(i);
    }


    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        lock (_connectionLock)
        {
            if (_isConnected)
            {
                _logger.LogDebug("ConnectAsync übersprungen — bereits verbunden.");
                return Task.CompletedTask;
            }

            if (_midiIn != null || _midiOut != null)
            {
                DisconnectInternal(fireEvent: false);
            }

            var (inputIndex, outputIndex) = FindDevice();
            if (inputIndex < 0 || outputIndex < 0)
            {
                _logger.LogDebug("X-Touch Extender nicht gefunden.");
                _isConnected = false;
                return Task.CompletedTask;
            }

            try
            {
                _midiIn = new MidiIn(inputIndex);
                _midiIn.MessageReceived += OnMidiMessageReceived;
                _midiIn.ErrorReceived += OnMidiError;
                _midiIn.Start();

                _midiOut = new MidiOut(outputIndex);
                _isConnected = true;
                _consecutiveErrors = 0;

                SendInitialization();
                _logger.LogInformation("X-Touch verbunden (In: {Input}, Out: {Output}).",
                    MidiIn.DeviceInfo(inputIndex).ProductName,
                    MidiOut.DeviceInfo(outputIndex).ProductName);
            }
            catch (NAudio.MmException ex) when (ex.Result == NAudio.MmResult.AlreadyAllocated)
            {
                _logger.LogError("X-Touch MIDI-Gerät ist bereits von einer anderen Anwendung geöffnet. " +
                    "Bitte schließe andere MIDI-Anwendungen (DAWs, MIDI-Monitore) und starte neu.");
                DisconnectInternal(fireEvent: false);
                _isConnected = false;
                ConnectionStateChanged?.Invoke(this, false);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Verbinden mit X-Touch.");
                DisconnectInternal(fireEvent: false);
                _isConnected = false;
                ConnectionStateChanged?.Invoke(this, false);
                return Task.CompletedTask;
            }
        }

        ConnectionStateChanged?.Invoke(this, true);
        return Task.CompletedTask;
    }

    public void Disconnect()
    {
        bool wasConnected;
        lock (_connectionLock)
        {
            wasConnected = _isConnected;
            DisconnectInternal(fireEvent: false);
        }

        if (wasConnected)
            ConnectionStateChanged?.Invoke(this, false);
    }

    private void DisconnectInternal(bool fireEvent)
    {
        bool wasConnected = _isConnected;
        _isConnected = false;

        if (_midiIn != null)
        {
            try { _midiIn.Stop(); } catch (Exception ex) { _logger.LogDebug(ex, "Fehler beim Stoppen von MidiIn."); }
            try { _midiIn.MessageReceived -= OnMidiMessageReceived; } catch { /* ignore */ }
            try { _midiIn.ErrorReceived -= OnMidiError; } catch { /* ignore */ }
            try { _midiIn.Dispose(); } catch (Exception ex) { _logger.LogDebug(ex, "Fehler beim Dispose von MidiIn."); }
            _midiIn = null;
        }

        if (_midiOut != null)
        {
            try { _midiOut.Dispose(); } catch (Exception ex) { _logger.LogDebug(ex, "Fehler beim Dispose von MidiOut."); }
            _midiOut = null;
        }

        _logger.LogInformation("X-Touch getrennt.");
        if (fireEvent && wasConnected)
            ConnectionStateChanged?.Invoke(this, false);
    }


    public IReadOnlyList<string> ListDevices()
    {
        var inputs = new HashSet<string>();
        var outputs = new HashSet<string>();

        for (int i = 0; i < MidiIn.NumberOfDevices; i++)
        {
            var name = MidiIn.DeviceInfo(i).ProductName;
            if (name.Contains("X-Touch", StringComparison.OrdinalIgnoreCase))
                inputs.Add(name);
        }

        for (int i = 0; i < MidiOut.NumberOfDevices; i++)
        {
            var name = MidiOut.DeviceInfo(i).ProductName;
            if (name.Contains("X-Touch", StringComparison.OrdinalIgnoreCase))
                outputs.Add(name);
        }

        inputs.IntersectWith(outputs);
        return inputs.OrderBy(n => n).ToList();
    }

    public bool IsDeviceStillPresent()
    {
        try
        {
            var (inputIndex, outputIndex) = FindDevice();
            return inputIndex >= 0 && outputIndex >= 0;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Fehler bei Geräte-Verfügbarkeitsprüfung.");
            return false;
        }
    }

    private (int inputIndex, int outputIndex) FindDevice()
    {
        int inputIndex = -1;
        int outputIndex = -1;
        string searchTerm = _selectedDeviceName ?? "X-Touch";

        for (int i = 0; i < MidiIn.NumberOfDevices; i++)
        {
            if (MidiIn.DeviceInfo(i).ProductName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                inputIndex = i;
                break;
            }
        }

        for (int i = 0; i < MidiOut.NumberOfDevices; i++)
        {
            if (MidiOut.DeviceInfo(i).ProductName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                outputIndex = i;
                break;
            }
        }

        return (inputIndex, outputIndex);
    }


    private void HandleConnectionLost()
    {
        lock (_connectionLock)
        {
            if (!_isConnected) return;
            _isConnected = false;
        }

        _logger.LogWarning("MIDI-Verbindung verloren — Reconnect wird vom Monitor ausgelöst.");
        ConnectionStateChanged?.Invoke(this, false);
    }


    private byte GetBehringerDeviceId()
    {
        var name = _selectedDeviceName ?? "";
        if (name.Contains("Ext", StringComparison.OrdinalIgnoreCase))
            return MackieProtocol.DeviceIdXTouchExt;
        return MackieProtocol.DeviceIdXTouch;
    }

    private static void ValidateChannel(int channel)
    {
        if (channel < 0 || channel >= MackieProtocol.ChannelCount)
            throw new ArgumentOutOfRangeException(nameof(channel),
                $"Kanal muss zwischen 0 und {MackieProtocol.ChannelCount - 1} liegen.");
    }


    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
        GC.SuppressFinalize(this);
    }
}
