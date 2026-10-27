using NUnit.Framework;

public class AppUpdaterTests
{
    [Test]
    public void CompareVersions_CorrectlyComparesSemVer()
    {
        Assert.Greater(AppUpdater.CompareVersions("v2.2.1", "v2.2.0"), 0);
        Assert.AreEqual(0, AppUpdater.CompareVersions("v2.2.1", "2.2.1"));
        Assert.Less(AppUpdater.CompareVersions("v2.2.0", "v2.2.1"), 0);
        Assert.Greater(AppUpdater.CompareVersions("v2.3.0", "v2.2.9"), 0);
        Assert.Greater(AppUpdater.CompareVersions("v3.0.0", "v2.9.9"), 0);
        Assert.Less(AppUpdater.CompareVersions("v2.2.0-beta", "v2.2.0"), 0);
    }

    [Test]
    public void CleanReleaseNotes_StripsCarriageReturnsAndSurrogates()
    {
        string raw = "Header\r\n## Section\r\n**Bold** Text \uD83D\uDE80 Rocket";
        string cleaned = AppUpdater.CleanReleaseNotes(raw);

        Assert.IsFalse(cleaned.Contains("\r"), "Contains carriage return");
        Assert.IsFalse(cleaned.Contains("**"), "Contains markdown bold asterisks");
        Assert.IsTrue(cleaned.Contains("■ Section"), "Markdown header converted to section bullet");
        Assert.IsTrue(cleaned.Contains("Bold Text"), "Bold markers stripped");
    }

    [Test]
    public void CleanSingleLine_ProducesSingleLineWithoutEmojis()
    {
        string raw = "First line \r\nSecond line \uD83C\uDFC1";
        string cleaned = AppUpdater.CleanSingleLine(raw);

        Assert.IsFalse(cleaned.Contains("\n"), "Contains newline");
        Assert.IsFalse(cleaned.Contains("\r"), "Contains carriage return");
        Assert.AreEqual("First line Second line", cleaned.Trim());
    }
}
