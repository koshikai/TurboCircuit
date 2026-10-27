using NUnit.Framework;
using UnityEngine;

// 言語切替（Loc）の文字列解決を確認する。Loc.Set は PlayerPrefs に保存するため、テスト後に元へ戻す。
public class LocTests
{
    const string PrefKey = "tc_lang";
    Loc.Lang savedLang;
    bool hadPref;
    int savedPref;

    [SetUp]
    public void SetUp()
    {
        savedLang = Loc.Current;
        hadPref = PlayerPrefs.HasKey(PrefKey);
        savedPref = PlayerPrefs.GetInt(PrefKey, 0);
    }

    [TearDown]
    public void TearDown()
    {
        Loc.Set(savedLang);
        if (hadPref) PlayerPrefs.SetInt(PrefKey, savedPref);
        else PlayerPrefs.DeleteKey(PrefKey);
        PlayerPrefs.Save();
    }

    [Test]
    public void T_ReturnsTextForCurrentLanguage()
    {
        Loc.Set(Loc.Lang.Ja);
        Assert.AreEqual("日本語", Loc.T("English", "日本語"));
        Loc.Set(Loc.Lang.En);
        Assert.AreEqual("English", Loc.T("English", "日本語"));
    }

    [Test]
    public void Toggle_SwitchesBetweenJaAndEn()
    {
        Loc.Set(Loc.Lang.Ja);
        Loc.Toggle();
        Assert.AreEqual(Loc.Lang.En, Loc.Current);
        Loc.Toggle();
        Assert.AreEqual(Loc.Lang.Ja, Loc.Current);
    }

    [Test]
    public void PlaceText_FormatsEnglishOrdinals()
    {
        Loc.Set(Loc.Lang.En);
        Assert.AreEqual("1ST", Loc.PlaceText(1));
        Assert.AreEqual("2ND", Loc.PlaceText(2));
        Assert.AreEqual("3RD", Loc.PlaceText(3));
        Assert.AreEqual("8TH", Loc.PlaceText(8));
    }

    [Test]
    public void PlaceText_FormatsJapanesePlaces()
    {
        Loc.Set(Loc.Lang.Ja);
        Assert.AreEqual("1位", Loc.PlaceText(1));
        Assert.AreEqual("8位", Loc.PlaceText(8));
    }

    [TestCase(Loc.Lang.Ja)]
    [TestCase(Loc.Lang.En)]
    public void CourseNames_AreNonEmptyForEveryDefaultCourse(Loc.Lang lang)
    {
        Loc.Set(lang);
        for (int i = 0; i < Track.DefaultCourses.Length; i++)
            Assert.IsFalse(string.IsNullOrWhiteSpace(Loc.CourseName(i, Track.DefaultCourses[i].Name)),
                $"course {i} has no name for {lang}");
    }

    [TestCase(Loc.Lang.Ja)]
    [TestCase(Loc.Lang.En)]
    public void DriverAndKartNames_AreNonEmptyForEveryDefault(Loc.Lang lang)
    {
        Loc.Set(lang);
        for (int i = 0; i < RaceManager.DefaultKartCharacters.Length; i++)
            Assert.IsFalse(string.IsNullOrWhiteSpace(Loc.DriverName(i, RaceManager.DefaultKartCharacters[i].name)),
                $"driver {i} has no name for {lang}");
        for (int i = 0; i < RaceManager.DefaultKartMachines.Length; i++)
            Assert.IsFalse(string.IsNullOrWhiteSpace(Loc.KartName(i, RaceManager.DefaultKartMachines[i].name)),
                $"machine {i} has no name for {lang}");
    }
}
