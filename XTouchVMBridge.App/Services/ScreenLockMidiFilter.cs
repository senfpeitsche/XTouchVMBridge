using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Events;
using XTouchVMBridge.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace XTouchVMBridge.App.Services;

public class ScreenLockMidiFilter
{
    private readonly ILogger<ScreenLockMidiFilter> _logger;
    private readonly IScreenLockDetector _lockDetector;
    private readonly IMidiDevice _xtouch;

    private bool _isDisplayingLockMessage;

    public ScreenLockMidiFilter(
        ILogger<ScreenLockMidiFilter> logger,
        IScreenLockDetector lockDetector,
        IMidiDevice xtouch)
    {
        _logger = logger;
        _lockDetector = lockDetector;
        _xtouch = xtouch;

        _xtouch.RawMidiReceived += OnRawMidiReceived;
        _lockDetector.LockStateChanged += OnLockStateChanged;
    }

    private void OnLockStateChanged(object? sender, bool isLocked)
    {
        if (isLocked)
        {
            ShowLockMessage();
            _logger.LogDebug("Bildschirm gesperrt — MIDI-Eingabe blockiert.");
        }
        else
        {
            _isDisplayingLockMessage = false;
            _logger.LogDebug("Bildschirm entsperrt — MIDI-Eingabe freigegeben.");
        }
    }

    private void OnRawMidiReceived(object? sender, MidiMessageEventArgs e)
    {
        if (_lockDetector.IsLocked)
        {
            e.Handled = true; // Nachricht konsumieren
        }
    }

    private void ShowLockMessage()
    {
        if (_isDisplayingLockMessage) return;
        _isDisplayingLockMessage = true;

        try
        {
            string[] topTexts = { " SCREEN", "       ", "LOCKED ", "       ", "       ", "       ", "       ", "       " };
            string[] bottomTexts = { "       ", "       ", "       ", "       ", "       ", "       ", "       ", "       " };

            for (int i = 0; i < 8; i++)
            {
                _xtouch.SetDisplayText(i, 0, topTexts[i]);
                _xtouch.SetDisplayText(i, 1, bottomTexts[i]);
                _xtouch.SetDisplayColor(i, XTouchColor.Red);
                _xtouch.SetButtonLed(i, XTouchButtonType.Rec, LedState.Off);
                _xtouch.SetButtonLed(i, XTouchButtonType.Solo, LedState.Off);
                _xtouch.SetButtonLed(i, XTouchButtonType.Mute, LedState.Off);
                _xtouch.SetButtonLed(i, XTouchButtonType.Select, LedState.Off);
                _xtouch.SetFader(i, -8192);
                _xtouch.SetLevelMeter(i, 0);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Anzeigen der Lock-Nachricht.");
        }
    }
}
