using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Hardware;

namespace XTouchVMBridge.Tests.Hardware;

public class EncoderFunctionTests
{
    // ─── EncoderFunction Tests ───────────────────────────────────────

    [Fact]
    public void ApplyTicks_IncrementsValue()
    {
        var fn = new EncoderFunction("HIGH", "Strip[0].EQGain3", -12, 12, 0.5, "dB");
        fn.ApplyTicks(4); // +2.0 dB
        Assert.Equal(2.0, fn.CurrentValue, 2);
    }

    [Fact]
    public void ApplyTicks_DecrementsValue()
    {
        var fn = new EncoderFunction("MID", "Strip[0].EQGain2", -12, 12, 0.5, "dB", initialValue: 3.0);
        fn.ApplyTicks(-4); // -2.0 → 1.0
        Assert.Equal(1.0, fn.CurrentValue, 2);
    }

    [Fact]
    public void ApplyTicks_ClampsToMax()
    {
        var fn = new EncoderFunction("HIGH", "Strip[0].EQGain3", -12, 12, 0.5, "dB", initialValue: 11.0);
        fn.ApplyTicks(10); // 11 + 5 = 16 → clamped to 12
        Assert.Equal(12.0, fn.CurrentValue, 2);
    }

    [Fact]
    public void ApplyTicks_ClampsToMin()
    {
        var fn = new EncoderFunction("LOW", "Strip[0].EQGain1", -12, 12, 0.5, "dB", initialValue: -11.0);
        fn.ApplyTicks(-10); // -11 - 5 = -16 → clamped to -12
        Assert.Equal(-12.0, fn.CurrentValue, 2);
    }

    [Theory]
        [InlineData(0.0, -12, 12, 5)]     // center -> updated mapping
        [InlineData(-12.0, -12, 12, 0)]   // min -> 0
        [InlineData(12.0, -12, 12, 10)]   // max -> updated mapping
        [InlineData(-6.0, -12, 12, 2)]    // 25% -> updated mapping
        [InlineData(6.0, -12, 12, 8)]     // 75% -> updated mapping
    public void ToRingPosition_MapsCorrectly(double value, double min, double max, int expectedRing)
    {
        var fn = new EncoderFunction("TEST", "param", min, max, 0.5, "dB", initialValue: value);
        Assert.Equal(expectedRing, fn.ToRingPosition());
    }

    [Fact]
    public void FormatValue_ShowsUnitAndPrecision()
    {
        var fn = new EncoderFunction("HIGH", "param", -12, 12, 0.5, "dB", initialValue: 3.5);
        Assert.Equal("3.5dB", fn.FormatValue());
    }

    [Fact]
    public void FormatValue_NoUnit()
    {
        var fn = new EncoderFunction("PAN", "param", -0.5, 0.5, 0.05, "", initialValue: 0.25);
        Assert.Equal("0.2", fn.FormatValue()); // 0.25 → F1 mit banker's rounding = "0.2"
    }

    // ─── EncoderControl Funktionsliste Tests ─────────────────────────

    [Fact]
    public void HasFunctions_FalseByDefault()
    {
        var enc = new EncoderControl(0);
        Assert.False(enc.HasFunctions);
        Assert.Null(enc.ActiveFunction);
    }

    [Fact]
    public void AddFunction_MakesFirstFunctionActive()
    {
        var enc = new EncoderControl(0);
        enc.AddFunction(new EncoderFunction("HIGH", "p1", -12, 12, 0.5, "dB"));
        enc.AddFunction(new EncoderFunction("MID", "p2", -12, 12, 0.5, "dB"));

        Assert.True(enc.HasFunctions);
        Assert.Equal(0, enc.ActiveFunctionIndex);
        Assert.Equal("HIGH", enc.ActiveFunction!.Name);
    }

    [Fact]
    public void CycleFunction_CyclesThroughList()
    {
        var enc = new EncoderControl(0);
        enc.AddFunction(new EncoderFunction("HIGH", "p1", -12, 12, 0.5, "dB"));
        enc.AddFunction(new EncoderFunction("MID", "p2", -12, 12, 0.5, "dB"));
        enc.AddFunction(new EncoderFunction("LOW", "p3", -12, 12, 0.5, "dB"));

        Assert.Equal("HIGH", enc.ActiveFunction!.Name);

        var fn1 = enc.CycleFunction();
        Assert.Equal("MID", fn1!.Name);
        Assert.Equal(1, enc.ActiveFunctionIndex);

        var fn2 = enc.CycleFunction();
        Assert.Equal("LOW", fn2!.Name);
        Assert.Equal(2, enc.ActiveFunctionIndex);

        // Wrap-around
        var fn3 = enc.CycleFunction();
        Assert.Equal("HIGH", fn3!.Name);
        Assert.Equal(0, enc.ActiveFunctionIndex);
    }

    [Fact]
    public void CycleFunctionReverse_CyclesBackward()
    {
        var enc = new EncoderControl(0);
        enc.AddFunction(new EncoderFunction("HIGH", "p1", -12, 12, 0.5, "dB"));
        enc.AddFunction(new EncoderFunction("MID", "p2", -12, 12, 0.5, "dB"));
        enc.AddFunction(new EncoderFunction("LOW", "p3", -12, 12, 0.5, "dB"));

        // Von HIGH rückwärts → LOW
        var fn = enc.CycleFunctionReverse();
        Assert.Equal("LOW", fn!.Name);
        Assert.Equal(2, enc.ActiveFunctionIndex);
    }

    [Fact]
    public void CycleFunction_ReturnsNull_WhenEmpty()
    {
        var enc = new EncoderControl(0);
        Assert.Null(enc.CycleFunction());
        Assert.Null(enc.CycleFunctionReverse());
    }

    [Fact]
    public void ApplyTicks_UpdatesValueAndRingPosition()
    {
        var enc = new EncoderControl(0);
        enc.AddFunction(new EncoderFunction("HIGH", "p1", -12, 12, 0.5, "dB", initialValue: 0));

        var fn = enc.ApplyTicks(4); // +2dB → value=2, ring ≈ 8
        Assert.NotNull(fn);
        Assert.Equal(2.0, fn!.CurrentValue, 2);
        Assert.True(enc.RingPosition > 0); // Ring moved from center
    }

    [Fact]
    public void ApplyTicks_ReturnsNull_WhenNoFunctions()
    {
        var enc = new EncoderControl(0);
        Assert.Null(enc.ApplyTicks(1));
    }

    [Fact]
    public void ClearFunctions_RemovesAll()
    {
        var enc = new EncoderControl(0);
        enc.AddFunction(new EncoderFunction("HIGH", "p1", -12, 12, 0.5, "dB"));
        enc.AddFunction(new EncoderFunction("MID", "p2", -12, 12, 0.5, "dB"));
        enc.CycleFunction(); // Index = 1

        enc.ClearFunctions();

        Assert.False(enc.HasFunctions);
        Assert.Equal(0, enc.ActiveFunctionIndex);
        Assert.Null(enc.ActiveFunction);
    }

    [Fact]
    public void SyncRingToActiveFunction_UpdatesRingPosition()
    {
        var enc = new EncoderControl(0);
        enc.AddFunction(new EncoderFunction("HIGH", "p1", -12, 12, 0.5, "dB", initialValue: 12.0));
        enc.AddFunction(new EncoderFunction("MID", "p2", -12, 12, 0.5, "dB", initialValue: -12.0));

        enc.SyncRingToActiveFunction();
                Assert.Equal(10, enc.RingPosition); // HIGH = max -> updated mapping

        enc.CycleFunction();
        enc.SyncRingToActiveFunction();
        Assert.Equal(0, enc.RingPosition); // MID = min → ring = 0
    }

    [Fact]
    public void AddFunctions_AddsBatch()
    {
        var enc = new EncoderControl(0);
        enc.AddFunctions(new[]
        {
            new EncoderFunction("A", "p1", 0, 10, 1, ""),
            new EncoderFunction("B", "p2", 0, 10, 1, ""),
            new EncoderFunction("C", "p3", 0, 10, 1, "")
        });

        Assert.Equal(3, enc.Functions.Count);
        Assert.Equal("A", enc.ActiveFunction!.Name);
    }

    [Fact]
    public void ActiveFunctionIndex_Set_WrapsNegative()
    {
        var enc = new EncoderControl(0);
        enc.AddFunction(new EncoderFunction("A", "p1", 0, 10, 1, ""));
        enc.AddFunction(new EncoderFunction("B", "p2", 0, 10, 1, ""));
        enc.AddFunction(new EncoderFunction("C", "p3", 0, 10, 1, ""));

        enc.ActiveFunctionIndex = -1;
        Assert.Equal(2, enc.ActiveFunctionIndex); // Wraps to last
    }

    [Fact]
    public void ActiveFunctionIndex_Set_WrapsOverflow()
    {
        var enc = new EncoderControl(0);
        enc.AddFunction(new EncoderFunction("A", "p1", 0, 10, 1, ""));
        enc.AddFunction(new EncoderFunction("B", "p2", 0, 10, 1, ""));

        enc.ActiveFunctionIndex = 5;
        Assert.Equal(1, enc.ActiveFunctionIndex); // 5 % 2 = 1
    }

    [Fact]
    public void EachFunction_MaintainsOwnValue()
    {
        var enc = new EncoderControl(0);
        enc.AddFunction(new EncoderFunction("HIGH", "p1", -12, 12, 0.5, "dB"));
        enc.AddFunction(new EncoderFunction("LOW", "p2", -12, 12, 0.5, "dB"));

        // Drehe HIGH auf +3
        enc.ApplyTicks(6);
        Assert.Equal(3.0, enc.ActiveFunction!.CurrentValue, 2);

        // Wechsle zu LOW
        enc.CycleFunction();
        Assert.Equal(0.0, enc.ActiveFunction!.CurrentValue, 2); // LOW unverändert

        // Drehe LOW auf -2
        enc.ApplyTicks(-4);
        Assert.Equal(-2.0, enc.ActiveFunction!.CurrentValue, 2);

        // Zurück zu HIGH → immer noch +3
        enc.CycleFunction();
        Assert.Equal(3.0, enc.ActiveFunction!.CurrentValue, 2);
    }
}
