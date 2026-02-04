#!/usr/bin/env python3
"""
Test-Script für X-Touch LCD Display.
Sendet Test-Nachrichten direkt an das Gerät.
"""
import mido
import time

def get_xtouch_output():
    """Findet X-Touch Output."""
    for name in mido.get_output_names():
        if "X-Touch" in name:
            return name
    raise OSError("No X-Touch output found")

def test_mackie_protocol_ext():
    """Test mit Mackie Control EXTENDED Protocol (0x15)."""
    output_name = get_xtouch_output()
    print(f"Verbinde mit: {output_name}")
    print("Verwende Mackie Control EXTENDED (0x15)")

    with mido.open_output(output_name) as out:
        # SysEx Prefix für Mackie Control EXTENDED
        prefix = [0xF0, 0x00, 0x00, 0x66, 0x15]  # 0x15 = MCU Extended
        suffix = [0xF7]

        # 1. Handshake senden
        print("Sende Handshake...")
        handshake = prefix + [0x13] + [0x00] + suffix
        out.send(mido.Message.from_bytes(handshake))
        time.sleep(0.1)

        # 2. Display-Farben
        print("Setze Display-Farben...")
        colors = [1, 2, 3, 4, 5, 6, 7, 1]
        color_msg = prefix + [0x72] + colors + suffix
        out.send(mido.Message.from_bytes(color_msg))
        time.sleep(0.1)

        # 3. Display-Text
        print("Setze Display-Text...")
        text_row1 = "EXT  1 EXT  2 EXT  3 EXT  4 EXT  5 EXT  6 EXT  7 EXT  8 "
        text_row2 = "-6.0dB -3.0dB  0.0dB +3.0dB -12 dB -inf   Test   Hello  "
        full_text = text_row1 + text_row2

        text_bytes = [ord(c) for c in full_text]
        text_msg = prefix + [0x12] + [0x00] + text_bytes + suffix
        out.send(mido.Message.from_bytes(text_msg))
        print(f"Hex: {' '.join(f'{b:02X}' for b in text_msg[:20])}...")


def test_mackie_protocol_main():
    """Test mit Mackie Control MAIN Protocol (0x14)."""
    output_name = get_xtouch_output()
    print(f"Verbinde mit: {output_name}")
    print("Verwende Mackie Control MAIN (0x14)")

    with mido.open_output(output_name) as out:
        # SysEx Prefix für Mackie Control MAIN (nicht Extended)
        prefix = [0xF0, 0x00, 0x00, 0x66, 0x14]  # 0x14 = MCU Main
        suffix = [0xF7]

        # 1. Handshake senden
        print("Sende Handshake...")
        handshake = prefix + [0x13] + [0x00] + suffix
        out.send(mido.Message.from_bytes(handshake))
        time.sleep(0.1)

        # 2. Display-Farben
        print("Setze Display-Farben...")
        colors = [7, 6, 5, 4, 3, 2, 1, 7]  # Andere Farben als Test
        color_msg = prefix + [0x72] + colors + suffix
        out.send(mido.Message.from_bytes(color_msg))
        time.sleep(0.1)

        # 3. Display-Text
        print("Setze Display-Text...")
        text_row1 = "MAIN 1 MAIN 2 MAIN 3 MAIN 4 MAIN 5 MAIN 6 MAIN 7 MAIN 8 "
        text_row2 = "Test 1 Test 2 Test 3 Test 4 Test 5 Test 6 Test 7 Test 8 "
        full_text = text_row1 + text_row2

        text_bytes = [ord(c) for c in full_text]
        text_msg = prefix + [0x12] + [0x00] + text_bytes + suffix
        out.send(mido.Message.from_bytes(text_msg))
        print(f"Hex: {' '.join(f'{b:02X}' for b in text_msg[:20])}...")

def test_behringer_lcd():
    """Test mit Behringer-spezifischem LCD Protocol."""
    output_name = get_xtouch_output()
    print(f"\nTeste Behringer LCD Protocol...")
    print(f"Verbinde mit: {output_name}")

    # Prüfe ob es X-Touch oder Extender ist
    is_extender = "Ext" in output_name
    device_id = 0x15 if is_extender else 0x14
    print(f"Device ID: 0x{device_id:02X} ({'Extender' if is_extender else 'X-Touch'})")

    with mido.open_output(output_name) as out:
        # Behringer SysEx: F0 00 20 32 dd 4C nn cc c1..c14 F7
        # dd = Device ID (0x14=X-Touch, 0x15=Extender)
        # nn = LCD nummer (0-7)
        # cc = color (0-7) + invert flags
        # c1..c14 = 14 ASCII zeichen (7 oben, 7 unten)

        for lcd in range(8):
            top = f"LCD {lcd}  "[:7]
            bottom = f"Test {lcd} "[:7]
            color = (lcd % 7) + 1  # 1-7, skip 0 (black/off)

            msg = [0xF0, 0x00, 0x20, 0x32, device_id, 0x4C, lcd, color]
            msg += [ord(c) for c in top]
            msg += [ord(c) for c in bottom]
            msg += [0xF7]

            out.send(mido.Message.from_bytes(msg))
            print(f"LCD {lcd}: top='{top}' bottom='{bottom}' color={color}")
            time.sleep(0.05)

        print("Behringer LCD Test fertig!")

if __name__ == "__main__":
    print("=== X-Touch LCD Test ===\n")

    print("Test 1a: Mackie Control EXTENDED Protocol (0x15)")
    print("-" * 50)
    try:
        test_mackie_protocol_ext()
    except Exception as e:
        print(f"Fehler: {e}")

    input("\n>>> Druecke Enter fuer naechsten Test... <<<\n")

    print("Test 1b: Mackie Control MAIN Protocol (0x14)")
    print("-" * 50)
    try:
        test_mackie_protocol_main()
    except Exception as e:
        print(f"Fehler: {e}")

    input("\n>>> Druecke Enter fuer naechsten Test... <<<\n")

    print("Test 2: Behringer LCD Protocol")
    print("-" * 50)
    try:
        test_behringer_lcd()
    except Exception as e:
        print(f"Fehler: {e}")

    print("\n=== Tests abgeschlossen ===")
    print("Welcher Test hat funktioniert? (1a, 1b, 2, oder keiner)")
