using System.Runtime.Intrinsics;

namespace Artificer.Test.Solver;

[TestClass]
public class IntrinsicsTests
{
    // ---- HMaxIndex ----

    [TestMethod]
    public void HMaxIndex_SingleElement_ReturnsZero()
    {
        var v = Vector256.Create(5f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
        Assert.AreEqual(0, Intrinsics.HMaxIndex(v, 1));
    }

    [TestMethod]
    public void HMaxIndex_MaxAtStart()
    {
        var v = Vector256.Create(9f, 1f, 2f, 3f, 0f, 0f, 0f, 0f);
        Assert.AreEqual(0, Intrinsics.HMaxIndex(v, 4));
    }

    [TestMethod]
    public void HMaxIndex_MaxAtEnd()
    {
        var v = Vector256.Create(1f, 2f, 3f, 9f, 0f, 0f, 0f, 0f);
        Assert.AreEqual(3, Intrinsics.HMaxIndex(v, 4));
    }

    [TestMethod]
    public void HMaxIndex_MaxInMiddle()
    {
        var v = Vector256.Create(1f, 9f, 3f, 2f, 0f, 0f, 0f, 0f);
        Assert.AreEqual(1, Intrinsics.HMaxIndex(v, 4));
    }

    [TestMethod]
    public void HMaxIndex_AllSameValue()
    {
        var v = Vector256.Create(5f, 5f, 5f, 5f, 5f, 5f, 5f, 5f);
        var idx = Intrinsics.HMaxIndex(v, 8);
        Assert.IsTrue(idx >= 0 && idx < 8);
    }

    [TestMethod]
    public void HMaxIndex_FullVector()
    {
        var v = Vector256.Create(1f, 2f, 3f, 4f, 8f, 6f, 7f, 5f);
        Assert.AreEqual(4, Intrinsics.HMaxIndex(v, 8));
    }

    // ---- NthBitSet ----

    [TestMethod]
    public void NthBitSet_SingleBit_ZerothSetBit()
    {
        // 0b1 = 1, only bit 0 is set; 0th set bit is at position 0
        Assert.AreEqual(0, Intrinsics.NthBitSet(0b1, 0));
    }

    [TestMethod]
    public void NthBitSet_TwoBits_FirstSetBit()
    {
        // 0b110 = bits 1 and 2 are set; 0th set bit is at position 1
        Assert.AreEqual(1, Intrinsics.NthBitSet(0b110, 0));
    }

    [TestMethod]
    public void NthBitSet_TwoBits_SecondSetBit()
    {
        // 0b110 = bits 1 and 2 are set; 1st set bit is at position 2
        Assert.AreEqual(2, Intrinsics.NthBitSet(0b110, 1));
    }

    [TestMethod]
    public void NthBitSet_NGreaterThanOrEqualPopCount_Returns64()
    {
        // 0b1 has popcount=1; n=1 >= 1 → 64
        Assert.AreEqual(64, Intrinsics.NthBitSet(0b1, 1));
    }

    [TestMethod]
    public void NthBitSet_ZeroValue_Returns64()
    {
        Assert.AreEqual(64, Intrinsics.NthBitSet(0, 0));
    }

    [TestMethod]
    public void NthBitSet_HighBit()
    {
        // bit 63 is set
        Assert.AreEqual(63, Intrinsics.NthBitSet(1ul << 63, 0));
    }

    [TestMethod]
    public void NthBitSet_MultipleBits_ThirdSetBit()
    {
        // 0b10101 = bits 0, 2, 4 set; 2nd set bit is at position 4
        Assert.AreEqual(4, Intrinsics.NthBitSet(0b10101, 2));
    }

    // ---- ReciprocalSqrt ----

    [TestMethod]
    public void ReciprocalSqrt_AllFours_CloseToHalf()
    {
        var data = Vector256.Create(4f);
        var result = Intrinsics.ReciprocalSqrt(data);

        for (var i = 0; i < Vector256<float>.Count; i++)
            Assert.AreEqual(0.5f, result[i], 0.01f, $"Element {i} should be ~0.5");
    }

    [TestMethod]
    public void ReciprocalSqrt_AllOnes_CloseToOne()
    {
        var data = Vector256.Create(1f);
        var result = Intrinsics.ReciprocalSqrt(data);

        for (var i = 0; i < Vector256<float>.Count; i++)
            Assert.AreEqual(1.0f, result[i], 0.01f, $"Element {i} should be ~1.0");
    }

    [TestMethod]
    public void ReciprocalSqrt_MixedValues()
    {
        var data = Vector256.Create(1f, 4f, 9f, 16f, 25f, 36f, 49f, 64f);
        var result = Intrinsics.ReciprocalSqrt(data);

        float[] expected = [1f, 0.5f, 1f / 3f, 0.25f, 0.2f, 1f / 6f, 1f / 7f, 0.125f];
        for (var i = 0; i < Vector256<float>.Count; i++)
            Assert.AreEqual(expected[i], result[i], 0.01f, $"Element {i}");
    }
}
