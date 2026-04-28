using System.Diagnostics;
using System.IO;
using System.Windows;
using XTouchVMBridge.Core.Events;
using XTouchVMBridge.Core.Interfaces;
using XTouchVMBridge.Core.Models;
using XTouchVMBridge.Voicemeeter.Services;
using Microsoft.Extensions.Logging;
using WindowsInput;
using WindowsInput.Native;
using Application = System.Windows.Application;

namespace XTouchVMBridge.App.Services;

public class MasterButtonActionService : IDisposable
{
    private readonly ILogger<MasterButtonActionService> _logger;
    private readonly IMidiDevice _midiDevice;
    private readonly IVoicemeeterService _vm;
    private readonly VoicemeeterBridge _bridge;
    private readonly XTouchVMBridgeConfig _config;
    private readonly MqttClientService _mqttClientService;
    private readonly InputSimulator _inputSimulator;
    private readonly Dictionary<int, bool> _toggleLedStates = new();
    private readonly Dictionary<int, int> _runningLaunchProgramCounts = new();
    private readonly object _runningLaunchProgramCountsLock = new();
    private string? _activeMqttDeviceId;
    private string? _activeMqttDeviceTopic;
    private bool _lockGuiState; // Local mirror because Command.Lock is write-only.
    private bool _disposed;

    public const int FlipButtonNote = 50;

    public MasterButtonActionService(
        ILogger<MasterButtonActionService> logger,
        IMidiDevice midiDevice,
        IVoicemeeterService vm,
        VoicemeeterBridge bridge,
        XTouchVMBridgeConfig config,
        MqttClientService mqttClientService)
    {
        _logger = logger;
        _midiDevice = midiDevice;
        _vm = vm;
        _bridge = bridge;
        _config = config;
        _mqttClientService = mqttClientService;
        _inputSimulator = new InputSimulator();

        _midiDevice.MasterButtonChanged += OnMasterButtonChanged;
        _midiDevice.ConnectionStateChanged += OnMidiConnectionStateChanged;

        RefreshLaunchProgramLedStates();

        _logger.LogInformation("MasterButtonActionService initialisiert.");
    }

    public void RefreshLaunchProgramLedStates()
    {
        _logger.LogInformation(
            "LaunchProgram-LED-Sync gestartet. X-Touch verbunden: {IsConnected}. Konfigurierte Master-Aktionen: {ActionCount}.",
            _midiDevice.IsConnected,
            _config.MasterButtonActions.Count);

        var desiredCounts = new Dictionary<int, int>();

        foreach (var (noteNumber, actionConfig) in _config.MasterButtonActions)
        {
            if (actionConfig.ActionType != MasterButtonActionType.LaunchProgram ||
                !actionConfig.KeepLedOnWhileProgramRuns ||
                string.IsNullOrWhiteSpace(actionConfig.ProgramPath))
            {
                continue;
            }

            int runningCount = CountRunningProcessesForConfiguredPath(actionConfig.ProgramPath);
            desiredCounts[noteNumber] = runningCount;

            _logger.LogInformation(
                "LaunchProgram-LED-Sync: Note {Note}, Pfad '{ProgramPath}', KeepLedOnWhileRunning={KeepLedOn}, laufende Prozesse={RunningCount}.",
                noteNumber,
                actionConfig.ProgramPath,
                actionConfig.KeepLedOnWhileProgramRuns,
                runningCount);
        }

        lock (_runningLaunchProgramCountsLock)
        {
            _runningLaunchProgramCounts.Clear();
            foreach (var (noteNumber, runningCount) in desiredCounts)
            {
                if (runningCount > 0)
                    _runningLaunchProgramCounts[noteNumber] = runningCount;
            }
        }

        foreach (var (noteNumber, actionConfig) in _config.MasterButtonActions)
        {
            if (actionConfig.ActionType != MasterButtonActionType.LaunchProgram)
                continue;

            bool keepLedOn = actionConfig.KeepLedOnWhileProgramRuns;
            bool isRunning = desiredCounts.TryGetValue(noteNumber, out int runningCount) && runningCount > 0;

            try
            {
                _midiDevice.SetMasterButtonLed(
                    noteNumber,
                    keepLedOn && isRunning ? Core.Enums.LedState.On : Core.Enums.LedState.Off);

                _logger.LogInformation(
                    "LaunchProgram-LED-Sync: Note {Note} -> LED {LedState}.",
                    noteNumber,
                    keepLedOn && isRunning ? "On" : "Off");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "LaunchProgram: Startup-LED-Abgleich für Note {Note} fehlgeschlagen.", noteNumber);
            }
        }
    }

    private void OnMasterButtonChanged(object? sender, MasterButtonEventArgs e)
    {
        if (!e.IsPressed)
        {
            ExecuteReleaseAction(e.NoteNumber);
            return;
        }

        if (e.NoteNumber == FlipButtonNote)
        {
            CycleChannelView();
            return;
        }

        ExecuteAction(e.NoteNumber);
    }

    private void OnMidiConnectionStateChanged(object? sender, bool isConnected)
    {
        if (!isConnected)
            return;

        RefreshLaunchProgramLedStates();
    }

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
                    ExecuteLaunchProgram(noteNumber, actionConfig);
                    break;
                case MasterButtonActionType.SendKeys:
                    ExecuteSendKeys(actionConfig);
                    break;
                case MasterButtonActionType.SendText:
                    ExecuteSendText(actionConfig);
                    break;
                case MasterButtonActionType.VmParameter:
                    ExecuteVmParameter(noteNumber, actionConfig);
                    break;
                case MasterButtonActionType.CycleChannelView:
                    CycleChannelView();
                    break;
                case MasterButtonActionType.RestartAudioEngine:
                    ExecuteRestartAudioEngine();
                    break;
                case MasterButtonActionType.ShowVoicemeeter:
                    ExecuteShowVoicemeeter();
                    break;
                case MasterButtonActionType.LockGui:
                    ExecuteLockGui();
                    break;
                case MasterButtonActionType.TriggerMacroButton:
                    ExecuteTriggerMacroButton(actionConfig);
                    break;
                case MasterButtonActionType.MqttPublish:
                    ExecuteMqttPublish(actionConfig, isPressed: true);
                    break;
                case MasterButtonActionType.SelectMqttDevice:
                    ExecuteSelectMqttDevice(noteNumber, actionConfig);
                    break;
                case MasterButtonActionType.MqttTransport:
                    ExecuteMqttTransport(actionConfig);
                    break;
            }

            bool vmLedFromVoicemeeter = actionConfig.ActionType == MasterButtonActionType.VmParameter &&
                                        actionConfig.VmLedSource == MasterVmLedSource.VoicemeeterState;
            bool launchProgramKeepsLedOn = actionConfig.ActionType == MasterButtonActionType.LaunchProgram &&
                                           actionConfig.KeepLedOnWhileProgramRuns;
            if (actionConfig.ActionType != MasterButtonActionType.SelectMqttDevice &&
                !vmLedFromVoicemeeter &&
                !launchProgramKeepsLedOn)
            {
                UpdateLedFeedback(noteNumber, actionConfig);
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

    private void ExecuteReleaseAction(int noteNumber)
    {
        if (!_config.MasterButtonActions.TryGetValue(noteNumber, out var actionConfig))
            return;
        if (actionConfig.ActionType != MasterButtonActionType.MqttPublish)
            return;

        try
        {
            ExecuteMqttPublish(actionConfig, isPressed: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler bei Master-Button Release-Aktion Note {Note}.", noteNumber);
        }
    }

    private void ExecuteLaunchProgram(int noteNumber, MasterButtonActionConfig config)
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

        var process = Process.Start(startInfo);

        if (config.KeepLedOnWhileProgramRuns)
        {
            TrackLaunchProgramLedLifetime(noteNumber, process);
        }
    }

    private void TrackLaunchProgramLedLifetime(int noteNumber, Process? process)
    {
        if (process == null)
        {
            _logger.LogDebug("LaunchProgram: Prozess für Note {Note} konnte nicht getrackt werden.", noteNumber);
            UpdateLedFeedback(noteNumber, new MasterButtonActionConfig { LedFeedback = LedFeedbackMode.Blink });
            return;
        }

        int exitHandled = 0;

        void HandleExitOnce()
        {
            if (Interlocked.Exchange(ref exitHandled, 1) != 0)
                return;

            OnLaunchProgramProcessExited(noteNumber, process);
        }

        lock (_runningLaunchProgramCountsLock)
        {
            _runningLaunchProgramCounts.TryGetValue(noteNumber, out int runningCount);
            _runningLaunchProgramCounts[noteNumber] = runningCount + 1;
        }

        _midiDevice.SetMasterButtonLed(noteNumber, Core.Enums.LedState.On);

        try
        {
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => HandleExitOnce();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "LaunchProgram: Exit-Tracking für Note {Note} konnte nicht registriert werden.", noteNumber);
        }

        if (process.HasExited)
        {
            HandleExitOnce();
        }
    }

    private void OnLaunchProgramProcessExited(int noteNumber, Process process)
    {
        bool shouldTurnLedOff = false;

        lock (_runningLaunchProgramCountsLock)
        {
            if (_runningLaunchProgramCounts.TryGetValue(noteNumber, out int runningCount))
            {
                runningCount = Math.Max(0, runningCount - 1);
                if (runningCount == 0)
                {
                    _runningLaunchProgramCounts.Remove(noteNumber);
                    shouldTurnLedOff = true;
                }
                else
                {
                    _runningLaunchProgramCounts[noteNumber] = runningCount;
                }
            }
        }

        try
        {
            process.Dispose();
        }
        catch
        {
            // ignorieren
        }

        if (!shouldTurnLedOff)
            return;

        try
        {
            _midiDevice.SetMasterButtonLed(noteNumber, Core.Enums.LedState.Off);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "LaunchProgram: LED für Note {Note} konnte nach Prozessende nicht deaktiviert werden.", noteNumber);
        }
    }

    private static int CountRunningProcessesForConfiguredPath(string configuredProgramPath)
    {
        string trimmedPath = configuredProgramPath.Trim();
        if (string.IsNullOrWhiteSpace(trimmedPath))
            return 0;

        string normalizedConfiguredPath = NormalizeExecutablePath(trimmedPath);
        bool compareByFullPath = Path.IsPathRooted(trimmedPath);
        string configuredFileName = Path.GetFileName(trimmedPath);
        string configuredProcessName = Path.GetFileNameWithoutExtension(trimmedPath);

        int matches = 0;

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                string processName = process.ProcessName;
                string? processPath = TryGetProcessMainModuleFileName(process);

                if (compareByFullPath)
                {
                    if (!string.IsNullOrWhiteSpace(processPath) &&
                        string.Equals(
                            NormalizeExecutablePath(processPath),
                            normalizedConfiguredPath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        matches++;
                    }
                    else if (string.Equals(
                                 processName,
                                 configuredProcessName,
                                 StringComparison.OrdinalIgnoreCase))
                    {
                        matches++;
                    }
                }
                else if (string.Equals(
                             processName,
                             configuredProcessName,
                             StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(
                             Path.GetFileName(processPath),
                             configuredFileName,
                             StringComparison.OrdinalIgnoreCase))
                {
                    matches++;
                }
            }
            catch
            {
                // Zugriff auf MainModule ist für manche Prozesse nicht erlaubt; diese ignorieren wir.
            }
            finally
            {
                process.Dispose();
            }
        }

        return matches;
    }

    private static string? TryGetProcessMainModuleFileName(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeExecutablePath(string path) =>
        Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim()));

    private void ExecuteSendKeys(MasterButtonActionConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.KeyCombination))
        {
            _logger.LogWarning("SendKeys: Keine Tastenkombination konfiguriert.");
            return;
        }

        _logger.LogInformation("Sende Tastenkombination: {Keys}", config.KeyCombination);

        var (modifiers, key) = ParseKeyCombination(config.KeyCombination);

        foreach (var mod in modifiers)
            _inputSimulator.Keyboard.KeyDown(mod);

        if (key.HasValue)
            _inputSimulator.Keyboard.KeyPress(key.Value);

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

        Application.Current?.Dispatcher?.Invoke(() =>
        {
            System.Windows.Clipboard.SetText(config.Text);
        });

        Thread.Sleep(50);

        _inputSimulator.Keyboard.ModifiedKeyStroke(
            VirtualKeyCode.CONTROL,
            VirtualKeyCode.VK_V);
    }

    private void ExecuteVmParameter(int noteNumber, MasterButtonActionConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.VmParameter))
        {
            _logger.LogWarning("VmParameter: Kein Parameter konfiguriert.");
            return;
        }

        _logger.LogInformation("Toggle VM-Parameter: {Param}", config.VmParameter);

        float currentValue = _vm.GetParameter(config.VmParameter);
        float newValue = currentValue > 0.5f ? 0f : 1f;
        _vm.SetParameter(config.VmParameter, newValue);

        if (config.VmLedSource == MasterVmLedSource.VoicemeeterState)
        {
            _midiDevice.SetMasterButtonLed(noteNumber, newValue > 0.5f
                ? Core.Enums.LedState.On
                : Core.Enums.LedState.Off);
        }
    }

    private void ExecuteRestartAudioEngine()
    {
        _logger.LogInformation("Voicemeeter Audio Engine wird neu gestartet.");
        _vm.Restart();
    }

    private void ExecuteShowVoicemeeter()
    {
        _logger.LogInformation("Voicemeeter-Fenster wird angezeigt.");
        _vm.ShowVoicemeeter();
    }

    private void ExecuteLockGui()
    {
        _lockGuiState = !_lockGuiState;
        _logger.LogInformation("Voicemeeter GUI wird {State}.", _lockGuiState ? "gesperrt" : "entsperrt");
        _vm.LockGui(_lockGuiState);
    }

    private void ExecuteTriggerMacroButton(MasterButtonActionConfig config)
    {
        if (!config.MacroButtonIndex.HasValue)
        {
            _logger.LogWarning("TriggerMacroButton: Kein Macro-Button-Index konfiguriert.");
            return;
        }

        _logger.LogInformation("Macro-Button {Index} wird ausgelöst.", config.MacroButtonIndex.Value);
        _vm.TriggerMacroButton(config.MacroButtonIndex.Value);
    }

    private void ExecuteMqttPublish(MasterButtonActionConfig config, bool isPressed)
    {
        if (string.IsNullOrWhiteSpace(config.MqttTopic))
        {
            _logger.LogWarning("MqttPublish: Kein Topic konfiguriert.");
            return;
        }

        string payload = isPressed
            ? (config.MqttPayloadPressed ?? "")
            : (config.MqttPayloadReleased ?? "");
        if (string.IsNullOrWhiteSpace(payload))
            return;

        _ = _mqttClientService.PublishAsync(
            config.MqttTopic,
            payload,
            config.MqttQos,
            config.MqttRetain);
    }

    private void ExecuteSelectMqttDevice(int noteNumber, MasterButtonActionConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.MqttDeviceId) || string.IsNullOrWhiteSpace(config.MqttDeviceCommandTopic))
        {
            _logger.LogWarning("SelectMqttDevice: DeviceId oder Topic fehlt.");
            return;
        }

        bool sameDevice = string.Equals(_activeMqttDeviceId, config.MqttDeviceId, StringComparison.OrdinalIgnoreCase);
        if (sameDevice)
        {
            _activeMqttDeviceId = null;
            _activeMqttDeviceTopic = null;
        }
        else
        {
            _activeMqttDeviceId = config.MqttDeviceId.Trim();
            _activeMqttDeviceTopic = config.MqttDeviceCommandTopic.Trim();
        }

        UpdateMqttSelectorLeds();
        _logger.LogInformation("MQTT Device Selection: {Device}", _activeMqttDeviceId ?? "(none)");
    }

    private void ExecuteMqttTransport(MasterButtonActionConfig config)
    {
        if (string.IsNullOrWhiteSpace(_activeMqttDeviceTopic))
        {
            _logger.LogDebug("MqttTransport: Kein aktives MQTT-Ziel geraet.");
            return;
        }

        var command = string.IsNullOrWhiteSpace(config.MqttTransportCommand)
            ? "play_pause"
            : config.MqttTransportCommand.Trim().ToLowerInvariant();
        var payload = string.IsNullOrWhiteSpace(config.MqttPayloadPressed)
            ? command
            : config.MqttPayloadPressed;

        _ = _mqttClientService.PublishAsync(
            _activeMqttDeviceTopic,
            payload,
            config.MqttQos,
            config.MqttRetain);
    }

    private void UpdateMqttSelectorLeds()
    {
        foreach (var (note, action) in _config.MasterButtonActions)
        {
            if (action.ActionType != MasterButtonActionType.SelectMqttDevice)
                continue;

            bool isActive = !string.IsNullOrWhiteSpace(_activeMqttDeviceId) &&
                            string.Equals(_activeMqttDeviceId, action.MqttDeviceId, StringComparison.OrdinalIgnoreCase);
            _midiDevice.SetMasterButtonLed(note, isActive ? Core.Enums.LedState.On : Core.Enums.LedState.Off);
        }
    }

    private void UpdateLedFeedback(int noteNumber, MasterButtonActionConfig config)
    {
        try
        {
            switch (config.LedFeedback)
            {
                case LedFeedbackMode.Blink:
                    _midiDevice.SetMasterButtonLed(noteNumber, Core.Enums.LedState.On);
                    Task.Delay(150).ContinueWith(_ =>
                    {
                        try { _midiDevice.SetMasterButtonLed(noteNumber, Core.Enums.LedState.Off); }
                        catch { /* ignorieren */ }
                    });
                    break;

                case LedFeedbackMode.Toggle:
                    _toggleLedStates.TryGetValue(noteNumber, out bool currentlyOn);
                    bool newState = !currentlyOn;
                    _toggleLedStates[noteNumber] = newState;
                    _midiDevice.SetMasterButtonLed(noteNumber,
                        newState ? Core.Enums.LedState.On : Core.Enums.LedState.Off);
                    _logger.LogDebug("LED Toggle Note {Note}: {State}", noteNumber, newState ? "An" : "Aus");
                    break;

                case LedFeedbackMode.Blinking:
                    _toggleLedStates.TryGetValue(noteNumber, out bool currentlyBlinking);
                    bool newBlinkState = !currentlyBlinking;
                    _toggleLedStates[noteNumber] = newBlinkState;
                    _midiDevice.SetMasterButtonLed(noteNumber,
                        newBlinkState ? Core.Enums.LedState.Blink : Core.Enums.LedState.Off);
                    _logger.LogDebug("LED Blinking Note {Note}: {State}", noteNumber, newBlinkState ? "Blinkt" : "Aus");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "LED-Feedback für Note {Note} fehlgeschlagen.", noteNumber);
        }
    }

    private void CycleChannelView()
    {
        _bridge.SwitchView(1); // +1 = nächste Ansicht

        _midiDevice.SetMasterButtonLed(FlipButtonNote, Core.Enums.LedState.On);

        Task.Delay(200).ContinueWith(_ =>
        {
            var state = _bridge.CurrentViewIndex > 0
                ? Core.Enums.LedState.On
                : Core.Enums.LedState.Off;
            _midiDevice.SetMasterButtonLed(FlipButtonNote, state);
        });

        _logger.LogInformation("Channel View gewechselt zu: {View} ({Index}/{Total})",
            _bridge.CurrentViewName, _bridge.CurrentViewIndex + 1, _bridge.ViewCount);
    }

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

    private static VirtualKeyCode? MapKeyName(string keyName)
    {
        if (keyName.StartsWith("F") && int.TryParse(keyName[1..], out int fNum) && fNum >= 1 && fNum <= 24)
            return (VirtualKeyCode)(0x6F + fNum); // VK_F1 = 0x70

        if (keyName.Length == 1 && char.IsLetter(keyName[0]))
            return (VirtualKeyCode)(keyName[0]); // VK_A = 0x41, etc.

        if (keyName.Length == 1 && char.IsDigit(keyName[0]))
            return (VirtualKeyCode)(keyName[0]); // VK_0 = 0x30, etc.

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
        _midiDevice.ConnectionStateChanged -= OnMidiConnectionStateChanged;

        GC.SuppressFinalize(this);
    }
}
