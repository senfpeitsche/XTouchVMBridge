using AudioManager.Core.Enums;
using AudioManager.Core.Hardware;

namespace AudioManager.Tests.Hardware;

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
    [InlineData(XTouchEncoderRingMode.Dot, 5, false, 5)]       // 0*16 + 5 = 5
    [InlineData(XTouchEncoderRingMode.Pan, 8, false, 24)]      // 1*16 + 8 = 24
    [InlineData(XTouchEncoderRingMode.Wrap, 10, false, 42)]    // 2*16 + 10 = 42
    [InlineData(XTouchEncoderRingMode.Spread, 3, false, 51)]   // 3*16 + 3 = 51
    [InlineData(XTouchEncoderRingMode.Dot, 5, true, 69)]       // 0*16 + 5 + 64 = 69
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
