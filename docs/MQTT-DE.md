[English](MQTT.md) | [Deutsch](MQTT-DE.md)

# MQTT Integration

Dieses Dokument beschreibt die MQTT-Funktionen in `XTouchVMBridge` und zeigt ein empfohlenes Setup
fuer die direkte Geraetesteuerung ohne Home Assistant.

## Ueberblick

Die MQTT-Integration besteht aus drei Ebenen:

1. Globaler MQTT-Client (Broker-Verbindung, Publish/Subscribe)
2. Channel-Buttons (pro Kanalbutton: VM-Parameter oder MQTT Publish, optional LED per MQTT)
3. Master-Buttons (MQTT Publish, Device Select, Transport, optional LED per MQTT bei MQTT Publish)

## Globaler MQTT-Client

Konfigurierbar im MQTT-Dialog:

- `enabled`, `host`, `port`, `useTls`
- Auth: `username`, `password`
- Publish Defaults: `publishTopic`, `publishQos`, `publishRetain`
- Subscriptions: `subscribeTopics`, `subscribeQos`

Zusatz: Topics aus LED-Mappings werden automatisch mit abonniert.

## Channel-Buttons

Im Panel-Mapping-Editor pro Kanalbutton:

- `ActionType = VmParameter` oder `ActionType = MqttPublish`
- `MqttPublish`: `topic`, `payloadPressed`, `payloadReleased`, `qos`, `retain`
- `MqttLedReceive`: `topic`, Payloads fuer `on`, `off`, `blink`, `toggle`

Hinweise:

- MQTT-LED-Felder (`MqttLedReceive`) sind im Editor nur sichtbar, wenn `ActionType = MqttPublish`.
- Bei der REC-Spezialaktion `Aufnahme Start/Stop (Dateiname: Kanal + Zeit)` folgt die LED dem Recorder-Status.
- Erster Druck startet die Aufnahme, zweiter Druck stoppt sie.

Testfunktionen im Editor:

- `Test Publish`: sendet den konfigurierten Press-Payload
- `Test LED`: sendet den gewaehlten LED-Testpayload (`On/Off/Blink/Toggle`)

## Master-Buttons

Im Master-Mapping-Editor stehen MQTT-bezogene Aktionstypen zur Verfuegung:

- `MqttPublish`
- `SelectMqttDevice`
- `MqttTransport`

Zusaetzlich (nicht-MQTT-Aktion):
- `VmParameter` unterstuetzt `vmLedSource`:
  - `ManualFeedback` (Standard): LED folgt `ledFeedback`
  - `VoicemeeterState`: LED folgt dem echten VM-Parameterzustand (On/Off)

### 1) MqttPublish

Felder:

- `mqttTopic`
- `mqttPayloadPressed`, `mqttPayloadReleased`
- `mqttQos`, `mqttRetain`

Optional:

- `LED per MQTT steuern` (nur bei `MqttPublish` sichtbar)
- `mqttLedTopic`, Payloads fuer `on/off/blink/toggle`

### 2) SelectMqttDevice

Felder:

- `mqttDeviceId`
- `mqttDeviceCommandTopic`

Verhalten:

- Druecken waehlt das Zielgeraet als aktiv
- erneutes Druecken auf denselben Selector deaktiviert das Ziel
- nur ein Selector ist aktiv, Selector-LED zeigt den aktiven Zustand

### 3) MqttTransport

Felder:

- `mqttTransportCommand` (`play_pause`, `play`, `pause`, `stop`, `next`, `prev`)
- optional `mqttPayloadPressed` als Payload-Override
- `mqttQos`, `mqttRetain`

Verhalten:

- sendet an das aktuell aktive Zielgeraet aus `SelectMqttDevice`
- ohne aktive Auswahl wird kein Transport-Befehl gesendet

Editor-Presets fuer typische Transport-Buttons:

- Note `91` (Rewind) -> `prev`
- Note `92` (Forward) -> `next`
- Note `93` (Stop) -> `stop`
- Note `94` (Play) -> `play_pause`
- Note `95` (Record) -> `pause`

## Empfohlenes Topic-Schema (ohne Home Assistant)

Fuer zwei Zielgeraete:

- `media/deviceA/cmd`
- `media/deviceB/cmd`

Payloads:

- `play_pause`, `play`, `pause`, `stop`, `next`, `prev`

Optional fuer LED-Rueckkanal:

- `media/deviceA/led`
- `media/deviceB/led`

mit Payloads:

- `on`, `off`, `blink`, `toggle`

## Praxisbeispiel DAT/CD

Broker/Benutzer:

- Host: `10.5.0.240`
- User: `mqtt`

Topics:

- DAT: `Remote/DAT`
- CD: `Remote/CD`

Transport-Payloads pro Geraet:

- DAT: `DATPlay`, `DATStop`, `DATFF`, `DATRew`
- CD: `CDPlay`, `CDStop`, `CDFF`, `CDRew`

Beispiel-CLI:

```bash
mosquitto_pub -h 10.5.0.240 -u mqtt -t Remote/DAT -m DATFF
```

Mapping-Idee im Editor:

- `SelectMqttDevice` fuer DAT mit `mqttDeviceId = DAT`, `mqttDeviceCommandTopic = Remote/DAT`
- `SelectMqttDevice` fuer CD mit `mqttDeviceId = CD`, `mqttDeviceCommandTopic = Remote/CD`
- `MqttTransport` auf Play/Stop/FF/Rew legen
- pro Transport-Button `mqttPayloadPressed` setzen:
  - Play: `Play`
  - Stop: `Stop`
  - FF: `FF`
  - Rew: `Rew`

Hinweis:

- Bei `SelectMqttDevice + MqttTransport` bleibt der Payload identisch und nur das Topic wechselt (z. B. `Remote/DAT` vs. `Remote/CD`).
- Falls ein Zielgeraet zwingend Praefix-Payloads wie `DATFF` / `CDFF` braucht, ist das mit einem gemeinsamen Transport-Button-Set aktuell nicht dynamisch abbildbar.
- Workaround: pro Geraet eigene Transport-Buttons als `MqttPublish` konfigurieren (feste Payloads je Button).

## Beispiel-Config (Ausschnitt)

```json
{
  "masterButtonActions": {
    "84": {
      "actionType": "SelectMqttDevice",
      "mqttDeviceId": "deviceA",
      "mqttDeviceCommandTopic": "media/deviceA/cmd"
    },
    "85": {
      "actionType": "SelectMqttDevice",
      "mqttDeviceId": "deviceB",
      "mqttDeviceCommandTopic": "media/deviceB/cmd"
    },
    "91": {
      "actionType": "MqttTransport",
      "mqttTransportCommand": "prev",
      "mqttQos": 0,
      "mqttRetain": false
    },
    "92": {
      "actionType": "MqttTransport",
      "mqttTransportCommand": "next",
      "mqttQos": 0,
      "mqttRetain": false
    },
    "93": {
      "actionType": "MqttTransport",
      "mqttTransportCommand": "stop",
      "mqttQos": 0,
      "mqttRetain": false
    },
    "94": {
      "actionType": "MqttTransport",
      "mqttTransportCommand": "play_pause",
      "mqttQos": 0,
      "mqttRetain": false
    }
  }
}
```

## Troubleshooting

- Keine Reaktion auf MQTT:
  - MQTT im Dialog aktivieren (`enabled = true`)
  - Broker-Verbindung mit `Testen` pruefen
  - Topic exakt vergleichen (inkl. Gross/Kleinschreibung bei Bedarf)
- LED reagiert nicht:
  - richtige LED-Topic/Payloads gesetzt
  - LED-Mapping gespeichert
  - fuer Master: LED-per-MQTT nur bei Aktion `MqttPublish` verfuegbar
- Transport sendet nichts:
  - erst ein `SelectMqttDevice` Button druecken (aktives Ziel muss gesetzt sein)
