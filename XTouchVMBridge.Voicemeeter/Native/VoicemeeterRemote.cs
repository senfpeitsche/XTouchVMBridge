using System.Runtime.InteropServices;

namespace XTouchVMBridge.Voicemeeter.Native;

/// <summary>
/// P/Invoke-Deklarationen für VoicemeeterRemote64.dll.
/// Verwendet DllImport für maximale Kompatibilität (kein unsafe nötig).
///
/// Die DLL wird aus dem Voicemeeter-Installationsverzeichnis geladen.
/// <see cref="EnsureDllSearchPath"/> muss einmalig aufgerufen werden,
/// bevor andere Methoden verwendet werden.
/// Der Aufruf erfolgt in <c>App.OnStartup</c> noch vor dem Host-Start,
/// damit die DLL bereits verfügbar ist wenn HostedServices (z.B. VoicemeeterBridge)
/// zum ersten Mal auf die API zugreifen.
/// </summary>
public static class VoicemeeterRemote
{
    private const string DllName = "VoicemeeterRemote64";

    /// <summary>Standard-Installationspfad von Voicemeeter.</summary>
    private static readonly string[] VoicemeeterSearchPaths =
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "VB", "Voicemeeter"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VB", "Voicemeeter"),
    };

    private static bool _dllSearchPathSet;

    /// <summary>
    /// Setzt den DLL-Suchpfad auf das Voicemeeter-Installationsverzeichnis,
    /// damit Windows die VoicemeeterRemote64.dll findet.
    /// Muss einmalig vor dem ersten API-Aufruf aufgerufen werden.
    /// </summary>
    /// <param name="configuredPath">
    /// Optionaler Pfad zu VoicemeeterRemote64.dll oder zum Voicemeeter-Installationsordner.
    /// </param>
    /// <returns>Den verwendeten Suchpfad oder null wenn nicht gefunden.</returns>
    public static string? EnsureDllSearchPath(string? configuredPath = null)
    {
        if (_dllSearchPathSet) return null;

        var configuredDirectory = ResolveConfiguredDirectory(configuredPath);
        if (!string.IsNullOrWhiteSpace(configuredDirectory) &&
            File.Exists(Path.Combine(configuredDirectory, "VoicemeeterRemote64.dll")))
        {
            SetDllDirectory(configuredDirectory);
            _dllSearchPathSet = true;
            return configuredDirectory;
        }

        foreach (var path in VoicemeeterSearchPaths)
        {
            if (File.Exists(Path.Combine(path, "VoicemeeterRemote64.dll")))
            {
                SetDllDirectory(path);
                _dllSearchPathSet = true;
                return path;
            }
        }

        return null;
    }

    private static string? ResolveConfiguredDirectory(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            return null;

        var fullPath = Path.GetFullPath(configuredPath.Trim().Trim('"'));
        if (Directory.Exists(fullPath))
            return fullPath;

        if (File.Exists(fullPath) &&
            string.Equals(Path.GetFileName(fullPath), "VoicemeeterRemote64.dll", StringComparison.OrdinalIgnoreCase))
            return Path.GetDirectoryName(fullPath);

        return null;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDllDirectory(string lpPathName);

    // ─── Login/Logout ───────────────────────────────────────────────

    [DllImport(DllName, EntryPoint = "VBVMR_Login")]
    internal static extern int Login();

    [DllImport(DllName, EntryPoint = "VBVMR_Logout")]
    internal static extern int Logout();

    [DllImport(DllName, EntryPoint = "VBVMR_GetVoicemeeterType")]
    internal static extern int GetVoicemeeterType(out int type);

    [DllImport(DllName, EntryPoint = "VBVMR_GetVoicemeeterVersion")]
    internal static extern int GetVoicemeeterVersion(out int version);

    // ─── Dirty Flags ────────────────────────────────────────────────

    [DllImport(DllName, EntryPoint = "VBVMR_IsParametersDirty")]
    internal static extern int IsParametersDirty();

    // ─── Parameter Getters ──────────────────────────────────────────

    [DllImport(DllName, EntryPoint = "VBVMR_GetParameterFloat", CharSet = CharSet.Ansi)]
    internal static extern int GetParameterFloat(string paramName, out float value);

    [DllImport(DllName, EntryPoint = "VBVMR_GetParameterStringA", CharSet = CharSet.Ansi)]
    internal static extern int GetParameterStringA(
        string paramName,
        [MarshalAs(UnmanagedType.LPArray, SizeConst = 512)] byte[] value);

    // ─── Parameter Setters ──────────────────────────────────────────

    [DllImport(DllName, EntryPoint = "VBVMR_SetParameterFloat", CharSet = CharSet.Ansi)]
    internal static extern int SetParameterFloat(string paramName, float value);

    [DllImport(DllName, EntryPoint = "VBVMR_SetParameterStringA", CharSet = CharSet.Ansi)]
    internal static extern int SetParameterStringA(string paramName, string value);

    // ─── Levels ─────────────────────────────────────────────────────

    [DllImport(DllName, EntryPoint = "VBVMR_GetLevel")]
    internal static extern int GetLevel(int type, int channel, out float value);

    // ─── Macro Buttons ──────────────────────────────────────────────

    [DllImport(DllName, EntryPoint = "VBVMR_MacroButton_GetStatus")]
    internal static extern int MacroButtonGetStatus(int buttonIndex, out float value, int mode);

    [DllImport(DllName, EntryPoint = "VBVMR_MacroButton_SetStatus")]
    internal static extern int MacroButtonSetStatus(int buttonIndex, float value, int mode);
}
