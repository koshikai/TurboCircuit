using UnityEngine;

// レース開始前のコース紹介シネマティック（Course Flythrough Intro）演出
public partial class RaceManager
{
    struct CourseIntroCut
    {
        public Vector3 camPosStart;
        public Vector3 camPosEnd;
        public Vector3 lookTargetStart;
        public Vector3 lookTargetEnd;
        public float fovStart;
        public float fovEnd;
    }

    const float IntroCut1Duration = 2.6f;
    const float IntroCut2Duration = 1.8f;
    public const float IntroTotalDuration = IntroCut1Duration + IntroCut2Duration; // 4.4秒

    static readonly CourseIntroCut[] CourseIntroCuts =
    {
        // 0: TURBO CIRCUIT - 高架橋立体交差ピークから北側ジャンプ台へ抜けるパノラマスウィープ
        new CourseIntroCut
        {
            camPosStart = new Vector3(170f, 27f, -15f),
            camPosEnd = new Vector3(215f, 22f, 65f),
            lookTargetStart = new Vector3(192f, 18f, 28f),
            lookTargetEnd = new Vector3(132f, 8f, 150f),
            fovStart = 58f,
            fovEnd = 52f
        },
        // 1: SUNSET DUNES - 夕陽を背負う超巨大砂丘の尾根滑走ライン
        new CourseIntroCut
        {
            camPosStart = new Vector3(210f, 38f, -25f),
            camPosEnd = new Vector3(260f, 30f, 55f),
            lookTargetStart = new Vector3(240f, 26f, 20f),
            lookTargetEnd = new Vector3(170f, 22f, 90f),
            fovStart = 62f,
            fovEnd = 54f
        },
        // 2: FROST PEAK - 標高36m雪山頂上から白銀の氷河台地を見下ろす雄大なアングル
        new CourseIntroCut
        {
            camPosStart = new Vector3(155f, 50f, -5f),
            camPosEnd = new Vector3(225f, 41f, 70f),
            lookTargetStart = new Vector3(200f, 36f, 30f),
            lookTargetEnd = new Vector3(160f, 33f, 90f),
            fovStart = 64f,
            fovEnd = 55f
        },
        // 3: NEON METROPOLIS - 高層ビル群の谷間を抜ける高架ハイウェイとサイバーネオン
        new CourseIntroCut
        {
            camPosStart = new Vector3(185f, 25f, 65f),
            camPosEnd = new Vector3(115f, 18f, 125f),
            lookTargetStart = new Vector3(150f, 13f, 105f),
            lookTargetEnd = new Vector3(60f, 13f, 110f),
            fovStart = 58f,
            fovEnd = 50f
        },
        // 4: HOKKAIDO CAMPUS - 黄金色に輝く北13条イチョウ並木通りを東へ真っ直ぐ抜ける絶景
        new CourseIntroCut
        {
            camPosStart = new Vector3(110f, 14f, 45f),
            camPosEnd = new Vector3(175f, 9f, 100f),
            lookTargetStart = new Vector3(150f, 2f, 75f),
            lookTargetEnd = new Vector3(130f, 2f, 125f),
            fovStart = 60f,
            fovEnd = 52f
        },
        // 5: SKYLINE OVERPASS - 海岸ベイエリアに聳える標高15mの巨大斜張橋と立体交差
        new CourseIntroCut
        {
            camPosStart = new Vector3(50f, 28f, 80f),
            camPosEnd = new Vector3(-35f, 23f, 135f),
            lookTargetStart = new Vector3(0f, 15f, 90f),
            lookTargetEnd = new Vector3(0f, 14.5f, 10f),
            fovStart = 62f,
            fovEnd = 54f
        }
    };

    void InitCourseIntro()
    {
        introTime = 0f;
    }

    float introTime;

    void UpdateIntro(MenuNav nav, float dt)
    {
        introTime += dt;

        bool modalOpen = (updater != null && updater.ShowModal) || showSettings || paused || showQuitConfirm;

        // スキップ入力（SPACE / ENTER / Aボタン / START / マウスクリック）
        bool skipRequested = Input.GetKeyDown(KeyCode.Space) ||
                             Input.GetKeyDown(KeyCode.Return) ||
                             Input.GetKeyDown(KeyCode.KeypadEnter) ||
                             nav.confirm ||
                             nav.padStart ||
                             (Input.GetMouseButtonDown(0) && !modalOpen);

        if (skipRequested || introTime >= IntroTotalDuration)
        {
            StartCountdown();
        }
    }

    void UpdateIntroCamera(Camera c, float dt)
    {
        if (track == null || track.Count == 0 || Player == null) return;

        int cIdx = Mathf.Clamp(SelectedCourse, 0, CourseIntroCuts.Length - 1);
        var cut1 = CourseIntroCuts[cIdx];

        if (introTime < IntroCut1Duration)
        {
            // ───────────────── Cut 1: コースランドマーク空撮ドリー ─────────────────
            float t01 = Mathf.Clamp01(introTime / IntroCut1Duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t01);

            Vector3 camPos = Vector3.Lerp(cut1.camPosStart, cut1.camPosEnd, smoothT);
            Vector3 lookTarget = Vector3.Lerp(cut1.lookTargetStart, cut1.lookTargetEnd, smoothT);
            float targetFov = Mathf.Lerp(cut1.fovStart, cut1.fovEnd, smoothT);

            c.transform.position = camPos;
            c.transform.LookAt(lookTarget);
            c.fieldOfView = targetFov;
        }
        else
        {
            // ───────────────── Cut 2: スターティンググリッド突入 ─────────────────
            float t01 = Mathf.Clamp01((introTime - IntroCut1Duration) / IntroCut2Duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t01);

            // スタート地点の前方・上空からグリッドを見渡す開始点
            var trackPt = track.PointAt(0, 0);
            var trackDir = track.Dirs[0];
            var trackRight = track.Rights[0];

            Vector3 gridCamStart = trackPt + trackDir * 16f + trackRight * 5.5f + Vector3.up * 4.2f;
            Vector3 gridLookStart = trackPt + trackDir * 1.5f + Vector3.up * 0.9f;

            // カウントダウン初期カメラ位置（プレイヤー背後、yaw + 180°の回り込み開始点）
            float pYaw = Player.Heading + 180f;
            var r = Quaternion.Euler(0f, pYaw, 0f);
            Vector3 countdownCamPos = Player.transform.position + r * new Vector3(0f, 2.7f, -5f);
            Vector3 countdownLookTarget = Player.transform.position + r * new Vector3(0f, 1.1f, 3.2f);

            c.transform.position = Vector3.Lerp(gridCamStart, countdownCamPos, smoothT);
            Vector3 curLook = Vector3.Lerp(gridLookStart, countdownLookTarget, smoothT);
            c.transform.LookAt(curLook);
            c.fieldOfView = Mathf.Lerp(66f, 60f, smoothT);
        }
    }

    void DrawIntroUI(float w, float h)
    {
        // 1. 映画風レターボックス（上下シネマティック黒帯）
        float barHeight = 52f;
        // 開始時（0〜0.4秒）と終了直前（4.0〜4.4秒）でスムーズにイン/アウト
        float barAlpha = 1f;
        if (introTime < 0.4f) barAlpha = Mathf.Clamp01(introTime / 0.4f);
        else if (introTime > IntroTotalDuration - 0.4f) barAlpha = Mathf.Clamp01((IntroTotalDuration - introTime) / 0.4f);

        GUI.color = new Color(0.04f, 0.05f, 0.08f, 0.92f * barAlpha);
        GUI.DrawTexture(new Rect(0, 0, w, barHeight), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0, h - barHeight, w, barHeight), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // 2. コース紹介情報カード（画面左下）
        float cardSlide = Mathf.Clamp01(introTime / 0.5f);
        float cardAlpha = introTime > IntroTotalDuration - 0.35f ? Mathf.Clamp01((IntroTotalDuration - introTime) / 0.35f) : cardSlide;

        if (cardAlpha > 0.01f)
        {
            var courses = Track.Courses;
            string rawName = (courses != null && SelectedCourse >= 0 && SelectedCourse < courses.Length)
                ? courses[SelectedCourse].Name
                : "CIRCUIT";
            string courseName = Loc.CourseName(SelectedCourse, rawName);

            float cardW = 420f, cardH = 96f;
            float cardX = 36f;
            float cardY = h - barHeight - cardH - 18f;

            Color prevGuiCol = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, cardAlpha);

            DrawPopCard(new Rect(cardX, cardY, cardW, cardH));

            // ヘッダーリボン
            string headerText = Loc.T($"★  COURSE 0{SelectedCourse + 1}  ★", $"★  コース 0{SelectedCourse + 1}  ★");
            DrawPopRibbon(new Rect(cardX + 16f, cardY - 14f, 160f, 28f), ribbonTrackTex, headerText);

            // コース名（太字ポップフォント）
            GUI.Label(new Rect(cardX + 20f, cardY + 22f, cardW - 40f, 32f), courseName, St(sBig, 22, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.12f, 0.16f, 0.28f)));

            // 周回数＆レコードタイム情報（絵文字フォントの豆腐化を防ぎ、クリーンなドット区切りで表記）
            float bestTime = GetBestTime(SelectedCourse);
            string bestStr = bestTime > 0.01f ? FormatTime(bestTime) : "--:--.--";
            string infoText = Loc.T($"{totalLaps} LAPS  •  BEST: {bestStr}  •  8 RACERS", $"{totalLaps}周レース  •  最速記録: {bestStr}  •  8台出走");
            GUI.Label(new Rect(cardX + 20f, cardY + 58f, cardW - 40f, 24f), infoText, St(sMid, 14, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.35f, 0.42f, 0.55f)));

            GUI.color = prevGuiCol;
        }

        // 3. 画面右下：スキップ案内ボタン
        float skipW = 180f, skipH = 34f;
        float skipX = w - skipW - 32f;
        float skipY = h - barHeight - skipH - 18f;

        float pulse = 0.85f + Mathf.PingPong(Time.time * 2.5f, 0.15f);
        Color skipBtnCol = new Color(0.15f, 0.20f, 0.30f, 0.85f * pulse);

        if (DrawModernButton(new Rect(skipX, skipY, skipW, skipH), Loc.T("SKIP", "スキップ"), "[SPACE / A]", skipBtnCol, false, false))
        {
            StartCountdown();
        }
    }
}
