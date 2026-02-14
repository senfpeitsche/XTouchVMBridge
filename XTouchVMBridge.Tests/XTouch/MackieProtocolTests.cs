using XTouchVMBridge.Midi.XTouch;

namespace XTouchVMBridge.Tests.XTouch;

public class MackieProtocolTests
{
    [Theory]
    [InlineData(1, 1)]      // Rechtsdrehung
    [InlineData(15, 15)]    // Max Rechtsdrehung
    [InlineData(65, -1)]    // Linksdrehung
    [InlineData(79, -15)]   // Max Linksdrehung
    [InlineData(0, 0)]      // Kein Tick
    [InlineData(100, 0)]    // Ungültiger Wert
    public void DecodeEncoderTicks_ProducesCorrectValues(int ccValue, int expected)
    {
        Assert.Equal(expected, MackieProtocol.DecodeEncoderTicks(ccValue));
    }

    [Fact]
    public void BuildDisplayTextMessage_HasCorrectFormat()
    {
        byte[] msg = MackieProtocol.BuildDisplayTextMessage(0, "Hello  ");

        // Beginnt mit SysEx Prefix
        Assert.Equal(0xF0, msg[0]);
        Assert.Equal(0x00, msg[1]);
        Assert.Equal(0x00, msg[2]);
        Assert.Equal(0x66, msg[3]);
        Assert.Equal(0x14, msg[4]);

        // Command byte
        Assert.Equal(MackieProtocol.CmdDisplayText, msg[5]);

        // Offset
        Assert.Equal(0, msg[6]);

        // Text
        Assert.Equal((byte)'H', msg[7]);
        Assert.Equal((byte)'e', msg[8]);

        // Endet mit SysEx End
        Assert.Equal(0xF7, msg[^1]);
    }

    [Fact]
    public void BuildDisplayColorMessage_RequiresExactly8Colors()
    {
        Assert.Throws<ArgumentException>(() =>
            MackieProtocol.BuildDisplayColorMessage(new byte[] { 1, 2, 3 }));

        var msg = MackieProtocol.BuildDisplayColorMessage(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 });
        Assert.Equal(0xF0, msg[0]);
        Assert.Equal(MackieProtocol.CmdDisplayColor, msg[5]);
        Assert.Equal(0xF7, msg[^1]);
    }

    [Theory]
    [InlineData(0, 0, 0)]    // Kanal 0, Zeile 0
    [InlineData(3, 0, 21)]   // Kanal 3, Zeile 0: 3*7 = 21
    [InlineData(0, 1, 56)]   // Kanal 0, Zeile 1: 56
    [InlineData(7, 1, 105)]  // Kanal 7, Zeile 1: 56 + 7*7 = 105
    public void GetDisplayOffset_CalculatesCorrectly(int channel, int row, int expected)
    {
        Assert.Equal(expected, MackieProtocol.GetDisplayOffset(channel, row));
    }
}
