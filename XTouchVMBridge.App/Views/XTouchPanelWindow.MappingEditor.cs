using System.Windows;
using System.Windows.Controls;
using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Models;
using XTouchVMBridge.Voicemeeter.Services;

namespace XTouchVMBridge.App.Views;

/// <summary>
/// Mapping-Editor: Parameter-Zuweisungen für Buttons, Fader, Encoder und Master-Buttons.
/// Enthält alle ShowXxxMappingPanel-Methoden, ComboBox-EventHandler, VM-Parameter-Dropdown-Kaskade,
/// Save/Reload-Logik.
/// </summary>
public partial class XTouchPanelWindow
{
    /// <summary>Blendet alle Mapping-Sub-Panels aus.</summary>
    private void HideMappingSubPanels()
    {
        ButtonMappingPanel.Visibility = Visibility.Collapsed;
        FaderMappingPanel.Visibility = Visibility.Collapsed;
        EncoderMappingPanel.Visibility = Visibility.Collapsed;
        MasterButtonMappingPanel.Visibility = Visibility.Collapsed;
    }

    /// <summary>Zeigt das Button-Mapping-Panel für einen Kanal und Button-Typ.</summary>
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

            // ComboBox mit Bool-Parametern befüllen
            var boolParams = VoicemeeterParameterCatalog.GetBoolParameters(vmCh);
            ButtonParamCombo.Items.Clear();
            ButtonParamCombo.Items.Add(new ComboBoxItem { Content = "(nicht zugewiesen)", Tag = "" });

            foreach (var p in boolParams)
            {
                ButtonParamCombo.Items.Add(new ComboBoxItem
                {
                    Content = $"{p.DisplayName}  [{p.Parameter}]",
                    Tag = p.Parameter
                });
            }

            // Aktuellen Wert auswählen
            string? currentParam = null;
            if (_config.Mappings.TryGetValue(vmCh, out var mapping))
            {
                string btnKey = buttonType.ToString();
                if (mapping.Buttons.TryGetValue(btnKey, out var btnMap) && btnMap != null)
                    currentParam = btnMap.Parameter;
            }

            ButtonParamCombo.SelectedIndex = 0; // Default: nicht zugewiesen
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

            ButtonMappingPanel.Visibility = Visibility.Visible;
            MappingPanelHeader.Text = "VM-Parameter Zuweisung";
            MappingPanel.Visibility = Visibility.Visible;
        }
        finally
        {
            _suppressMappingEvents = false;
        }
    }

    /// <summary>Zeigt das Fader-Mapping-Panel für einen Kanal.</summary>
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

            // ComboBox mit Float-Parametern befüllen
            var floatParams = VoicemeeterParameterCatalog.GetFloatParameters(vmCh);
            FaderParamCombo.Items.Clear();
            FaderParamCombo.Items.Add(new ComboBoxItem { Content = "(nicht zugewiesen)", Tag = "" });

            foreach (var p in floatParams)
            {
                FaderParamCombo.Items.Add(new ComboBoxItem
                {
                    Content = $"{p.DisplayName}  [{p.Parameter}]",
                    Tag = p.Parameter
                });
            }

            // Aktuellen Wert auswählen
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
        }
        finally
        {
            _suppressMappingEvents = false;
        }
    }

    /// <summary>Zeigt das Encoder-Mapping-Panel für einen Kanal.</summary>
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

            // Funktionsliste befüllen
            RefreshEncoderFunctionList(vmCh);

            // ComboBox mit Float-Parametern befüllen (für "Hinzufügen")
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
        }
        finally
        {
            _suppressMappingEvents = false;
        }
    }

    /// <summary>Aktualisiert die Encoder-Funktionsliste im Panel.</summary>
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

    // ═══════════════════════════════════════════════════════════════════
    //  Master-Button-Mapping-Editor
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Zeigt das Master-Button-Mapping-Panel für eine MIDI-Note.</summary>
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

            // ActionType-ComboBox befüllen
            MasterActionTypeCombo.Items.Clear();
            MasterActionTypeCombo.Items.Add(new ComboBoxItem { Content = "(keine Aktion)", Tag = MasterButtonActionType.None });
            MasterActionTypeCombo.Items.Add(new ComboBoxItem { Content = "VM-Parameter toggeln", Tag = MasterButtonActionType.VmParameter });
            MasterActionTypeCombo.Items.Add(new ComboBoxItem { Content = "Programm starten", Tag = MasterButtonActionType.LaunchProgram });
            MasterActionTypeCombo.Items.Add(new ComboBoxItem { Content = "Tastenkombination senden", Tag = MasterButtonActionType.SendKeys });
            MasterActionTypeCombo.Items.Add(new ComboBoxItem { Content = "Text senden", Tag = MasterButtonActionType.SendText });

            // Aktuelle Config laden
            MasterButtonActionConfig? actionConfig = null;
            _config.MasterButtonActions.TryGetValue(noteNumber, out actionConfig);

            var activeType = actionConfig?.ActionType ?? MasterButtonActionType.None;

            // ComboBox auf aktuellen Typ setzen
            for (int i = 0; i < MasterActionTypeCombo.Items.Count; i++)
            {
                if (MasterActionTypeCombo.Items[i] is ComboBoxItem item &&
                    item.Tag is MasterButtonActionType type && type == activeType)
                {
                    MasterActionTypeCombo.SelectedIndex = i;
                    break;
                }
            }

            // Felder befüllen
            MasterProgramPathBox.Text = actionConfig?.ProgramPath ?? "";
            MasterProgramArgsBox.Text = actionConfig?.ProgramArgs ?? "";
            MasterKeyCombinationBox.Text = actionConfig?.KeyCombination ?? "";
            MasterTextBox.Text = actionConfig?.Text ?? "";

            // VM-Parameter-Dropdowns initialisieren
            InitMasterVmParamDropdowns(actionConfig?.VmParameter);

            // Sub-Panel für aktiven Typ anzeigen
            UpdateMasterActionSubPanels(activeType);

            MasterButtonMappingPanel.Visibility = Visibility.Visible;
            MappingPanelHeader.Text = "Master-Button Aktion";
            MappingPanel.Visibility = Visibility.Visible;
        }
        finally
        {
            _suppressMappingEvents = false;
        }
    }

    /// <summary>Zeigt/versteckt die Sub-Panels je nach Aktionstyp.</summary>
    private void UpdateMasterActionSubPanels(MasterButtonActionType type)
    {
        MasterVmParamPanel.Visibility = type == MasterButtonActionType.VmParameter ? Visibility.Visible : Visibility.Collapsed;
        MasterLaunchPanel.Visibility = type == MasterButtonActionType.LaunchProgram ? Visibility.Visible : Visibility.Collapsed;
        MasterSendKeysPanel.Visibility = type == MasterButtonActionType.SendKeys ? Visibility.Visible : Visibility.Collapsed;
        MasterSendTextPanel.Visibility = type == MasterButtonActionType.SendText ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Aktionstyp-ComboBox geändert.</summary>
    private void OnMasterActionTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressMappingEvents) return;
        if (MasterActionTypeCombo.SelectedItem is not ComboBoxItem item) return;
        if (item.Tag is not MasterButtonActionType type) return;

        UpdateMasterActionSubPanels(type);
    }

    /// <summary>Programm-Pfad per Datei-Dialog auswählen.</summary>
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

    /// <summary>Master-Button-Aktion speichern.</summary>
    private void OnMasterActionSave(object sender, RoutedEventArgs e)
    {
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
            _config.MasterButtonActions[_selectedMasterButtonNote] = new MasterButtonActionConfig
            {
                ActionType = selectedType,
                VmParameter = selectedType == MasterButtonActionType.VmParameter ? MasterVmParamBox.Text.Trim() : null,
                ProgramPath = selectedType == MasterButtonActionType.LaunchProgram ? MasterProgramPathBox.Text.Trim() : null,
                ProgramArgs = selectedType == MasterButtonActionType.LaunchProgram ? MasterProgramArgsBox.Text.Trim() : null,
                KeyCombination = selectedType == MasterButtonActionType.SendKeys ? MasterKeyCombinationBox.Text.Trim() : null,
                Text = selectedType == MasterButtonActionType.SendText ? MasterTextBox.Text : null
            };
        }

        SaveAndReload();
    }

    /// <summary>Master-Button-Aktion entfernen.</summary>
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
        UpdateMasterActionSubPanels(MasterButtonActionType.None);
        _suppressMappingEvents = false;

        SaveAndReload();
    }

    // ─── VM-Parameter Dropdown-Kaskade ──────────────────────────────

    /// <summary>Initialisiert die VM-Parameter-Dropdowns beim Anzeigen des Panels.</summary>
    private void InitMasterVmParamDropdowns(string? currentParam)
    {
        _suppressMappingEvents = true;
        try
        {
            // Kanaltyp-Dropdown befüllen
            MasterVmChannelTypeCombo.Items.Clear();
            MasterVmChannelTypeCombo.Items.Add(new ComboBoxItem { Content = "Strip", Tag = "Strip" });
            MasterVmChannelTypeCombo.Items.Add(new ComboBoxItem { Content = "Bus", Tag = "Bus" });

            // Versuche aktuellen Parameter zu parsen um Dropdowns vorzubelegen
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

                // Finde passende Gruppe und Template
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

            // Kanaltyp setzen
            MasterVmChannelTypeCombo.SelectedIndex = preselectedType == "Bus" ? 1 : 0;

            // Gruppen befüllen
            PopulateMasterVmGroups(preselectedType, preselectedGroup);

            // Parameter befüllen (falls Gruppe gefunden)
            if (preselectedGroup != null)
                PopulateMasterVmParams(preselectedType, preselectedGroup, preselectedTemplate);

            // Index-Dropdown befüllen
            PopulateMasterVmIndex(preselectedIndex);

            // Ergebnis aktualisieren
            UpdateMasterVmResultParam();
        }
        finally
        {
            _suppressMappingEvents = false;
        }
    }

    /// <summary>Befüllt das Gruppen-Dropdown für den gewählten Kanaltyp.</summary>
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

    /// <summary>Befüllt das Parameter-Dropdown für die gewählte Gruppe.</summary>
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

    /// <summary>Befüllt das Index-Dropdown (0–7).</summary>
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

    /// <summary>Aktualisiert das Ergebnis-TextBox basierend auf den Dropdown-Auswahlen.</summary>
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

    /// <summary>Kanaltyp-Dropdown geändert → Gruppen neu laden.</summary>
    private void OnMasterVmChannelTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressMappingEvents) return;
        if (MasterVmChannelTypeCombo.SelectedItem is not ComboBoxItem item) return;
        var channelType = item.Tag as string ?? "Strip";

        _suppressMappingEvents = true;
        PopulateMasterVmGroups(channelType);
        // Ersten Gruppeneintrag auswählen → löst Parameter-Laden aus
        if (MasterVmGroupCombo.Items.Count > 0)
        {
            var firstGroupName = (MasterVmGroupCombo.Items[0] as ComboBoxItem)?.Tag as string;
            PopulateMasterVmParams(channelType, firstGroupName ?? "");
        }
        UpdateMasterVmResultParam();
        _suppressMappingEvents = false;
    }

    /// <summary>Gruppen-Dropdown geändert → Parameter neu laden.</summary>
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

    /// <summary>Parameter-Dropdown geändert → Ergebnis aktualisieren.</summary>
    private void OnMasterVmParamComboChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressMappingEvents) return;
        UpdateMasterVmResultParam();
    }

    /// <summary>Index-Dropdown geändert → Ergebnis aktualisieren.</summary>
    private void OnMasterVmIndexChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressMappingEvents) return;
        UpdateMasterVmResultParam();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Mapping-Editor: Event-Handler (aus XAML referenziert)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Button-Parameter geändert (ComboBox SelectionChanged).</summary>
    private void OnButtonParamChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressMappingEvents || _config == null || _configService == null) return;
        if (_selectedVmChannel < 0 || _selectedControlType != "Button") return;

        var selected = ButtonParamCombo.SelectedItem as ComboBoxItem;
        string paramName = (string)(selected?.Tag ?? "");

        EnsureMapping(_selectedVmChannel);
        var mapping = _config.Mappings[_selectedVmChannel];
        string btnKey = _selectedButtonType.ToString();

        if (string.IsNullOrEmpty(paramName))
        {
            mapping.Buttons[btnKey] = null;
        }
        else
        {
            mapping.Buttons[btnKey] = new ButtonMappingConfig { Parameter = paramName };
        }

        SaveAndReload();
    }

    /// <summary>Button-Zuweisung entfernen (Clear-Button Click).</summary>
    private void OnButtonMappingClear(object sender, RoutedEventArgs e)
    {
        if (_config == null || _configService == null) return;
        if (_selectedVmChannel < 0 || _selectedControlType != "Button") return;

        EnsureMapping(_selectedVmChannel);
        string btnKey = _selectedButtonType.ToString();
        _config.Mappings[_selectedVmChannel].Buttons[btnKey] = null;

        _suppressMappingEvents = true;
        ButtonParamCombo.SelectedIndex = 0;
        _suppressMappingEvents = false;

        SaveAndReload();
    }

    /// <summary>Fader-Parameter geändert (ComboBox SelectionChanged).</summary>
    private void OnFaderParamChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressMappingEvents || _config == null || _configService == null) return;
        if (_selectedVmChannel < 0 || _selectedControlType != "Fader") return;

        var selected = FaderParamCombo.SelectedItem as ComboBoxItem;
        string paramName = (string)(selected?.Tag ?? "");

        if (string.IsNullOrEmpty(paramName))
        {
            // Sofort speichern: Fader-Zuweisung entfernen
            EnsureMapping(_selectedVmChannel);
            _config.Mappings[_selectedVmChannel].Fader = null;
            SaveAndReload();
            return;
        }

        // Min/Max aus Katalog vorausfüllen
        var template = VoicemeeterParameterCatalog.FindTemplate(paramName);
        if (template != null)
        {
            FaderMinBox.Text = template.DefaultMin.ToString();
            FaderMaxBox.Text = template.DefaultMax.ToString();
        }
    }

    /// <summary>Fader-Mapping speichern (Speichern-Button Click).</summary>
    private void OnFaderMappingSave(object sender, RoutedEventArgs e)
    {
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

    /// <summary>Encoder-Funktion hinzufügen (+-Button Click).</summary>
    private void OnEncoderFunctionAdd(object sender, RoutedEventArgs e)
    {
        if (_config == null || _configService == null) return;
        if (_selectedVmChannel < 0 || _selectedControlType != "Encoder") return;

        var selected = EncoderAddParamCombo.SelectedItem as ComboBoxItem;
        string paramName = (string)(selected?.Tag ?? "");
        if (string.IsNullOrEmpty(paramName)) return;

        string label = EncoderAddLabelBox.Text.Trim();
        if (string.IsNullOrEmpty(label))
        {
            // Label aus DisplayName ableiten
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

    /// <summary>Encoder-Funktion entfernen (−-Button Click).</summary>
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

    // ─── Mapping Helpers ─────────────────────────────────────────────

    /// <summary>Stellt sicher, dass ein Mapping für den VM-Kanal existiert.</summary>
    private void EnsureMapping(int vmChannel)
    {
        if (_config == null) return;
        if (!_config.Mappings.ContainsKey(vmChannel))
        {
            _config.Mappings[vmChannel] = new ControlMappingConfig();
        }
    }

    /// <summary>Speichert die Config und benachrichtigt die Bridge.</summary>
    private void SaveAndReload()
    {
        if (_config == null || _configService == null) return;
        _configService.Save(_config);
        _bridge?.ReloadMappings();
    }
}
