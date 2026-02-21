namespace XTouchVMBridge.Core.Models;

public static class VoicemeeterParameterCatalog
{
    public record ParameterTemplate(
        string Template,
        string DisplayName,
        double DefaultMin,
        double DefaultMax,
        double DefaultStep,
        string Unit);


    public static readonly List<ParameterTemplate> StripBoolParameters = new()
    {
        new("Strip[{s}].Mute",   "Mute",           0, 1, 1, ""),
        new("Strip[{s}].Solo",   "Solo",           0, 1, 1, ""),
        new("Strip[{s}].Mono",   "Mono",           0, 1, 1, ""),
        new("Strip[{s}].MC",     "Mix down Mono",  0, 1, 1, ""),
        new("Strip[{s}].EQ.on",  "EQ Ein/Aus",     0, 1, 1, ""),
        new("Strip[{s}].EQ.AB",  "EQ A/B",         0, 1, 1, ""),
        new("Strip[{s}].A1",     "Routing → A1",   0, 1, 1, ""),
        new("Strip[{s}].A2",     "Routing → A2",   0, 1, 1, ""),
        new("Strip[{s}].A3",     "Routing → A3",   0, 1, 1, ""),
        new("Strip[{s}].A4",     "Routing → A4",   0, 1, 1, ""),
        new("Strip[{s}].A5",     "Routing → A5",   0, 1, 1, ""),
        new("Strip[{s}].B1",     "Routing → B1",   0, 1, 1, ""),
        new("Strip[{s}].B2",     "Routing → B2",   0, 1, 1, ""),
        new("Strip[{s}].B3",     "Routing → B3",   0, 1, 1, ""),
        new("Strip[{s}].PostReverb",  "Reverb Post-Fader", 0, 1, 1, ""),
        new("Strip[{s}].PostDelay",   "Delay Post-Fader",  0, 1, 1, ""),
        new("Strip[{s}].PostFx1",     "FX1 Post-Fader",    0, 1, 1, ""),
        new("Strip[{s}].PostFx2",     "FX2 Post-Fader",    0, 1, 1, ""),
        new("Strip[{s}].Comp.MakeUp", "Comp MakeUp",       0, 1, 1, ""),
    };

    public static readonly List<ParameterTemplate> BusBoolParameters = new()
    {
        new("Bus[{b}].Mute",      "Mute",        0, 1, 1, ""),
        new("Bus[{b}].Mono",      "Mono",        0, 1, 1, ""),
        new("Bus[{b}].Sel",       "Select",      0, 1, 1, ""),
        new("Bus[{b}].EQ.on",     "EQ Ein/Aus",  0, 1, 1, ""),
        new("Bus[{b}].EQ.AB",     "EQ A/B",      0, 1, 1, ""),
        new("Bus[{b}].Monitor",   "Monitor",     0, 1, 1, ""),
    };


    public static readonly List<ParameterTemplate> StripFloatParameters = new()
    {
        new("Strip[{s}].Gain",       "Gain",        -60,   12,   0.5,  "dB"),
        new("Strip[{s}].Pan_x",      "Pan L/R",     -0.5,  0.5,  0.05, ""),
        new("Strip[{s}].Pan_y",      "Pan F/B",      0,    1.0,  0.05, ""),
        new("Strip[{s}].Audibility",  "Audibility",   0,   10,   0.5,  ""),
        new("Strip[{s}].Limit",       "Limiter",    -40,   12,   1.0,  "dB"),

        new("Strip[{s}].EQGain1",    "EQ Low",      -12,   12,   0.5,  "dB"),
        new("Strip[{s}].EQGain2",    "EQ Mid",      -12,   12,   0.5,  "dB"),
        new("Strip[{s}].EQGain3",    "EQ High",     -12,   12,   0.5,  "dB"),

        new("Strip[{s}].Reverb",     "Reverb",        0,   10,   0.5,  ""),
        new("Strip[{s}].Delay",      "Delay",         0,   10,   0.5,  ""),
        new("Strip[{s}].Fx1",        "FX1",           0,   10,   0.5,  ""),
        new("Strip[{s}].Fx2",        "FX2",           0,   10,   0.5,  ""),

        new("Strip[{s}].Color_x",    "Color L/R",   -0.5,  0.5,  0.05, ""),
        new("Strip[{s}].Color_y",    "Color F/B",     0,    1.0,  0.05, ""),
        new("Strip[{s}].fx_x",       "FX Pos L/R",  -0.5,  0.5,  0.05, ""),
        new("Strip[{s}].fx_y",       "FX Pos F/B",    0,    1.0,  0.05, ""),

        new("Strip[{s}].Gate",              "Gate Knob",       0,   10,    0.5,  ""),
        new("Strip[{s}].Gate.Threshold",    "Gate Threshold", -60,    0,   1.0,  "dB"),
        new("Strip[{s}].Gate.Damping",      "Gate Damping",   -60,    0,   1.0,  "dB"),
        new("Strip[{s}].Gate.BPSidechain",  "Gate Sidechain", 100, 4000,  50,    "Hz"),
        new("Strip[{s}].Gate.Attack",       "Gate Attack",    0.1, 1000,  10,    "ms"),
        new("Strip[{s}].Gate.Hold",         "Gate Hold",        0, 5000,  50,    "ms"),
        new("Strip[{s}].Gate.Release",      "Gate Release",   0.1, 5000,  50,    "ms"),

        new("Strip[{s}].Comp",              "Comp Knob",       0,   10,   0.5,  ""),
        new("Strip[{s}].Comp.GainIn",       "Comp GainIn",   -24,   24,   0.5,  "dB"),
        new("Strip[{s}].Comp.Threshold",    "Comp Threshold", -40,    0,   1.0,  "dB"),
        new("Strip[{s}].Comp.Ratio",        "Comp Ratio",     1.0,  8.0,  0.5,  ""),
        new("Strip[{s}].Comp.Attack",       "Comp Attack",    0.1,  200,   5.0, "ms"),
        new("Strip[{s}].Comp.Release",      "Comp Release",   0.1, 5000,  50,   "ms"),
        new("Strip[{s}].Comp.Knee",         "Comp Knee",        0,  1.0,  0.1,  ""),
        new("Strip[{s}].Comp.GainOut",      "Comp GainOut",  -24,   24,   0.5,  "dB"),

        new("Strip[{s}].Denoiser",          "Denoiser",        0,   10,   0.5,  ""),
    };

    public static readonly List<ParameterTemplate> BusFloatParameters = new()
    {
        new("Bus[{b}].Gain",          "Gain",          -60, 12,  0.5, "dB"),
        new("Bus[{b}].ReturnReverb",  "Return Reverb",   0, 10,  0.5, ""),
        new("Bus[{b}].ReturnDelay",   "Return Delay",    0, 10,  0.5, ""),
        new("Bus[{b}].ReturnFx1",     "Return FX1",      0, 10,  0.5, ""),
        new("Bus[{b}].ReturnFx2",     "Return FX2",      0, 10,  0.5, ""),
    };


    public static List<ResolvedParameter> GetBoolParameters(int vmChannel)
    {
        bool isStrip = vmChannel < 8;
        var templates = isStrip ? StripBoolParameters : BusBoolParameters;
        int index = isStrip ? vmChannel : vmChannel - 8;

        return templates.Select(t => new ResolvedParameter(
            ResolveTemplate(t.Template, vmChannel),
            t.DisplayName
        )).ToList();
    }

    public static List<ResolvedParameter> GetFloatParameters(int vmChannel)
    {
        bool isStrip = vmChannel < 8;
        var templates = isStrip ? StripFloatParameters : BusFloatParameters;
        int index = isStrip ? vmChannel : vmChannel - 8;

        return templates.Select(t => new ResolvedParameter(
            ResolveTemplate(t.Template, vmChannel),
            t.DisplayName,
            t.DefaultMin, t.DefaultMax, t.DefaultStep, t.Unit
        )).ToList();
    }

    public static ParameterTemplate? FindTemplate(string resolvedParam)
    {
        foreach (var list in new[] { StripFloatParameters, BusFloatParameters })
        {
            foreach (var t in list)
            {
                string pattern = t.Template
                    .Replace("{s}", @"\d+")
                    .Replace("{b}", @"\d+");
                if (System.Text.RegularExpressions.Regex.IsMatch(resolvedParam, $"^{pattern}$"))
                    return t;
            }
        }

        foreach (var list in new[] { StripBoolParameters, BusBoolParameters })
        {
            foreach (var t in list)
            {
                string pattern = t.Template
                    .Replace("{s}", @"\d+")
                    .Replace("{b}", @"\d+");
                if (System.Text.RegularExpressions.Regex.IsMatch(resolvedParam, $"^{pattern}$"))
                    return t;
            }
        }

        return null;
    }

    private static string ResolveTemplate(string template, int vmChannel)
    {
        bool isStrip = vmChannel < 8;
        int index = isStrip ? vmChannel : vmChannel - 8;
        return template
            .Replace("{s}", index.ToString())
            .Replace("{b}", index.ToString());
    }

    public record ResolvedParameter(
        string Parameter,
        string DisplayName,
        double DefaultMin = 0,
        double DefaultMax = 1,
        double DefaultStep = 1,
        string Unit = "");


    public record ParameterGroup(string GroupName, List<ParameterTemplate> Parameters);

    public static readonly List<ParameterGroup> StripBoolGroups = new()
    {
        new("Basis", new List<ParameterTemplate>
        {
            new("Strip[{s}].Mute",   "Mute",           0, 1, 1, ""),
            new("Strip[{s}].Solo",   "Solo",           0, 1, 1, ""),
            new("Strip[{s}].Mono",   "Mono",           0, 1, 1, ""),
            new("Strip[{s}].MC",     "Mix down Mono",  0, 1, 1, ""),
        }),
        new("EQ", new List<ParameterTemplate>
        {
            new("Strip[{s}].EQ.on",  "EQ Ein/Aus",     0, 1, 1, ""),
            new("Strip[{s}].EQ.AB",  "EQ A/B",         0, 1, 1, ""),
        }),
        new("Routing", new List<ParameterTemplate>
        {
            new("Strip[{s}].A1",     "Routing → A1",   0, 1, 1, ""),
            new("Strip[{s}].A2",     "Routing → A2",   0, 1, 1, ""),
            new("Strip[{s}].A3",     "Routing → A3",   0, 1, 1, ""),
            new("Strip[{s}].A4",     "Routing → A4",   0, 1, 1, ""),
            new("Strip[{s}].A5",     "Routing → A5",   0, 1, 1, ""),
            new("Strip[{s}].B1",     "Routing → B1",   0, 1, 1, ""),
            new("Strip[{s}].B2",     "Routing → B2",   0, 1, 1, ""),
            new("Strip[{s}].B3",     "Routing → B3",   0, 1, 1, ""),
        }),
        new("FX Post-Fader", new List<ParameterTemplate>
        {
            new("Strip[{s}].PostReverb",  "Reverb Post-Fader", 0, 1, 1, ""),
            new("Strip[{s}].PostDelay",   "Delay Post-Fader",  0, 1, 1, ""),
            new("Strip[{s}].PostFx1",     "FX1 Post-Fader",    0, 1, 1, ""),
            new("Strip[{s}].PostFx2",     "FX2 Post-Fader",    0, 1, 1, ""),
        }),
        new("Compressor", new List<ParameterTemplate>
        {
            new("Strip[{s}].Comp.MakeUp", "Comp MakeUp",       0, 1, 1, ""),
        }),
    };

    public static readonly List<ParameterGroup> BusBoolGroups = new()
    {
        new("Basis", new List<ParameterTemplate>
        {
            new("Bus[{b}].Mute",      "Mute",        0, 1, 1, ""),
            new("Bus[{b}].Mono",      "Mono",        0, 1, 1, ""),
            new("Bus[{b}].Sel",       "Select",      0, 1, 1, ""),
        }),
        new("EQ", new List<ParameterTemplate>
        {
            new("Bus[{b}].EQ.on",     "EQ Ein/Aus",  0, 1, 1, ""),
            new("Bus[{b}].EQ.AB",     "EQ A/B",      0, 1, 1, ""),
        }),
        new("Monitor", new List<ParameterTemplate>
        {
            new("Bus[{b}].Monitor",   "Monitor",     0, 1, 1, ""),
        }),
    };

    public static List<ParameterGroup> GetBoolGroups(string channelType)
    {
        return channelType == "Bus" ? BusBoolGroups : StripBoolGroups;
    }
}
