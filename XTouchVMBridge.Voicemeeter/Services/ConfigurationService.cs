using System.Text.Json;
using System.Text.Json.Serialization;
using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Interfaces;
using XTouchVMBridge.Core.Models;
using Microsoft.Extensions.Logging;

namespace XTouchVMBridge.Voicemeeter.Services;

/// <summary>
/// Laden und Speichern der config.json.
/// Entspricht XtouchVMconfig.py (Config-Klasse) aus dem Python-Original.
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;
    private readonly string _configPath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public ConfigurationService(ILogger<ConfigurationService> logger, string configPath = "config.json")
    {
        _logger = logger;
        _configPath = configPath;
    }

    public XTouchVMBridgeConfig Load()
    {
        if (!File.Exists(_configPath))
        {
            _logger.LogInformation("Keine config.json gefunden — erstelle Standardkonfiguration.");
            var defaultConfig = CreateDefault();
            Save(defaultConfig);
            return defaultConfig;
        }

        try
        {
            string json = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize<XTouchVMBridgeConfig>(json, JsonOptions);

            if (config == null)
            {
                _logger.LogWarning("config.json konnte nicht deserialisiert werden — verwende Standard.");
                return CreateDefault();
            }

            ValidateConfig(config);
            _logger.LogInformation("Konfiguration geladen: {Count} Kanäle.", config.Channels.Count);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der config.json — verwende Standard.");
            return CreateDefault();
        }
    }

    public void Save(XTouchVMBridgeConfig config)
    {
        try
        {
            string json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(_configPath, json);
            _logger.LogInformation("Konfiguration gespeichert.");
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
            VoicemeeterApiType = "potato",
            DeviceMonitorIntervalMs = 5000,
            EnableXTouch = true,
            Channels = new Dictionary<int, ChannelConfig>
            {
                // Input Strips (0–7)
                [0] = new() { Name = "WaveMIC", Type = "Hardware Input 1", Color = XTouchColor.Green },
                [1] = new() { Name = "RiftMIC", Type = "Hardware Input 2", Color = XTouchColor.Green },
                [2] = new() { Name = "HW In 3", Type = "Hardware Input 3", Color = XTouchColor.Blue },
                [3] = new() { Name = "V Mic",   Type = "Virtual Input 1",  Color = XTouchColor.Yellow },
                [4] = new() { Name = "Discord", Type = "Virtual Input 2",  Color = XTouchColor.Magenta },
                [5] = new() { Name = "System",  Type = "Virtual Input 3",  Color = XTouchColor.Cyan },
                [6] = new() { Name = "Game",    Type = "Virtual Input 4",  Color = XTouchColor.Red },
                [7] = new() { Name = "Music",   Type = "Virtual Input 5",  Color = XTouchColor.Blue },

                // Output Buses (8–15)
                [8]  = new() { Name = "V Mic",   Type = "Physical Bus A1", Color = XTouchColor.Yellow },
                [9]  = new() { Name = "Speaker", Type = "Physical Bus A2", Color = XTouchColor.White },
                [10] = new() { Name = "BTHead",  Type = "Physical Bus A3", Color = XTouchColor.Cyan },
                [11] = new() { Name = "Rift",    Type = "Physical Bus A4", Color = XTouchColor.Green },
                [12] = new() { Name = "Mix",     Type = "Virtual Bus B1",  Color = XTouchColor.Red },
                [13] = new() { Name = "Cable",   Type = "Virtual Bus B2",  Color = XTouchColor.Magenta },
                [14] = new() { Name = "Discrd",  Type = "Virtual Bus B3",  Color = XTouchColor.Magenta },
                [15] = new() { Name = "Record",  Type = "Virtual Bus B4",  Color = XTouchColor.White }
            },
            Mappings = CreateDefaultMappings(),
            ChannelViews = CreateDefaultChannelViews()
        };

        return config;
    }

    /// <summary>
    /// Erzeugt Standard-Mappings für alle 16 Kanäle.
    /// Reproduziert das bisherige hardcoded Verhalten aus VoicemeeterBridge.
    /// </summary>
    public static Dictionary<int, ControlMappingConfig> CreateDefaultMappings()
    {
        var mappings = new Dictionary<int, ControlMappingConfig>();

        // Input Strips (0–7)
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
                    ["Mute"]   = new() { Parameter = $"Strip[{i}].Mute" },
                    ["Solo"]   = new() { Parameter = $"Strip[{i}].Solo" },
                    ["Rec"]    = null,
                    ["Select"] = null
                },
                EncoderFunctions = new List<EncoderFunctionConfig>
                {
                    new() { Label = "HIGH", Parameter = $"Strip[{i}].EQGain3", Min = -12, Max = 12, Step = 0.5, Unit = "dB" },
                    new() { Label = "MID",  Parameter = $"Strip[{i}].EQGain2", Min = -12, Max = 12, Step = 0.5, Unit = "dB" },
                    new() { Label = "LOW",  Parameter = $"Strip[{i}].EQGain1", Min = -12, Max = 12, Step = 0.5, Unit = "dB" },
                    new() { Label = "PAN",  Parameter = $"Strip[{i}].Pan_x",   Min = -0.5, Max = 0.5, Step = 0.05, Unit = "" },
                    new() { Label = "GAIN", Parameter = $"Strip[{i}].Gain",    Min = -60, Max = 12, Step = 0.5, Unit = "dB" }
                }
            };
        }

        // Output Buses (8–15 → Bus[0]–Bus[7])
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
                    ["Mute"]   = new() { Parameter = $"Bus[{i}].Mute" },
                    ["Solo"]   = null,
                    ["Rec"]    = null,
                    ["Select"] = null
                },
                EncoderFunctions = new List<EncoderFunctionConfig>()
            };
        }

        return mappings;
    }

    /// <summary>
    /// Erzeugt Standard-Channel-Views (reproduziert bisheriges hardcoded Verhalten).
    /// </summary>
    public static List<ChannelViewConfig> CreateDefaultChannelViews()
    {
        return new List<ChannelViewConfig>
        {
            new() { Name = "Home",    Channels = new[] { 3, 4, 5, 6, 7, 9, 10, 12 }, MainFaderChannel = 12 },
            new() { Name = "Outputs", Channels = new[] { 8, 9, 10, 11, 12, 13, 14, 15 }, MainFaderChannel = null },
            new() { Name = "Inputs",  Channels = new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, MainFaderChannel = null }
        };
    }

    private void ValidateConfig(XTouchVMBridgeConfig config)
    {
        foreach (var (key, channel) in config.Channels)
        {
            // Name auf 7 Zeichen begrenzen
            if (channel.Name.Length > 7)
            {
                _logger.LogWarning("Kanalname '{Name}' zu lang (max 7). Wird gekürzt.", channel.Name);
                channel.Name = channel.Name[..7];
            }

            // Farbwert validieren
            if (!Enum.IsDefined(channel.Color))
            {
                _logger.LogWarning("Ungültige Farbe für Kanal {Key}. Setze auf Off.", key);
                channel.Color = XTouchColor.Off;
            }
        }

        // Fehlende Kanäle mit Standard auffüllen
        var defaults = CreateDefault();
        for (int i = 0; i < 16; i++)
        {
            if (!config.Channels.ContainsKey(i) && defaults.Channels.ContainsKey(i))
            {
                config.Channels[i] = defaults.Channels[i];
                _logger.LogDebug("Kanal {Channel} mit Standardwert ergänzt.", i);
            }
        }

        // Fehlende Mappings mit Standard auffüllen
        if (config.Mappings.Count == 0)
        {
            _logger.LogInformation("Keine Mappings in config.json — verwende Standard-Mappings.");
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
                    _logger.LogDebug("Mapping für Kanal {Channel} mit Standard ergänzt.", i);
                }
            }

            // Encoder-Labels auf 7 Zeichen begrenzen
            foreach (var (key, mapping) in config.Mappings)
            {
                foreach (var fn in mapping.EncoderFunctions)
                {
                    if (fn.Label.Length > 7)
                        fn.Label = fn.Label[..7];
                }
            }
        }

        // Channel Views validieren
        if (config.ChannelViews.Count == 0)
        {
            _logger.LogInformation("Keine ChannelViews in config.json — verwende Standard-Views.");
            config.ChannelViews = CreateDefaultChannelViews();
        }
        else
        {
            foreach (var view in config.ChannelViews)
            {
                // Name auf 7 Zeichen begrenzen
                if (view.Name.Length > 7)
                {
                    _logger.LogWarning("View-Name '{Name}' zu lang (max 7). Wird gekürzt.", view.Name);
                    view.Name = view.Name[..7];
                }

                // Channel-Array muss genau 8 Einträge haben
                if (view.Channels.Length != 8)
                {
                    _logger.LogWarning("View '{Name}' hat {Count} Kanäle statt 8. Wird auf 8 angepasst.",
                        view.Name, view.Channels.Length);
                    var fixed8 = new int[8];
                    for (int i = 0; i < 8; i++)
                        fixed8[i] = i < view.Channels.Length ? view.Channels[i] : i;
                    view.Channels = fixed8;
                }

                // Kanal-Indizes auf 0–15 begrenzen
                for (int i = 0; i < view.Channels.Length; i++)
                    view.Channels[i] = Math.Clamp(view.Channels[i], 0, 15);

                if (view.MainFaderChannel.HasValue)
                    view.MainFaderChannel = Math.Clamp(view.MainFaderChannel.Value, 0, 15);
            }
        }
    }
}
