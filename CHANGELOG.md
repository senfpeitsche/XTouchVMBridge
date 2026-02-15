# Changelog

## [Unreleased] - 2026-02-15

### MQTT: Mapping-Editor und Runtime erweitert
- **Neu**: Button-Mapping kann jetzt zwischen `VM-Parameter` und `MQTT Publish` umschalten
- **Neu**: MQTT-LED-Empfang fuer Channel-Buttons (`On/Off/Blink/Toggle` per Topic+Payload)
- **Neu**: Test-Funktionen im Button-Mapping:
  - `Test Publish` fuer den aktuell konfigurierten MQTT-Publish
  - `Test LED` fuer den aktuell konfigurierten MQTT-LED-Topic/Payload

### Master-Buttons: MQTT erweitert
- **Neu**: Master-Aktionstyp `MqttPublish` (Press/Release Payload, QoS, Retain)
- **Neu**: `LED per MQTT steuern` auch in der Master-Section fuer `MqttPublish`
- **Neu**: Master-Aktionstyp `SelectMqttDevice`:
  - waehlt ein aktives MQTT-Zielgeraet (`DeviceId` + `CommandTopic`)
  - Selector-LEDs zeigen aktive Auswahl (genau ein aktiv)
- **Neu**: Master-Aktionstyp `MqttTransport`:
  - sendet `play_pause/play/pause/stop/next/prev` an das aktive Zielgeraet
  - optionales Payload-Override, QoS, Retain
- **Neu**: Transport-Presets im Editor je nach Transport-Button:
  - Rewind -> `prev`, Forward -> `next`, Stop -> `stop`, Play -> `play_pause`, Record -> `pause`

### MQTT-Subscription
- **Neu**: MQTT-Client abonniert automatisch LED-Topics aus:
  - Channel-Button `MqttLedReceive`
  - Master-Button `MqttLed*`

## [Unreleased] - 2025-02-09

### Master-Button-Aktionen: Neue Aktionstypen
- **Neu**: VM Audio Engine neu starten (`RestartAudioEngine`)
- **Neu**: VM-Fenster anzeigen (`ShowVoicemeeter`) — bringt Voicemeeter in den Vordergrund via `Command.Show`
- **Neu**: VM-GUI sperren/entsperren (`LockGui`) — toggelt `Command.Lock`
- **Neu**: Voicemeeter Macro-Button auslösen (`TriggerMacroButton`) — triggert Macro-Buttons 0–79 via `MacroButton_SetStatus`
- **Neu**: Macro-Button Index-Eingabefeld (0–79) im Mapping-Editor

### LED-Feedback für Master-Buttons
- **Neu**: Konfigurierbarer LED-Feedback-Modus pro Master-Button-Aktion
  - **Blink** (Standard): LED blinkt 150ms auf als Bestätigung
  - **Toggle**: LED wechselt bei jedem Druck zwischen An und Aus (1. Druck = an, 2. Druck = aus)
  - **Blinking** (Dauerhaft): LED blinkt dauerhaft via Hardware-Blink (Mackie Protocol Velocity 2), erneutes Drücken stoppt das Blinken
- **Neu**: LED-Feedback-ComboBox im Mapping-Editor (sichtbar bei aktiver Aktion)
- **Neu**: LED-Feedback wird in `config.json` gespeichert (`"ledFeedback": "Blink"` / `"Toggle"` / `"Blinking"`)

### 7-Segment-Display: Cycle-Button geändert
- **Änderung**: Standard-Cycle-Button von SMPTE (Note 113) auf NAME/VALUE (Note 52) geändert
- **Verbesserung**: LED-Feedback für den Cycle-Button (LED an wenn nicht im Time-Modus)
- **Fix**: Thread-Safety mit `volatile` für Display-Mode-Felder

### Voicemeeter API-Erweiterungen
- **Neu**: `IVoicemeeterService.ShowVoicemeeter()` — `Command.Show = 1`
- **Neu**: `IVoicemeeterService.LockGui(bool)` — `Command.Lock` setzen/aufheben
- **Neu**: `IVoicemeeterService.TriggerMacroButton(int)` — `MacroButton_SetStatus(index, 1, mode=2)`

### Code-Qualität: File-Splitting in Partial Classes
- **Refactoring**: Große Dateien in Partial Classes aufgeteilt:
  - `XTouchPanelWindow` → `.MasterSection.cs`, `.ChannelStrips.cs`, `.MainFader.cs`, `.Templates.cs`, `.DetailPanels.cs`, `.EncoderInteraction.cs`, `.MappingEditor.cs`
  - `VoicemeeterBridge` → `.Sync.cs`, `.Callbacks.cs`
  - `XTouchDevice` → `.Input.cs`, `.Output.cs`
  - `MidiMessageDecoder` → `.SysEx.cs`

## [Previous] - 2025-02-06

### X-Touch Panel: Encoder-Steuerung per Maus
- **Neu**: Strg+Klick auf Encoder cycled durch die zugewiesenen Funktionen (identisch mit Hardware-Drücken)
- **Neu**: Mausrad auf Encoder ändert den Wert der aktiven Funktion (±1 Step pro Notch)
- **Neu**: Strg+Mausrad auf Encoder für grobe Steuerung (±5 Steps pro Notch)
- **Neu**: ToolTip auf Encoder-Knobs zeigt Bedienungshinweise

### X-Touch Panel: Button-LED-Toggle für alle Buttons
- **Neu**: Strg+Klick auf nicht-zugewiesene Kanal-Buttons (REC/SELECT) toggelt die LED direkt (On/Off)
- **Neu**: Strg+Klick auf Master-Buttons toggelt die LED (On/Off) wenn keine Aktion konfiguriert ist
- **Fix**: LED-Toggle funktioniert jetzt zuverlässig (eigener State-Speicher im PanelView)
- **Fix**: VoicemeeterBridge überschreibt nicht-zugewiesene Button-LEDs nicht mehr auf Off

### X-Touch Initialisierung
- **Fix**: Master-Section-Buttons (Notes 40–103) werden beim Start zurückgesetzt
  (vorher nur Channel-Buttons Notes 0–31)

## [Previous] - 2025-02-04

### LCD-Displays
- **Fix**: LCD-Displays funktionieren jetzt korrekt
  - SysEx-Prefix von 0x15 (MCU Extended) auf 0x14 (MCU Main) geändert
  - Handshake-Nachricht bei Initialisierung hinzugefügt (`F0 00 00 66 14 13 00 F7`)

### 7-Segment-Display
- **Fix**: 7-Segment-Display funktioniert jetzt korrekt
  - Von Behringer SysEx auf Mackie Control CC (CC 64-75) umgestellt
  - Reihenfolge korrigiert (rechts nach links)
  - ASCII-Zeichenkonvertierung angepasst

### Encoder LED-Ringe
- **Fix**: LED-Ringe zeigen jetzt die korrekte Position an
  - CC-Wert-Mapping empirisch ermittelt und implementiert
  - Position 0-10 statt 0-15 (entspricht den 11 nutzbaren LEDs)
  - Mode-spezifische Wertebereiche: Dot=1-11, Pan=17-27, Wrap=33-43, Spread=49-54

### Encoder-Funktionen
- **Verbesserung**: Encoder-Funktionen werden bei View-Wechsel neu registriert
- **Verbesserung**: Encoder-Ringe werden bei jedem Parameter-Update synchronisiert
- **Verbesserung**: Display zeigt Parameter-Name (oben) und Wert (unten) bei Encoder-Bedienung
- **Verbesserung**: Automatisches Zurückschalten auf Kanalname nach 5 Sekunden

### Channel View Cycling
- **Änderung**: Flip-Button (Note 50) ist jetzt für Channel View Cycling reserviert
- **Änderung**: Fader Bank Left/Right (Notes 46-47) sind jetzt frei zuweisbare Buttons

### Encoder 0 Speziallogik
- **Entfernt**: Encoder 0 verhält sich jetzt wie alle anderen Encoder (keine Sonderbehandlung)

### Bug Fixes
- **Fix**: MIDI Debug Window NullReferenceException beim Öffnen behoben

### Test-Skripte
- **Neu**: `test_lcd.py` - LCD-Display Protokoll-Tests
- **Neu**: `test_segment.py` - 7-Segment-Display Protokoll-Tests
- **Neu**: `test_encoder_ring.py` - Encoder LED-Ring Protokoll-Tests (CC 48-55 Mapping)

### Dokumentation
- **Aktualisiert**: ARCHITECTURE.md mit X-Touch MIDI-Protokoll-Details
  - Encoder LED-Ring Mapping (empirisch ermittelt)
  - 7-Segment-Display CC-Protokoll
  - LCD-Display SysEx-Format
  - Handshake-Nachricht
