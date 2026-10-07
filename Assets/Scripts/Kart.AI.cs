using UnityEngine;

// CPU の運転（速度計画・ライン追従・ドリフト・スタック脱出）
public partial class Kart
{
    // ───────────────────────── AI (Tanaka Control / Optimal Racing) ─────────────────────────

    public KartInput AIInput(float dt)
    {
        var inp = new KartInput();
        if (track == null || track.Count == 0) return inp;

        float v = Mathf.Abs(Speed);
        float currentLateral = Lateral;
        Vector3 curPos = transform.position;
        Vector3 fwd = Forward;

        // 1. 曲率プロファイル予測と許容限界速度の計算 (MPC / Receding Horizon Prediction)
        // 速度に応じた先読みステップ数 (約14〜38ステップ = 28〜76m 先までスキャン)
        int scanSteps = Mathf.Clamp(14 + Mathf.RoundToInt(v * 0.6f), 14, 38);
        float minSafeSpeed = MaxSpeed * AISpeedFactor();
        float distAccum = 0f;
        float apexCurvature = 0f;
        float apexBend = 0f;
        int apexOffset = 0;

        for (int step = 2; step <= scanSteps; step += 2)
        {
            int i0 = track.Wrap(Index + step - 2);
            int i1 = track.Wrap(Index + step);
            int i2 = track.Wrap(Index + step + 2);

            // ウェイポイント間の角度変化から曲率 kappa を計算
            float segAngle = Vector3.SignedAngle(track.Dirs[i0], track.Dirs[i2], Vector3.up);
            float segDist = (track.Pts[i1] - track.Pts[i0]).magnitude + (track.Pts[i2] - track.Pts[i1]).magnitude;
            if (segDist < 0.1f) segDist = 4f;
            float curvature = Mathf.Abs(segAngle) * Mathf.Deg2Rad / segDist;
            distAccum += segDist * 0.5f;

            if (curvature > apexCurvature)
            {
                apexCurvature = curvature;
                apexBend = segAngle;
                apexOffset = step;
            }

            // 最大許容横加速度 a_lat = v^2 * kappa <= a_max
            // 通常時: 16.5m/s^2, ドリフト時: 22.0m/s^2
            float aLatMax = drifting ? 22f : 16.5f;
            float cornerSpeedLimit = Mathf.Sqrt(aLatMax / Mathf.Max(curvature, 0.004f));

            // 制動安全速度: v_safe = sqrt(v_limit^2 + 2 * a_brake * dist)
            float safeSpeed = Mathf.Sqrt(cornerSpeedLimit * cornerSpeedLimit + 2f * 22f * distAccum);
            if (safeSpeed < minSafeSpeed)
            {
                minSafeSpeed = safeSpeed;
            }
        }

        // 2. 動的レーシングライン生成 (Smooth Racing Line)
        // 極端な端への振りを抑え、コース中央寄りの安全なラインを滑らかにトレース
        float targetLane = 0f;

        if (apexCurvature > 0.015f && Mathf.Abs(apexBend) > 12f)
        {
            float cornerDir = Mathf.Sign(apexBend); // 1 = 右カーブ, -1 = 左カーブ
            if (apexOffset > 10)
            {
                // コーナー手前（アプローチ）：わずかにアウト側に振る
                targetLane = -cornerDir * 1.5f;
            }
            else
            {
                // エペックス付近〜通過：適度にイン側へ寄せる（極端なインベタを避ける）
                targetLane = cornerDir * 2.2f;
            }
        }
        else
        {
            // 直線：個別の走行レーンを維持
            targetLane = Mathf.Clamp(aiLane, -1.8f, 1.8f);
        }

        // 3. 非線形 Stanley 操舵制御 (Smooth Path Tracking Control)
        // 先読み距離を長めに取り、遠くを見て滑らかな円弧を描いて曲がる
        int look = Mathf.Clamp(7 + Mathf.RoundToInt(v * 0.55f), 9, 22);
        int targetIdx = track.Wrap(Index + look);
        Vector3 targetPos = track.PointAt(targetIdx, targetLane);

        // 方位角誤差 theta_e
        Vector3 toTarget = (targetPos - curPos).normalized;
        float headingError = Vector3.SignedAngle(fwd, toTarget, Vector3.up);

        // 横偏差 e_lateral (目標レーシングラインからのズレ)
        float lateralError = currentLateral - targetLane;
        // Stanley クロストラック制御則: delta = arctan(k * e / (v + v0))
        float kCross = 1.0f;
        float crossSteer = -Mathf.Atan2(kCross * lateralError, Mathf.Max(v, 4.0f)) * Mathf.Rad2Deg;

        // 先読み曲率フィードフォワード（旋回遅れを緩やかに先回り補正）
        float futureAngle = Vector3.SignedAngle(track.Dirs[Index], track.Dirs[track.Wrap(Index + 12)], Vector3.up);
        float ffSteer = Mathf.Clamp(futureAngle * 0.02f, -0.30f, 0.30f);

        // 操舵入力の合算（ゲインを落ち着かせ、過敏な急ハンドルを防止）
        float steerCmd = (headingError * 0.028f) + (crossSteer * 0.015f) + ffSteer;
        inp.steer = Mathf.Clamp(steerCmd, -1f, 1f);

        // 4. 速度・制動制御 (Speed Profiling & Longitudinal Control)
        // 注意: inp.throttle < 0 は物理的に「バック走行（後退）」を引き起こすため、
        // 前進中 (Speed > 6.0m/s) かつ 安全速度を大きく超えている時のみ一時的なブレーキとして使用する。
        float speedMargin = Speed - minSafeSpeed;
        if (speedMargin > 2.5f && Speed > 7.0f)
        {
            // コーナー手前での的確なブレーキ（前進速度が十分ある時のみ減速）
            inp.throttle = -1f;
        }
        else if (speedMargin > 0.5f && Speed > minSafeSpeed)
        {
            // 目標速度を超えている場合はコースティング（アクセルオフ）
            inp.throttle = 0f;
        }
        else
        {
            // 基本は常に前進全開加速！
            inp.throttle = 1f;
        }

        // 逆走ガード：コース順方向と逆を向いている場合は、素早く前進ステアで方向転換
        float forwardDot = Vector3.Dot(fwd, track.Dirs[Index]);
        if (forwardDot < -0.1f)
        {
            float correctAngle = Vector3.SignedAngle(fwd, track.Dirs[Index], Vector3.up);
            inp.steer = Mathf.Sign(correctAngle);
            inp.throttle = 1f; // バックではなく前進で方向転換
            inp.drift = false;
            aiSteer = inp.steer;
            return inp;
        }

        // 5. 自己駆動ドリフト制御 (Self-Triggered Smooth Drift)
        // ドリフト発動トリガー：
        // 中速カーブは通常ステアで曲がり、ヘアピン等の急カーブ（曲率 > 0.026 または旋回角 > 34度）でのみ発動
        bool canStartDrift = !drifting 
            && (apexCurvature > 0.026f || Mathf.Abs(futureAngle) > 34f) 
            && v > 15f 
            && Mathf.Sign(futureAngle) == Mathf.Sign(inp.steer) 
            && Mathf.Abs(headingError) > 8f;

        if (canStartDrift)
        {
            inp.drift = true;
            inp.driftDown = true;
        }
        else if (drifting)
        {
            // ドリフト継続トリガー：カーブが続いており、かつ外壁に突っ込まない安全領域
            bool curveRemains = Mathf.Abs(futureAngle) > 14f && Mathf.Sign(futureAngle) == driftDir;
            bool wallSafe = Mathf.Abs(currentLateral) < Track.HalfWidth - 0.5f;

            // ミニターボがチャージされ、コーナーの出口が見えたら即座に解除してロケットダッシュ！
            if (curveRemains && wallSafe && DriftLevel < 2)
            {
                inp.drift = true;
                // ドリフト中はイン固定ではなく、目標方位に合わせて微調整（カウンターステア／微インステア）
                // これによりイン側への極端な巻き込み内壁激突を完全に防止！
                float driftSteer = headingError * 0.022f;
                inp.steer = Mathf.Clamp(driftSteer, -0.45f, 0.45f);
            }
            else
            {
                // ドリフト解除！ミニターボ発動！
                inp.drift = false;
            }
        }

        // 6. コントロール・バリア・ファンクション (CBF / 滑らかな境界反発)
        float safeBoundary = Track.HalfWidth - 0.8f; // 8.2m
        float distToEdge = safeBoundary - Mathf.Abs(currentLateral);
        float outwardVelocity = Vector3.Dot(VelDir, track.Rights[Index] * Mathf.Sign(currentLateral));

        if (distToEdge < 2.0f && outwardVelocity > 0.05f)
        {
            // 距離に応じた滑らかな比例反発（急激なフルステア反発で蛇行するのを防ぐ）
            float repelIntensity = Mathf.Clamp01((2.0f - distToEdge) / 1.5f);
            float repelSteer = -Mathf.Sign(currentLateral) * repelIntensity * 0.55f;
            inp.steer = Mathf.Clamp(inp.steer + repelSteer, -1f, 1f);
            inp.drift = false; // コース端での横滑りを即座に収束
            if (distToEdge < 0.6f && outwardVelocity > 0.15f && Speed > 7.0f)
            {
                inp.throttle = -1f; // 十分な前進速度がある場合のみ減速ブレーキ
            }
        }

        // ステアリングの滑らかな補間（カクカクした急ハンドルを根絶）
        aiSteer = Mathf.MoveTowards(aiSteer, inp.steer, dt * 4.5f);
        inp.steer = aiSteer;

        // 7. スタック脱出 (Anti-Stuck Logic)
        // スタート直後（レース開始後3秒以内）は絶対にスタック判定を行わない！
        // また、前進入力中 (inp.throttle > 0) なのに壁に引っかかって車速がほぼゼロ (< 1.2m/s) の状態が
        // 2.5秒以上続いた場合のみ、一時的なバック脱出 (0.4秒間) を行う。
        if (rm != null && rm.RaceRunning && rm.RaceTime > 3.0f && Speed < 1.2f && inp.throttle > 0)
        {
            aiStuck += dt;
        }
        else
        {
            aiStuck = 0;
        }

        if (aiStuck > 2.5f)
        {
            aiReverse = 0.5f;
            aiStuck = 0;
        }

        if (aiReverse > 0)
        {
            aiReverse -= dt;
            inp.throttle = -1f; // 0.5秒だけ後退脱出
            inp.steer = -Mathf.Sign(headingError != 0 ? headingError : (Lateral >= 0 ? 1f : -1f));
            aiSteer = inp.steer;
            inp.drift = false;
        }

        // 8. 戦略的アイテム使用 (Strategic Item Usage)
        if (Item != ItemType.None && RouletteTimer <= 0)
        {
            aiItemTimer -= dt;
            bool canUse = false;
            switch (Item)
            {
                case ItemType.Missile:
                    canUse = rm.KartAheadWithin(this, 55f);
                    break;
                case ItemType.Banana:
                    canUse = rm.KartBehindWithin(this, 18f);
                    break;
                case ItemType.Turbo:
                    canUse = Mathf.Abs(futureAngle) < 15f && Mathf.Abs(currentLateral) < Track.HalfWidth - 1.5f;
                    break;
                case ItemType.Shield:
                    canUse = true;
                    break;
            }
            if (aiItemTimer <= 0 && (canUse || aiItemTimer < -3.5f))
            {
                inp.useItem = true;
                aiItemTimer = Random.Range(1.5f, 3.0f);
            }
        }

        return inp;
    }

    float AISpeedFactor()
    {
        if (IsPlayer && !Finished && !rm.Demo) return 1f;
        float f = aiSkill;
        var player = rm.Player;
        if (player != null && !player.Finished && player != this)
        {
            // ラバーバンド：プレイヤーとの差に応じて少し速さを調整
            float diff = player.RaceDistance - RaceDistance;
            f += Mathf.Clamp(diff / 220f, -0.06f, 0.08f);
        }
        return Mathf.Clamp(f, 0.90f, 1.05f);
    }
}
