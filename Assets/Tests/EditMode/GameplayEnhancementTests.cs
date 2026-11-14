using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// M2 で追加された機能（グランプリカップ定義・スピードクラス/AI難易度・タイムアタックスプリット計算）の EditMode ユニットテスト。
public class GameplayEnhancementTests
{
    const string PrefCcKey = "tc_cc";
    bool hadSavedCc;
    int savedCc;

    [SetUp]
    public void SetUp()
    {
        hadSavedCc = PlayerPrefs.HasKey(PrefCcKey);
        savedCc = hadSavedCc ? PlayerPrefs.GetInt(PrefCcKey) : 0;
    }

    [TearDown]
    public void TearDown()
    {
        if (hadSavedCc)
            PlayerPrefs.SetInt(PrefCcKey, savedCc);
        else
            PlayerPrefs.DeleteKey(PrefCcKey);
        PlayerPrefs.Save();
    }

    // ───────────────────────── Cup Definitions & Points ─────────────────────────

    [Test]
    public void DefaultCups_HaveValidUniqueIdsAndInRangeCourseIndices()
    {
        Assert.IsNotNull(RaceManager.DefaultCups);
        Assert.GreaterOrEqual(RaceManager.DefaultCups.Length, 3);

        var ids = new HashSet<string>();
        for (int c = 0; c < RaceManager.DefaultCups.Length; c++)
        {
            var cup = RaceManager.DefaultCups[c];
            Assert.IsFalse(string.IsNullOrWhiteSpace(cup.id), $"Cup {c} has empty id");
            Assert.IsTrue(ids.Add(cup.id), $"Duplicate cup id: {cup.id}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(cup.nameEn), $"Cup {cup.id} missing nameEn");
            Assert.IsFalse(string.IsNullOrWhiteSpace(cup.nameJa), $"Cup {cup.id} missing nameJa");
            Assert.IsFalse(string.IsNullOrWhiteSpace(cup.descEn), $"Cup {cup.id} missing descEn");
            Assert.IsFalse(string.IsNullOrWhiteSpace(cup.descJa), $"Cup {cup.id} missing descJa");
            Assert.IsNotNull(cup.courses, $"Cup {cup.id} courses is null");
            Assert.GreaterOrEqual(cup.courses.Length, 1, $"Cup {cup.id} has no courses");

            for (int i = 0; i < cup.courses.Length; i++)
            {
                Assert.That(cup.courses[i], Is.InRange(0, Track.DefaultCourses.Length - 1),
                    $"Cup {cup.id} stage {i} course index {cup.courses[i]} out of range");
            }
        }
    }

    [Test]
    public void CupPointsTable_HasLength8AndStrictlyDescendingPositiveValues()
    {
        Assert.IsNotNull(RaceManager.CupPointsTable);
        Assert.AreEqual(8, RaceManager.CupPointsTable.Length);

        for (int i = 0; i < RaceManager.CupPointsTable.Length; i++)
        {
            Assert.Greater(RaceManager.CupPointsTable[i], 0, $"Points at index {i} must be positive");
            if (i > 0)
                Assert.Less(RaceManager.CupPointsTable[i], RaceManager.CupPointsTable[i - 1],
                    $"Points must strictly descend at index {i}");
        }

        for (int place = 1; place <= 8; place++)
            Assert.AreEqual(RaceManager.CupPointsTable[place - 1], RaceManager.GetCupPointsForPlace(place));

        Assert.AreEqual(0, RaceManager.GetCupPointsForPlace(0));
        Assert.AreEqual(0, RaceManager.GetCupPointsForPlace(-1));
        Assert.AreEqual(0, RaceManager.GetCupPointsForPlace(9));
    }

    // ───────────────────────── Speed Class & AI Difficulty ─────────────────────────

    [Test]
    public void GetSpeedMultiplier_Orders50ccLessThan100ccLessThan150cc()
    {
        float m50 = RaceManager.GetSpeedMultiplier(SpeedClass.Cc50);
        float m100 = RaceManager.GetSpeedMultiplier(SpeedClass.Cc100);
        float m150 = RaceManager.GetSpeedMultiplier(SpeedClass.Cc150);

        Assert.AreEqual(0.80f, m50, 1e-5f);
        Assert.AreEqual(1.00f, m100, 1e-5f);
        Assert.AreEqual(1.15f, m150, 1e-5f);
        Assert.Less(m50, m100);
        Assert.Less(m100, m150);
    }

    [Test]
    public void EffectiveSpeedClass_LocksTo100ccInTimeAttack_AndFollowsSelectionInSingleAndCup()
    {
        var go = new GameObject("SpeedClassTest_RM");
        try
        {
            var rm = go.AddComponent<RaceManager>();
            Assert.AreEqual(RaceManager.GameMode.SingleRace, rm.CurrentMode);
            Assert.AreEqual(SpeedClass.Cc100, rm.CurrentSpeedClass);

            rm.CycleSpeedClass(1);
            Assert.AreEqual(SpeedClass.Cc150, rm.CurrentSpeedClass);
            Assert.AreEqual(SpeedClass.Cc150, rm.EffectiveSpeedClass);
            Assert.AreEqual(RaceManager.GetSpeedMultiplier(SpeedClass.Cc150), rm.SpeedClassMultiplier, 1e-5f);

            // SingleRace -> Cup: SpeedClass 選択が維持される
            rm.CycleGameMode(1);
            Assert.IsTrue(rm.IsCupMode);
            Assert.AreEqual(SpeedClass.Cc150, rm.EffectiveSpeedClass);

            // Cup -> TimeAttack: 100cc に固定される
            rm.CycleGameMode(1);
            Assert.IsTrue(rm.IsTimeAttack);
            Assert.AreEqual(SpeedClass.Cc100, rm.EffectiveSpeedClass);
            Assert.AreEqual(1.00f, rm.SpeedClassMultiplier, 1e-5f);

            // TimeAttack -> SingleRace: 選択していた 150cc に戻る
            rm.CycleGameMode(1);
            Assert.AreEqual(RaceManager.GameMode.SingleRace, rm.CurrentMode);
            Assert.AreEqual(SpeedClass.Cc150, rm.EffectiveSpeedClass);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void GetAiClassParams_MonotonicallyIncreasesAcrossSpeedClasses()
    {
        Kart.GetAiClassParams(SpeedClass.Cc50, out float off50, out float rbMin50, out float rbMax50, out float cMin50, out float cMax50);
        Kart.GetAiClassParams(SpeedClass.Cc100, out float off100, out float rbMin100, out float rbMax100, out float cMin100, out float cMax100);
        Kart.GetAiClassParams(SpeedClass.Cc150, out float off150, out float rbMin150, out float rbMax150, out float cMin150, out float cMax150);

        Assert.Less(off50, off100);
        Assert.Less(off100, off150);

        Assert.Less(rbMin50, rbMin100);
        Assert.Less(rbMin100, rbMin150);

        Assert.Less(rbMax50, rbMax100);
        Assert.Less(rbMax100, rbMax150);

        Assert.Less(cMin50, cMin100);
        Assert.Less(cMin100, cMin150);

        Assert.Less(cMax50, cMax100);
        Assert.Less(cMax100, cMax150);

        Assert.Less(rbMin50, rbMax50);
        Assert.Less(rbMin100, rbMax100);
        Assert.Less(rbMin150, rbMax150);

        Assert.Less(cMin50, cMax50);
        Assert.Less(cMin100, cMax100);
        Assert.Less(cMin150, cMax150);
    }

    // ───────────────────────── GhostReplay Split Delta & Distance ─────────────────────────

    [Test]
    public void TryEvaluateSplitDelta_ReturnsFalseForNullOrTooFewFrames()
    {
        Assert.IsFalse(GhostReplay.TryEvaluateSplitDelta(null, 10f, 1f, out float d0));
        Assert.AreEqual(0f, d0);

        var empty = new List<GhostFrame>();
        Assert.IsFalse(GhostReplay.TryEvaluateSplitDelta(empty, 10f, 1f, out float d1));
        Assert.AreEqual(0f, d1);

        var single = new List<GhostFrame> { new GhostFrame { t = 0.5f, dist = 10f } };
        Assert.IsFalse(GhostReplay.TryEvaluateSplitDelta(single, 10f, 1f, out float d2));
        Assert.AreEqual(0f, d2);
    }

    [Test]
    public void TryEvaluateSplitDelta_InterpolatesAndClampsBoundariesAccurately()
    {
        var frames = new List<GhostFrame>
        {
            new GhostFrame { t = 1.0f, dist = 10f },
            new GhostFrame { t = 5.0f, dist = 110f },
            new GhostFrame { t = 11.0f, dist = 210f }
        };

        // 中間補間: dist=160 は [110..210] の 0.5 → refTime=8.0s
        Assert.IsTrue(GhostReplay.TryEvaluateSplitDelta(frames, 160f, 8.35f, out float deltaSlow));
        Assert.AreEqual(0.35f, deltaSlow, 1e-4f);

        Assert.IsTrue(GhostReplay.TryEvaluateSplitDelta(frames, 160f, 7.25f, out float deltaFast));
        Assert.AreEqual(-0.75f, deltaFast, 1e-4f);

        // 下限クランプ: dist <= frames[0].dist → frames[0].t (1.0s) との差
        Assert.IsTrue(GhostReplay.TryEvaluateSplitDelta(frames, 0f, 0.5f, out float deltaBefore));
        Assert.AreEqual(-0.5f, deltaBefore, 1e-4f);

        // 上限クランプ: dist >= frames[last].dist → frames[last].t (11.0s) との差
        Assert.IsTrue(GhostReplay.TryEvaluateSplitDelta(frames, 500f, 12.25f, out float deltaAfter));
        Assert.AreEqual(1.25f, deltaAfter, 1e-4f);
    }

    [Test]
    public void EnsureDistances_ReconstructsMonotonicDistancesAndLapWrapForLegacyFrames()
    {
        var go = new GameObject("GhostDistanceTest_Track");
        try
        {
            var track = go.AddComponent<Track>();
            track.BuildPath(0);

            int n = track.Count;
            // 1周目の 85% 付近からスタートラインを跨いで 2周目の 15% 付近まで（dist=0 のレガシーデータ）
            int[] sampleIndices =
            {
                Mathf.FloorToInt(n * 0.80f),
                Mathf.FloorToInt(n * 0.90f),
                Mathf.FloorToInt(n * 0.98f),
                Mathf.FloorToInt(n * 0.05f),
                Mathf.FloorToInt(n * 0.15f)
            };

            var frames = new List<GhostFrame>();
            for (int i = 0; i < sampleIndices.Length; i++)
            {
                frames.Add(new GhostFrame
                {
                    t = i * 1.0f,
                    pos = track.PointAt(sampleIndices[i], 0f),
                    heading = 0f,
                    dist = 0f
                });
            }

            GhostReplay.EnsureDistances(frames, track);

            for (int i = 1; i < frames.Count; i++)
            {
                Assert.Greater(frames[i].dist, frames[i - 1].dist,
                    $"Reconstructed distance should increase across lap wrap at index {i}");
            }
            Assert.Greater(frames[frames.Count - 1].dist, track.Length,
                "Final distance after crossing start line should exceed 1 lap length");

            // 既に dist が埋まっている場合は上書きされないこと
            float customLast = frames[frames.Count - 1].dist + 123f;
            var lastF = frames[frames.Count - 1];
            lastF.dist = customLast;
            frames[frames.Count - 1] = lastF;
            float firstBefore = frames[0].dist;

            GhostReplay.EnsureDistances(frames, track);
            Assert.AreEqual(firstBefore, frames[0].dist, 1e-5f);
            Assert.AreEqual(customLast, frames[frames.Count - 1].dist, 1e-5f);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
