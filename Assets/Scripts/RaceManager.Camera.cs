using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// カメラ：追従カメラ、後方確認、観戦切り替え、ポストプロセス
public partial class RaceManager
{
    void SetupPostProcessing()
    {
        var camData = cam.GetUniversalAdditionalCameraData();
        if (camData != null) camData.renderPostProcessing = true;

        var camData2 = cam2.GetUniversalAdditionalCameraData();
        if (camData2 != null) camData2.renderPostProcessing = true;

        var volGo = new GameObject("Global PostProcess Volume");
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "RuntimePostProcessProfile";

        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.value = 0.95f;
        bloom.threshold.value = 0.85f;
        bloom.scatter.value = 0.72f;
        bloom.tint.value = Color.white;

        var tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.mode.value = TonemappingMode.ACES;

        var colorAdj = profile.Add<ColorAdjustments>(true);
        colorAdj.contrast.value = 12f;
        colorAdj.saturation.value = 14f;

        vol.profile = profile;
    }

    // ───────────────────────── Camera ─────────────────────────

    void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        if (paused) return;

        // 対戦中は左右 2 分割（P1 = 左、P2 = 右）
        bool split = TwoPlayer && state != State.Title;
        cam.rect = split ? new Rect(0f, 0f, 0.5f, 1f) : new Rect(0f, 0f, 1f, 1f);
        if (cam2.enabled != split) cam2.enabled = split;

        if (state == State.Title)
        {
            if (LobbyActive && track != null && track.Count > 0)
            {
                UpdateLobbyStage(dt);
            }
            else if (Player != null && track != null && track.Count > 0)
            {
                ShowAllKarts();
                var trackPt = track.PointAt(0, 0);
                var trackDir = track.Dirs[0];
                var trackRight = track.Rights[0];

                Player.transform.position = trackPt;
                float baseHeading = Quaternion.LookRotation(trackDir).eulerAngles.y;
                Player.Heading = baseHeading - 25f + Mathf.Sin(Time.time * 1.3f) * 10f;

                // ショールームカメラ：カートの前方やや右寄りローアングルからカート正面を見上げる
                Vector3 desiredCamPos = trackPt + trackDir * 4.4f + trackRight * 1.7f + Vector3.up * 1.05f;
                Vector3 lookTarget = trackPt + Vector3.up * 0.6f;

                cam.transform.position = Vector3.Lerp(cam.transform.position, desiredCamPos, 1f - Mathf.Exp(-5f * dt));
                var targetRot = Quaternion.LookRotation(lookTarget - cam.transform.position);
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, targetRot, 1f - Mathf.Exp(-5f * dt));
                cam.fieldOfView = 48f;
            }
            return;
        }

        Kart targetKart1 = Player;
        if (!TwoPlayer && (state == State.Racing || state == State.Results))
        {
            if (Player.Finished)
            {
                if (spectateIndex < 0) spectateIndex = MyKartIndex;
                if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.JoystickButton4))
                    CycleSpectate(-1);
                else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.JoystickButton5))
                    CycleSpectate(1);

                if (spectateIndex >= 0 && spectateIndex < Karts.Count)
                    targetKart1 = Karts[spectateIndex];
            }
            else
            {
                spectateIndex = -1;
            }
        }

        UpdateChaseCamera(cam, targetKart1, 0, dt);
        if (split) UpdateChaseCamera(cam2, Player2, 1, dt);
    }

    void CycleSpectate(int dir)
    {
        if (Karts.Count <= 1) return;
        if (spectateIndex < 0) spectateIndex = MyKartIndex;
        spectateIndex = (spectateIndex + dir + Karts.Count) % Karts.Count;
        if (Audio != null) Audio.Tick();
    }

    void UpdateChaseCamera(Camera c, Kart pl, int p, float dt)
    {
        var kp = pl.transform.position;
        bool spectating = Spectating;
        if (state == State.Results && Time.time > finishAt + 1.5f && !spectating) camYaws[p] += 25f * dt;
        else camYaws[p] = Mathf.LerpAngle(camYaws[p], pl.Heading, 1f - Mathf.Exp(-5f * dt));

        bool lookBehind = (p == 0 && !pl.Finished && !spectating && 
            (Input.GetKey(InputSettings.KeyRearView) || Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.JoystickButton9)));
        isLookingBehind = lookBehind;

        float yaw = camYaws[p];
        if (lookBehind) yaw += 180f;

        float dist = 6.8f, height = 2.7f;
        if (state == State.Results && !spectating) { dist = 9f; height = 5f; }
        if (state == State.Countdown)
        {
            // カウントダウン中はカートの前から後ろへ回り込む
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(stateTime / 2f));
            yaw += 180f * (1f - t);
            dist = Mathf.Lerp(5f, 6.8f, t);
        }
        else if (state == State.Racing)
        {
            // 加速・ブーストによるダイナミックなカメラの引き込み（Gフォース感）
            float spd01 = Mathf.Clamp01(Mathf.Abs(pl.Speed) / Kart.MaxSpeed);
            float gPull = (pl.Boosting || pl.Slipstreaming ? 1.5f : spd01 * 0.9f);
            dist += gPull;
            height -= gPull * 0.12f;
        }

        var r = Quaternion.Euler(0, yaw, 0);
        var target = kp + r * new Vector3(0, height, -dist);
        bool snap = state == State.Countdown && stateTime < 0.05f;
        c.transform.position = snap ? target : Vector3.Lerp(c.transform.position, target, 1f - Mathf.Exp(-14f * dt));
        float pitchAng = pl.transform.eulerAngles.x;
        if (pitchAng > 180f) pitchAng -= 360f;
        float pitchOffset = Mathf.Clamp(pitchAng * -0.06f, -2.5f, 2.5f);
        c.transform.LookAt(kp + r * new Vector3(0, 1.1f + pitchOffset, 3.2f));
        if (shake > 0) c.transform.position += Random.insideUnitSphere * shake * shake * 0.5f;

        // ダイナミック FOV ブースト：最高速・ブースト・スリップストリーム・トリック連動
        float baseFov = TwoPlayer ? 68f : 60f;
        float spdRatio = Mathf.Clamp01(Mathf.Abs(pl.Speed) / Kart.MaxSpeed);
        float boostFov = (pl.Boosting ? 14f : 0f) + (pl.Slipstreaming ? 11f : 0f) + (pl.InDraftStream ? 3.5f : 0f);
        if (pl.IsAirborne) boostFov += pl.TrickSuccess ? 16f : 11f;
        if (lookBehind) boostFov += 8f;

        float targetFov = baseFov + spdRatio * 6f + boostFov;
        c.fieldOfView = Mathf.Lerp(c.fieldOfView, targetFov, 1f - Mathf.Exp(-7f * dt));
    }
}
