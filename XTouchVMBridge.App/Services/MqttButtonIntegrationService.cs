using Microsoft.Extensions.Logging;
using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Events;
using XTouchVMBridge.Core.Interfaces;
using XTouchVMBridge.Core.Models;
using XTouchVMBridge.Voicemeeter.Services;

namespace XTouchVMBridge.App.Services;

public class MqttButtonIntegrationService : IDisposable
{
    private readonly ILogger<MqttButtonIntegrationService> _logger;
    private readonly IMidiDevice _midiDevice;
    private readonly XTouchVMBridgeConfig _config;
    private readonly VoicemeeterBridge _bridge;
    private readonly MqttClientService _mqttClientService;
    private readonly Dictionary<int, bool> _masterToggleStates = new();
    private bool _disposed;

    public MqttButtonIntegrationService(
        ILogger<MqttButtonIntegrationService> logger,
        IMidiDevice midiDevice,
        XTouchVMBridgeConfig config,
        VoicemeeterBridge bridge,
        MqttClientService mqttClientService)
    {
        _logger = logger;
        _midiDevice = midiDevice;
        _config = config;
        _bridge = bridge;
        _mqttClientService = mqttClientService;

        _midiDevice.ButtonChanged += OnButtonChanged;
        _mqttClientService.MessageReceived += OnMqttMessageReceived;
    }

    private void OnButtonChanged(object? sender, ButtonEventArgs e)
    {
        try
        {
            int vmCh = ResolveVmChannel(e.Channel);
            if (!_config.Mappings.TryGetValue(vmCh, out var mapping))
                return;

            string btnKey = e.ButtonType.ToString();
            if (!mapping.Buttons.TryGetValue(btnKey, out var btnMap) || btnMap == null)
                return;
            if (btnMap.ActionType != ButtonActionType.MqttPublish)
                return;

            var publish = btnMap.MqttPublish;
            if (publish == null || string.IsNullOrWhiteSpace(publish.Topic))
                return;

            string payload = e.IsPressed ? publish.PayloadPressed : publish.PayloadReleased;
            if (string.IsNullOrWhiteSpace(payload))
                return;

            _ = _mqttClientService.PublishAsync(
                publish.Topic,
                payload,
                publish.Qos,
                publish.Retain);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MQTT Publish fuer Button-Event fehlgeschlagen.");
        }
    }

    private void OnMqttMessageReceived(object? sender, MqttMessageReceivedEventArgs e)
    {
        try
        {
            ApplyLedBindings(e.Topic, e.Payload);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MQTT LED Verarbeitung fehlgeschlagen.");
        }
    }

    private void ApplyLedBindings(string topic, string payload)
    {
        var mapping = _bridge.CurrentChannelMapping;
        for (int xtCh = 0; xtCh < mapping.Length; xtCh++)
        {
            int vmCh = mapping[xtCh];
            if (!_config.Mappings.TryGetValue(vmCh, out var controlMap))
                continue;

            foreach (XTouchButtonType buttonType in Enum.GetValues<XTouchButtonType>())
            {
                if (!controlMap.Buttons.TryGetValue(buttonType.ToString(), out var buttonMap) || buttonMap == null)
                    continue;

                var ledCfg = buttonMap.MqttLedReceive;
                if (ledCfg?.Enabled != true || string.IsNullOrWhiteSpace(ledCfg.Topic))
                    continue;
                if (!TopicMatches(ledCfg.Topic, topic))
                    continue;

                if (!TryMapPayloadToLedState(ledCfg, payload, xtCh, buttonType, out var targetState))
                    continue;

                _midiDevice.SetButtonLed(xtCh, buttonType, targetState);
            }
        }

        foreach (var (noteNumber, action) in _config.MasterButtonActions)
        {
            if (!action.MqttLedEnabled || string.IsNullOrWhiteSpace(action.MqttLedTopic))
                continue;
            if (!TopicMatches(action.MqttLedTopic, topic))
                continue;

            if (!TryMapMasterPayloadToLedState(action, payload, noteNumber, out var masterState))
                continue;

            _midiDevice.SetMasterButtonLed(noteNumber, masterState);
        }
    }

    private bool TryMapPayloadToLedState(
        MqttButtonLedReceiveConfig cfg,
        string payload,
        int xtCh,
        XTouchButtonType buttonType,
        out LedState ledState)
    {
        ledState = LedState.Off;

        var comparison = cfg.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var value = payload?.Trim() ?? string.Empty;

        if (EqualsWithComparison(value, cfg.PayloadOn, comparison))
        {
            ledState = LedState.On;
            return true;
        }

        if (EqualsWithComparison(value, cfg.PayloadOff, comparison))
        {
            ledState = LedState.Off;
            return true;
        }

        if (EqualsWithComparison(value, cfg.PayloadBlink, comparison))
        {
            ledState = LedState.Blink;
            return true;
        }

        if (EqualsWithComparison(value, cfg.PayloadToggle, comparison))
        {
            var current = _midiDevice.Channels[xtCh].GetButton(buttonType).LedState;
            ledState = current == LedState.Off ? LedState.On : LedState.Off;
            return true;
        }

        return false;
    }

    private static bool EqualsWithComparison(string value, string configured, StringComparison comparison)
    {
        if (string.IsNullOrWhiteSpace(configured))
            return false;
        return string.Equals(value, configured.Trim(), comparison);
    }

    private bool TryMapMasterPayloadToLedState(
        MasterButtonActionConfig action,
        string payload,
        int noteNumber,
        out LedState ledState)
    {
        ledState = LedState.Off;
        var value = payload?.Trim() ?? string.Empty;
        var comparison = StringComparison.OrdinalIgnoreCase;

        if (EqualsWithComparison(value, action.MqttLedPayloadOn ?? "on", comparison))
        {
            ledState = LedState.On;
            _masterToggleStates[noteNumber] = true;
            return true;
        }

        if (EqualsWithComparison(value, action.MqttLedPayloadOff ?? "off", comparison))
        {
            ledState = LedState.Off;
            _masterToggleStates[noteNumber] = false;
            return true;
        }

        if (EqualsWithComparison(value, action.MqttLedPayloadBlink ?? "blink", comparison))
        {
            ledState = LedState.Blink;
            _masterToggleStates[noteNumber] = true;
            return true;
        }

        if (EqualsWithComparison(value, action.MqttLedPayloadToggle ?? "toggle", comparison))
        {
            _masterToggleStates.TryGetValue(noteNumber, out bool current);
            var next = !current;
            _masterToggleStates[noteNumber] = next;
            ledState = next ? LedState.On : LedState.Off;
            return true;
        }

        return false;
    }

    private int ResolveVmChannel(int xtChannel)
    {
        var map = _bridge.CurrentChannelMapping;
        if (xtChannel >= 0 && xtChannel < map.Length)
            return map[xtChannel];
        return xtChannel;
    }

    private static bool TopicMatches(string filter, string topic)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return false;

        var filterLevels = filter.Split('/');
        var topicLevels = topic.Split('/');

        int fi = 0;
        int ti = 0;
        while (fi < filterLevels.Length && ti < topicLevels.Length)
        {
            string f = filterLevels[fi];
            if (f == "#")
                return true;
            if (f != "+" && !string.Equals(f, topicLevels[ti], StringComparison.Ordinal))
                return false;

            fi++;
            ti++;
        }

        if (fi == filterLevels.Length && ti == topicLevels.Length)
            return true;

        if (fi == filterLevels.Length - 1 && filterLevels[fi] == "#")
            return true;

        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _midiDevice.ButtonChanged -= OnButtonChanged;
        _mqttClientService.MessageReceived -= OnMqttMessageReceived;
    }
}
