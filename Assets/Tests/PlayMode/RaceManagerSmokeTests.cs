using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// PlayMode スモークテスト（Issue #82）:
// RaceManager を生成してフレームを進め、例外ゼロ・全カート座標が有限値・CPU レースが前進することを検証する。
public class RaceManagerSmokeTests
{
    readonly HashSet<GameObject> initialRoots = new HashSet<GameObject>();
    float savedTimeScale;

    [SetUp]
    public void SetUp()
    {
        savedTimeScale = Time.timeScale;
        initialRoots.Clear();
        var scene = SceneManager.GetActiveScene();
        if (scene.IsValid())
        {
            foreach (var root in scene.GetRootGameObjects())
                initialRoots.Add(root);
        }
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = savedTimeScale;
        AudioListener.pause = false;
        var scene = SceneManager.GetActiveScene();
        if (scene.IsValid())
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root != null && !initialRoots.Contains(root))
                    Object.Destroy(root);
            }
        }
        initialRoots.Clear();
    }

    [UnityTest]
    public IEnumerator RaceManager_TitleIdle_StepsFramesWithFiniteKartPositions()
    {
        var rmGo = new GameObject("RaceManager_SmokeTest");
        var rm = rmGo.AddComponent<RaceManager>();

        // Awake/Start と数フレームの Title 更新を回す
        for (int i = 0; i < 10; i++)
            yield return null;

        Assert.IsNotNull(rm, "RaceManager should remain alive");
        Assert.IsFalse(rm.RaceRunning, "Title state should not be RaceRunning");
        Assert.AreEqual(8, rm.Karts.Count, "Expected 8 karts on the grid");
        Assert.IsNotNull(rm.Player, "Player kart should be assigned");

        foreach (var k in rm.Karts)
        {
            Assert.IsNotNull(k);
            Assert.IsTrue(IsFinite(k.transform.position), $"{k.Name} has non-finite position");
            Assert.IsTrue(IsFinite(k.RaceDistance), $"{k.Name} has non-finite RaceDistance");
        }
    }

    [UnityTest]
    public IEnumerator RaceManager_DemoCpuRace_Course0_ProgressesForwardWithoutErrors()
    {
        yield return RunCpuRaceSmokeTest(0);
    }

    [UnityTest]
    public IEnumerator RaceManager_DemoCpuRace_Course6_ProgressesForwardWithoutErrors()
    {
        yield return RunCpuRaceSmokeTest(6);
    }

    static IEnumerator RunCpuRaceSmokeTest(int course)
    {
        var rmGo = new GameObject($"RaceManager_SmokeCourse{course}");
        var rm = rmGo.AddComponent<RaceManager>();

        // 1フレーム待って Awake/Start を完了させる
        yield return null;

        if (course != 0)
            rm.LoadCourse(course);

        // プレイヤーも CPU 運転にし、イントロをスキップしてカウントダウン開始
        var demoSetter = typeof(RaceManager).GetProperty("Demo", BindingFlags.Instance | BindingFlags.Public)?.GetSetMethod(true);
        Assert.IsNotNull(demoSetter, "RaceManager.Demo private setter not found");
        demoSetter.Invoke(rm, new object[] { true });

        var startCountdown = typeof(RaceManager).GetMethod("StartCountdown", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(startCountdown, "RaceManager.StartCountdown not found");
        startCountdown.Invoke(rm, new object[] { false });

        Assert.IsTrue(rm.InCountdown, "RaceManager should enter Countdown state");

        var startDistances = new float[rm.Karts.Count];
        for (int i = 0; i < rm.Karts.Count; i++)
            startDistances[i] = rm.Karts[i].RaceDistance;

        // ヘッドレス環境でも短時間でカウントダウン(4秒) + 走行(2秒)が進むよう timeScale を上げる
        Time.timeScale = Application.isBatchMode ? 5f : 4f;

        const int maxFrames = 1200;
        int frames = 0;
        while ((!rm.RaceRunning || rm.RaceTime < 2.0f) && frames < maxFrames)
        {
            frames++;
            yield return null;
        }

        Assert.IsTrue(rm.RaceRunning, $"Race did not start within {maxFrames} frames on course {course}");
        Assert.GreaterOrEqual(rm.RaceTime, 2.0f, $"RaceTime did not advance to 2.0s on course {course}");

        for (int i = 0; i < rm.Karts.Count; i++)
        {
            var k = rm.Karts[i];
            Assert.IsNotNull(k);
            Assert.IsTrue(IsFinite(k.transform.position), $"Course {course}: {k.Name} non-finite position");
            Assert.IsTrue(IsFinite(k.Speed), $"Course {course}: {k.Name} non-finite speed");
            Assert.Greater(k.RaceDistance, startDistances[i] + 5f,
                $"Course {course}: {k.Name} did not progress forward (start={startDistances[i]:F1}, now={k.RaceDistance:F1})");
        }
    }

    static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

    static bool IsFinite(Vector3 v) => IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
}
