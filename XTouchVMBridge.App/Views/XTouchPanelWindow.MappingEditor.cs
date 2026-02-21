using System.Windows;
using System.Windows.Controls;
using XTouchVMBridge.App.Services;
using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Models;
using XTouchVMBridge.Voicemeeter.Services;

namespace XTouchVMBridge.App.Views;

/// <summary>
/// Mapping editor logic for channel controls (button/fader/encoder) and master-section actions.
/// Handles panel population, form state synchronization, and saving mappings back to config.
/// </summary>
public partial class XTouchPanelWindow
{
    /// <summary>
    /// Hides all mapping sub-panels so the active editor panel can be shown explicitly.
    /// </summary>
    private void HideMappingSubPanels()
    {
        ButtonMappingPanel.Visibility = Visibility.Collapsed;
        FaderMappingPanel.Visibility = Visibility.Collapsed;
        EncoderMappingPanel.Visibility = Visibility.Collapsed;
        MasterButtonMappingPanel.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Opens the channel button mapping panel for the selected strip/button type.
    /// Supports VM-parameter and MQTT publish modes.
    /// </summary>
    private void ShowButtonMappingPanel(int xtChannel, XTouchButtonType buttonType)
    {
        if (_config == null || _configService == null)
        {
            MappingPanel.Visibility = Visibility.Collapsed;
            return;
        }

        int vmCh = ResolveVmChannel(xtChannel);
        _selectedVmChannel = vmCh;
        _selectedControlType = "Button";
        _selectedButtonType = buttonType;

        _suppressMappingEvents = true;
        try
        {
            HideMappingSubPanels();

            ButtonActionTypeCombo.Items.Clear();
            ButtonActionTypeCombo.Items.Add(new ComboBoxItem { Content = LocalizationService.T("VM-Parameter toggeln", "Toggle VM parameter"), Tag = ButtonActionType.VmParameter });
            ButtonActionTypeCombo.Items.Add(new ComboBoxItem { Content = "MQTT Publish", Tag = ButtonActionType.MqttPublish });
            ButtonMqttQosCombo.ItemsSource = new[] { "0", "1", "2" };
            ButtonMqttLedTestModeCombo.ItemsSource = new[] { "On", "Off", "Blink", "Toggle" };
            ButtonMqttLedTestModeCombo.SelectedItem = "On";

            var boolParams = VoicemeeterParameterCatalog.GetBoolParameters(vmCh);
            ButtonParamCombo.Items.Clear();
            ButtonParamCombo.Items.Add(new ComboBoxItem { Content = LocalizationService.T("(nicht zugewiesen)", "(not assigned)"), Tag = "" });
            if (buttonType == XTouchButtonType.Rec)
            {
                ButtonParamCombo.Items.Add(new ComboBoxItem
                {
                    Content = LocalizationService.T("Aufnahme Start/Stop (Dateiname: Kanal + Zeit)", "Recording start/stop (filename: channel + time)"),
                    Tag = ButtonMappingConfig.ChannelRecordActionParameter
                });
            }

            foreach (var p in boolParams)
            {
                ButtonParamCombo.Items.Add(new ComboBoxItem
                {
                    Content = $"{p.DisplayName}  [{p.Parameter}]",
                    Tag = p.Parameter
                });
            }

            ButtonMappingConfig? currentMapping = null;
            string? currentParam = null;
            if (_config.Mappings.TryGetValue(vmCh, out var mapping))
            {
                string btnKey = buttonType.ToString();
                if (mapping.Buttons.TryGetValue(btnKey, out var btnMap) && btnMap != null)
                {
                    currentMapping = btnMap;
                    currentParam = btnMap.Parameter;
                }
            }

            ButtonParamCombo.SelectedIndex = 0; // Default: not assigned
            if (currentParam != null)
            {
                for (int i = 1; i < ButtonParamCombo.Items.Count; i++)
                {
                    if (ButtonParamCombo.Items[i] is ComboBoxItem item && (string)item.Tag == currentParam)
                    {
                        ButtonParamCombo.SelectedIndex = i;
                        break;
                    }
                }
            }

            var actionType = currentMapping?.ActionType ?? ButtonActionType.VmParameter;
            ButtonActionTypeCombo.SelectedIndex = actionType == ButtonActionType.MqttPublish ? 1 : 0;

            var publish = currentMapping?.MqttPublish;
            ButtonMqttTopicBox.Text = publish?.Topic ?? "";
            ButtonMqttPayloadPressBox.Text = publish?.PayloadPressed ?? "on";
            ButtonMqttPayloadReleaseBox.Text = publish?.PayloadReleased ?? "";
            ButtonMqttQosCombo.SelectedItem = (publish?.Qos ?? 0).ToString();
            ButtonMqttRetainBox.IsChecked = publish?.Retain == true;

            var led = currentMapping?.MqttLedReceive;
            ButtonMqttLedEnabledBox.IsChecked = led?.Enabled == true;
            ButtonMqttLedTopicBox.Text = led?.Topic ?? "";
            ButtonMqttLedOnPayloadBox.Text = led?.PayloadOn ?? "on";
            ButtonMqttLedOffPayloadBox.Text = led?.PayloadOff ?? "off";
            ButtonMqttLedBlinkPayloadBox.Text = led?.PayloadBlink ?? "blink";
            ButtonMqttLedTogglePayloadBox.Text = led?.PayloadToggle ?? "toggle";

            UpdateButtonActionSubPanels(actionType);

            ButtonMappingPanel.Visibility = Visibility.Visible;
            MappingPanelHeader.Text = LocalizationService.T("Button Mapping", "Button Mapping");
            MappingPanel.Visibility = Visibility.Visible;
            LocalizationService.LocalizeWindow(this);
        }
        finally
        {
            _suppressMappingEvents = false;
        }
    }

    /// <summary>
    /// Applies action-type-specific visibility for channel button mappings.
    /// </summary>
    private void UpdateButtonActionSubPanels(ButtonActionType actionType)
    {
        ButtonVmParamPanel.Visibility = actionType == ButtonActionType.VmParameter
            ? Visibility.Visible : Visibility.Collapsed;
        ButtonMqttPublishPanel.Visibility = actionType == ButtonActionType.MqttPublish
            ? Visibility.Visible : Visibility.Collapsed;
        ButtonMqttLedPanel.Visibility = actionType == ButtonActionType.MqttPublish
            ? Visibility.Visible : Visibility.Collapsed;
        RefreshButtonVmRecHintVisibility(actionType);
    }

    private void RefreshButtonVmRecHintVisibility(ButtonActionType actionType)
    {
        bool showRecHint = false;
        if (actionType == ButtonActionType.VmParameter &&
            ButtonParamCombo.SelectedItem is ComboBoxItem selected &&
            selected.Tag is string paramName)
        {
            showRecHint = string.Equals(
                paramName,
                ButtonMappingConfig.ChannelRecordActionParameter,
                StringComparison.Ordinal);
        }

        ButtonVmRecHintText.Visibility = showRecHint ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Opens the fader mapping panel for the selected strip.
    /// </summary>
    private void ShowFaderMappingPanel(int xtChannel)
    {
        if (_config == null || _configService == null)
        {
            MappingPanel.Visibility = Visibility.Collapsed;
            return;
        }

        int vmCh = ResolveVmChannel(xtChannel);
        _selectedVmChannel = vmCh;
        _selectedControlType = "Fader";

        _suppressMappingEvents = true;
        try
        {
            HideMappingSubPanels();

            var floatParams = VoicemeeterParameterCatalog.GetFloatParameters(vmCh);
            FaderParamCombo.Items.Clear();
            FaderParamCombo.Items.Add(new ComboBoxItem { Content = LocalizationService.T("(nicht zugewiesen)", "(not assigned)"), Tag = "" });

            foreach (var p in floatParams)
            {
                FaderParamCombo.Items.Add(new ComboBoxItem
                {
                    Content = $"{p.DisplayName}  [{p.Parameter}]",
                    Tag = p.Parameter
                });
            }

            FaderParamCombo.SelectedIndex = 0;
            FaderMinBox.Text = "-60";
            FaderMaxBox.Text = "12";

            if (_config.Mappings.TryGetValue(vmCh, out var mapping) && mapping.Fader != null)
            {
                FaderMinBox.Text = mapping.Fader.Min.ToString();
                FaderMaxBox.Text = mapping.Fader.Max.ToString();

                for (int i = 1; i < FaderParamCombo.Items.Count; i++)
                {
                    if (FaderParamCombo.Items[i] is ComboBoxItem item && (string)item.Tag == mapping.Fader.Parameter)
                    {
                        FaderParamCombo.SelectedIndex = i;
                        break;
                    }
                }
            }

            FaderMappingPanel.Visibility = Visibility.Visible;
            MappingPanel.Visibility = Visibility.Visible;
            LocalizationService.LocalizeWindow(this);
        }
        finally
        {
            _suppressMappingEvents = false;
        }
    }

    /// <summary>
    /// Opens the encoder mapping panel and loads current function assignments.
    /// </summary>
    private void ShowEncoderMappingPanel(int xtChannel)
    {
        if (_config == null || _configService == null)
        {
            MappingPanel.Visibility = Visibility.Collapsed;
            return;
        }

        int vmCh = ResolveVmChannel(xtChannel);
        _selectedVmChannel = vmCh;
        _selectedControlType = "Encoder";

        _suppressMappingEvents = true;
        try
        {
            HideMappingSubPanels();

            RefreshEncoderFunctionList(vmCh);

            var floatParams = VoicemeeterParameterCatalog.GetFloatParameters(vmCh);
            EncoderAddParamCombo.Items.Clear();
            foreach (var p in floatParams)
            {
                EncoderAddParamCombo.Items.Add(new ComboBoxItem
                {
                    Content = $"{p.DisplayName}  [{p.Parameter}]",
                    Tag = p.Parameter
                });
            }
            if (EncoderAddParamCombo.Items.Count > 0)
                EncoderAddParamCombo.SelectedIndex = 0;

            EncoderAddLabelBox.Text = "";

            EncoderMappingPanel.Visibility = Visibility.Visible;
            MappingPanel.Visibility = Visibility.Visible;
            LocalizationService.LocalizeWindow(this);
        }
        finally
        {
            _suppressMappingEvents = false;
        }
    }

    private void RefreshEncoderFunctionList(int vmCh)
    {
        EncoderFunctionList.Items.Clear();

        if (_config?.Mappings.TryGetValue(vmCh, out var mapping) == true)
        {
            foreach (var fn in mapping.EncoderFunctions)
            {
                EncoderFunctionList.Items.Add(new ListBoxItem
                {
                    Content = $"{fn.Label,-7} → {fn.Parameter} ({fn.Min}..{fn.Max}, Step {fn.Step} {fn.Unit})",
                    Tag = fn
                });
            }
        }
    }


    /// <summary>
    /// Opens the master-button action editor for a specific MIDI note.
    /// </summary>
    private void ShowMasterButtonMappingPanel(int noteNumber)
    {
        if (_config == null || _configService == null)
        {
            MappingPanel.Visibility = Visibility.Collapsed;
            return;
        }

        _suppressMappingEvents = true;
        try
        {
            HideMappingSubPanels();

            MasterActionTypeCombo.Items.Clear();
            MasterActionTypeCombo.Items.Add(new ComboBoxItem { Content = LocalizationService.T("(keine Aktion)", "(no action)"), Tag = MasterButtonActionType.None });
            MasterActionTypeCombo.Items.Add(new ComboBoxItem { Content = LocalizationService.T("VM-Parameter toggeln", "Toggle VM parameter"), Tag = MasterButtonActionType.VmParameter });
            MasterActionTypeCombo.Items.Add(new ComboBoxItem { Content = LocalizationService.T("Programm starten", "Launch program"), Tag = MasterButtonActionType.LaunchProgram });
            MasterActionTypeCombo.Items.Add(new ComboBoxItem { Content = LocalizationService.T("Tastenkombination senden", "Send key combination"), Tag = MasterButtonActionType.SendKeys });
            MasterActionTypeCombo.Items.Add(new ComboBoxItem { Content = LocalizationService.T("Text senden", "Send text"), Tag = MasterButtonActionType.SendText });
            MasterActionTypeCombo.Items.Add(new ComboBoxItem { Content = LocalizationService.T("VM Audio Engine neu starten", "Restart VM audio engine"), Tag = MasterButtonActionType.RestartAudioEngine });
            MasterActionTypeCombo.Items.Add(new ComboBoxItem { Content = LocalizationService.T("VM-Fenster anzeigen", "Show VM window"), Tag = MasterButtonActionType.ShowVoicemeeter });
            MasterActionTypeCombo.Items.Add(new ComboBoxItem { Content = LocalizationService.T("VM-GUI sperren/entsperren", "Lock/unlock VM GUI"), Tag = MasterButtonActionType.LockGui });
            MasterActionTypeCombo.Items.Add(new ComboBoxItem { Content = LocalizationService.T("Macro-Button auslösen", "Trigger macro button"), Tag = MasterButtonActionType.TriggerMacroButton });
            MasterActionTypeCombo.Items.Add(new ComboBoxItem { Content = "MQTT Publish", Tag = MasterButtonActionType.MqttPublish });
            MasterMqttQosCombo.ItemsSource = new[] { "0", "1", "2" };
            MasterActionTypeCombo.Items.Add(new ComboBoxItem { Content = LocalizationService.T("MQTT Geraet auswaehlen", "Select MQTT device"), Tag = MasterButtonActionType.SelectMqttDevice });
            MasterActionTypeCombo.Items.Add(new ComboBoxItem { Content = "MQTT Transport", Tag = MasterButtonActionType.MqttTransport });
            MasterMqttTransportQosCombo.ItemsSource = new[] { "0", "1", "2" };
            MasterMqttTransportCommandCombo.ItemsSource = new[] { "play_pause", "play", "pause", "stop", "next", "prev" };

            MasterButtonActionConfig? actionConfig = null;
            _config.MasterButtonActions.TryGetValue(noteNumber, out actionConfig);

            var activeType = actionConfig?.ActionType ?? MasterButtonActionType.None;

            for (int i = 0; i < MasterActionTypeCombo.Items.Count; i++)
            {
                if (MasterActionTypeCombo.Items[i] is ComboBoxItem item &&
                    item.Tag is MasterButtonActionType type && type == activeType)
                {
                    MasterActionTypeCombo.SelectedIndex = i;
                    break;
                }
            }

            MasterProgramPathBox.Text = actionConfig?.ProgramPath ?? "";
            MasterProgramArgsBox.Text = actionConfig?.ProgramArgs ?? "";
            MasterKeyCombinationBox.Text = actionConfig?.KeyCombination ?? "";
            MasterTextBox.Text = actionConfig?.Text ?? "";
            MasterMacroButtonIndexBox.Text = actionConfig?.MacroButtonIndex?.ToString() ?? "0";
            MasterMqttTopicBox.Text = actionConfig?.MqttTopic ?? "";
            MasterMqttPayloadPressBox.Text = actionConfig?.MqttPayloadPressed ?? "on";
            MasterMqttPayloadReleaseBox.Text = actionConfig?.MqttPayloadReleased ?? "";
            MasterMqttQosCombo.SelectedItem = actionConfig?.MqttQos.ToString() ?? "0";
            MasterMqttRetainBox.IsChecked = actionConfig?.MqttRetain == true;
            MasterMqttLedEnabledBox.IsChecked = actionConfig?.MqttLedEnabled == true;
            MasterMqttLedTopicBox.Text = actionConfig?.MqttLedTopic ?? "";
            MasterMqttLedOnPayloadBox.Text = actionConfig?.MqttLedPayloadOn ?? "on";
            MasterMqttLedOffPayloadBox.Text = actionConfig?.MqttLedPayloadOff ?? "off";
            MasterMqttLedBlinkPayloadBox.Text = actionConfig?.MqttLedPayloadBlink ?? "blink";
            MasterMqttLedTogglePayloadBox.Text = actionConfig?.MqttLedPayloadToggle ?? "toggle";
            MasterMqttDeviceIdBox.Text = actionConfig?.MqttDeviceId ?? "";
            MasterMqttDeviceCommandTopicBox.Text = actionConfig?.MqttDeviceCommandTopic ?? "";
            MasterMqttTransportCommandCombo.SelectedItem =
                actionConfig?.MqttTransportCommand ??
                GetDefaultTransportCommandForMasterNote(_selectedMasterButtonNote) ??
                "play_pause";
            MasterMqttTransportPayloadBox.Text = actionConfig?.MqttPayloadPressed ?? "";
            MasterMqttTransportQosCombo.SelectedItem = actionConfig?.MqttQos.ToString() ?? "0";
            MasterMqttTransportRetainBox.IsChecked = actionConfig?.MqttRetain == true;

            MasterVmLedSourceCombo.Items.Clear();
            MasterVmLedSourceCombo.Items.Add(new ComboBoxItem { Content = LocalizationService.T("Manuell (LED-Feedback)", "Manual (LED feedback)"), Tag = MasterVmLedSource.ManualFeedback });
            MasterVmLedSourceCombo.Items.Add(new ComboBoxItem { Content = LocalizationService.T("Aus Voicemeeter-Status", "From Voicemeeter state"), Tag = MasterVmLedSource.VoicemeeterState });
            var activeVmLedSource = actionConfig?.VmLedSource ?? MasterVmLedSource.ManualFeedback;
            MasterVmLedSourceCombo.SelectedIndex = activeVmLedSource == MasterVmLedSource.VoicemeeterState ? 1 : 0;

            MasterLedFeedbackCombo.Items.Clear();
            MasterLedFeedbackCombo.Items.Add(new ComboBoxItem { Content = LocalizationService.T("Kurz aufblinken", "Short blink"), Tag = LedFeedbackMode.Blink });
            MasterLedFeedbackCombo.Items.Add(new ComboBoxItem { Content = LocalizationService.T("An/Aus (Toggle)", "On/Off (toggle)"), Tag = LedFeedbackMode.Toggle });
            MasterLedFeedbackCombo.Items.Add(new ComboBoxItem { Content = LocalizationService.T("Dauerhaft blinken", "Continuous blink"), Tag = LedFeedbackMode.Blinking });
            var activeLedMode = actionConfig?.LedFeedback ?? LedFeedbackMode.Blink;
            MasterLedFeedbackCombo.SelectedIndex = activeLedMode switch
            {
                LedFeedbackMode.Toggle => 1,
                LedFeedbackMode.Blinking => 2,
                _ => 0
            };

            InitMasterVmParamDropdowns(actionConfig?.VmParameter);

            UpdateMasterActionSubPanels(activeType);

            MasterButtonMappingPanel.Visibility = Visibility.Visible;
            MappingPanelHeader.Text = LocalizationService.T("Master-Button Aktion", "Master button action");
            MappingPanel.Visibility = Visibility.Visible;
            LocalizationService.LocalizeWindow(this);
        }
        finally
        {
            _suppressMappingEvents = false;
        }
    }

    private void UpdateMasterActionSubPanels(MasterButtonActionType type)
    {
        MasterVmParamPanel.Visibility = type == MasterButtonActionType.VmParameter ? Visibility.Visible : Visibility.Collapsed;
        MasterLaunchPanel.Visibility = type == MasterButtonActionType.LaunchProgram ? Visibility.Visible : Visibility.Collapsed;
        MasterSendKeysPanel.Visibility = type == MasterButtonActionType.SendKeys ? Visibility.Visible : Visibility.Collapsed;
        MasterSendTextPanel.Visibility = type == MasterButtonActionType.SendText ? Visibility.Visible : Visibility.Collapsed;
        MasterMacroButtonPanel.Visibility = type == MasterButtonActionType.TriggerMacroButton ? Visibility.Visible : Visibility.Collapsed;
        MasterMqttPanel.Visibility = type == MasterButtonActionType.MqttPublish ? Visibility.Visible : Visibility.Collapsed;
        MasterMqttDeviceSelectPanel.Visibility = type == MasterButtonActionType.SelectMqttDevice ? Visibility.Visible : Visibility.Collapsed;
        MasterMqttTransportPanel.Visibility = type == MasterButtonActionType.MqttTransport ? Visibility.Visible : Visibility.Collapsed;

        RefreshMasterLedFeedbackPanelVisibility(type);
    }

    private void OnMasterActionTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressMappingEvents) return;
        if (MasterActionTypeCombo.SelectedItem is not ComboBoxItem item) return;
        if (item.Tag is not MasterButtonActionType type) return;

        UpdateMasterActionSubPanels(type);
        if (type == MasterButtonActionType.MqttTransport)
            ApplyMasterTransportPresetIfNeeded();
    }

    private void ApplyMasterTransportPresetIfNeeded()
    {
        var preset = GetDefaultTransportCommandForMasterNote(_selectedMasterButtonNote);
        if (string.IsNullOrWhiteSpace(preset))
            return;

        var current = MasterMqttTransportCommandCombo.SelectedItem?.ToString() ?? "";
        if (!string.IsNullOrWhiteSpace(current) && current != "play_pause")
            return;

        MasterMqttTransportCommandCombo.SelectedItem = preset;
    }

    private static string? GetDefaultTransportCommandForMasterNote(int noteNumber) => noteNumber switch
    {
        91 => "prev",        // Rewind
        92 => "next",        // Forward
        93 => "stop",        // Stop
        94 => "play_pause",  // Play
        95 => "pause",       // Record button as dedicated pause preset
        _ => null
    };

    private void OnMasterBrowseProgram(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Programm auswählen",
            Filter = "Ausführbare Dateien (*.exe)|*.exe|Alle Dateien (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            MasterProgramPathBox.Text = dlg.FileName;
        }
    }

    private void OnMasterActionSave(object sender, RoutedEventArgs e)
    {
        // Commits master-section action configuration including transport/device/MQTT options.
        if (_config == null || _configService == null || _selectedMasterButtonNote < 0) return;

        var selectedType = MasterButtonActionType.None;
        if (MasterActionTypeCombo.SelectedItem is ComboBoxItem item && item.Tag is MasterButtonActionType type)
            selectedType = type;

        if (selectedType == MasterButtonActionType.None)
        {
            _config.MasterButtonActions.Remove(_selectedMasterButtonNote);
        }
        else
        {
            int? macroIndex = null;
            if (selectedType == MasterButtonActionType.TriggerMacroButton &&
                int.TryParse(MasterMacroButtonIndexBox.Text.Trim(), out int parsedIndex))
            {
                macroIndex = Math.Clamp(parsedIndex, 0, 79);
            }

            var ledMode = LedFeedbackMode.Blink;
            if (MasterLedFeedbackCombo.SelectedItem is ComboBoxItem ledItem &&
                ledItem.Tag is LedFeedbackMode mode)
            {
                ledMode = mode;
            }

            var vmLedSource = GetSelectedMasterVmLedSource();

            _config.MasterButtonActions[_selectedMasterButtonNote] = new MasterButtonActionConfig
            {
                ActionType = selectedType,
                VmParameter = selectedType == MasterButtonActionType.VmParameter ? MasterVmParamBox.Text.Trim() : null,
                VmLedSource = selectedType == MasterButtonActionType.VmParameter ? vmLedSource : MasterVmLedSource.ManualFeedback,
                ProgramPath = selectedType == MasterButtonActionType.LaunchProgram ? MasterProgramPathBox.Text.Trim() : null,
                ProgramArgs = selectedType == MasterButtonActionType.LaunchProgram ? MasterProgramArgsBox.Text.Trim() : null,
                KeyCombination = selectedType == MasterButtonActionType.SendKeys ? MasterKeyCombinationBox.Text.Trim() : null,
                Text = selectedType == MasterButtonActionType.SendText ? MasterTextBox.Text : null,
                MacroButtonIndex = macroIndex,
                MqttTopic = selectedType == MasterButtonActionType.MqttPublish ? MasterMqttTopicBox.Text.Trim() : null,
                MqttPayloadReleased = selectedType == MasterButtonActionType.MqttPublish ? MasterMqttPayloadReleaseBox.Text : null,
                MqttDeviceId = selectedType == MasterButtonActionType.SelectMqttDevice ? MasterMqttDeviceIdBox.Text.Trim() : null,
                MqttDeviceCommandTopic = selectedType == MasterButtonActionType.SelectMqttDevice ? MasterMqttDeviceCommandTopicBox.Text.Trim() : null,
                MqttTransportCommand = selectedType == MasterButtonActionType.MqttTransport
                    ? (MasterMqttTransportCommandCombo.SelectedItem?.ToString() ?? "play_pause")
                    : null,
                MqttPayloadPressed = selectedType == MasterButtonActionType.MqttTransport
                    ? MasterMqttTransportPayloadBox.Text
                    : (selectedType == MasterButtonActionType.MqttPublish ? MasterMqttPayloadPressBox.Text : null),
                MqttQos = selectedType == MasterButtonActionType.MqttTransport &&
                          int.TryParse(MasterMqttTransportQosCombo.SelectedItem?.ToString(), out int transportQos)
                    ? Math.Clamp(transportQos, 0, 2)
                    : (selectedType == MasterButtonActionType.MqttPublish &&
                       int.TryParse(MasterMqttQosCombo.SelectedItem?.ToString(), out int mqttQos)
                        ? Math.Clamp(mqttQos, 0, 2)
                        : 0),
                MqttRetain = selectedType == MasterButtonActionType.MqttTransport
                    ? MasterMqttTransportRetainBox.IsChecked == true
                    : (selectedType == MasterButtonActionType.MqttPublish && MasterMqttRetainBox.IsChecked == true),
                MqttLedEnabled = selectedType == MasterButtonActionType.MqttPublish && MasterMqttLedEnabledBox.IsChecked == true,
                MqttLedTopic = selectedType == MasterButtonActionType.MqttPublish ? MasterMqttLedTopicBox.Text.Trim() : null,
                MqttLedPayloadOn = selectedType == MasterButtonActionType.MqttPublish ? MasterMqttLedOnPayloadBox.Text : null,
                MqttLedPayloadOff = selectedType == MasterButtonActionType.MqttPublish ? MasterMqttLedOffPayloadBox.Text : null,
                MqttLedPayloadBlink = selectedType == MasterButtonActionType.MqttPublish ? MasterMqttLedBlinkPayloadBox.Text : null,
                MqttLedPayloadToggle = selectedType == MasterButtonActionType.MqttPublish ? MasterMqttLedTogglePayloadBox.Text : null,
                LedFeedback = ledMode
            };
        }

        SaveAndReload();
    }

    private void OnMasterActionClear(object sender, RoutedEventArgs e)
    {
        if (_config == null || _configService == null || _selectedMasterButtonNote < 0) return;

        _config.MasterButtonActions.Remove(_selectedMasterButtonNote);

        _suppressMappingEvents = true;
        MasterActionTypeCombo.SelectedIndex = 0;
        MasterVmParamBox.Text = "";
        MasterProgramPathBox.Text = "";
        MasterProgramArgsBox.Text = "";
        MasterKeyCombinationBox.Text = "";
        MasterTextBox.Text = "";
        MasterMacroButtonIndexBox.Text = "0";
        MasterMqttTopicBox.Text = "";
        MasterMqttPayloadPressBox.Text = "on";
        MasterMqttPayloadReleaseBox.Text = "";
        MasterMqttQosCombo.SelectedItem = "0";
        MasterMqttRetainBox.IsChecked = false;
        MasterMqttLedEnabledBox.IsChecked = false;
        MasterMqttLedTopicBox.Text = "";
        MasterMqttLedOnPayloadBox.Text = "on";
        MasterMqttLedOffPayloadBox.Text = "off";
        MasterMqttLedBlinkPayloadBox.Text = "blink";
        MasterMqttLedTogglePayloadBox.Text = "toggle";
        MasterMqttDeviceIdBox.Text = "";
        MasterMqttDeviceCommandTopicBox.Text = "";
        MasterMqttTransportCommandCombo.SelectedItem = "play_pause";
        MasterMqttTransportPayloadBox.Text = "";
        MasterMqttTransportQosCombo.SelectedItem = "0";
        MasterMqttTransportRetainBox.IsChecked = false;
        if (MasterVmLedSourceCombo.Items.Count > 0)
            MasterVmLedSourceCombo.SelectedIndex = 0;
        if (MasterLedFeedbackCombo.Items.Count > 0)
            MasterLedFeedbackCombo.SelectedIndex = 0;
        UpdateMasterActionSubPanels(MasterButtonActionType.None);
        _suppressMappingEvents = false;

        SaveAndReload();
    }


    private void InitMasterVmParamDropdowns(string? currentParam)
    {
        _suppressMappingEvents = true;
        try
        {
            MasterVmChannelTypeCombo.Items.Clear();
            MasterVmChannelTypeCombo.Items.Add(new ComboBoxItem { Content = "Strip", Tag = "Strip" });
            MasterVmChannelTypeCombo.Items.Add(new ComboBoxItem { Content = "Bus", Tag = "Bus" });

            string preselectedType = "Strip";
            int preselectedIndex = 0;
            string? preselectedGroup = null;
            string? preselectedTemplate = null;

            if (!string.IsNullOrWhiteSpace(currentParam))
            {
                if (currentParam.StartsWith("Bus["))
                {
                    preselectedType = "Bus";
                    var match = System.Text.RegularExpressions.Regex.Match(currentParam, @"Bus\[(\d+)\]");
                    if (match.Success) preselectedIndex = int.Parse(match.Groups[1].Value);
                }
                else if (currentParam.StartsWith("Strip["))
                {
                    preselectedType = "Strip";
                    var match = System.Text.RegularExpressions.Regex.Match(currentParam, @"Strip\[(\d+)\]");
                    if (match.Success) preselectedIndex = int.Parse(match.Groups[1].Value);
                }

                var groups = VoicemeeterParameterCatalog.GetBoolGroups(preselectedType);
                foreach (var group in groups)
                {
                    foreach (var param in group.Parameters)
                    {
                        string resolved = param.Template
                            .Replace("{s}", preselectedIndex.ToString())
                            .Replace("{b}", preselectedIndex.ToString());
                        if (resolved == currentParam)
                        {
                            preselectedGroup = group.GroupName;
                            preselectedTemplate = param.Template;
                            break;
                        }
                    }
                    if (preselectedGroup != null) break;
                }
            }

            MasterVmChannelTypeCombo.SelectedIndex = preselectedType == "Bus" ? 1 : 0;

            PopulateMasterVmGroups(preselectedType, preselectedGroup);

            if (preselectedGroup != null)
                PopulateMasterVmParams(preselectedType, preselectedGroup, preselectedTemplate);

            PopulateMasterVmIndex(preselectedIndex);

            UpdateMasterVmResultParam();
        }
        finally
        {
            _suppressMappingEvents = false;
        }
    }

    private void PopulateMasterVmGroups(string channelType, string? preselectedGroup = null)
    {
        MasterVmGroupCombo.Items.Clear();
        var groups = VoicemeeterParameterCatalog.GetBoolGroups(channelType);
        int selectIndex = 0;
        for (int i = 0; i < groups.Count; i++)
        {
            MasterVmGroupCombo.Items.Add(new ComboBoxItem { Content = groups[i].GroupName, Tag = groups[i].GroupName });
            if (groups[i].GroupName == preselectedGroup) selectIndex = i;
        }
        if (MasterVmGroupCombo.Items.Count > 0)
            MasterVmGroupCombo.SelectedIndex = selectIndex;
    }

    private void PopulateMasterVmParams(string channelType, string groupName, string? preselectedTemplate = null)
    {
        MasterVmParamCombo.Items.Clear();
        var groups = VoicemeeterParameterCatalog.GetBoolGroups(channelType);
        var group = groups.FirstOrDefault(g => g.GroupName == groupName);
        if (group == null) return;

        int selectIndex = 0;
        for (int i = 0; i < group.Parameters.Count; i++)
        {
            var p = group.Parameters[i];
            MasterVmParamCombo.Items.Add(new ComboBoxItem { Content = p.DisplayName, Tag = p.Template });
            if (p.Template == preselectedTemplate) selectIndex = i;
        }
        if (MasterVmParamCombo.Items.Count > 0)
            MasterVmParamCombo.SelectedIndex = selectIndex;
    }

    private void PopulateMasterVmIndex(int preselectedIndex = 0)
    {
        MasterVmIndexCombo.Items.Clear();
        for (int i = 0; i < 8; i++)
        {
            MasterVmIndexCombo.Items.Add(new ComboBoxItem { Content = i.ToString(), Tag = i });
        }
        if (preselectedIndex >= 0 && preselectedIndex < 8)
            MasterVmIndexCombo.SelectedIndex = preselectedIndex;
        else
            MasterVmIndexCombo.SelectedIndex = 0;
    }

    private void UpdateMasterVmResultParam()
    {
        string? template = null;
        if (MasterVmParamCombo.SelectedItem is ComboBoxItem paramItem)
            template = paramItem.Tag as string;

        int index = 0;
        if (MasterVmIndexCombo.SelectedItem is ComboBoxItem indexItem && indexItem.Tag is int idx)
            index = idx;

        if (template != null)
        {
            MasterVmParamBox.Text = template
                .Replace("{s}", index.ToString())
                .Replace("{b}", index.ToString());
        }
        else
        {
            MasterVmParamBox.Text = "";
        }
    }

    private void OnMasterVmChannelTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressMappingEvents) return;
        if (MasterVmChannelTypeCombo.SelectedItem is not ComboBoxItem item) return;
        var channelType = item.Tag as string ?? "Strip";

        _suppressMappingEvents = true;
        PopulateMasterVmGroups(channelType);
        if (MasterVmGroupCombo.Items.Count > 0)
        {
            var firstGroupName = (MasterVmGroupCombo.Items[0] as ComboBoxItem)?.Tag as string;
            PopulateMasterVmParams(channelType, firstGroupName ?? "");
        }
        UpdateMasterVmResultParam();
        _suppressMappingEvents = false;
    }

    private void OnMasterVmGroupChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressMappingEvents) return;
        if (MasterVmGroupCombo.SelectedItem is not ComboBoxItem groupItem) return;
        var groupName = groupItem.Tag as string ?? "";
        var channelType = "Strip";
        if (MasterVmChannelTypeCombo.SelectedItem is ComboBoxItem ctItem)
            channelType = ctItem.Tag as string ?? "Strip";

        _suppressMappingEvents = true;
        PopulateMasterVmParams(channelType, groupName);
        UpdateMasterVmResultParam();
        _suppressMappingEvents = false;
    }

    private void OnMasterVmParamComboChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressMappingEvents) return;
        UpdateMasterVmResultParam();
    }

    private void OnMasterVmIndexChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressMappingEvents) return;
        UpdateMasterVmResultParam();
    }

    private void OnMasterVmLedSourceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressMappingEvents) return;
        if (MasterActionTypeCombo.SelectedItem is not ComboBoxItem item) return;
        if (item.Tag is not MasterButtonActionType type) return;
        RefreshMasterLedFeedbackPanelVisibility(type);
    }

    private MasterVmLedSource GetSelectedMasterVmLedSource()
    {
        if (MasterVmLedSourceCombo.SelectedItem is ComboBoxItem item &&
            item.Tag is MasterVmLedSource source)
            return source;
        return MasterVmLedSource.ManualFeedback;
    }

    private void RefreshMasterLedFeedbackPanelVisibility(MasterButtonActionType type)
    {
        bool hideForVmStateLed = type == MasterButtonActionType.VmParameter &&
                                 GetSelectedMasterVmLedSource() == MasterVmLedSource.VoicemeeterState;
        bool show = type != MasterButtonActionType.None &&
                    type != MasterButtonActionType.SelectMqttDevice &&
                    !hideForVmStateLed;
        MasterLedFeedbackPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }


    private void OnButtonActionTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressMappingEvents) return;
        if (ButtonActionTypeCombo.SelectedItem is not ComboBoxItem item) return;
        if (item.Tag is not ButtonActionType actionType) return;
        UpdateButtonActionSubPanels(actionType);
    }

    private void OnButtonParamChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressMappingEvents || _config == null || _configService == null) return;
        if (_selectedVmChannel < 0 || _selectedControlType != "Button") return;
        if (ButtonActionTypeCombo.SelectedItem is not ComboBoxItem item ||
            item.Tag is not ButtonActionType actionType ||
            actionType != ButtonActionType.VmParameter)
            return;

        RefreshButtonVmRecHintVisibility(actionType);

        var selected = ButtonParamCombo.SelectedItem as ComboBoxItem;
        string paramName = (string)(selected?.Tag ?? "");

        EnsureMapping(_selectedVmChannel);
        var mapping = _config.Mappings[_selectedVmChannel];
        string btnKey = _selectedButtonType.ToString();

        if (!mapping.Buttons.TryGetValue(btnKey, out var btnMap) || btnMap == null)
            btnMap = new ButtonMappingConfig();

        btnMap.ActionType = ButtonActionType.VmParameter;
        btnMap.Parameter = paramName;
        mapping.Buttons[btnKey] = string.IsNullOrWhiteSpace(paramName) ? null : btnMap;

        SaveAndReload();
    }

    private void OnButtonMappingSave(object sender, RoutedEventArgs e)
    {
        // Persist either VM mapping or MQTT publish payload mapping for the selected button.
        if (_config == null || _configService == null) return;
        if (_selectedVmChannel < 0 || _selectedControlType != "Button") return;
        if (ButtonActionTypeCombo.SelectedItem is not ComboBoxItem item ||
            item.Tag is not ButtonActionType actionType)
            return;

        EnsureMapping(_selectedVmChannel);
        var mapping = _config.Mappings[_selectedVmChannel];
        string btnKey = _selectedButtonType.ToString();

        var btnMap = mapping.Buttons.TryGetValue(btnKey, out var existing) && existing != null
            ? existing
            : new ButtonMappingConfig();

        btnMap.ActionType = actionType;

        if (actionType == ButtonActionType.VmParameter)
        {
            var selected = ButtonParamCombo.SelectedItem as ComboBoxItem;
            btnMap.Parameter = (string)(selected?.Tag ?? "");
            btnMap.MqttPublish = null;
        }
        else
        {
            btnMap.Parameter = "";
            btnMap.MqttPublish = new MqttButtonPublishConfig
            {
                Topic = ButtonMqttTopicBox.Text.Trim(),
                PayloadPressed = ButtonMqttPayloadPressBox.Text ?? "",
                PayloadReleased = ButtonMqttPayloadReleaseBox.Text ?? "",
                Qos = int.TryParse(ButtonMqttQosCombo.SelectedItem?.ToString(), out int qos) ? Math.Clamp(qos, 0, 2) : 0,
                Retain = ButtonMqttRetainBox.IsChecked == true
            };
        }

        btnMap.MqttLedReceive = new MqttButtonLedReceiveConfig
        {
            Enabled = ButtonMqttLedEnabledBox.IsChecked == true,
            Topic = ButtonMqttLedTopicBox.Text.Trim(),
            PayloadOn = ButtonMqttLedOnPayloadBox.Text ?? "on",
            PayloadOff = ButtonMqttLedOffPayloadBox.Text ?? "off",
            PayloadBlink = ButtonMqttLedBlinkPayloadBox.Text ?? "blink",
            PayloadToggle = ButtonMqttLedTogglePayloadBox.Text ?? "toggle",
            IgnoreCase = true
        };

        mapping.Buttons[btnKey] = btnMap;
        SaveAndReload();
    }

    private async void OnButtonMqttTestPublish(object sender, RoutedEventArgs e)
    {
        // Sends the current pressed payload to validate broker/topic wiring without saving.
        if (_mqttClientService == null)
            return;

        var topic = ButtonMqttTopicBox.Text.Trim();
        var payload = ButtonMqttPayloadPressBox.Text ?? "";
        if (string.IsNullOrWhiteSpace(topic) || string.IsNullOrWhiteSpace(payload))
            return;

        await _mqttClientService.PublishAsync(
            topic,
            payload,
            int.TryParse(ButtonMqttQosCombo.SelectedItem?.ToString(), out int qos) ? Math.Clamp(qos, 0, 2) : 0,
            ButtonMqttRetainBox.IsChecked == true);
    }

    private async void OnButtonMqttTestLed(object sender, RoutedEventArgs e)
    {
        // Publishes one configured LED payload to validate topic/payload behavior.
        if (_mqttClientService == null)
            return;

        var topic = ButtonMqttLedTopicBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(topic))
            return;

        var mode = ButtonMqttLedTestModeCombo.SelectedItem?.ToString() ?? "On";
        var payload = mode switch
        {
            "Off" => ButtonMqttLedOffPayloadBox.Text,
            "Blink" => ButtonMqttLedBlinkPayloadBox.Text,
            "Toggle" => ButtonMqttLedTogglePayloadBox.Text,
            _ => ButtonMqttLedOnPayloadBox.Text
        };
        if (string.IsNullOrWhiteSpace(payload))
            return;

        await _mqttClientService.PublishAsync(topic, payload, 0, false);
    }

    private void OnButtonMappingClear(object sender, RoutedEventArgs e)
    {
        if (_config == null || _configService == null) return;
        if (_selectedVmChannel < 0 || _selectedControlType != "Button") return;

        EnsureMapping(_selectedVmChannel);
        string btnKey = _selectedButtonType.ToString();
        _config.Mappings[_selectedVmChannel].Buttons[btnKey] = null;

        _suppressMappingEvents = true;
        ButtonActionTypeCombo.SelectedIndex = 0;
        ButtonParamCombo.SelectedIndex = 0;
        ButtonMqttTopicBox.Text = "";
        ButtonMqttPayloadPressBox.Text = "on";
        ButtonMqttPayloadReleaseBox.Text = "";
        ButtonMqttQosCombo.SelectedItem = "0";
        ButtonMqttRetainBox.IsChecked = false;
        ButtonMqttLedEnabledBox.IsChecked = false;
        ButtonMqttLedTopicBox.Text = "";
        ButtonMqttLedOnPayloadBox.Text = "on";
        ButtonMqttLedOffPayloadBox.Text = "off";
        ButtonMqttLedBlinkPayloadBox.Text = "blink";
        ButtonMqttLedTogglePayloadBox.Text = "toggle";
        ButtonMqttLedTestModeCombo.SelectedItem = "On";
        UpdateButtonActionSubPanels(ButtonActionType.VmParameter);
        _suppressMappingEvents = false;

        SaveAndReload();
    }

    private void OnFaderParamChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressMappingEvents || _config == null || _configService == null) return;
        if (_selectedVmChannel < 0 || _selectedControlType != "Fader") return;

        var selected = FaderParamCombo.SelectedItem as ComboBoxItem;
        string paramName = (string)(selected?.Tag ?? "");

        if (string.IsNullOrEmpty(paramName))
        {
            EnsureMapping(_selectedVmChannel);
            _config.Mappings[_selectedVmChannel].Fader = null;
            SaveAndReload();
            return;
        }

        var template = VoicemeeterParameterCatalog.FindTemplate(paramName);
        if (template != null)
        {
            FaderMinBox.Text = template.DefaultMin.ToString();
            FaderMaxBox.Text = template.DefaultMax.ToString();
        }
    }

    private void OnFaderMappingSave(object sender, RoutedEventArgs e)
    {
        // Stores range and parameter binding for the selected strip fader.
        if (_config == null || _configService == null) return;
        if (_selectedVmChannel < 0 || _selectedControlType != "Fader") return;

        var selected = FaderParamCombo.SelectedItem as ComboBoxItem;
        string paramName = (string)(selected?.Tag ?? "");

        EnsureMapping(_selectedVmChannel);

        if (string.IsNullOrEmpty(paramName))
        {
            _config.Mappings[_selectedVmChannel].Fader = null;
        }
        else
        {
            if (!double.TryParse(FaderMinBox.Text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double min))
                min = -60;
            if (!double.TryParse(FaderMaxBox.Text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double max))
                max = 12;

            var tmpl = VoicemeeterParameterCatalog.FindTemplate(paramName);
            double step = tmpl?.DefaultStep ?? 0.1;

            _config.Mappings[_selectedVmChannel].Fader = new FaderMappingConfig
            {
                Parameter = paramName,
                Min = min,
                Max = max,
                Step = step
            };
        }

        SaveAndReload();
    }

    private void OnEncoderFunctionAdd(object sender, RoutedEventArgs e)
    {
        // Adds an encoder function using defaults from the parameter catalog.
        if (_config == null || _configService == null) return;
        if (_selectedVmChannel < 0 || _selectedControlType != "Encoder") return;

        var selected = EncoderAddParamCombo.SelectedItem as ComboBoxItem;
        string paramName = (string)(selected?.Tag ?? "");
        if (string.IsNullOrEmpty(paramName)) return;

        string label = EncoderAddLabelBox.Text.Trim();
        if (string.IsNullOrEmpty(label))
        {
            var resolved = VoicemeeterParameterCatalog.GetFloatParameters(_selectedVmChannel)
                .FirstOrDefault(p => p.Parameter == paramName);
            label = resolved?.DisplayName ?? "PARAM";
            if (label.Length > 7) label = label[..7];
        }
        else if (label.Length > 7)
        {
            label = label[..7];
        }

        var tmpl = VoicemeeterParameterCatalog.FindTemplate(paramName);

        EnsureMapping(_selectedVmChannel);
        _config.Mappings[_selectedVmChannel].EncoderFunctions.Add(new EncoderFunctionConfig
        {
            Label = label.ToUpperInvariant(),
            Parameter = paramName,
            Min = tmpl?.DefaultMin ?? 0,
            Max = tmpl?.DefaultMax ?? 1,
            Step = tmpl?.DefaultStep ?? 0.5,
            Unit = tmpl?.Unit ?? ""
        });

        RefreshEncoderFunctionList(_selectedVmChannel);
        EncoderAddLabelBox.Text = "";
        SaveAndReload();
    }

    private void OnEncoderFunctionRemove(object sender, RoutedEventArgs e)
    {
        if (_config == null || _configService == null) return;
        if (_selectedVmChannel < 0 || _selectedControlType != "Encoder") return;

        int idx = EncoderFunctionList.SelectedIndex;
        if (idx < 0) return;

        if (_config.Mappings.TryGetValue(_selectedVmChannel, out var mapping) &&
            idx < mapping.EncoderFunctions.Count)
        {
            mapping.EncoderFunctions.RemoveAt(idx);
            RefreshEncoderFunctionList(_selectedVmChannel);
            SaveAndReload();
        }
    }


    private void EnsureMapping(int vmChannel)
    {
        if (_config == null) return;
        if (!_config.Mappings.ContainsKey(vmChannel))
        {
            _config.Mappings[vmChannel] = new ControlMappingConfig();
        }
    }

    /// <summary>
    /// Persists the current in-memory config and triggers bridge mapping reload.
    /// </summary>
    private void SaveAndReload()
    {
        if (_config == null || _configService == null) return;
        _configService.Save(_config);
        _bridge?.ReloadMappings();
    }
}


