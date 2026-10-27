using NUnit.Framework;

// コード側の既定データ（ExportAllData で .asset に書き出される元）の件数・値の整合性を固定する。
// コース/ドライバー/マシンを追加したときは、ここの期待件数も更新する（README・AGENTS.md の記載と合わせる）。
public class DefaultDataTests
{
    const int ExpectedCourses = 7;
    const int ExpectedDrivers = 10;
    const int ExpectedMachines = 8;

    [Test]
    public void DefaultCourses_HaveExpectedCount()
    {
        Assert.AreEqual(ExpectedCourses, Track.DefaultCourses.Length);
    }

    [Test]
    public void DefaultCourses_HaveNameAndEnoughControlPoints()
    {
        for (int i = 0; i < Track.DefaultCourses.Length; i++)
        {
            var c = Track.DefaultCourses[i];
            Assert.IsFalse(string.IsNullOrWhiteSpace(c.Name), $"course {i}: empty name");
            Assert.IsNotNull(c.Control, $"course {i} ({c.Name}): Control is null");
            Assert.GreaterOrEqual(c.Control.Length, 4, $"course {i} ({c.Name}): too few control points");
        }
    }

    [Test]
    public void DefaultCourses_HaveUniqueNames()
    {
        var names = new System.Collections.Generic.HashSet<string>();
        foreach (var c in Track.DefaultCourses)
            Assert.IsTrue(names.Add(c.Name), $"duplicate course name: {c.Name}");
    }

    [Test]
    public void DefaultKartCharacters_HaveExpectedCountAndValidStats()
    {
        Assert.AreEqual(ExpectedDrivers, RaceManager.DefaultKartCharacters.Length);
        var names = new System.Collections.Generic.HashSet<string>();
        foreach (var d in RaceManager.DefaultKartCharacters)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(d.name), "driver with empty name");
            Assert.IsTrue(names.Add(d.name), $"duplicate driver name: {d.name}");
            AssertStat(d.name, "speed", d.speed);
            AssertStat(d.name, "accel", d.accel);
            AssertStat(d.name, "handling", d.handling);
            AssertStat(d.name, "weight", d.weight);
        }
    }

    [Test]
    public void DefaultKartMachines_HaveExpectedCountAndValidStats()
    {
        Assert.AreEqual(ExpectedMachines, RaceManager.DefaultKartMachines.Length);
        var names = new System.Collections.Generic.HashSet<string>();
        foreach (var m in RaceManager.DefaultKartMachines)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(m.name), "machine with empty name");
            Assert.IsTrue(names.Add(m.name), $"duplicate machine name: {m.name}");
            AssertStat(m.name, "speed", m.speed);
            AssertStat(m.name, "accel", m.accel);
            AssertStat(m.name, "handling", m.handling);
            AssertStat(m.name, "weight", m.weight);
        }
    }


    [Test]
    public void DefaultCourses_AllHaveBgmAssetInResources()
    {
        for (int i = 0; i < Track.DefaultCourses.Length; i++)
        {
            var clip = UnityEngine.Resources.Load<UnityEngine.AudioClip>("Audio/bgm_" + i);
            Assert.IsNotNull(clip, $"Course {i} ({Track.DefaultCourses[i].Name}) is missing Audio/bgm_{i}");
        }
    }

    [Test]
    public void CourseBadges_AllProceduralBadgesGenerateNonNull()
    {
        Assert.IsNotNull(TextureGen.CityBadge(), "CityBadge is null");
        Assert.IsNotNull(TextureGen.HokkaidoBadge(), "HokkaidoBadge is null");
        Assert.IsNotNull(TextureGen.OverpassBadge(), "OverpassBadge is null");
        Assert.IsNotNull(TextureGen.CanyonBadge(), "CanyonBadge is null");
    }

    static void AssertStat(string who, string stat, int value)
    {
        Assert.That(value, Is.InRange(1, 8), $"{who}: {stat}={value} is outside 1..8");
    }
}
