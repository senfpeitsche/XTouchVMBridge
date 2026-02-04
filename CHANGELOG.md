# Changelog

## [Unreleased] - 2025-02-04

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
