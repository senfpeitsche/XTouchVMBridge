using XTouchVMBridge.Core.Logic;

namespace XTouchVMBridge.Tests.Logic;

public class RecordingFileNamingTests
{
    [Fact]
    public void BuildRecordingFileName_SanitizesInvalidCharacters()
    {
        var now = new DateTime(2026, 2, 20, 21, 10, 5);

        var fileName = RecordingFileNaming.BuildRecordingFileName("DAT/Rec", now);

        Assert.Equal("DAT_Rec_2026-02-20_21-10-05.wav", fileName);
    }

    [Fact]
    public void BuildRecordingFileName_UsesFallbackNameForEmptyChannel()
    {
        var now = new DateTime(2026, 2, 20, 21, 10, 5);

        var fileName = RecordingFileNaming.BuildRecordingFileName("  ", now);

        Assert.Equal("Channel_2026-02-20_21-10-05.wav", fileName);
    }
}
