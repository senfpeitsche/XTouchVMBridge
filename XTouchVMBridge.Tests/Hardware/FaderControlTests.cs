using XTouchVMBridge.Core.Hardware;

namespace XTouchVMBridge.Tests.Hardware;

public class FaderControlTests
{
    [Fact]
    public void Position_Clamps_ToMinMax()
    {
        var fader = new FaderControl(0);

        fader.Position = -10000;
        Assert.Equal(FaderControl.MinPosition, fader.Position);

        fader.Position = 10000;
        Assert.Equal(FaderControl.MaxPosition, fader.Position);
    }

    [Fact]
    public void PositionToDb_AtKnownPoints()
    {
        // Aus den Lookup-Tabellen des Python-Originals
        Assert.Equal(-70.0, FaderControl.PositionToDb(-8192));
        Assert.Equal(8.0, FaderControl.PositionToDb(8188));
    }

    [Fact]
    public void DbToPosition_AtKnownPoints()
    {
        Assert.Equal(-8192, FaderControl.DbToPosition(-70.0));
        Assert.Equal(8188, FaderControl.DbToPosition(8.0));
    }

    [Fact]
    public void PositionToDb_And_DbToPosition_AreInverse()
    {
        double db = -30.0;
        int pos = FaderControl.DbToPosition(db);
        double roundTrip = FaderControl.PositionToDb(pos);

        Assert.InRange(roundTrip, db - 1.0, db + 1.0);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-10.0)]
    [InlineData(-60.0)]
    public void PositionDb_Setter_Getter_Roundtrip(double db)
    {
        var fader = new FaderControl(0);
        fader.PositionDb = db;

        Assert.InRange(fader.PositionDb, db - 1.0, db + 1.0);
    }
}
