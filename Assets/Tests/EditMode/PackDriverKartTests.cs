using NUnit.Framework;

// ネット同期でやり取りする「ドライバー番号 + マシン番号」のパック形式（各4bit）の仕様を固定する。
public class PackDriverKartTests
{
    [Test]
    public void PackThenUnpack_RoundTripsAllDriverAndMachineCombinations()
    {
        for (int d = 0; d < RaceManager.KartCharacters.Length; d++)
        {
            for (int k = 0; k < RaceManager.KartMachines.Length; k++)
            {
                int packed = RaceManager.PackDriverKart(d, k);
                RaceManager.UnpackDriverKart(packed, out int d2, out int k2);
                Assert.AreEqual(d, d2, $"driver mismatch for ({d},{k})");
                Assert.AreEqual(k, k2, $"machine mismatch for ({d},{k})");
            }
        }
    }

    [Test]
    public void Pack_UsesLowNibbleForDriverAndHighNibbleForMachine()
    {
        Assert.AreEqual(0x00, RaceManager.PackDriverKart(0, 0));
        Assert.AreEqual(0x03, RaceManager.PackDriverKart(3, 0));
        Assert.AreEqual(0x40, RaceManager.PackDriverKart(0, 4));
        Assert.AreEqual(0x52, RaceManager.PackDriverKart(2, 5));
    }

    [Test]
    public void Pack_ClampsOutOfRangeIndicesTo4Bits()
    {
        Assert.AreEqual(0xFF, RaceManager.PackDriverKart(99, 99));
        Assert.AreEqual(0x00, RaceManager.PackDriverKart(-5, -5));
    }

    [Test]
    public void Pack_AlwaysFitsInOneByte()
    {
        for (int d = 0; d < 16; d++)
            for (int k = 0; k < 16; k++)
                Assert.That(RaceManager.PackDriverKart(d, k), Is.InRange(0, 0xFF));
    }

    [Test]
    public void Unpack_ClampsToAvailableDriversAndMachines()
    {
        RaceManager.UnpackDriverKart(0xFF, out int d, out int k);
        Assert.AreEqual(RaceManager.KartCharacters.Length - 1, d);
        Assert.AreEqual(RaceManager.KartMachines.Length - 1, k);
    }

    [Test]
    public void ContentCountsFitInTheNetworkPackFormat()
    {
        // 4bit × 2 でパックするため、ドライバー・マシンは各16種まで。
        Assert.LessOrEqual(RaceManager.KartCharacters.Length, 16);
        Assert.LessOrEqual(RaceManager.KartMachines.Length, 16);
    }
}
