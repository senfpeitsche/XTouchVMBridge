using System.Text.Json;
using System.Text.Json.Serialization;
using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Interfaces;
using XTouchVMBridge.Core.Models;
using Microsoft.Extensions.Logging;

namespace XTouchVMBridge.Voicemeeter.Services;

public class ConfigurationService : IConfigurationService
{
    public const int CurrentConfigVersion = 1;

    private readonly ILogger<ConfigurationService> _logger;
    private readonly string _configPath;
    private readonly string _backupConfigPath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public ConfigurationService(ILogger<ConfigurationService> logger, string? configPath = null)
    {
        _logger = logger;
        _configPath = Path.GetFullPath(string.IsNullOrWhiteSpace(configPath)
            ? GetDefaultConfigPath()
            : configPath);
        _backupConfigPath = GetBackupPath(_configPath);
    }

    public XTouchVMBridgeConfig Load()
    {
        var resolvedConfigPath = Path.GetFullPath(_configPath);
        var resolvedBackupPath = Path.GetFullPath(_backupConfigPath);
        _logger.LogInformation("Lade Konfiguration aus: {ConfigPath}", resolvedConfigPath);
        _logger.LogInformation("Backup-Konfiguration: {BackupConfigPath}", resolvedBackupPath);

        if (!File.Exists(_configPath))
        {
            if (File.Exists(_backupConfigPath) && TryLoadConfigFromPath(_backupConfigPath, out var backupConfig))
            {
                _logger.LogWarning("config.json fehlt. Wiederherstellung aus Backup: {BackupConfigPath}", resolvedBackupPath);
                Save(backupConfig!);
                return backupConfig!;
            }

            _logger.LogInformation("Keine config.json gefunden unter {ConfigPath} - erstelle Standardkonfiguration.", resolvedConfigPath);
            var defaultConfig = CreateDefault();
            Save(defaultConfig);
            return defaultConfig;
        }

        try
        {
            if (!TryLoadConfigFromPath(_configPath, out var config))
                throw new InvalidDataException("config.json konnte nicht deserialisiert werden.");
            EnsureBackupIsUpToDate(config!);

            _logger.LogInformation("Konfiguration geladen: {Count} Kanaele.", config!.Channels.Count);
            return config!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der config.json.");

            if (File.Exists(_backupConfigPath) && TryLoadConfigFromPath(_backupConfigPath, out var backupConfig))
            {
                _logger.LogWarning("Verwende Backup-Konfiguration und stelle config.json wieder her.");
                Save(backupConfig!);
                return backupConfig!;
            }

            _logger.LogWarning("Backup nicht vorhanden oder ungueltig - verwende Standardkonfiguration.");
            var defaultConfig = CreateDefault();
            Save(defaultConfig);
            return defaultConfig;
        }
    }

    public void Save(XTouchVMBridgeConfig config)
    {
        try
        {
            var resolvedConfigPath = _configPath;
            var configDirectory = Path.GetDirectoryName(resolvedConfigPath);
            if (!string.IsNullOrWhiteSpace(configDirectory))
                Directory.CreateDirectory(configDirectory);

            string json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(_configPath, json);
            File.WriteAllText(_backupConfigPath, json);
            _logger.LogInformation("Konfiguration gespeichert: {ConfigPath}", resolvedConfigPath);
            _logger.LogInformation("Backup gespeichert: {BackupConfigPath}", _backupConfigPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Speichern der config.json.");
        }
    }

    public XTouchVMBridgeConfig CreateDefault()
    {
        var config = new XTouchVMBridgeConfig
        {
            ConfigVersion = CurrentConfigVersion,
            VoicemeeterApiType = "potato",
            DeviceMonitorIntervalMs = 5000,
            EnableXTouch = true,
            UiLanguage = "de",
            Channels = new Dictionary<int, ChannelConfig>
            {
                [0] = new() { Name = "WaveMIC", Type = "Hardware Input 1", Color = XTouchColor.Green },
                [1] = new() { Name = "RiftMIC", Type = "Hardware Input 2", Color = XTouchColor.Green },
                [2] = new() { Name = "HW In 3", Type = "Hardware Input 3", Color = XTouchColor.Blue },
                [3] = new() { Name = "V Mic", Type = "Virtual Input 1", Color = XTouchColor.Yellow },
                [4] = new() { Name = "Discord", Type = "Virtual Input 2", Color = XTouchColor.Magenta },
                [5] = new() { Name = "System", Type = "Virtual Input 3", Color = XTouchColor.Cyan },
                [6] = new() { Name = "Game", Type = "Virtual Input 4", Color = XTouchColor.Red },
                [7] = new() { Name = "Music", Type = "Virtual Input 5", Color = XTouchColor.Blue },

                [8] = new() { Name = "V Mic", Type = "Physical Bus A1", Color = XTouchColor.Yellow },
                [9] = new() { Name = "Speaker", Type = "Physical Bus A2", Color = XTouchColor.White },
                [10] = new() { Name = "BTHead", Type = "Physical Bus A3", Color = XTouchColor.Cyan },
                [11] = new() { Name = "Rift", Type = "Physical Bus A4", Color = XTouchColor.Green },
                [12] = new() { Name = "Mix", Type = "Virtual Bus B1", Color = XTouchColor.Red },
                [13] = new() { Name = "Cable", Type = "Virtual Bus B2", Color = XTouchColor.Magenta },
                [14] = new() { Name = "Discrd", Type = "Virtual Bus B3", Color = XTouchColor.Magenta },
                [15] = new() { Name = "Record", Type = "Virtual Bus B4", Color = XTouchColor.White }
            },
            Mappings = CreateDefaultMappings(),
            ChannelViews = CreateDefaultChannelViews()
        };

        return config;
    }

    public static Dictionary<int, ControlMappingConfig> CreateDefaultMappings()
    {
        var mappings = new Dictionary<int, ControlMappingConfig>();

        for (int i = 0; i < 8; i++)
        {
            mappings[i] = new ControlMappingConfig
            {
                Fader = new FaderMappingConfig
                {
                    Parameter = $"Strip[{i}].Gain",
                    Min = -60, Max = 12, Step = 0.1
                },
                Buttons = new Dictionary<string, ButtonMappingConfig?>
                {
                    ["Mute"] = new() { Parameter = $"Strip[{i}].Mute" },
                    ["Solo"] = new() { Parameter = $"Strip[{i}].Solo" },
                    ["Rec"] = null,
                    ["Select"] = null
                },
                EncoderFunctions = new List<EncoderFunctionConfig>
                {
                    new() { Label = "HIGH", Parameter = $"Strip[{i}].EQGain3", Min = -12, Max = 12, Step = 0.5, Unit = "dB" },
                    new() { Label = "MID", Parameter = $"Strip[{i}].EQGain2", Min = -12, Max = 12, Step = 0.5, Unit = "dB" },
                    new() { Label = "LOW", Parameter = $"Strip[{i}].EQGain1", Min = -12, Max = 12, Step = 0.5, Unit = "dB" },
                    new() { Label = "PAN", Parameter = $"Strip[{i}].Pan_x", Min = -0.5, Max = 0.5, Step = 0.05, Unit = "" },
                    new() { Label = "GAIN", Parameter = $"Strip[{i}].Gain", Min = -60, Max = 12, Step = 0.5, Unit = "dB" }
                }
            };
        }

        for (int i = 0; i < 8; i++)
        {
            mappings[i + 8] = new ControlMappingConfig
            {
                Fader = new FaderMappingConfig
                {
                    Parameter = $"Bus[{i}].Gain",
                    Min = -60, Max = 12, Step = 0.1
                },
                Buttons = new Dictionary<string, ButtonMappingConfig?>
                {
                    ["Mute"] = new() { Parameter = $"Bus[{i}].Mute" },
                    ["Solo"] = null,
                    ["Rec"] = null,
                    ["Select"] = null
                },
                EncoderFunctions = new List<EncoderFunctionConfig>()
            };
        }

        return mappings;
    }

    public static List<ChannelViewConfig> CreateDefaultChannelViews()
    {
        return new List<ChannelViewConfig>
        {
            new() { Name = "Home", Channels = new[] { 3, 4, 5, 6, 7, 9, 10, 12 }, MainFaderChannel = 12 },
            new() { Name = "Outputs", Channels = new[] { 8, 9, 10, 11, 12, 13, 14, 15 }, MainFaderChannel = null },
            new() { Name = "Inputs", Channels = new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, MainFaderChannel = null }
        };
    }

    private static string GetDefaultConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "XTouchVMBridge", "config.json");
    }

    private static string GetBackupPath(string configPath)
    {
        var directory = Path.GetDirectoryName(configPath) ?? string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(configPath);
        var extension = Path.GetExtension(configPath);
        return Path.Combine(directory, $"{fileNameWithoutExtension}.backup{extension}");
    }

    private bool TryLoadConfigFromPath(string path, out XTouchVMBridgeConfig? config)
    {
        config = null;

        try
        {
            string json = File.ReadAllText(path);
            config = JsonSerializer.Deserialize<XTouchVMBridgeConfig>(json, JsonOptions);
            if (config == null)
            {
                _logger.LogWarning("Konfigurationsdatei konnte nicht deserialisiert werden: {ConfigPath}", path);
                return false;
            }

            bool migrated = ApplyMigrations(config);
            ValidateConfig(config);
            if (migrated)
                Save(config);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Konfigurationsdatei ungueltig oder nicht lesbar: {ConfigPath}", path);
            return false;
        }
    }

    private void EnsureBackupIsUpToDate(XTouchVMBridgeConfig config)
    {
        try
        {
            string json = JsonSerializer.Serialize(config, JsonOptions);

            if (!File.Exists(_backupConfigPath))
            {
                File.WriteAllText(_backupConfigPath, json);
                _logger.LogInformation("Backup-Konfiguration erstellt: {BackupConfigPath}", _backupConfigPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Backup-Konfiguration konnte nicht erstellt werden.");
        }
    }

    private bool ApplyMigrations(XTouchVMBridgeConfig config)
    {
        if (config.ConfigVersion > CurrentConfigVersion)
        {
            _logger.LogWarning(
                "Konfigurationsversion {ConfigVersion} ist neuer als diese App ({CurrentVersion}). Es werden keine Migrationsschritte angewendet.",
                config.ConfigVersion,
                CurrentConfigVersion);
            return false;
        }

        bool migrated = false;
        int version = config.ConfigVersion;

        if (version < 1)
        {
            config.Channels ??= new Dictionary<int, ChannelConfig>();
            config.Mappings ??= new Dictionary<int, ControlMappingConfig>();
            config.ChannelViews ??= new List<ChannelViewConfig>();
            config.MasterButtonActions ??= new Dictionary<int, MasterButtonActionConfig>();
            config.Mqtt ??= new MqttConfig();
            config.UiLanguage ??= "de";
            version = 1;
            migrated = true;
            _logger.LogInformation("Konfigurationsmigration v0 -> v1 angewendet.");
        }

        config.ConfigVersion = version;
        return migrated;
    }

    private void ValidateConfig(XTouchVMBridgeConfig config)
    {
        if (config.ConfigVersion <= 0)
            config.ConfigVersion = CurrentConfigVersion;

        config.Channels ??= new Dictionary<int, ChannelConfig>();
        config.Mappings ??= new Dictionary<int, ControlMappingConfig>();
        config.ChannelViews ??= new List<ChannelViewConfig>();
        config.MasterButtonActions ??= new Dictionary<int, MasterButtonActionConfig>();

        config.VoicemeeterDllPath = string.IsNullOrWhiteSpace(config.VoicemeeterDllPath)
            ? null
            : config.VoicemeeterDllPath.Trim();

        config.Mqtt ??= new MqttConfig();
        config.Mqtt.Normalize();
        config.UiLanguage = NormalizeUiLanguage(config.UiLanguage);

        foreach (var (key, channel) in config.Channels)
        {
            channel.Name ??= "       ";
            channel.Type ??= string.Empty;

            if (channel.Name.Length > 7)
            {
                _logger.LogWarning("Kanalname '{Name}' zu lang (max 7). Wird gekuerzt.", channel.Name);
                channel.Name = channel.Name[..7];
            }

            if (!Enum.IsDefined(channel.Color))
            {
                _logger.LogWarning("Ungueltige Farbe fuer Kanal {Key}. Setze auf Off.", key);
                channel.Color = XTouchColor.Off;
            }
        }

        var defaults = CreateDefault();
        for (int i = 0; i < 16; i++)
        {
            if (!config.Channels.ContainsKey(i) && defaults.Channels.ContainsKey(i))
            {
                config.Channels[i] = defaults.Channels[i];
                _logger.LogDebug("Kanal {Channel} mit Standardwert ergaenzt.", i);
            }
        }

        if (config.Mappings.Count == 0)
        {
            _logger.LogInformation("Keine Mappings in config.json - verwende Standard-Mappings.");
            config.Mappings = CreateDefaultMappings();
        }
        else
        {
            var defaultMappings = CreateDefaultMappings();
            for (int i = 0; i < 16; i++)
            {
                if (!config.Mappings.ContainsKey(i) && defaultMappings.ContainsKey(i))
                {
                    config.Mappings[i] = defaultMappings[i];
                    _logger.LogDebug("Mapping fuer Kanal {Channel} mit Standard ergaenzt.", i);
                }
            }

            foreach (var (_, mapping) in config.Mappings)
            {
                mapping.Buttons ??= new Dictionary<string, ButtonMappingConfig?>();
                mapping.EncoderFunctions ??= new List<EncoderFunctionConfig>();

                foreach (var fn in mapping.EncoderFunctions)
                {
                    fn.Label ??= string.Empty;
                    if (fn.Label.Length > 7)
                        fn.Label = fn.Label[..7];
                }

                foreach (var (_, buttonMapping) in mapping.Buttons)
                {
                    if (buttonMapping == null)
                        continue;

                    if (!Enum.IsDefined(buttonMapping.ActionType))
                        buttonMapping.ActionType = ButtonActionType.VmParameter;

                    if (buttonMapping.MqttPublish != null)
                    {
                        buttonMapping.MqttPublish.Topic = buttonMapping.MqttPublish.Topic?.Trim() ?? string.Empty;
                        buttonMapping.MqttPublish.PayloadPressed ??= string.Empty;
                        buttonMapping.MqttPublish.PayloadReleased ??= string.Empty;
                        buttonMapping.MqttPublish.Qos = Math.Clamp(buttonMapping.MqttPublish.Qos, 0, 2);
                    }

                    if (buttonMapping.MqttLedReceive != null)
                    {
                        buttonMapping.MqttLedReceive.Topic = buttonMapping.MqttLedReceive.Topic?.Trim() ?? string.Empty;
                        buttonMapping.MqttLedReceive.PayloadOn ??= "on";
                        buttonMapping.MqttLedReceive.PayloadOff ??= "off";
                        buttonMapping.MqttLedReceive.PayloadBlink ??= "blink";
                        buttonMapping.MqttLedReceive.PayloadToggle ??= "toggle";
                    }
                }
            }
        }

        if (config.ChannelViews.Count == 0)
        {
            _logger.LogInformation("Keine ChannelViews in config.json - verwende Standard-Views.");
            config.ChannelViews = CreateDefaultChannelViews();
        }
        else
        {
            foreach (var view in config.ChannelViews)
            {
                view.Name ??= "View";
                view.Channels ??= Array.Empty<int>();

                if (view.Name.Length > 7)
                {
                    _logger.LogWarning("View-Name '{Name}' zu lang (max 7). Wird gekuerzt.", view.Name);
                    view.Name = view.Name[..7];
                }

                if (view.Channels.Length != 8)
                {
                    _logger.LogWarning("View '{Name}' hat {Count} Kanaele statt 8. Wird auf 8 angepasst.",
                        view.Name, view.Channels.Length);
                    var fixed8 = new int[8];
                    for (int i = 0; i < 8; i++)
                        fixed8[i] = i < view.Channels.Length ? view.Channels[i] : i;
                    view.Channels = fixed8;
                }

                for (int i = 0; i < view.Channels.Length; i++)
                    view.Channels[i] = Math.Clamp(view.Channels[i], 0, 15);

                if (view.MainFaderChannel.HasValue)
                    view.MainFaderChannel = Math.Clamp(view.MainFaderChannel.Value, 0, 15);
            }
        }

        foreach (var (_, action) in config.MasterButtonActions)
        {
            if (!Enum.IsDefined(typeof(MasterVmLedSource), action.VmLedSource))
                action.VmLedSource = MasterVmLedSource.ManualFeedback;
            action.MqttQos = Math.Clamp(action.MqttQos, 0, 2);
            action.MqttTopic = action.MqttTopic?.Trim();
            action.MqttPayloadPressed ??= string.Empty;
            action.MqttPayloadReleased ??= string.Empty;
            action.MqttDeviceId = action.MqttDeviceId?.Trim();
            action.MqttDeviceCommandTopic = action.MqttDeviceCommandTopic?.Trim();
            action.MqttTransportCommand = string.IsNullOrWhiteSpace(action.MqttTransportCommand)
                ? "play_pause"
                : action.MqttTransportCommand.Trim().ToLowerInvariant();
            action.MqttLedTopic = action.MqttLedTopic?.Trim();
            action.MqttLedPayloadOn ??= "on";
            action.MqttLedPayloadOff ??= "off";
            action.MqttLedPayloadBlink ??= "blink";
            action.MqttLedPayloadToggle ??= "toggle";
        }
    }

    private static string NormalizeUiLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return "de";

        var normalized = language.Trim().ToLowerInvariant();
        if (normalized.StartsWith("en"))
            return "en";
        return "de";
    }
}
