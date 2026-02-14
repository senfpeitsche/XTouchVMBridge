using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Hardware;

namespace XTouchVMBridge.Tests.Hardware;

public class EncoderControlTests
{
    [Fact]
    public void RingPosition_Clamps_ToRange()
    {
        var encoder = new EncoderControl(0);

        encoder.RingPosition = -5;
        Assert.Equal(EncoderControl.MinPosition, encoder.RingPosition);

        encoder.RingPosition = 99;
        Assert.Equal(EncoderControl.MaxPosition, encoder.RingPosition);
    }

    [Theory]
    [InlineData(XTouchEncoderRingMode.Dot, 5, false, 6)]       // 0*16 + 5 + 1 = 6
    [InlineData(XTouchEncoderRingMode.Pan, 8, false, 25)]      // 1*16 + 8 + 1 = 25
    [InlineData(XTouchEncoderRingMode.Wrap, 10, false, 43)]    // 2*16 + 10 + 1 = 43
    [InlineData(XTouchEncoderRingMode.Spread, 3, false, 52)]   // 3*16 + 3 + 1 = 52
    [InlineData(XTouchEncoderRingMode.Dot, 5, true, 70)]       // 0*16 + 5 + 64 + 1 = 70
    public void CalculateCcValue_ProducesCorrectValues(
        XTouchEncoderRingMode mode, int position, bool led, byte expected)
    {
        var encoder = new EncoderControl(0)
        {
            RingMode = mode,
            RingPosition = position,
            RingLed = led
        };

        Assert.Equal(expected, encoder.CalculateCcValue());
    }
}
