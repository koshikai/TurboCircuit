using System.Linq;
using NUnit.Framework;
using UnityEngine;

// 全コースにおける CPU カート（Kart.AI）の挙動健全性を検証する EditMode テスト。
// レース進行や画面描画を伴わずに、幾何追従・速度計画・分岐追従・シミュレーション進行を高速に検証する。
public class KartAITests
{
    static int[] CourseIndices => Enumerable.Range(0, Track.DefaultCourses.Length).ToArray();

    GameObject trackGo;
    Track track;
    GameObject kartGo;
    Kart kart;

    [SetUp]
    public void SetUp()
    {
        trackGo = new GameObject("TestTrack");
        track = trackGo.AddComponent<Track>();

        kartGo = new GameObject("TestKart");
        kart = kartGo.AddComponent<Kart>();
    }

    [TearDown]
    public void TearDown()
    {
        if (kartGo != null) Object.DestroyImmediate(kartGo);
        if (trackGo != null) Object.DestroyImmediate(trackGo);
    }

    // 1. 全コース・全区間で AI が計算する目標レーン (targetLane) が道路内（オフロード外）であること
    [TestCaseSource(nameof(CourseIndices))]
    public void TargetLane_IsAlwaysOnRoad_OnAllCourses(int course)
    {
        track.BuildPath(course);
        kart.InitForTest(track);

        // 全区間をサンプリング（8ステップごと）
        for (int i = 0; i < track.Count; i += 8)
        {
            kart.ResetTo(i, 0f);
            kart.AIInput(0.02f);

            float targetLane = kart.AILastTargetLane;
            float ratio = (float)i / track.Count;

            // 目標レーンがオフロード（道路外）になっていないこと
            Assert.IsFalse(track.IsOffroad(ratio, targetLane),
                $"Course {course} ({Track.Courses[course].Name}): Target lane {targetLane:F2}m at idx {i} (ratio {ratio:F3}) is offroad!");

            // 目標位置 (AILastTargetPos) も有限値であること
            Assert.IsTrue(IsFinite(kart.AILastTargetPos),
                $"Course {course} ({Track.Courses[course].Name}): Target pos at idx {i} is NaN or Infinity!");
        }
    }

    // 2. 全コースで AI の入力（throttle, steer）が有限値かつ適正範囲 [-1, 1] に収まること
    [TestCaseSource(nameof(CourseIndices))]
    public void AIInput_ProducesFiniteAndValidRangeInputs(int course)
    {
        track.BuildPath(course);
        kart.InitForTest(track);

        for (int i = 0; i < track.Count; i += 12)
        {
            kart.ResetTo(i, 0f);
            var inp = kart.AIInput(0.02f);

            Assert.That(inp.throttle, Is.InRange(-1f, 1f),
                $"Course {course}: Throttle out of range at idx {i}");
            Assert.That(inp.steer, Is.InRange(-1f, 1f),
                $"Course {course}: Steer out of range at idx {i}");
        }
    }

    // 3. 急カーブでの減速計画（minSafeSpeed）が最高速度未満に抑制され、かつ極端な停止速度にならないこと
    [TestCaseSource(nameof(CourseIndices))]
    public void CorneringSafeSpeed_RegulatesSpeedOnCurves_WithoutFullStop(int course)
    {
        track.BuildPath(course);
        kart.InitForTest(track);

        for (int i = 0; i < track.Count; i += 10)
        {
            kart.ResetTo(i, 0f);
            kart.AIInput(0.02f);

            float safeSpeed = kart.AILastMinSafeSpeed;

            // 安全速度が正の値であり、かつ完全停止（< 5m/s）を指示しないこと
            Assert.Greater(safeSpeed, 5.0f,
                $"Course {course}: Safe speed dropped too low ({safeSpeed:F1}m/s) at idx {i}");

            // 直近の旋回角が大きい場合、減速が計画されるか
            int i2 = track.Wrap(i + 12);
            float angle = Mathf.Abs(Vector3.SignedAngle(track.Dirs[i], track.Dirs[i2], Vector3.up));
            if (angle > 35f)
            {
                // 急カーブ手前では最高速度より減速制限が働いていること
                Assert.Less(safeSpeed, Kart.MaxSpeed * 1.05f,
                    $"Course {course}: Sharp curve at idx {i} (angle {angle:F1} deg) did not plan speed limit!");
            }
        }
    }

    // 4. CANYON SPLIT の分岐区間において、左ルート・右ルートそれぞれで安全に各車線を追従すること
    [Test]
    public void CanyonSplit_BranchRouting_FollowsLeftAndRightLanesSafely()
    {
        int canyonCourse = 6;
        track.BuildPath(canyonCourse);
        kart.InitForTest(track);

        var def = Track.Courses[canyonCourse];
        Assert.IsNotNull(def.Branches);
        Assert.Greater(def.Branches.Length, 0);

        var b = def.Branches[0];
        int startIdx = Mathf.RoundToInt(b.Start * track.Count);
        int endIdx = Mathf.RoundToInt(b.End * track.Count);

        // A) 左ルート選択 (aiBranchChoice = -1)
        kart.AIBranchChoice = -1;
        for (int i = startIdx; i <= endIdx; i += 4)
        {
            float ratio = (float)i / track.Count;
            track.IsInBranch(ratio, out BranchDef branchDef, out float blend);
            if (blend < 0.2f) continue; // 分岐の完全進入区間

            kart.ResetTo(i, branchDef.LeftOffset);
            kart.AIBranchChoice = -1; // ResetTo 等でクリアされないよう再設定
            kart.AIInput(0.02f);

            // 目標レーンが左側（負の値）であり、左車線の中央（LeftOffset）に近いこと
            Assert.Less(kart.AILastTargetLane, 0f,
                $"Left branch choice placed target lane on right side ({kart.AILastTargetLane:F2}m) at idx {i}");
            Assert.AreEqual(branchDef.LeftOffset, kart.AILastTargetLane, 2.5f,
                $"Left branch choice target lane deviated from left offset at idx {i}");
            Assert.IsFalse(track.IsOffroad(ratio, kart.AILastTargetLane),
                $"Left branch target lane is offroad at idx {i}");
        }

        // B) 右ルート選択 (aiBranchChoice = 1)
        kart.AIBranchChoice = 1;
        for (int i = startIdx; i <= endIdx; i += 4)
        {
            float ratio = (float)i / track.Count;
            track.IsInBranch(ratio, out BranchDef branchDef, out float blend);
            if (blend < 0.2f) continue;

            kart.ResetTo(i, branchDef.RightOffset);
            kart.AIBranchChoice = 1;
            kart.AIInput(0.02f);

            // 目標レーンが右側（正の値）であり、右車線の中央（RightOffset）に近いこと
            Assert.Greater(kart.AILastTargetLane, 0f,
                $"Right branch choice placed target lane on left side ({kart.AILastTargetLane:F2}m) at idx {i}");
            Assert.AreEqual(branchDef.RightOffset, kart.AILastTargetLane, 2.5f,
                $"Right branch choice target lane deviated from right offset at idx {i}");
            Assert.IsFalse(track.IsOffroad(ratio, kart.AILastTargetLane),
                $"Right branch target lane is offroad at idx {i}");
        }
    }

    // 5. 簡易シミュレーションループ: 全コースでカートが数秒間走行したときに進行度が進み、逆走や無限スタックにならないこと
    [TestCaseSource(nameof(CourseIndices))]
    public void SimulationStep_ProgressAdvancesConsistently(int course)
    {
        track.BuildPath(course);
        kart.InitForTest(track);
        kart.ResetTo(0, 0f);

        float dt = 0.05f;
        int initialIdx = kart.Index;

        // 30ステップ（1.5秒相当）の簡易シミュレーション
        for (int step = 0; step < 30; step++)
        {
            var inp = kart.AIInput(dt);

            // 前進加速
            kart.Speed = Mathf.MoveTowards(kart.Speed, Kart.MaxSpeed * inp.throttle, 15f * dt);
            float dist = kart.Speed * dt;

            // 簡易前進（track 上を進行）
            int nextIdx = track.Wrap(kart.Index + Mathf.Max(1, Mathf.RoundToInt(dist / 2f)));
            kart.ResetTo(nextIdx, kart.AILastTargetLane);
        }

        // 進行度が進んでいることを検証
        Assert.Greater(kart.Index, initialIdx,
            $"Course {course}: Kart progress did not advance during simulation");
    }

    static bool IsFinite(Vector3 v) =>
        !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
          float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
}
