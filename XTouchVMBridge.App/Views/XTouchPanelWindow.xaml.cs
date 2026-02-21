using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Hardware;
using XTouchVMBridge.Core.Interfaces;
using XTouchVMBridge.Core.Models;
using XTouchVMBridge.App.Services;
using XTouchVMBridge.Voicemeeter.Services;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using ProgressBar = System.Windows.Controls.ProgressBar;

namespace XTouchVMBridge.App.Views;

public partial class XTouchPanelWindow : Window
{
    private readonly IMidiDevice? _device;
    private readonly XTouchVMBridgeConfig? _config;
    private readonly IConfigurationService? _configService;
    private readonly VoicemeeterBridge? _bridge;
    private readonly IVoicemeeterService? _vm;
    private readonly MasterButtonActionService? _masterButtonActionService;
    private readonly MqttClientService? _mqttClientService;
    private readonly DispatcherTimer _refreshTimer;

    private readonly Border[] _displayPanels = new Border[8];
    private readonly TextBlock[] _displayTop = new TextBlock[8];
    private readonly TextBlock[] _displayBottom = new TextBlock[8];
    private readonly Button[] _encoderButtons = new Button[8];
    private readonly Grid[] _encoderRingContainers = new Grid[8];
    private readonly Border[] _encoderRingIndicators = new Border[8];
    private readonly Button[] _recButtons = new Button[8];
    private readonly Button[] _soloButtons = new Button[8];
    private readonly Button[] _muteButtons = new Button[8];
    private readonly Button[] _selectButtons = new Button[8];
    private readonly Slider[] _faderSliders = new Slider[8];
    private readonly TextBlock[] _faderDbLabels = new TextBlock[8];
    private readonly ProgressBar[] _levelMeters = new ProgressBar[8];
    private readonly Ellipse[] _touchIndicators = new Ellipse[8];

    private Slider? _mainFaderSlider;
    private TextBlock? _mainFaderDbLabel;
    private bool _draggingMainFader;

    private readonly Dictionary<string, Button> _masterButtons = new();

    private int _selectedVmChannel = -1;
    private string _selectedControlType = ""; // "Button", "Fader", "Encoder", "MasterButton"
    private XTouchButtonType _selectedButtonType;
    private int _selectedMasterButtonNote = -1;
    private bool _suppressMappingEvents;

    private string _activeDetailType = ""; // "Fader", "LevelMeter", "Encoder", "Button", "Master", ""
    private int _activeDetailChannel = -1;

    private int _draggingFaderChannel = -1; // -1 means no fader is currently mouse-driven

    private DateTime _lastFaderClickTime = DateTime.MinValue;
    private int _lastFaderClickChannel = -1;
    private const int DoubleTapThresholdMs = 400;

    private readonly Dictionary<(int Channel, XTouchButtonType Type), bool> _manualLedState = new();
    private readonly Dictionary<int, bool> _masterButtonLedState = new();
    private bool _panelRecorderActive;

    public XTouchPanelWindow() : this(null, null, null, null, null, null, null) { }

    public XTouchPanelWindow(IMidiDevice? device, XTouchVMBridgeConfig? config,
        IConfigurationService? configService = null, VoicemeeterBridge? bridge = null,
        IVoicemeeterService? vm = null, MasterButtonActionService? masterButtonActionService = null,
        MqttClientService? mqttClientService = null)
    {
        InitializeComponent();
        Icon = AppIconFactory.CreateWindowIcon();
        _device = device;
        _config = config;
        _configService = configService;
        _bridge = bridge;
        _vm = vm;
        _masterButtonActionService = masterButtonActionService;
        _mqttClientService = mqttClientService;

        BuildChannelStrips();
        BuildMainFader();
        BuildMasterSection();
        LocalizationService.LocalizeWindow(this);
        SubscribeToEvents();

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _refreshTimer.Tick += (_, _) => RefreshAll();
        _refreshTimer.Start();
    }


    private void SubscribeToEvents()
    {
        if (_device == null) return;

        _device.FaderChanged += (_, e) => Dispatcher.BeginInvoke(() => OnFaderUpdate(e.Channel));
        _device.ButtonChanged += (_, e) => Dispatcher.BeginInvoke(() => OnButtonUpdate(e.Channel, e.ButtonType, e.IsPressed));
        _device.EncoderRotated += (_, e) => Dispatcher.BeginInvoke(() => OnEncoderUpdate(e.Channel));
        _device.EncoderPressed += (_, e) => Dispatcher.BeginInvoke(() => OnEncoderPressUpdate(e.Channel, e.IsPressed));
        _device.FaderTouched += (_, e) => Dispatcher.BeginInvoke(() => OnFaderTouchUpdate(e.Channel, e.IsTouched));
    }


    private void RefreshAll()
    {
        if (_device == null) return;

        for (int ch = 0; ch < 8 && ch < _device.Channels.Count; ch++)
        {
            var xtCh = _device.Channels[ch];

            _displayTop[ch].Text = xtCh.Display.TopRow;
            _displayBottom[ch].Text = xtCh.Display.BottomRow;
            _displayPanels[ch].Background = new SolidColorBrush(XTouchColorToWpf(xtCh.Display.Color, dim: true));
            _displayTop[ch].Foreground = new SolidColorBrush(XTouchColorToWpf(xtCh.Display.Color, dim: false));

            UpdateEncoderRingVisual(ch, xtCh.Encoder);
            if (xtCh.Encoder.HasFunctions && xtCh.Encoder.ActiveFunction != null)
            {
                _encoderButtons[ch].Content = new TextBlock
                {
                    Text = xtCh.Encoder.ActiveFunction.Name,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 180, 0)),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 7, FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            if (_draggingFaderChannel != ch)
            {
                _faderSliders[ch].Value = xtCh.Fader.Position;
                double db = FaderControl.PositionToDb(xtCh.Fader.Position);
                _faderDbLabels[ch].Text = db <= -65 ? "-∞ dB" : $"{db:F1} dB";
            }

            _touchIndicators[ch].Fill = new SolidColorBrush(
                xtCh.Fader.IsTouched ? Color.FromRgb(0, 200, 255) : Color.FromRgb(40, 40, 40));

            _levelMeters[ch].Value = xtCh.LevelMeter.Level;
            _levelMeters[ch].Foreground = new SolidColorBrush(
                xtCh.LevelMeter.Level > 10 ? Color.FromRgb(255, 60, 30)
                : xtCh.LevelMeter.Level > 7 ? Color.FromRgb(255, 200, 0)
                : Color.FromRgb(0, 200, 80));

            UpdateButtonVisual(_recButtons[ch], GetEffectiveLedState(ch, XTouchButtonType.Rec, xtCh));
            UpdateButtonVisual(_soloButtons[ch], GetEffectiveLedState(ch, XTouchButtonType.Solo, xtCh));
            UpdateButtonVisual(_muteButtons[ch], GetEffectiveLedState(ch, XTouchButtonType.Mute, xtCh));
            UpdateButtonVisual(_selectButtons[ch], GetEffectiveLedState(ch, XTouchButtonType.Select, xtCh));
        }

        UpdateMainFaderVisual();

        if (_bridge != null)
        {
            ViewSwitchButton.Content = $"⚙ {_bridge.CurrentViewName}";

            if (_masterButtons.TryGetValue("Flip", out var flipBtn))
            {
                bool isActive = _bridge.CurrentViewIndex > 0;
                flipBtn.Background = new SolidColorBrush(isActive
            ? Color.FromRgb(120, 80, 160)   // active: brighter violet
            : Color.FromRgb(45, 37, 53));   // inactive: darker tone
            }
        }

        var now = DateTime.Now;
        SegmentDisplay.Text = now.ToString("HH : mm : ss");

        RefreshDetailLiveValues();
    }

    private void RefreshDetailLiveValues()
    {
        if (_device == null || _activeDetailChannel < 0 || _activeDetailChannel >= _device.Channels.Count)
            return;

        var ch = _activeDetailChannel;
        var xtCh = _device.Channels[ch];

        switch (_activeDetailType)
        {
            case "LevelMeter":
            {
                int level = xtCh.LevelMeter.Level;
                string bar = new string('█', level) + new string('░', 13 - level);
                DetailHeader.Text = $"Level Meter — Kanal {ch + 1}";
                DetailMidiInfo.Text =
                    $"Level: {level,2}/13  [{bar}]\n" +
                    $"Aftertouch: ({ch}<<4 | {level}) = {(ch << 4) | level}";
                break;
            }

            case "Fader":
            {
                var fader = xtCh.Fader;
                int pos = fader.Position;
                double db = FaderControl.PositionToDb(pos);
                string dbStr = db <= -65 ? "-∞" : $"{db:F1}";
                string bar = new string('█', Math.Max(0, (pos + 8192) * 20 / 16384)) + new string('░', Math.Max(0, 20 - (pos + 8192) * 20 / 16384));
                DetailMidiInfo.Text =
                    $"Position: {pos,6}  [{bar}]  {dbStr} dB\n" +
                    $"Touch: {(fader.IsTouched ? "Ja" : "Nein")}   PW: {pos + 8192}";
                break;
            }

            case "Encoder":
            {
                var enc = xtCh.Encoder;
                if (enc.HasFunctions && enc.ActiveFunction != null)
                {
                    DetailMidiInfo.Text =
                        $"Aktiv: {enc.ActiveFunction.Name} = {enc.ActiveFunction.FormatValue()}\n" +
                        $"Ring: {enc.RingPosition}/15   Gedrückt: {(enc.IsPressed ? "Ja" : "Nein")}";
                }
                else
                {
                    DetailMidiInfo.Text =
                        $"Ring: {enc.RingPosition}/15   Gedrückt: {(enc.IsPressed ? "Ja" : "Nein")}";
                }
                break;
            }

            case "MainFader":
            {
                if (_mainFaderSlider == null)
                    break;

                int pos = (int)_mainFaderSlider.Value;
                double db = FaderControl.PositionToDb(pos);
                string dbStr = db <= -65 ? "-inf" : $"{db:F1}";
                DetailMidiInfo.Text =
                    $"Position: {pos,6}\n" +
                    $"Wert:     {dbStr} dB\n" +
                    $"Touch:    {(_device.IsMainFaderTouched ? "Ja" : "Nein")}";
                break;
            }
        }
    }

    private void UpdateMainFaderVisual()
    {
        if (_draggingMainFader || _mainFaderSlider == null || _mainFaderDbLabel == null)
            return;
        if (_config == null || _vm == null || _bridge == null)
            return;
        if (_bridge.CurrentViewIndex < 0 || _bridge.CurrentViewIndex >= _config.ChannelViews.Count)
            return;

        var currentView = _config.ChannelViews[_bridge.CurrentViewIndex];
        if (!currentView.MainFaderChannel.HasValue)
            return;

        int vmCh = currentView.MainFaderChannel.Value;
        double db;

        if (_config.Mappings.TryGetValue(vmCh, out var mapping) &&
            mapping.Fader != null &&
            !string.IsNullOrWhiteSpace(mapping.Fader.Parameter))
        {
            db = _vm.GetParameter(mapping.Fader.Parameter);
        }
        else
        {
            string prefix = vmCh < 8 ? $"Strip[{vmCh}]" : $"Bus[{vmCh - 8}]";
            db = _vm.GetParameter($"{prefix}.Gain");
        }

        _mainFaderSlider.Value = FaderControl.DbToPosition(db);
        _mainFaderDbLabel.Text = db <= -65 ? "-inf dB" : $"{db:F1} dB";
    }


    private LedState GetEffectiveLedState(int ch, XTouchButtonType type, XTouchChannel xtCh)
    {
        var key = (ch, type);
        if (_manualLedState.TryGetValue(key, out bool isOn))
            return isOn ? LedState.On : LedState.Off;
        return xtCh.GetButton(type).LedState;
    }

    private void UpdateButtonVisual(Button btn, LedState state)
    {
        if (btn.Tag is not ButtonTag tag) return;
        var color = state switch
        {
            LedState.On => tag.ActiveColor,
            LedState.Blink => tag.ActiveColor,
            _ => tag.InactiveColor
        };
        btn.Background = new SolidColorBrush(color);
    }

    private record ButtonTag(int Channel, XTouchButtonType Type, Color ActiveColor, Color InactiveColor);


    private void OnFaderUpdate(int ch)
    {
        if (ch < 0 || ch >= 8 || _device == null) return;
        var xtCh = _device.Channels[ch];
        _faderSliders[ch].Value = xtCh.Fader.Position;
        double db = FaderControl.PositionToDb(xtCh.Fader.Position);
        _faderDbLabels[ch].Text = db <= -65 ? "-∞ dB" : $"{db:F1} dB";
    }

    private void OnButtonUpdate(int ch, XTouchButtonType type, bool pressed) { /* RefreshAll aktualisiert visuell */ }
    private void OnEncoderUpdate(int ch)
    {
        if (ch >= 0 && ch < 8 && _device != null)
            UpdateEncoderRingVisual(ch, _device.Channels[ch].Encoder);
    }
    private void OnEncoderPressUpdate(int ch, bool pressed) { }

    private void OnFaderTouchUpdate(int ch, bool touched)
    {
        if (ch >= 0 && ch < 8)
            _touchIndicators[ch].Fill = new SolidColorBrush(
                touched ? Color.FromRgb(0, 200, 255) : Color.FromRgb(40, 40, 40));
    }


    private int ResolveVmChannel(int xtChannel)
    {
        if (_bridge != null)
        {
            var mapping = _bridge.CurrentChannelMapping;
            if (xtChannel >= 0 && xtChannel < mapping.Length)
                return mapping[xtChannel];
        }

        return xtChannel;
    }

    private string GetConfigName(int ch)
    {
        return _device?.Channels[ch].Display.TopRow.TrimEnd() ?? $"Ch {ch + 1}";
    }

    private static Color XTouchColorToWpf(XTouchColor color, bool dim)
    {
        int factor = dim ? 40 : 255;
        return color switch
        {
            XTouchColor.Red => Color.FromRgb((byte)factor, 0, 0),
            XTouchColor.Green => Color.FromRgb(0, (byte)factor, 0),
            XTouchColor.Yellow => Color.FromRgb((byte)factor, (byte)(factor * 0.85), 0),
            XTouchColor.Blue => Color.FromRgb(0, (byte)(factor * 0.4), (byte)factor),
            XTouchColor.Magenta => Color.FromRgb((byte)factor, 0, (byte)factor),
            XTouchColor.Cyan => Color.FromRgb(0, (byte)factor, (byte)factor),
            XTouchColor.White => Color.FromRgb((byte)(factor * 0.8), (byte)(factor * 0.8), (byte)(factor * 0.8)),
            _ => Color.FromRgb(20, 20, 20)
        };
    }


    private void OnOpenChannelViewEditor(object sender, RoutedEventArgs e)
    {
        if (_config == null || _configService == null) return;

        var dialog = new ChannelViewEditorDialog(_config, _configService, _bridge, _vm)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private void OnOpenMqttConfig(object sender, RoutedEventArgs e)
    {
        if (_config == null || _configService == null) return;

        var dialog = new MqttConfigDialog(_config, _configService, _mqttClientService)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }


    protected override void OnClosed(EventArgs e)
    {
        _refreshTimer.Stop();
        base.OnClosed(e);
    }
}
