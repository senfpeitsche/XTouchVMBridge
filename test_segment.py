#!/usr/bin/env python3
"""
Test-Script für X-Touch 7-Segment Display.
Testet verschiedene Protokolle und Device-IDs.
"""
import mido
import time

def get_xtouch_output():
    """Findet X-Touch Output."""
    for name in mido.get_output_names():
        if "X-Touch" in name:
            return name
    raise OSError("No X-Touch output found")

# 7-Segment Font (Bit 0=a, 1=b, 2=c, 3=d, 4=e, 5=f, 6=g)
SEGMENT_FONT = {
    '0': 0x3F, '1': 0x06, '2': 0x5B, '3': 0x4F,
    '4': 0x66, '5': 0x6D, '6': 0x7D, '7': 0x07,
    '8': 0x7F, '9': 0x6F,
    'A': 0x77, 'b': 0x7C, 'C': 0x39, 'd': 0x5E,
    'E': 0x79, 'F': 0x71, 'H': 0x76, 'L': 0x38,
    'P': 0x73, 'U': 0x3E, '-': 0x40, ' ': 0x00,
}

def text_to_segments(text):
    """Konvertiert Text zu 12 Segment-Bytes."""
    segments = []
    for i in range(12):
        if i < len(text):
            c = text[i].upper() if text[i].upper() in SEGMENT_FONT else text[i]
            segments.append(SEGMENT_FONT.get(c, 0x00))
        else:
            segments.append(0x00)
    return segments

def test_behringer_segment_0x14():
    """Test mit Behringer SysEx und Device-ID 0x14 (X-Touch)."""
    output_name = get_xtouch_output()
    print(f"Verbinde mit: {output_name}")
    print("Device-ID: 0x14 (X-Touch)")

    with mido.open_output(output_name) as out:
        # Behringer SysEx: F0 00 20 32 dd 37 s1..s12 d1 d2 F7
        device_id = 0x14
        segments = text_to_segments("00.00.00.00")
        dots1 = 0b00101010  # Dots bei Position 2, 4, 6
        dots2 = 0b00000000

        msg = [0xF0, 0x00, 0x20, 0x32, device_id, 0x37]
        msg += segments
        msg += [dots1, dots2, 0xF7]

        print(f"Sende: {' '.join(f'{b:02X}' for b in msg)}")
        out.send(mido.Message.from_bytes(msg))
        print("Gesendet!")

def test_behringer_segment_0x15():
    """Test mit Behringer SysEx und Device-ID 0x15 (X-Touch Extender)."""
    output_name = get_xtouch_output()
    print(f"Verbinde mit: {output_name}")
    print("Device-ID: 0x15 (X-Touch Extender)")

    with mido.open_output(output_name) as out:
        device_id = 0x15
        segments = text_to_segments("11.11.11.11")
        dots1 = 0b00101010
        dots2 = 0b00000000

        msg = [0xF0, 0x00, 0x20, 0x32, device_id, 0x37]
        msg += segments
        msg += [dots1, dots2, 0xF7]

        print(f"Sende: {' '.join(f'{b:02X}' for b in msg)}")
        out.send(mido.Message.from_bytes(msg))
        print("Gesendet!")

def test_behringer_segment_0x00():
    """Test mit Behringer SysEx und Device-ID 0x00 (Broadcast?)."""
    output_name = get_xtouch_output()
    print(f"Verbinde mit: {output_name}")
    print("Device-ID: 0x00 (Broadcast)")

    with mido.open_output(output_name) as out:
        device_id = 0x00
        segments = text_to_segments("22.22.22.22")
        dots1 = 0b00101010
        dots2 = 0b00000000

        msg = [0xF0, 0x00, 0x20, 0x32, device_id, 0x37]
        msg += segments
        msg += [dots1, dots2, 0xF7]

        print(f"Sende: {' '.join(f'{b:02X}' for b in msg)}")
        out.send(mido.Message.from_bytes(msg))
        print("Gesendet!")

def test_behringer_segment_0x42():
    """Test mit Behringer SysEx und Device-ID 0x42 (X-Touch bei anderem DIP-Switch)."""
    output_name = get_xtouch_output()
    print(f"Verbinde mit: {output_name}")
    print("Device-ID: 0x42 (alternate)")

    with mido.open_output(output_name) as out:
        device_id = 0x42
        segments = text_to_segments("33.33.33.33")
        dots1 = 0b00101010
        dots2 = 0b00000000

        msg = [0xF0, 0x00, 0x20, 0x32, device_id, 0x37]
        msg += segments
        msg += [dots1, dots2, 0xF7]

        print(f"Sende: {' '.join(f'{b:02X}' for b in msg)}")
        out.send(mido.Message.from_bytes(msg))
        print("Gesendet!")

def test_mackie_cc_segment():
    """Test mit Mackie Control CC-Nachrichten (CC 64-75)."""
    output_name = get_xtouch_output()
    print(f"Verbinde mit: {output_name}")
    print("Verwende Mackie Control CC 64-75")

    with mido.open_output(output_name) as out:
        # Mackie Control: CC 64-75 für 12 Digits (rechts nach links)
        # Value: Bit 6 = Dot, Bits 5-0 = ASCII-Zeichen (0x30-0x3F mapped)
        text = "123456789012"

        for i, char in enumerate(text[:12]):
            cc = 64 + i  # CC 64-75
            # ASCII 0x30-0x3F direkt, 0x40-0x5F minus 64
            ascii_val = ord(char)
            if ascii_val >= 0x40:
                value = ascii_val - 0x40
            else:
                value = ascii_val - 0x30

            # Dot bei geraden Positionen
            if i % 3 == 2:
                value |= 0x40  # Bit 6 = Dot

            msg = mido.Message('control_change', control=cc, value=value)
            out.send(msg)
            print(f"CC {cc}: '{char}' -> value={value:02X}")
            time.sleep(0.01)

        print("Gesendet!")

def test_all_segments_on():
    """Test: Alle Segmente an (zum Testen ob Hardware reagiert)."""
    output_name = get_xtouch_output()
    print(f"Verbinde mit: {output_name}")
    print("Alle Segmente AN (0x7F)")

    with mido.open_output(output_name) as out:
        device_id = 0x14
        segments = [0x7F] * 12  # Alle Segmente an
        dots1 = 0x7F  # Alle Dots an
        dots2 = 0x1F  # Alle Dots an

        msg = [0xF0, 0x00, 0x20, 0x32, device_id, 0x37]
        msg += segments
        msg += [dots1, dots2, 0xF7]

        print(f"Sende: {' '.join(f'{b:02X}' for b in msg)}")
        out.send(mido.Message.from_bytes(msg))
        print("Gesendet!")

def test_all_segments_off():
    """Test: Alle Segmente aus."""
    output_name = get_xtouch_output()
    print(f"Verbinde mit: {output_name}")
    print("Alle Segmente AUS (0x00)")

    with mido.open_output(output_name) as out:
        device_id = 0x14
        segments = [0x00] * 12
        dots1 = 0x00
        dots2 = 0x00

        msg = [0xF0, 0x00, 0x20, 0x32, device_id, 0x37]
        msg += segments
        msg += [dots1, dots2, 0xF7]

        print(f"Sende: {' '.join(f'{b:02X}' for b in msg)}")
        out.send(mido.Message.from_bytes(msg))
        print("Gesendet!")

def test_individual_digits():
    """Test: Jedes Digit einzeln testen."""
    output_name = get_xtouch_output()
    print(f"Verbinde mit: {output_name}")
    print("Teste jedes Digit einzeln...")

    with mido.open_output(output_name) as out:
        device_id = 0x14

        for digit in range(12):
            segments = [0x00] * 12
            segments[digit] = 0x7F  # Nur dieses Digit an

            msg = [0xF0, 0x00, 0x20, 0x32, device_id, 0x37]
            msg += segments
            msg += [0x00, 0x00, 0xF7]

            out.send(mido.Message.from_bytes(msg))
            print(f"Digit {digit + 1} aktiv")
            time.sleep(0.5)

        # Am Ende alle aus
        msg = [0xF0, 0x00, 0x20, 0x32, device_id, 0x37] + [0x00] * 12 + [0x00, 0x00, 0xF7]
        out.send(mido.Message.from_bytes(msg))
        print("Alle aus")

if __name__ == "__main__":
    print("=== X-Touch 7-Segment Display Test ===\n")

    tests = [
        ("1", "Behringer SysEx mit Device-ID 0x14 (X-Touch)", test_behringer_segment_0x14),
        ("2", "Behringer SysEx mit Device-ID 0x15 (Extender)", test_behringer_segment_0x15),
        ("2a", "Behringer SysEx mit Device-ID 0x00 (Broadcast)", test_behringer_segment_0x00),
        ("2b", "Behringer SysEx mit Device-ID 0x42 (alternate)", test_behringer_segment_0x42),
        ("3", "Mackie Control CC 64-75", test_mackie_cc_segment),
        ("4", "Alle Segmente AN", test_all_segments_on),
        ("5", "Alle Segmente AUS", test_all_segments_off),
        ("6", "Einzelne Digits nacheinander", test_individual_digits),
    ]

    for num, name, _ in tests:
        print(f"  {num}. {name}")
    print("  q. Beenden")

    while True:
        choice = input("\nWaehle Test (1-6, 2a, 2b, q): ").strip().lower()

        if choice == 'q':
            break

        for num, name, func in tests:
            if choice == num:
                print(f"\n--- {name} ---")
                try:
                    func()
                except Exception as e:
                    print(f"Fehler: {e}")
                break
        else:
            print("Unbekannte Auswahl")

    print("\nTest beendet.")
