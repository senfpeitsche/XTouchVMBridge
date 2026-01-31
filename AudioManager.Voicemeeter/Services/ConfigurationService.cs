using System.Text.Json;
using System.Text.Json.Serialization;
using AudioManager.Core.Enums;
using AudioManager.Core.Interfaces;
using AudioManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace AudioManager.Voicemeeter.Services;

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

    public AudioManagerConfig Load()
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
            var config = JsonSerializer.Deserialize<AudioManagerConfig>(json, JsonOptions);

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

    public void Save(AudioManagerConfig config)
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

    public AudioManagerConfig CreateDefault()
    {
        var config = new AudioManagerConfig
        {
            VoicemeeterApiType = "potato",
            DeviceMonitorIntervalMs = 5000,
            EnableXTouch = true,
            EnableFantom = true,
            Channels = new Dictionary<int, ChannelConfig>
            {
                // Input Strips (0–7)
                [0] = new() { Name = "WaveMIC", Type = "Hardware Input 1", Color = XTouchColor.Green },
                [1] = new() { Name = "RiftMIC", Type = "Hardware Input 2", Color = XTouchColor.Green },
                [2] = new() { Name = "Fantom",  Type = "Hardware Input 3", Color = XTouchColor.Blue },
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
            }
        };

        return config;
    }

    private void ValidateConfig(AudioManagerConfig config)
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
    }
}
