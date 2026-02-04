#!/usr/bin/env python3
"""
Test-Script für X-Touch Encoder LED-Ringe.
Testet verschiedene CC-Bereiche und Werte.
"""
import mido
import time

def get_xtouch_output():
    """Findet X-Touch Output."""
    for name in mido.get_output_names():
        if "X-Touch" in name:
            return name
    raise OSError("No X-Touch output found")

def test_cc_48_55():
    """Test mit CC 48-55 (Mackie Control Standard)."""
    output_name = get_xtouch_output()
    print(f"Verbinde mit: {output_name}")
    print("CC 48-55 (Mackie Control Standard)")

    with mido.open_output(output_name) as out:
        # Alle Ringe auf Mitte setzen (Pan-Mode, Position 8 = Mitte)
        for ch in range(8):
            cc = 48 + ch
            # Mode 1 (Pan) * 16 + Position 8 = 24
            value = 1 * 16 + 8
            msg = mido.Message('control_change', control=cc, value=value)
            out.send(msg)
            print(f"CC {cc}: value={value} (Mode=Pan, Pos=8)")
        print("Gesendet!")

def test_cc_48_55_values():
    """Test verschiedene Werte für einen Encoder."""
    output_name = get_xtouch_output()
    print(f"Verbinde mit: {output_name}")
    print("CC 48 mit verschiedenen Werten (Encoder 1)")

    with mido.open_output(output_name) as out:
        # Teste verschiedene Modi und Positionen
        tests = [
            (0, 0, "Dot Mode, Pos 0 (links)"),
            (0, 7, "Dot Mode, Pos 7 (mitte-links)"),
            (0, 15, "Dot Mode, Pos 15 (rechts)"),
            (1, 0, "Pan Mode, Pos 0 (voll links)"),
            (1, 7, "Pan Mode, Pos 7 (leicht links)"),
            (1, 8, "Pan Mode, Pos 8 (mitte)"),
            (1, 15, "Pan Mode, Pos 15 (voll rechts)"),
            (2, 0, "Wrap Mode, Pos 0 (aus)"),
            (2, 8, "Wrap Mode, Pos 8 (halb)"),
            (2, 15, "Wrap Mode, Pos 15 (voll)"),
        ]

        for mode, pos, desc in tests:
            value = mode * 16 + pos
            msg = mido.Message('control_change', control=48, value=value)
            out.send(msg)
            print(f"Value {value:3d} = Mode {mode} + Pos {pos:2d}: {desc}")
            input("Enter für nächsten Test...")

        # Am Ende aus
        out.send(mido.Message('control_change', control=48, value=0))
        print("Test beendet!")

def test_cc_80_87():
    """Test mit CC 80-87 (Behringer X-Touch laut Doku)."""
    output_name = get_xtouch_output()
    print(f"Verbinde mit: {output_name}")
    print("CC 80-87 (Behringer X-Touch)")

    with mido.open_output(output_name) as out:
        # Alle Ringe auf Mitte setzen
        for ch in range(8):
            cc = 80 + ch
            value = 64  # Mitte (0-127)
            msg = mido.Message('control_change', control=cc, value=value)
            out.send(msg)
            print(f"CC {cc}: value={value}")
        print("Gesendet!")

def test_cc_80_87_sweep():
    """Sweep-Test mit CC 80-87."""
    output_name = get_xtouch_output()
    print(f"Verbinde mit: {output_name}")
    print("CC 80-87 Sweep (0 -> 127 -> 0)")

    with mido.open_output(output_name) as out:
        # Sweep von 0 bis 127 und zurück
        for value in list(range(0, 128, 8)) + list(range(127, -1, -8)):
            for ch in range(8):
                cc = 80 + ch
                msg = mido.Message('control_change', control=cc, value=value)
                out.send(msg)
            print(f"Value: {value}")
            time.sleep(0.1)
        print("Sweep beendet!")

def test_cc_48_55_sweep():
    """Sweep-Test mit CC 48-55."""
    output_name = get_xtouch_output()
    print(f"Verbinde mit: {output_name}")
    print("CC 48-55 Sweep (verschiedene Modi)")

    with mido.open_output(output_name) as out:
        # Mode 0: Dot (einzelner Punkt)
        print("\nMode 0 (Dot) - Position 0-15:")
        for pos in range(16):
            for ch in range(8):
                cc = 48 + ch
                value = 0 * 16 + pos  # Mode 0, Position variabel
                msg = mido.Message('control_change', control=cc, value=value)
                out.send(msg)
            print(f"Position: {pos}")
            time.sleep(0.2)

        # Mode 1: Pan (von Mitte aus)
        print("\nMode 1 (Pan) - Position 0-15:")
        for pos in range(16):
            for ch in range(8):
                cc = 48 + ch
                value = 1 * 16 + pos
                msg = mido.Message('control_change', control=cc, value=value)
                out.send(msg)
            print(f"Position: {pos}")
            time.sleep(0.2)

        # Mode 2: Wrap (füllt sich)
        print("\nMode 2 (Wrap) - Position 0-15:")
        for pos in range(16):
            for ch in range(8):
                cc = 48 + ch
                value = 2 * 16 + pos
                msg = mido.Message('control_change', control=cc, value=value)
                out.send(msg)
            print(f"Position: {pos}")
            time.sleep(0.2)

        # Mode 3: Spread (von Mitte symmetrisch)
        print("\nMode 3 (Spread) - Position 0-15:")
        for pos in range(16):
            for ch in range(8):
                cc = 48 + ch
                value = 3 * 16 + pos
                msg = mido.Message('control_change', control=cc, value=value)
                out.send(msg)
            print(f"Position: {pos}")
            time.sleep(0.2)

        print("Sweep beendet!")

def test_cc_48_raw_sweep():
    """Sweep durch alle Werte 0-127 für CC 48 mit Pause nach jedem Wert."""
    output_name = get_xtouch_output()
    print(f"Verbinde mit: {output_name}")
    print("CC 48 Raw Sweep (Werte 0-127)")
    print("Drücke Enter für nächsten Wert, 'q' zum Beenden, Zahl zum Springen")
    print("Notiere welche LEDs leuchten: L6 L5 L4 L3 L2 L1 [M] R1 R2 R3 R4 R5 R6")
    print()

    with mido.open_output(output_name) as out:
        value = 0
        while value < 128:
            msg = mido.Message('control_change', control=48, value=value)
            out.send(msg)
            print(f"Value {value:3d} (0x{value:02X}) - LEDs: ", end="")

            user_input = input().strip().lower()

            if user_input == 'q':
                break
            elif user_input.isdigit():
                value = int(user_input)
            else:
                value += 1

        # Am Ende aus
        out.send(mido.Message('control_change', control=48, value=0))
        print("\nSweep beendet!")

def test_all_off():
    """Alle Encoder-Ringe ausschalten."""
    output_name = get_xtouch_output()
    print(f"Verbinde mit: {output_name}")
    print("Alle Ringe AUS")

    with mido.open_output(output_name) as out:
        # Versuche beide CC-Bereiche
        for ch in range(8):
            # CC 48-55
            out.send(mido.Message('control_change', control=48+ch, value=0))
            # CC 80-87
            out.send(mido.Message('control_change', control=80+ch, value=0))
        print("Gesendet!")

def test_all_on():
    """Alle Encoder-Ringe auf Maximum."""
    output_name = get_xtouch_output()
    print(f"Verbinde mit: {output_name}")
    print("Alle Ringe AN (Maximum)")

    with mido.open_output(output_name) as out:
        # Versuche beide CC-Bereiche
        for ch in range(8):
            # CC 48-55: Mode 2 (Wrap) + Position 15 = volle Anzeige
            out.send(mido.Message('control_change', control=48+ch, value=2*16+15))
            # CC 80-87
            out.send(mido.Message('control_change', control=80+ch, value=127))
        print("Gesendet!")

def test_individual_encoder():
    """Testet jeden Encoder einzeln."""
    output_name = get_xtouch_output()
    print(f"Verbinde mit: {output_name}")
    print("Teste jeden Encoder einzeln...")

    with mido.open_output(output_name) as out:
        # Erst alle aus
        for ch in range(8):
            out.send(mido.Message('control_change', control=48+ch, value=0))
            out.send(mido.Message('control_change', control=80+ch, value=0))

        # Dann jeden einzeln testen
        for ch in range(8):
            print(f"\nEncoder {ch + 1} aktiv (CC 48+{ch}={48+ch}, CC 80+{ch}={80+ch})")

            # CC 48-55 Test
            out.send(mido.Message('control_change', control=48+ch, value=2*16+15))
            time.sleep(0.5)
            out.send(mido.Message('control_change', control=48+ch, value=0))

            # CC 80-87 Test
            out.send(mido.Message('control_change', control=80+ch, value=127))
            time.sleep(0.5)
            out.send(mido.Message('control_change', control=80+ch, value=0))

        print("\nTest beendet!")

if __name__ == "__main__":
    print("=== X-Touch Encoder Ring LED Test ===\n")

    tests = [
        ("1", "CC 48-55 (Mackie Control Standard)", test_cc_48_55),
        ("1a", "CC 48 verschiedene Werte testen", test_cc_48_55_values),
        ("1b", "CC 48 Raw Sweep (0-63)", test_cc_48_raw_sweep),
        ("2", "CC 80-87 (Behringer X-Touch)", test_cc_80_87),
        ("3", "CC 80-87 Sweep (0-127-0)", test_cc_80_87_sweep),
        ("4", "CC 48-55 Sweep (Modi 0-3)", test_cc_48_55_sweep),
        ("5", "Alle Ringe AUS", test_all_off),
        ("6", "Alle Ringe AN (Maximum)", test_all_on),
        ("7", "Jeden Encoder einzeln testen", test_individual_encoder),
    ]

    for num, name, _ in tests:
        print(f"  {num}. {name}")
    print("  q. Beenden")

    while True:
        choice = input("\nWaehle Test (1-7, q): ").strip().lower()

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
