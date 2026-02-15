namespace XTouchVMBridge.Core.Models;

public class MqttConfig
{
    public bool Enabled { get; set; } = false;
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public bool UseTls { get; set; } = false;
    public bool AllowUntrustedCertificates { get; set; } = false;
    public string ClientId { get; set; } = $"XTouchVMBridge-{Environment.MachineName}";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public int KeepAliveSeconds { get; set; } = 30;
    public bool CleanSession { get; set; } = true;
    public string PublishTopic { get; set; } = "xtouchvmbridge/events";
    public int PublishQos { get; set; } = 0;
    public bool PublishRetain { get; set; } = false;
    public List<string> SubscribeTopics { get; set; } = new();
    public int SubscribeQos { get; set; } = 0;

    public void Normalize()
    {
        Host = string.IsNullOrWhiteSpace(Host) ? "localhost" : Host.Trim();
        Port = Port <= 0 ? (UseTls ? 8883 : 1883) : Port;
        KeepAliveSeconds = KeepAliveSeconds <= 0 ? 30 : KeepAliveSeconds;
        ClientId = string.IsNullOrWhiteSpace(ClientId) ? $"XTouchVMBridge-{Environment.MachineName}" : ClientId.Trim();
        PublishTopic = string.IsNullOrWhiteSpace(PublishTopic) ? "xtouchvmbridge/events" : PublishTopic.Trim();
        SubscribeTopics = SubscribeTopics
            .Where(topic => !string.IsNullOrWhiteSpace(topic))
            .Select(topic => topic.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        PublishQos = ClampQos(PublishQos);
        SubscribeQos = ClampQos(SubscribeQos);
    }

    private static int ClampQos(int qos) => Math.Clamp(qos, 0, 2);
}
