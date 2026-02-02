using System.Diagnostics;
using System.Windows;
using XTouchVMBridge.Core.Events;
using XTouchVMBridge.Core.Interfaces;
using XTouchVMBridge.Core.Models;
using Microsoft.Extensions.Logging;
using WindowsInput;
using WindowsInput.Native;
using Application = System.Windows.Application;

namespace XTouchVMBridge.App.Services;

/// <summary>
/// Service für die Ausführung von Master-Button-Aktionen.
/// Registriert sich auf das MasterButtonChanged-Event und führt
/// konfigurierte Aktionen aus (Programm starten, Tasten senden, Text senden).
/// </summary>
public class MasterButtonActionService : IDisposable
{
    private readonly ILogger<MasterButtonActionService> _logger;
    private readonly IMidiDevice _midiDevice;
    private readonly IVoicemeeterService _vm;
    private readonly XTouchVMBridgeConfig _config;
    private readonly InputSimulator _inputSimulator;
    private bool _disposed;

    public MasterButtonActionService(
        ILogger<MasterButtonActionService> logger,
        IMidiDevice midiDevice,
        IVoicemeeterService vm,
        XTouchVMBridgeConfig config)
    {
        _logger = logger;
        _midiDevice = midiDevice;
        _vm = vm;
        _config = config;
        _inputSimulator = new InputSimulator();

        // Events abonnieren
        _midiDevice.MasterButtonChanged += OnMasterButtonChanged;

        _logger.LogInformation("MasterButtonActionService initialisiert.");
    }

    private void OnMasterButtonChanged(object? sender, MasterButtonEventArgs e)
    {
        // Nur bei Tastendruck (nicht beim Loslassen)
        if (!e.IsPressed) return;

        ExecuteAction(e.NoteNumber);
    }

    /// <summary>
    /// Führt die konfigurierte Aktion für eine Master-Button-Note aus.
    /// Kann sowohl vom MIDI-Event als auch von der UI (Strg+Klick) aufgerufen werden.
    /// Gibt true zurück wenn eine Aktion ausgeführt wurde, false wenn keine konfiguriert ist.
    /// </summary>
    public bool ExecuteAction(int noteNumber)
    {
        if (!_config.MasterButtonActions.TryGetValue(noteNumber, out var actionConfig))
        {
            _logger.LogDebug("Keine Aktion für Master-Button Note {Note} konfiguriert.", noteNumber);
            return false;
        }

        if (actionConfig.ActionType == MasterButtonActionType.None)
            return false;

        _logger.LogDebug("Master-Button Note {Note}: Führe Aktion {Action} aus.", noteNumber, actionConfig.ActionType);

        try
        {
            switch (actionConfig.ActionType)
            {
                case MasterButtonActionType.LaunchProgram:
                    ExecuteLaunchProgram(actionConfig);
                    break;
                case MasterButtonActionType.SendKeys:
                    ExecuteSendKeys(actionConfig);
                    break;
                case MasterButtonActionType.SendText:
                    ExecuteSendText(actionConfig);
                    break;
                case MasterButtonActionType.VmParameter:
                    ExecuteVmParameter(actionConfig);
                    break;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler bei Master-Button-Aktion Note {Note} ({Action}).",
                noteNumber, actionConfig.ActionType);
            return false;
        }
    }

    private void ExecuteLaunchProgram(MasterButtonActionConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ProgramPath))
        {
            _logger.LogWarning("LaunchProgram: Kein Programmpfad konfiguriert.");
            return;
        }

        _logger.LogInformation("Starte Programm: {Path} {Args}", config.ProgramPath, config.ProgramArgs ?? "");

        var startInfo = new ProcessStartInfo
        {
            FileName = config.ProgramPath,
            Arguments = config.ProgramArgs ?? "",
            UseShellExecute = true
        };

        Process.Start(startInfo);
    }

    private void ExecuteSendKeys(MasterButtonActionConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.KeyCombination))
        {
            _logger.LogWarning("SendKeys: Keine Tastenkombination konfiguriert.");
            return;
        }

        _logger.LogInformation("Sende Tastenkombination: {Keys}", config.KeyCombination);

        var (modifiers, key) = ParseKeyCombination(config.KeyCombination);

        // Modifier drücken
        foreach (var mod in modifiers)
            _inputSimulator.Keyboard.KeyDown(mod);

        // Taste drücken und loslassen
        if (key.HasValue)
            _inputSimulator.Keyboard.KeyPress(key.Value);

        // Modifier loslassen (umgekehrte Reihenfolge)
        for (int i = modifiers.Count - 1; i >= 0; i--)
            _inputSimulator.Keyboard.KeyUp(modifiers[i]);
    }

    private void ExecuteSendText(MasterButtonActionConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Text))
        {
            _logger.LogWarning("SendText: Kein Text konfiguriert.");
            return;
        }

        _logger.LogInformation("Sende Text: {Text}", config.Text);

        // Text in die Zwischenablage kopieren und einfügen
        Application.Current?.Dispatcher?.Invoke(() =>
        {
            System.Windows.Clipboard.SetText(config.Text);
        });

        // Kurz warten damit Clipboard aktualisiert ist
        Thread.Sleep(50);

        // Ctrl+V senden
        _inputSimulator.Keyboard.ModifiedKeyStroke(
            VirtualKeyCode.CONTROL,
            VirtualKeyCode.VK_V);
    }

    private void ExecuteVmParameter(MasterButtonActionConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.VmParameter))
        {
            _logger.LogWarning("VmParameter: Kein Parameter konfiguriert.");
            return;
        }

        _logger.LogInformation("Toggle VM-Parameter: {Param}", config.VmParameter);

        // Bool-Parameter toggeln
        float currentValue = _vm.GetParameter(config.VmParameter);
        float newValue = currentValue > 0.5f ? 0f : 1f;
        _vm.SetParameter(config.VmParameter, newValue);
    }

    /// <summary>
    /// Parst eine Tastenkombination wie "Ctrl+Shift+M" in Modifier und Haupttaste.
    /// </summary>
    private static (List<VirtualKeyCode> modifiers, VirtualKeyCode? key) ParseKeyCombination(string combination)
    {
        var modifiers = new List<VirtualKeyCode>();
        VirtualKeyCode? key = null;

        var parts = combination.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var upper = part.ToUpperInvariant();
            switch (upper)
            {
                case "CTRL" or "CONTROL":
                    modifiers.Add(VirtualKeyCode.CONTROL);
                    break;
                case "ALT":
                    modifiers.Add(VirtualKeyCode.MENU);
                    break;
                case "SHIFT":
                    modifiers.Add(VirtualKeyCode.SHIFT);
                    break;
                case "WIN" or "WINDOWS":
                    modifiers.Add(VirtualKeyCode.LWIN);
                    break;
                default:
                    key = MapKeyName(upper);
                    break;
            }
        }

        return (modifiers, key);
    }

    /// <summary>
    /// Mappt einen Tastennamen auf VirtualKeyCode.
    /// </summary>
    private static VirtualKeyCode? MapKeyName(string keyName)
    {
        // Funktionstasten F1–F24
        if (keyName.StartsWith("F") && int.TryParse(keyName[1..], out int fNum) && fNum >= 1 && fNum <= 24)
            return (VirtualKeyCode)(0x6F + fNum); // VK_F1 = 0x70

        // Einzelne Buchstaben A–Z
        if (keyName.Length == 1 && char.IsLetter(keyName[0]))
            return (VirtualKeyCode)(keyName[0]); // VK_A = 0x41, etc.

        // Zahlen 0–9
        if (keyName.Length == 1 && char.IsDigit(keyName[0]))
            return (VirtualKeyCode)(keyName[0]); // VK_0 = 0x30, etc.

        // Spezielle Tasten
        return keyName switch
        {
            "ENTER" or "RETURN" => VirtualKeyCode.RETURN,
            "ESC" or "ESCAPE" => VirtualKeyCode.ESCAPE,
            "TAB" => VirtualKeyCode.TAB,
            "SPACE" => VirtualKeyCode.SPACE,
            "BACKSPACE" or "BACK" => VirtualKeyCode.BACK,
            "DELETE" or "DEL" => VirtualKeyCode.DELETE,
            "INSERT" or "INS" => VirtualKeyCode.INSERT,
            "HOME" => VirtualKeyCode.HOME,
            "END" => VirtualKeyCode.END,
            "PAGEUP" or "PGUP" => VirtualKeyCode.PRIOR,
            "PAGEDOWN" or "PGDN" => VirtualKeyCode.NEXT,
            "UP" => VirtualKeyCode.UP,
            "DOWN" => VirtualKeyCode.DOWN,
            "LEFT" => VirtualKeyCode.LEFT,
            "RIGHT" => VirtualKeyCode.RIGHT,
            "PRINTSCREEN" or "PRTSC" => VirtualKeyCode.SNAPSHOT,
            "PAUSE" => VirtualKeyCode.PAUSE,
            "NUMLOCK" => VirtualKeyCode.NUMLOCK,
            "SCROLLLOCK" => VirtualKeyCode.SCROLL,
            "CAPSLOCK" => VirtualKeyCode.CAPITAL,
            "VOLUMEUP" => VirtualKeyCode.VOLUME_UP,
            "VOLUMEDOWN" => VirtualKeyCode.VOLUME_DOWN,
            "VOLUMEMUTE" or "MUTE" => VirtualKeyCode.VOLUME_MUTE,
            "MEDIANEXT" or "NEXTTRACK" => VirtualKeyCode.MEDIA_NEXT_TRACK,
            "MEDIAPREV" or "PREVTRACK" => VirtualKeyCode.MEDIA_PREV_TRACK,
            "MEDIAPLAY" or "PLAYPAUSE" => VirtualKeyCode.MEDIA_PLAY_PAUSE,
            "MEDIASTOP" => VirtualKeyCode.MEDIA_STOP,
            _ => null
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _midiDevice.MasterButtonChanged -= OnMasterButtonChanged;

        GC.SuppressFinalize(this);
    }
}
