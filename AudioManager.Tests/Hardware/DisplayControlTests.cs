using AudioManager.Core.Enums;
using AudioManager.Core.Hardware;

namespace AudioManager.Tests.Hardware;

public class DisplayControlTests
{
    [Fact]
    public void TopRow_TruncatesToMaxLength()
    {
        var display = new DisplayControl(0);
        display.TopRow = "HelloWorld123";

        Assert.Equal(7, display.TopRow.Length);
        Assert.Equal("HelloWo", display.TopRow);
    }

    [Fact]
    public void TopRow_PadsShortStrings()
    {
        var display = new DisplayControl(0);
        display.TopRow = "Hi";

        Assert.Equal(7, display.TopRow.Length);
        Assert.Equal("Hi     ", display.TopRow);
    }

    [Fact]
    public void TopRow_ReplacesNonAscii()
    {
        var display = new DisplayControl(0);
        display.TopRow = "Hällö";

        // ä, ö are filtered out, remaining: "Hll" padded to 7
        Assert.Equal(7, display.TopRow.Length);
        Assert.DoesNotContain("ä", display.TopRow);
    }

    [Fact]
    public void EmptyString_ResultsInSpaces()
    {
        var display = new DisplayControl(0);
        display.TopRow = "";

        Assert.Equal("       ", display.TopRow);
    }

    [Fact]
    public void Color_DefaultIsOff()
    {
        var display = new DisplayControl(0);
        Assert.Equal(XTouchColor.Off, display.Color);
    }

    [Fact]
    public void SetRow_And_GetRow_AreConsistent()
    {
        var display = new DisplayControl(0);
        display.SetRow(0, "Test");
        display.SetRow(1, "Row2");

        Assert.Equal("Test   ", display.GetRow(0));
        Assert.Equal("Row2   ", display.GetRow(1));
    }
}
