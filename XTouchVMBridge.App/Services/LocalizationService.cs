using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace XTouchVMBridge.App.Services;

public static class LocalizationService
{
    private static string _language = "de";

    private static readonly Dictionary<string, string> DeToEn = new(StringComparer.Ordinal)
    {
        ["Verbunden"] = "Connected",
        ["Getrennt"] = "Disconnected",
        ["Log anzeigen"] = "Show log",
        ["Mit Windows starten"] = "Start with Windows",
        ["Voicemeeter neustarten"] = "Restart Voicemeeter",
        ["XTouchVMBridge neustarten"] = "Restart XTouchVMBridge",
        ["Beenden"] = "Exit",
        ["X-Touch Gerät"] = "X-Touch device",
        ["Kein X-Touch gefunden"] = "No X-Touch found",
        ["Sprache"] = "Language",
        ["Deutsch"] = "German",
        ["Englisch"] = "English",
        ["Log Monitor"] = "Log monitor",
        ["Log Level:"] = "Log level:",
        ["Im Explorer öffnen"] = "Open in Explorer",
        ["Lade Log mit Filter..."] = "Loading log with filter...",
        ["Lade Log-Datei:"] = "Loading log file:",
        ["Keine Log-Datei gefunden."] = "No log file found.",
        ["Erwartet unter:"] = "Expected at:",
        ["Fehler beim Laden:"] = "Load error:",
        ["MIDI Debug Monitor"] = "MIDI debug monitor",
        ["Eingehend (IN)"] = "Incoming (IN)",
        ["Ausgehend (OUT)"] = "Outgoing (OUT)",
        [" Filter:"] = " Filter:",
        ["Alle"] = "All",
        [" Kanal:"] = " Channel:",
        ["Zeit"] = "Time",
        ["Wert"] = "Value",
        ["Aktion"] = "Action",
        ["Nachrichten-Liste"] = "Message list",
        ["Status-Leiste"] = "Status bar",
        ["Warte auf MIDI-Gerät..."] = "Waiting for MIDI device...",
        ["0 Nachrichten"] = "0 messages",
        ["Leeren"] = "Clear",
        ["Verbunden — empfange MIDI"] = "Connected — receiving MIDI",
        ["Gerät nicht verbunden"] = "Device not connected",
        ["Kein MIDI-Gerät zugewiesen"] = "No MIDI device assigned",
        ["Nachrichten"] = "messages",
        ["sichtbar"] = "visible",
        ["X-Touch — Live Panel"] = "X-Touch — Live Panel",
        ["Klick: Channel Views bearbeiten\nWechsel über Fader Bank ◄ ►"] = "Click: edit channel views\nSwitch via Fader Bank ◄ ►",
        ["MQTT Server konfigurieren"] = "Configure MQTT server",
        ["Test LED"] = "Test LED",
        ["Speichern"] = "Save",
        ["Entfernen"] = "Remove",
        ["Parameter:"] = "Parameter:",
        ["Min:"] = "Min:",
        ["Max:"] = "Max:",
        ["Funktionen (Drücken = nächste):"] = "Functions (press = next):",
        ["Label (max 7 Zeichen)"] = "Label (max 7 chars)",
        ["Funktion hinzufügen"] = "Add function",
        ["Ausgewählte Funktion entfernen"] = "Remove selected function",
        ["Aktionstyp:"] = "Action type:",
        ["LED-Feedback:"] = "LED feedback:",
        ["Kanaltyp:"] = "Channel type:",
        ["Gruppe:"] = "Group:",
        ["Ergebnis:"] = "Result:",
        ["Strip/Bus-Index:"] = "Strip/Bus index:",
        ["LED-Zustand:"] = "LED state:",
        ["Programmpfad:"] = "Program path:",
        ["Programm auswählen"] = "Select program",
        ["Argumente:"] = "Arguments:",
        ["Tastenkombination:"] = "Key combination:",
        ["Text der in die Zwischenablage kopiert und eingefügt wird"] = "Text copied to clipboard and pasted",
        ["Macro-Button Index (0–79):"] = "Macro button index (0–79):",
        ["Eindeutige Geraete-ID (z.B. tv, player2)"] = "Unique device ID (e.g. tv, player2)",
        ["Topic fuer Transport-Befehle dieses Geraets"] = "Topic for transport commands of this device",
        ["Payload Override (optional):"] = "Payload override (optional):",
        ["Leer = Command-Text als Payload"] = "Empty = command text as payload",
        ["MQTT Topic:"] = "MQTT topic:",
        ["Payload Press:"] = "Payload press:",
        ["Payload Release (optional):"] = "Payload release (optional):",
        ["LED per MQTT steuern"] = "Control LED via MQTT",
        ["LED Topic:"] = "LED topic:",
        ["Payload On / Off / Blink / Toggle:"] = "Payload On / Off / Blink / Toggle:",
        ["Button-Mapping speichern"] = "Save button mapping",
        ["Zuweisung entfernen"] = "Remove assignment",
        ["Aktion speichern"] = "Save action",
        ["Aktion entfernen"] = "Remove action",
        ["MQTT Einstellungen"] = "MQTT settings",
        ["Nicht verbunden"] = "Not connected",
        ["Teste Verbindung..."] = "Testing connection...",
        ["Test erfolgreich"] = "Test successful",
        ["Test fehlgeschlagen"] = "Test failed",
        ["Verbindung erfolgreich."] = "Connection successful.",
        ["Fehler:"] = "Error:",
        ["MQTT Service nicht verfuegbar."] = "MQTT service not available.",
        ["Mindestens eine View muss vorhanden sein."] = "At least one view must exist.",
        ["Channel Views gespeichert."] = "Channel views saved.",
        ["Sprache wird nach Neustart aktiv."] = "Language will be applied after restart."
        ,
        ["Log-Datei wurde zurückgesetzt"] = "Log file was reset",
        ["Zeilen geladen"] = "lines loaded",
        ["Fehler beim Öffnen:"] = "Open error:",
        ["(Standard)"] = "(Default)",
        ["(Keine)"] = "(None)",
        ["Aktion"] = "Action",
        ["Aktionstyp"] = "Action type",
        ["Zuweisung"] = "Assignment",
        ["Hinweis"] = "Hint",
        ["Programmpfad"] = "Program path",
        ["Argumente"] = "Arguments",
        ["Tastenkombination"] = "Key combination",
        ["Ergebnis"] = "Result",
        ["LED-Zustand"] = "LED state",
        ["Kanaltyp"] = "Channel type",
        ["Gruppe"] = "Group",
        ["Payload Press"] = "Payload press",
        ["Payload Release (optional)"] = "Payload release (optional)",
        ["LED per MQTT steuern"] = "Control LED via MQTT",
        ["Button-Mapping speichern"] = "Save button mapping",
        ["Zuweisung entfernen"] = "Remove assignment",
        ["Aktion speichern"] = "Save action",
        ["Aktion entfernen"] = "Remove action",
        ["Resultierender VM-Parameter (z.B. Strip[0].Mute)"] = "Resulting VM parameter (e.g. Strip[0].Mute)",
        ["Pfad zum Programm (z.B. notepad.exe)"] = "Path to program (e.g. notepad.exe)",
        ["Optionale Programmargumente"] = "Optional program arguments",
        ["Klicke auf ein Control um Details zu sehen"] = "Click a control to view details",
        ["VM-Parameter Zuweisung"] = "VM parameter mapping",
        ["Kanal"] = "Channel",
        ["Obere Zeile"] = "Top row",
        ["Untere Zeile"] = "Bottom row",
        ["Farbe"] = "Color",
        ["Funktion"] = "Function",
        ["Hersteller"] = "Vendor",
        ["Aktive Funktion"] = "Active function",
        ["Drücken"] = "Press",
        ["Drehen"] = "Rotate",
        ["Funktionsliste"] = "Function list",
        ["Keine Funktionsliste zugewiesen."] = "No function list assigned.",
        ["Steuert den im Encoder Assign gewählten Parameter."] = "Controls the parameter selected in encoder assign.",
        ["Ring-Position"] = "Ring position",
        ["Ring-Modus"] = "Ring mode",
        ["Center-LED"] = "Center LED",
        ["Gedrückt"] = "Pressed",
        ["Ja"] = "Yes",
        ["Nein"] = "No",
        ["MIDI Drehen"] = "MIDI rotate",
        ["MIDI Drücken"] = "MIDI press",
        ["MIDI Ring"] = "MIDI ring",
        ["Toggle Mute auf VM-Kanal"] = "Toggle mute on VM channel",
        ["Toggle Solo auf VM-Kanal (nur Input-Strips)"] = "Toggle solo on VM channel (input strips only)",
        ["Kanal auswählen"] = "Select channel",
        ["Nicht zugewiesen"] = "Not assigned",
        ["LED-Status"] = "LED status",
        ["MIDI Input"] = "MIDI input",
        ["MIDI LED"] = "MIDI LED",
        ["Note-Formel"] = "Note formula",
        ["Position"] = "Position",
        ["dB-Wert"] = "dB value",
        ["Berührt"] = "Touched",
        ["Setzt Gain auf VM-Kanal. Werte unter -60 dB → -inf."] = "Sets gain on VM channel. Values below -60 dB -> -inf.",
        ["Bei Touch: dB-Wert im Display anzeigen."] = "On touch: show dB value on display.",
        ["Bei Release: Ansichtsname wiederherstellen (nach 2s)."] = "On release: restore view name (after 2s).",
        ["MIDI Position"] = "MIDI position",
        ["MIDI Touch"] = "MIDI touch",
        ["nur im MIDI-Mode"] = "MIDI mode only",
        ["Stufen"] = "Levels",
        ["Stille"] = "Silence",
        ["Laut"] = "Loud",
        ["Zeigt den Post-Fader-Pegel des gemappten VM-Kanals."] = "Shows post-fader level of the mapped VM channel.",
        ["Wird alle 100ms per Polling aktualisiert."] = "Updated every 100ms via polling.",
        ["dB-Skala"] = "dB scale",
        ["Main Fader"] = "Main fader",
        ["Steuert den Master-Bus-Pegel."] = "Controls master bus level.",
        ["VM-Parameter toggeln"] = "Toggle VM parameter",
        ["(nicht zugewiesen)"] = "(not assigned)",
        ["Aufnahme Start/Stop (Dateiname: Kanal + Zeit)"] = "Recording start/stop (filename: channel + time)",
        ["(keine Aktion)"] = "(no action)",
        ["Programm starten"] = "Launch program",
        ["Tastenkombination senden"] = "Send key combination",
        ["VM Audio Engine neu starten"] = "Restart VM audio engine",
        ["VM-Fenster anzeigen"] = "Show VM window",
        ["Macro-Button auslösen"] = "Trigger macro button",
        ["MQTT Geraet auswaehlen"] = "Select MQTT device",
        ["Ausführbare Dateien (*.exe)|*.exe|Alle Dateien (*.*)|*.*"] = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
        ["Wählt den Parameter für alle 8 Encoder."] = "Selects parameter for all 8 encoders.",
        ["Drehen der Encoder ändert den gewählten Parameter pro Kanal."] = "Rotating encoders changes selected parameter per channel.",
        ["Zeigt Kanalnamen im Display"] = "Shows channel names on display",
        ["Zeigt Parameterwert im Display"] = "Shows parameter value on display",
        ["Aktiviert/deaktiviert den Global-View-Modus."] = "Enables/disables global view mode.",
        ["Zeigt alle Kanäle unabhängig vom Typ."] = "Shows all channels regardless of type.",
        ["Globale Ansicht"] = "Global view",
        ["Filtert die Kanalstreifen nach Typ."] = "Filters channel strips by type.",
        ["benutzerdefinierbar"] = "user-definable",
        ["Zuweisung hängt von der DAW/Anwendung ab."] = "Assignment depends on DAW/application.",
        ["Modifier-Taste"] = "Modifier key",
        ["Kombiniert mit anderen Tasten für erweiterte Funktionen."] = "Combined with other keys for advanced functions.",
        ["Automation-Modus"] = "Automation mode",
        ["Steuert den Automation-Modus der DAW."] = "Controls DAW automation mode.",
        ["Utility-Taste"] = "Utility key",
        ["Wird von der DAW zugewiesen."] = "Assigned by DAW.",
        ["Transport-Taste"] = "Transport key",
        ["Steuert die DAW-Transport-Funktionen."] = "Controls DAW transport functions.",
        ["Fader Bank Links"] = "Fader bank left",
        ["Fader Bank Rechts"] = "Fader bank right",
        ["Frei zuweisbar."] = "Freely assignable.",
        ["Channel View Cycling erfolgt über FLIP-Button"] = "Channel view cycling is handled by FLIP button",
        ["Cursor hoch"] = "Cursor up",
        ["Cursor runter"] = "Cursor down",
        ["Navigation in Listen/Menüs"] = "Navigation in lists/menus",
        ["Auswahl bestätigen / Zoom toggle"] = "Confirm selection / toggle zoom",
        ["SCRUB-Taste aktiviert den Scrub-Modus für das Jog Wheel."] = "SCRUB key enables scrub mode for jog wheel.",
        ["Im Scrub-Modus: Frame-genaue Audio-Wiedergabe beim Drehen."] = "In scrub mode: frame-accurate audio playback while rotating."
    };

    public static string CurrentLanguage => _language;
    public static bool IsEnglish => string.Equals(_language, "en", StringComparison.OrdinalIgnoreCase);

    public static void SetLanguage(string? language)
    {
        _language = NormalizeLanguage(language);
        var cultureName = IsEnglish ? "en-US" : "de-DE";
        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    public static string T(string german, string english)
    {
        return IsEnglish ? english : german;
    }

    public static string Translate(string text)
    {
        if (!IsEnglish || string.IsNullOrWhiteSpace(text))
            return text;
        if (DeToEn.TryGetValue(text, out var translated))
            return translated;

        var result = text;
        foreach (var pair in DeToEn.OrderByDescending(p => p.Key.Length))
            result = result.Replace(pair.Key, pair.Value, StringComparison.Ordinal);
        return result;
    }

    public static void LocalizeWindow(Window window)
    {
        if (window == null)
            return;

        window.Title = Translate(window.Title);
        TranslateObject(window, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    private static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return "de";

        return language.Trim().ToLowerInvariant().StartsWith("en")
            ? "en"
            : "de";
    }

    private static void TranslateObject(object? obj, HashSet<object> visited)
    {
        if (obj == null)
            return;
        if (!visited.Add(obj))
            return;

        switch (obj)
        {
            case TextBlock textBlock:
                textBlock.Text = Translate(textBlock.Text);
                break;
            case System.Windows.Controls.Button button when button.Content is string s:
                button.Content = Translate(s);
                break;
            case System.Windows.Controls.CheckBox checkBox when checkBox.Content is string s:
                checkBox.Content = Translate(s);
                break;
            case System.Windows.Controls.Label label when label.Content is string s:
                label.Content = Translate(s);
                break;
            case MenuItem menuItem:
                if (menuItem.Header is string header)
                    menuItem.Header = Translate(header);
                break;
            case ComboBoxItem comboBoxItem when comboBoxItem.Content is string s:
                comboBoxItem.Content = Translate(s);
                break;
            case HeaderedContentControl headered when headered.Header is string headerText:
                headered.Header = Translate(headerText);
                break;
        }

        if (obj is FrameworkElement element && element.ToolTip is string tip)
            element.ToolTip = Translate(tip);

        if (obj is ItemsControl itemsControl)
        {
            foreach (var item in itemsControl.Items)
                TranslateObject(item, visited);
        }

        if (obj is DependencyObject dependencyObject)
        {
            foreach (var logicalChild in LogicalTreeHelper.GetChildren(dependencyObject))
                TranslateObject(logicalChild, visited);

            if (dependencyObject is Visual || dependencyObject is Visual3D)
            {
                var visualChildren = VisualTreeHelper.GetChildrenCount(dependencyObject);
                for (int i = 0; i < visualChildren; i++)
                    TranslateObject(VisualTreeHelper.GetChild(dependencyObject, i), visited);
            }
        }
    }
}




