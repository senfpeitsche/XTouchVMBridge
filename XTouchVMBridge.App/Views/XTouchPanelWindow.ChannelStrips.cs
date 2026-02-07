using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Interfaces;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using ProgressBar = System.Windows.Controls.ProgressBar;

namespace XTouchVMBridge.App.Views;

/// <summary>
/// Channel-Strip-Aufbau: 8 Kanalstreifen mit Display, Encoder, Buttons, Fader, Level-Meter.
/// </summary>
public partial class XTouchPanelWindow
{
    private void BuildChannelStrips()
    {
        for (int ch = 0; ch < 8; ch++)
        {
            int channel = ch;
            var strip = BuildSingleChannelStrip(channel);
            ChannelGrid.Children.Add(strip);
        }
    }

    private Border BuildSingleChannelStrip(int ch)
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

        // Kanal-Nummer
        stack.Children.Add(new TextBlock
        {
            Text = $"CH {ch + 1}",
            Foreground = Brushes.Gray,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        });

        // LCD Display
        var displayBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(20, 20, 20)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(4, 2, 4, 2),
            Margin = new Thickness(0, 0, 0, 4),
            Cursor = System.Windows.Input.Cursors.Hand,
            Width = 72
        };
        var displayStack = new StackPanel();
        var topText = new TextBlock
        {
            Text = "       ",
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11, FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var bottomText = new TextBlock
        {
            Text = "       ",
            Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        displayStack.Children.Add(topText);
        displayStack.Children.Add(bottomText);
        displayBorder.Child = displayStack;
        displayBorder.MouseLeftButtonDown += (_, _) => ShowDisplayDetail(ch);
        stack.Children.Add(displayBorder);
        _displayPanels[ch] = displayBorder;
        _displayTop[ch] = topText;
        _displayBottom[ch] = bottomText;

        // Encoder Ring (modusabhängig: Pan=von Mitte, Wrap=von links)
        var ringContainer = new Grid
        {
            Width = 48, Height = 6,
            Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
            Margin = new Thickness(0, 2, 0, 0),
            ClipToBounds = true
        };
        var ringIndicator = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(255, 180, 0)),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch,
            Width = 2,
            Margin = new Thickness(23, 0, 0, 0)
        };
        ringContainer.Children.Add(ringIndicator);
        stack.Children.Add(ringContainer);
        _encoderRingContainers[ch] = ringContainer;
        _encoderRingIndicators[ch] = ringIndicator;

        // Encoder Button (Knob)
        var encoderBtn = new Button
        {
            Width = 44, Height = 44,
            Margin = new Thickness(0, 2, 0, 4),
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = $"Encoder {ch + 1}\nStrg+Klick: Funktion wechseln\nMausrad: Wert ändern\nStrg+Mausrad: Grob (5×)"
        };
        encoderBtn.Template = CreateEncoderTemplate();
        encoderBtn.Click += (_, _) => OnEncoderClick(ch);
        encoderBtn.MouseWheel += (_, e) => OnEncoderMouseWheel(ch, e);
        stack.Children.Add(encoderBtn);
        _encoderButtons[ch] = encoderBtn;

        // Buttons: REC, SOLO, MUTE, SELECT
        _recButtons[ch] = CreateHwButton("REC", Color.FromRgb(180, 40, 40), Color.FromRgb(60, 15, 15), ch, XTouchButtonType.Rec);
        _soloButtons[ch] = CreateHwButton("SOLO", Color.FromRgb(200, 180, 30), Color.FromRgb(60, 55, 10), ch, XTouchButtonType.Solo);
        _muteButtons[ch] = CreateHwButton("MUTE", Color.FromRgb(220, 100, 20), Color.FromRgb(65, 30, 8), ch, XTouchButtonType.Mute);
        _selectButtons[ch] = CreateHwButton("SEL", Color.FromRgb(40, 140, 220), Color.FromRgb(12, 42, 65), ch, XTouchButtonType.Select);
        stack.Children.Add(_recButtons[ch]);
        stack.Children.Add(_soloButtons[ch]);
        stack.Children.Add(_muteButtons[ch]);
        stack.Children.Add(_selectButtons[ch]);

        // Touch-Indikator
        var touchDot = new Ellipse
        {
            Width = 8, Height = 8,
            Fill = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
            Margin = new Thickness(0, 6, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            ToolTip = "Fader Touch"
        };
        stack.Children.Add(touchDot);
        _touchIndicators[ch] = touchDot;

        // Fader + Level-Meter nebeneinander
        var faderPanel = new Grid { Margin = new Thickness(0, 2, 0, 0) };
        faderPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        faderPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Level Meter (links, schmal)
        var meter = new ProgressBar
        {
            Orientation = Orientation.Vertical,
            Width = 8, Height = 150,
            Minimum = 0, Maximum = 13, Value = 0,
            Background = new SolidColorBrush(Color.FromRgb(25, 25, 25)),
            Foreground = new SolidColorBrush(Color.FromRgb(0, 200, 80)),
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0, 0, 4, 0)
        };
        meter.MouseLeftButtonDown += (_, _) => ShowLevelMeterDetail(ch);
        Grid.SetColumn(meter, 0);
        faderPanel.Children.Add(meter);
        _levelMeters[ch] = meter;

        // Fader (rechts) — Slider ist disabled, daher transparentes Overlay für Maus-Events
        var faderContainer = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var faderHost = new Grid { Width = 32, Height = 150 };
        var fader = new Slider
        {
            Orientation = Orientation.Vertical,
            Height = 150, Width = 32,
            Minimum = -8192, Maximum = 8188, Value = 0,
            IsEnabled = false,
            IsHitTestVisible = false
        };
        fader.ValueChanged += (_, e) => OnFaderValueChanged(ch, e.NewValue);
        faderHost.Children.Add(fader);

        // Transparentes Overlay fängt Maus-Events ab (auch wenn Slider disabled ist)
        var faderOverlay = new Border
        {
            Background = Brushes.Transparent,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        faderOverlay.MouseLeftButtonDown += (_, e) => OnFaderMouseDown(ch, e);
        faderOverlay.MouseLeftButtonUp += (_, _) => OnFaderMouseUp(ch);
        faderOverlay.MouseMove += (_, e) => OnFaderMouseMove(ch, e);
        faderOverlay.LostMouseCapture += (_, _) => OnFaderMouseUp(ch);
        faderHost.Children.Add(faderOverlay);

        faderContainer.Children.Add(faderHost);

        var dbLabel = new TextBlock
        {
            Text = "-∞ dB",
            Foreground = new SolidColorBrush(Color.FromRgb(150, 210, 150)),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 9,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0)
        };
        faderContainer.Children.Add(dbLabel);
        Grid.SetColumn(faderContainer, 1);
        faderPanel.Children.Add(faderContainer);
        _faderSliders[ch] = fader;
        _faderDbLabels[ch] = dbLabel;

        stack.Children.Add(faderPanel);

        // Channel-Nummer unten
        stack.Children.Add(new TextBlock
        {
            Text = $"{ch + 1}",
            Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 14, FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0)
        });

        return new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(51, 51, 51)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.FromRgb(26, 26, 26)),
            Margin = new Thickness(3),
            Padding = new Thickness(6),
            Child = stack
        };
    }

    private Button CreateHwButton(string label, Color activeColor, Color inactiveColor, int ch, XTouchButtonType type)
    {
        var btn = new Button
        {
            Content = label,
            Width = 56, Height = 24,
            Margin = new Thickness(0, 2, 0, 2),
            Background = new SolidColorBrush(inactiveColor),
            BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
            BorderThickness = new Thickness(1),
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10, FontWeight = FontWeights.Bold,
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = new ButtonTag(ch, type, activeColor, inactiveColor)
        };
        btn.Template = CreateRoundedButtonTemplate(3);
        btn.Click += (_, _) => OnHwButtonClick(ch, type);
        return btn;
    }

    /// <summary>
    /// Wird von allen Hardware-Buttons (REC, SOLO, MUTE, SELECT) als Click-Handler verwendet.
    /// Bei gedrückter Strg-Taste wird der zugewiesene VM-Parameter direkt getoggelt,
    /// ansonsten wird das Detail-Panel angezeigt.
    /// </summary>
    private void OnHwButtonClick(int ch, XTouchButtonType type)
    {
        if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
        {
            ExecuteHwButtonAction(ch, type);
            return;
        }

        ShowButtonDetail(ch, type);
    }

    /// <summary>
    /// Führt die zugewiesene Aktion eines Hardware-Buttons aus (VM-Parameter toggeln).
    /// </summary>
    private void ExecuteHwButtonAction(int ch, XTouchButtonType type)
    {
        // Prüfen ob ein zugewiesener VM-Parameter existiert
        bool hasMapping = false;
        if (_config != null && _vm != null)
        {
            int vmCh = ResolveVmChannel(ch);
            if (_config.Mappings.TryGetValue(vmCh, out var mapping))
            {
                string btnKey = type.ToString();
                if (mapping.Buttons.TryGetValue(btnKey, out var btnMap) && btnMap != null)
                {
                    // Bool-Parameter toggeln
                    float currentValue = _vm.GetParameter(btnMap.Parameter);
                    float newValue = currentValue > 0.5f ? 0f : 1f;
                    _vm.SetParameter(btnMap.Parameter, newValue);
                    hasMapping = true;
                }
            }
        }

        // Kein Mapping vorhanden → LED manuell toggeln (Panel + Hardware)
        if (!hasMapping)
        {
            var key = (ch, type);
            _manualLedState.TryGetValue(key, out bool isOn);
            _manualLedState[key] = !isOn;

            // Hardware-LED setzen falls Gerät verbunden
            if (_device != null && ch < _device.Channels.Count)
                _device.SetButtonLed(ch, type, !isOn ? LedState.On : LedState.Off);
        }
    }
}
