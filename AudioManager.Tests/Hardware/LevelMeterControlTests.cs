using AudioManager.Core.Hardware;

namespace AudioManager.Tests.Hardware;

public class LevelMeterControlTests
{
    [Theory]
    [InlineData(-200.0, 0)]   // Kein Signal
    [InlineData(-150.0, 0)]   // Unter Schwelle
    [InlineData(-50.0, 2)]
    [InlineData(-10.0, 9)]
    [InlineData(0.0, 11)]
    [InlineData(5.0, 12)]
    [InlineData(10.0, 12)]    // Über +5dB → vorletzter Bereich
    public void DbToLevel_ReturnsExpectedLevel(double db, int expectedLevel)
    {
        int level = LevelMeterControl.DbToLevel(db);
        Assert.Equal(expectedLevel, level);
    }

    [Fact]
    public void Level_Clamps_ToRange()
    {
        var meter = new LevelMeterControl(0);

        meter.Level = -5;
        Assert.Equal(LevelMeterControl.MinLevel, meter.Level);

        meter.Level = 99;
        Assert.Equal(LevelMeterControl.MaxLevel, meter.Level);
    }
}
