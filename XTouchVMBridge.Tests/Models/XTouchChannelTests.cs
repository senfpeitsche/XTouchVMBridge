using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Models;

namespace XTouchVMBridge.Tests.Models;

public class XTouchChannelTests
{
    [Fact]
    public void Constructor_CreatesAllControls()
    {
        var channel = new XTouchChannel(3);

        Assert.Equal(3, channel.Index);
        Assert.NotNull(channel.Display);
        Assert.NotNull(channel.Fader);
        Assert.NotNull(channel.Encoder);
        Assert.NotNull(channel.LevelMeter);
    }

    [Fact]
    public void Constructor_CreatesAllButtonTypes()
    {
        var channel = new XTouchChannel(0);

        // Alle Enum-Werte sollten vorhanden sein
        foreach (var buttonType in Enum.GetValues<XTouchButtonType>())
        {
            Assert.True(channel.Buttons.ContainsKey(buttonType),
                $"Button {buttonType} fehlt.");
        }
    }

    [Fact]
    public void GetButton_ReturnsCorrectType()
    {
        var channel = new XTouchChannel(2);

        var muteBtn = channel.GetButton(XTouchButtonType.Mute);
        Assert.Equal(XTouchButtonType.Mute, muteBtn.ButtonType);
        Assert.Equal(2, muteBtn.Channel);
    }

    [Fact]
    public void Buttons_HaveCorrectNoteNumbers()
    {
        var channel = new XTouchChannel(5);

        Assert.Equal(5, channel.GetButton(XTouchButtonType.Rec).NoteNumber);    // 0*8+5
        Assert.Equal(13, channel.GetButton(XTouchButtonType.Solo).NoteNumber);   // 1*8+5
        Assert.Equal(21, channel.GetButton(XTouchButtonType.Mute).NoteNumber);   // 2*8+5
        Assert.Equal(29, channel.GetButton(XTouchButtonType.Select).NoteNumber); // 3*8+5
    }

    [Fact]
    public void Controls_HaveCorrectChannelIndex()
    {
        var channel = new XTouchChannel(6);

        Assert.Equal(6, channel.Display.Channel);
        Assert.Equal(6, channel.Fader.Channel);
        Assert.Equal(6, channel.Encoder.Channel);
        Assert.Equal(6, channel.LevelMeter.Channel);
    }
}
