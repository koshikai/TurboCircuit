using System.Linq;
using NUnit.Framework;
using UnityEngine;

// 全コースの経路（Track.BuildPath）の幾何を検証する。描画やレース進行を使わないため EditMode で動く。
public class TrackGeometryTests
{
    // Track は 2m 間隔でリサンプルする。閉じ目（最後の点→最初の点）だけは、近すぎる終点を落とす処理で最大 約3.2m になる。
    const float MaxPointGap = 2.2f;
    const float MaxClosingGap = 3.3f;

    static int[] CourseIndices => Enumerable.Range(0, Track.DefaultCourses.Length).ToArray();

    GameObject go;
    Track track;

    [SetUp]
    public void SetUp()
    {
        go = new GameObject("TrackGeometryTests");
        track = go.AddComponent<Track>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(go);
    }

    [Test]
    public void LoadedCourses_MatchTheCodeDefaults()
    {
        // Resources の CourseData（ExportAllData の出力）がコード側の既定値と食い違っていないこと。
        Assert.AreEqual(Track.DefaultCourses.Length, Track.Courses.Length);
        for (int i = 0; i < Track.DefaultCourses.Length; i++)
        {
            Assert.AreEqual(Track.DefaultCourses[i].Name, Track.Courses[i].Name, $"course {i} name");
            Assert.AreEqual(Track.DefaultCourses[i].Control.Length, Track.Courses[i].Control.Length, $"course {i} control points");
        }
    }

    [TestCaseSource(nameof(CourseIndices))]
    public void BuildPath_ProducesAClosedFinitePath(int course)
    {
        track.BuildPath(course);

        Assert.Greater(track.Count, 50, $"course {course}: too few points");
        Assert.That(track.Length, Is.InRange(200f, 8000f), $"course {course}: length {track.Length}");
        foreach (var p in track.Pts)
            Assert.IsTrue(IsFinite(p), $"course {course}: non-finite point");

        for (int i = 0; i < track.Count; i++)
        {
            bool closing = i == track.Count - 1;
            float gap = (track.Pts[track.Wrap(i + 1)] - track.Pts[i]).magnitude;
            Assert.LessOrEqual(gap, closing ? MaxClosingGap : MaxPointGap, $"course {course}: gap {gap:F2}m between points {i} and {i + 1}");
        }
    }

    [TestCaseSource(nameof(CourseIndices))]
    public void BuildPath_FramesAreUnitLengthAndDistanceIncreases(int course)
    {
        track.BuildPath(course);

        for (int i = 0; i < track.Count; i++)
        {
            Assert.AreEqual(1f, track.Dirs[i].magnitude, 1e-3f, $"course {course}: Dirs[{i}]");
            Assert.AreEqual(1f, track.Rights[i].magnitude, 1e-3f, $"course {course}: Rights[{i}]");
            Assert.AreEqual(1f, track.Normals[i].magnitude, 1e-3f, $"course {course}: Normals[{i}]");
            if (i > 0)
                Assert.Greater(track.Dist[i], track.Dist[i - 1], $"course {course}: Dist not increasing at {i}");
        }
        Assert.Greater(track.Length, track.Dist[track.Count - 1]);
    }

    [TestCaseSource(nameof(CourseIndices))]
    public void Locate_OnTheCenterLine_ReturnsTheSameIndexAndNoLateralOffset(int course)
    {
        track.BuildPath(course);

        for (int i = 0; i < track.Count; i += 25)
        {
            int index = i;
            track.Locate(track.PointAt(i, 0f), ref index, out float lateral, out float progress);
            Assert.AreEqual(i, index, $"course {course}: index at {i}");
            Assert.AreEqual(0f, lateral, 0.5f, $"course {course}: lateral at {i}");
            Assert.AreEqual(track.Dist[i], progress, 1.5f, $"course {course}: progress at {i}");
        }
    }

    [TestCaseSource(nameof(CourseIndices))]
    public void Locate_ReportsTheLateralOffsetOfAPointBesideTheCenterLine(int course)
    {
        track.BuildPath(course);

        foreach (float offset in new[] { -4f, 4f })
        {
            for (int i = 0; i < track.Count; i += 40)
            {
                int index = i;
                track.Locate(track.PointAt(i, offset), ref index, out float lateral, out _);
                Assert.AreEqual(offset, lateral, 0.5f, $"course {course}: lateral {offset} at {i}");
            }
        }
    }

    [TestCaseSource(nameof(CourseIndices))]
    public void Locate_ProgressAdvancesAlongThePath(int course)
    {
        track.BuildPath(course);

        int index = 0;
        float previous = -1f;
        for (int i = 0; i < track.Count; i += 10)
        {
            track.Locate(track.PointAt(i, 0f), ref index, out _, out float progress);
            Assert.Greater(progress, previous, $"course {course}: progress did not advance at {i}");
            previous = progress;
        }
    }

    [TestCaseSource(nameof(CourseIndices))]
    public void JumpRamps_ArePlacedInsideThePathWithPositivePower(int course)
    {
        track.BuildPath(course);

        foreach (var ramp in track.JumpRamps)
        {
            Assert.That(ramp.index, Is.InRange(0, track.Count - 1), $"course {course}: ramp index");
            Assert.Greater(ramp.power, 0f, $"course {course}: ramp power");
            Assert.That(Mathf.Abs(ramp.lateral), Is.LessThanOrEqualTo(30f), $"course {course}: ramp lateral");
        }
    }

    [TestCaseSource(nameof(CourseIndices))]
    public void Branches_HaveValidRangesAndAreDetectedInside(int course)
    {
        track.BuildPath(course);

        var branches = Track.Courses[course].Branches;
        if (branches == null) return;

        foreach (var b in branches)
        {
            Assert.That(b.Start, Is.InRange(0f, 1f), $"course {course}: branch start");
            Assert.That(b.End, Is.InRange(0f, 1f), $"course {course}: branch end");
            Assert.Less(b.Start, b.End, $"course {course}: branch start >= end");
            Assert.Greater(b.LaneWidth, 0f, $"course {course}: lane width");
            Assert.GreaterOrEqual(b.DividerWidth, 0f, $"course {course}: divider width");

            float mid = (b.Start + b.End) * 0.5f;
            Assert.IsTrue(track.IsInBranch(mid, out _, out _), $"course {course}: midpoint {mid} not in branch");
        }
    }

    [Test]
    public void IsOffroad_OnStraightPath_RecognizesRoadAndOutside()
    {
        track.BuildPath(0);
        Assert.IsFalse(track.IsOffroad(0.1f, 0f), "center line");
        Assert.IsFalse(track.IsOffroad(0.1f, 8.5f), "inside road");
        Assert.IsFalse(track.IsOffroad(0.1f, 9.8f), "on curb");
        Assert.IsTrue(track.IsOffroad(0.1f, 10.5f), "outside curb");
        Assert.IsTrue(track.IsOffroad(0.1f, -11.0f), "outside left curb");
    }

    [Test]
    public void IsOffroad_InBranchSection_RecognizesBothLanesAndRiver()
    {
        int canyonCourse = 6;
        track.BuildPath(canyonCourse);
        var branch = Track.Courses[canyonCourse].Branches[0];
        float mid = (branch.Start + branch.End) * 0.5f;

        // 左右の各車線（アスファルト・木橋）はオンロード
        Assert.IsFalse(track.IsOffroad(mid, branch.LeftOffset), "left branch center");
        Assert.IsFalse(track.IsOffroad(mid, branch.LeftOffset - branch.LaneWidth * 0.5f), "left branch mid");
        Assert.IsFalse(track.IsOffroad(mid, branch.LeftOffset + branch.LaneWidth * 0.8f), "left branch inner edge");

        Assert.IsFalse(track.IsOffroad(mid, branch.RightOffset), "right branch center");
        Assert.IsFalse(track.IsOffroad(mid, branch.RightOffset - branch.LaneWidth * 0.5f), "right branch mid");
        Assert.IsFalse(track.IsOffroad(mid, branch.RightOffset + branch.LaneWidth * 0.8f), "right branch outer edge");

        // 中央の激流リバー（左右車線の間）およびコース外側はオフロード
        Assert.IsTrue(track.IsOffroad(mid, 0f), "river center");
        Assert.IsTrue(track.IsOffroad(mid, 5.0f), "river right");
        Assert.IsTrue(track.IsOffroad(mid, -5.0f), "river left");
        Assert.IsTrue(track.IsOffroad(mid, branch.RightOffset + branch.LaneWidth + Track.CurbWidth + 2f), "beyond right curb");
        Assert.IsTrue(track.IsOffroad(mid, branch.LeftOffset - branch.LaneWidth - Track.CurbWidth - 2f), "beyond left curb");
    }

    [Test]
    public void PointAt_InBranchSection_ElevatesRightLaneOnly()
    {
        int canyonCourse = 6;
        track.BuildPath(canyonCourse);
        var branch = Track.Courses[canyonCourse].Branches[0];
        float mid = (branch.Start + branch.End) * 0.5f;
        int idx = Mathf.RoundToInt(mid * track.Count);

        Vector3 leftPt = track.PointAt(idx, branch.LeftOffset);
        Vector3 rightPt = track.PointAt(idx, branch.RightOffset);
        Vector3 centerPt = track.PointAt(idx, 0f);

        // 右車線（木橋）は RightHeight (4.5m) 上昇、左車線と中央川底は地面高度
        float rightHDiff = rightPt.y - track.Pts[idx].y;
        float leftHDiff = leftPt.y - track.Pts[idx].y;
        float centerHDiff = centerPt.y - track.Pts[idx].y;

        Assert.AreEqual(branch.RightHeight, rightHDiff, 0.2f, "right lane elevated");
        Assert.AreEqual(0f, leftHDiff, 0.2f, "left lane on ground");
        Assert.AreEqual(0f, centerHDiff, 0.2f, "center at ground");
    }

    static bool IsFinite(Vector3 v) =>
        !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
          float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
}
