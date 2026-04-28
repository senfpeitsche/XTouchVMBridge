using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using XTouchVMBridge.App.Services;
using XTouchVMBridge.Core.Hardware;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;

namespace XTouchVMBridge.App.Views;

public partial class XTouchPanelWindow
{
    private DateTime _lastMainFaderClickTime = DateTime.MinValue;

    private void BuildMainFader()
    {
        var stack = MainFaderPanel;

        stack.Children.Add(new TextBlock
        {
            Text = "MAIN",
            Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10, FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var flipButton = new Button
        {
            Content = "FLIP",
            Height = 28, Width = 50,
            Margin = new Thickness(0, 0, 0, 8),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 9, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
            Background = new SolidColorBrush(Color.FromRgb(45, 37, 53)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(80, 70, 90)),
            BorderThickness = new Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = "FLIP: Wechselt durch die Channel-Ansichten (Views).\n" +
                      "Fest zugewiesen - kann nicht geändert werden."
        };
        flipButton.Template = CreateRoundedButtonTemplate(3);
        RegisterMasterButtonVisual(flipButton, MasterButtonActionService.FlipButtonNote, Color.FromRgb(120, 80, 160), Color.FromRgb(45, 37, 53));
        flipButton.Click += (_, _) =>
        {
            System.Windows.MessageBox.Show(
                "FLIP-Button ist fest für Channel View Cycling reserviert.\n\n" +
                "Drücken Sie FLIP am X-Touch, um durch die verschiedenen\n" +
                "Kanal-Zuweisungen (Views) zu wechseln.\n\n" +
                "Die LED leuchtet, wenn Sie nicht in der ersten Ansicht sind.",
                "FLIP - Channel View Cycling",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        };
        stack.Children.Add(flipButton);
        _masterButtons["Flip"] = flipButton;

        var faderHost = new Grid { Width = 32, Height = 200 };
        _mainFaderSlider = new Slider
        {
            Orientation = Orientation.Vertical,
            Height = 200, Width = 32,
            Minimum = -8192, Maximum = 8188, Value = 0,
            IsEnabled = false,
            IsHitTestVisible = false
        };
        faderHost.Children.Add(_mainFaderSlider);

        var faderOverlay = new Border
        {
            Background = Brushes.Transparent,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        faderOverlay.MouseLeftButtonDown += OnMainFaderMouseDown;
        faderOverlay.MouseLeftButtonUp += OnMainFaderMouseUp;
        faderOverlay.MouseMove += OnMainFaderMouseMove;
        faderOverlay.LostMouseCapture += (_, _) => _draggingMainFader = false;
        faderHost.Children.Add(faderOverlay);
        stack.Children.Add(faderHost);

        _mainFaderDbLabel = new TextBlock
        {
            Text = "-∞ dB",
            Foreground = new SolidColorBrush(Color.FromRgb(150, 210, 150)),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 9,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0)
        };
        stack.Children.Add(_mainFaderDbLabel);

        stack.Children.Add(new TextBlock
        {
            Text = "MAIN",
            Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12, FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        });
    }

    private void OnMainFaderMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var now = DateTime.Now;

        if ((now - _lastMainFaderClickTime).TotalMilliseconds < DoubleTapThresholdMs)
        {
            SetMainFaderTo0dB();
            _lastMainFaderClickTime = DateTime.MinValue;
            e.Handled = true;
            return;
        }

        _lastMainFaderClickTime = now;

        if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
        {
            _draggingMainFader = true;
            SetMainFaderFromMousePosition(e.GetPosition(_mainFaderSlider));

            if (e.Source is UIElement overlay)
                overlay.CaptureMouse();

            e.Handled = true;
            return;
        }

        ShowMainFaderDetail();
    }

    private void SetMainFaderTo0dB()
    {
        if (_config == null || _vm == null || _bridge == null || _mainFaderSlider == null) return;

        var currentView = _config.ChannelViews.ElementAtOrDefault(_bridge.CurrentViewIndex);
        if (currentView?.MainFaderChannel == null) return;

        int vmCh = currentView.MainFaderChannel.Value;
        if (!_config.Mappings.TryGetValue(vmCh, out var mapping) || mapping.Fader == null) return;

        double db = Math.Clamp(0.0, mapping.Fader.Min, mapping.Fader.Max);
        _vm.SetParameter(mapping.Fader.Parameter, (float)db);

        int position = FaderControl.DbToPosition(db);
        _mainFaderSlider.Value = position;
        if (_mainFaderDbLabel != null)
            _mainFaderDbLabel.Text = "0.0 dB";
    }

    private void OnMainFaderMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_draggingMainFader) return;
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
        {
            OnMainFaderMouseUp(sender, null!);
            return;
        }

        SetMainFaderFromMousePosition(e.GetPosition(_mainFaderSlider));
    }

    private void OnMainFaderMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs? e)
    {
        if (!_draggingMainFader) return;
        _draggingMainFader = false;

        if (sender is UIElement overlay)
            overlay.ReleaseMouseCapture();
    }

    private void SetMainFaderFromMousePosition(System.Windows.Point mousePos)
    {
        if (_mainFaderSlider == null) return;

        double ratio = 1.0 - Math.Clamp(mousePos.Y / _mainFaderSlider.ActualHeight, 0.0, 1.0);
        double value = _mainFaderSlider.Minimum + ratio * (_mainFaderSlider.Maximum - _mainFaderSlider.Minimum);
        int position = (int)Math.Clamp(value, _mainFaderSlider.Minimum, _mainFaderSlider.Maximum);

        _mainFaderSlider.Value = position;

        if (_config == null || _vm == null || _bridge == null) return;

        var currentView = _config.ChannelViews.ElementAtOrDefault(_bridge.CurrentViewIndex);
        if (currentView?.MainFaderChannel == null) return;

        int vmCh = currentView.MainFaderChannel.Value;
        if (!_config.Mappings.TryGetValue(vmCh, out var mapping) || mapping.Fader == null) return;

        double db = FaderControl.PositionToDb(position);
        db = Math.Clamp(db, mapping.Fader.Min, mapping.Fader.Max);
        _vm.SetParameter(mapping.Fader.Parameter, (float)db);

        if (_mainFaderDbLabel != null)
            _mainFaderDbLabel.Text = db <= -65 ? "-∞ dB" : $"{db:F1} dB";
    }
}
