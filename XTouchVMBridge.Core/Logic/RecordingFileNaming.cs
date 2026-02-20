namespace XTouchVMBridge.Core.Logic;

public static class RecordingFileNaming
{
    public static string BuildRecordingFileName(string? channelName, DateTime localNow, string extension = ".wav")
    {
        var safeChannelName = SanitizeFileNamePart(channelName);
        var timestamp = localNow.ToString("yyyy-MM-dd_HH-mm-ss");
        var normalizedExtension = string.IsNullOrWhiteSpace(extension)
            ? ".wav"
            : extension.StartsWith('.') ? extension : $".{extension}";

        return $"{safeChannelName}_{timestamp}{normalizedExtension}";
    }

    public static string SanitizeFileNamePart(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "Channel";

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(input
            .Trim()
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "Channel" : sanitized;
    }
}
