using System.Text;
using System.Linq;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using XTouchVMBridge.Core.Models;

namespace XTouchVMBridge.App.Services;

public class MqttClientService : BackgroundService
{
    private readonly ILogger<MqttClientService> _logger;
    private readonly XTouchVMBridgeConfig _config;
    private readonly SemaphoreSlim _sync = new(1, 1);
    private IMqttClient? _client;

    public event EventHandler<MqttMessageReceivedEventArgs>? MessageReceived;

    public MqttClientService(ILogger<MqttClientService> logger, XTouchVMBridgeConfig config)
    {
        _logger = logger;
        _config = config;
    }

    public bool IsConnected => _client?.IsConnected == true;

    public async Task ReloadAsync()
    {
        await _sync.WaitAsync();
        try
        {
            await DisconnectAsync(CancellationToken.None);
            await ApplyConfigAsync();
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task<(bool Success, string? Error)> TestConnectionAsync(MqttConfig config, CancellationToken ct = default)
    {
        try
        {
            var factory = new MqttFactory();
            using var client = factory.CreateMqttClient();
            var options = BuildOptions(config);
            await client.ConnectAsync(options, ct);
            await client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), ct);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task PublishAsync(string topic, string payload, int? qos = null, bool? retain = null, CancellationToken ct = default)
    {
        if (_client?.IsConnected != true || string.IsNullOrWhiteSpace(topic))
            return;

        var mqtt = _config.Mqtt;
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic.Trim())
            .WithPayload(payload ?? string.Empty)
            .WithQualityOfServiceLevel(ToQos(qos ?? mqtt.PublishQos))
            .WithRetainFlag(retain ?? mqtt.PublishRetain)
            .Build();

        await _client.PublishAsync(message, ct);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _sync.WaitAsync(stoppingToken);
            try
            {
                await ApplyConfigAsync();
            }
            finally
            {
                _sync.Release();
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            await DisconnectAsync(cancellationToken);
        }
        finally
        {
            _sync.Release();
        }

        await base.StopAsync(cancellationToken);
    }

    private async Task ApplyConfigAsync()
    {
        var mqtt = _config.Mqtt;
        mqtt.Normalize();

        if (!mqtt.Enabled)
        {
            if (_client?.IsConnected == true)
                await DisconnectAsync(CancellationToken.None);
            return;
        }

        if (_client?.IsConnected == true)
            return;

        _client ??= CreateClient();

        var options = BuildOptions(mqtt);
        _logger.LogInformation("MQTT: Verbinde zu {Host}:{Port} (TLS={Tls}, ClientId={ClientId}, Username={UsernameSet})",
            mqtt.Host,
            mqtt.Port,
            mqtt.UseTls,
            mqtt.ClientId,
            string.IsNullOrWhiteSpace(mqtt.Username) ? "<none>" : "<set>");
        if (mqtt.UseTls && mqtt.AllowUntrustedCertificates)
            _logger.LogWarning("MQTT: Unsichere TLS-Einstellung aktiv (AllowUntrustedCertificates=true).");
        await _client.ConnectAsync(options, CancellationToken.None);

        var allSubscribeTopics = GetAllSubscribeTopics(mqtt).ToList();
        if (allSubscribeTopics.Count > 0)
        {
            var subscribeBuilder = new MqttClientSubscribeOptionsBuilder();
            foreach (var topic in allSubscribeTopics)
            {
                subscribeBuilder.WithTopicFilter(filter =>
                {
                    filter.WithTopic(topic)
                        .WithQualityOfServiceLevel(ToQos(mqtt.SubscribeQos));
                });
            }

            var subscribeOptions = subscribeBuilder.Build();
            await _client.SubscribeAsync(subscribeOptions, CancellationToken.None);
            _logger.LogInformation("MQTT: Abonniert {Count} Topics.", allSubscribeTopics.Count);
        }
    }

    private async Task DisconnectAsync(CancellationToken ct)
    {
        if (_client?.IsConnected == true)
        {
            await _client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), ct);
            _logger.LogInformation("MQTT: Verbindung getrennt.");
        }
    }

    private IMqttClient CreateClient()
    {
        var factory = new MqttFactory();
        var client = factory.CreateMqttClient();

        client.ApplicationMessageReceivedAsync += e =>
        {
            var segment = e.ApplicationMessage.PayloadSegment;
            var payload = segment.Array == null
                ? string.Empty
                : Encoding.UTF8.GetString(segment.Array, segment.Offset, segment.Count);
            MessageReceived?.Invoke(this, new MqttMessageReceivedEventArgs(
                e.ApplicationMessage.Topic,
                payload,
                (int)e.ApplicationMessage.QualityOfServiceLevel,
                e.ApplicationMessage.Retain));
            _logger.LogDebug("MQTT: Nachricht empfangen: {Topic} ({Length} bytes)", e.ApplicationMessage.Topic, payload.Length);
            return Task.CompletedTask;
        };

        client.DisconnectedAsync += e =>
        {
            _logger.LogWarning("MQTT: Verbindung getrennt: {Reason}", e.ReasonString);
            return Task.CompletedTask;
        };

        client.ConnectedAsync += _ =>
        {
            _logger.LogInformation("MQTT: Verbunden.");
            return Task.CompletedTask;
        };

        return client;
    }

    private static MqttClientOptions BuildOptions(MqttConfig config)
    {
        var builder = new MqttClientOptionsBuilder()
            .WithClientId(config.ClientId)
            .WithTcpServer(config.Host, config.Port)
            .WithCleanSession(config.CleanSession)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(config.KeepAliveSeconds));

        if (!string.IsNullOrWhiteSpace(config.Username))
            builder.WithCredentials(config.Username, config.Password);

        if (config.UseTls)
        {
            builder.WithTlsOptions(options =>
            {
                options.UseTls();
                if (config.AllowUntrustedCertificates)
                {
                    options.WithCertificateValidationHandler(_ => true);
                }
            });
        }

        return builder.Build();
    }

    private static MqttQualityOfServiceLevel ToQos(int qos) => qos switch
    {
        1 => MqttQualityOfServiceLevel.AtLeastOnce,
        2 => MqttQualityOfServiceLevel.ExactlyOnce,
        _ => MqttQualityOfServiceLevel.AtMostOnce
    };

    private IEnumerable<string> GetAllSubscribeTopics(MqttConfig mqtt)
    {
        var topics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var topic in mqtt.SubscribeTopics)
        {
            if (!string.IsNullOrWhiteSpace(topic))
                topics.Add(topic.Trim());
        }

        foreach (var (_, mapping) in _config.Mappings)
        {
            foreach (var (_, buttonMapping) in mapping.Buttons)
            {
                var ledCfg = buttonMapping?.MqttLedReceive;
                if (ledCfg?.Enabled != true || string.IsNullOrWhiteSpace(ledCfg.Topic))
                    continue;

                topics.Add(ledCfg.Topic.Trim());
            }
        }

        foreach (var (_, action) in _config.MasterButtonActions)
        {
            if (!action.MqttLedEnabled || string.IsNullOrWhiteSpace(action.MqttLedTopic))
                continue;
            topics.Add(action.MqttLedTopic.Trim());
        }

        return topics;
    }
}

public record MqttMessageReceivedEventArgs(string Topic, string Payload, int Qos, bool Retain);
