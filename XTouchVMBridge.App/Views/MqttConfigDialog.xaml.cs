using System.Globalization;
using System.Linq;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using XTouchVMBridge.App.Services;
using XTouchVMBridge.Core.Interfaces;
using XTouchVMBridge.Core.Models;

namespace XTouchVMBridge.App.Views;

public partial class MqttConfigDialog : Window
{
    private readonly XTouchVMBridgeConfig _config;
    private readonly IConfigurationService _configService;
    private readonly MqttClientService? _mqttClientService;

    public MqttConfigDialog(XTouchVMBridgeConfig config, IConfigurationService configService, MqttClientService? mqttClientService)
    {
        InitializeComponent();
        _config = config;
        _configService = configService;
        _mqttClientService = mqttClientService;

        PublishQosBox.ItemsSource = new[] { "0", "1", "2" };
        SubscribeQosBox.ItemsSource = new[] { "0", "1", "2" };

        LoadFromConfig();
    }

    private void LoadFromConfig()
    {
        var mqtt = _config.Mqtt ?? new MqttConfig();
        mqtt.Normalize();

        EnabledBox.IsChecked = mqtt.Enabled;
        HostBox.Text = mqtt.Host;
        PortBox.Text = mqtt.Port.ToString(CultureInfo.InvariantCulture);
        UseTlsBox.IsChecked = mqtt.UseTls;
        AllowUntrustedBox.IsChecked = mqtt.AllowUntrustedCertificates;
        ClientIdBox.Text = mqtt.ClientId;
        UsernameBox.Text = mqtt.Username;
        PasswordBox.Password = mqtt.Password;
        KeepAliveBox.Text = mqtt.KeepAliveSeconds.ToString(CultureInfo.InvariantCulture);
        CleanSessionBox.IsChecked = mqtt.CleanSession;
        PublishTopicBox.Text = mqtt.PublishTopic;
        PublishQosBox.SelectedItem = mqtt.PublishQos.ToString(CultureInfo.InvariantCulture);
        PublishRetainBox.IsChecked = mqtt.PublishRetain;
        SubscribeTopicsBox.Text = string.Join(Environment.NewLine, mqtt.SubscribeTopics);
        SubscribeQosBox.SelectedItem = mqtt.SubscribeQos.ToString(CultureInfo.InvariantCulture);
        StatusText.Text = _mqttClientService?.IsConnected == true ? "Verbunden" : "Nicht verbunden";
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        var mqtt = BuildConfigFromForm();
        _config.Mqtt = mqtt;
        _configService.Save(_config);

        if (_mqttClientService != null)
        {
            await _mqttClientService.ReloadAsync();
            StatusText.Text = _mqttClientService.IsConnected ? "Verbunden" : "Nicht verbunden";
        }

        Close();
    }

    private async void OnTestConnection(object sender, RoutedEventArgs e)
    {
        if (_mqttClientService == null)
        {
            MessageBox.Show("MQTT Service nicht verfuegbar.", "MQTT Test",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var mqtt = BuildConfigFromForm();
        StatusText.Text = "Teste Verbindung...";
        var (success, error) = await _mqttClientService.TestConnectionAsync(mqtt);
        StatusText.Text = success ? "Test erfolgreich" : "Test fehlgeschlagen";
        MessageBox.Show(success ? "Verbindung erfolgreich." : $"Fehler: {error}", "MQTT Test",
            MessageBoxButton.OK, success ? MessageBoxImage.Information : MessageBoxImage.Error);
    }

    private MqttConfig BuildConfigFromForm()
    {
        var mqtt = new MqttConfig
        {
            Enabled = EnabledBox.IsChecked == true,
            Host = HostBox.Text.Trim(),
            Port = ParseInt(PortBox.Text, 1883),
            UseTls = UseTlsBox.IsChecked == true,
            AllowUntrustedCertificates = AllowUntrustedBox.IsChecked == true,
            ClientId = ClientIdBox.Text.Trim(),
            Username = UsernameBox.Text.Trim(),
            Password = PasswordBox.Password ?? "",
            KeepAliveSeconds = ParseInt(KeepAliveBox.Text, 30),
            CleanSession = CleanSessionBox.IsChecked == true,
            PublishTopic = PublishTopicBox.Text.Trim(),
            PublishQos = ParseInt(PublishQosBox.SelectedItem?.ToString(), 0),
            PublishRetain = PublishRetainBox.IsChecked == true,
            SubscribeTopics = SubscribeTopicsBox.Text
                .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(topic => topic.Trim())
                .Where(topic => !string.IsNullOrWhiteSpace(topic))
                .ToList(),
            SubscribeQos = ParseInt(SubscribeQosBox.SelectedItem?.ToString(), 0)
        };

        mqtt.Normalize();
        return mqtt;
    }

    private static int ParseInt(string? input, int fallback)
    {
        if (string.IsNullOrWhiteSpace(input))
            return fallback;

        return int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }
}


