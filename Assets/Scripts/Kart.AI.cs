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
            // ドリフト中のオーバースピードによる膨らみ・イン巻きを防ぐため安全マージンを確保
            float aLatMax = drifting ? 18.0f : 16.5f;
            float cornerSpeedLimit = Mathf.Sqrt(aLatMax / Mathf.Max(curvature, 0.004f));

            // 制動安全速度: v_safe = sqrt(v_limit^2 + 2 * a_brake * dist)
            float safeSpeed = Mathf.Sqrt(cornerSpeedLimit * cornerSpeedLimit + 2f * 22f * distAccum);
            if (safeSpeed < minSafeSpeed)
            {
                minSafeSpeed = safeSpeed;
            }
        }

        // 2. 動的レーシングライン生成 (Smooth Racing Line & Tactical Adjustments)
        // 極端な端への振りを抑え、コース中央寄りの安全なラインを滑らかにトレース
        float targetLane = 0f;

        if (apexCurvature > 0.015f && Mathf.Abs(apexBend) > 12f)
        {
            float cornerDir = Mathf.Sign(apexBend); // 1 = 右カーブ, -1 = 左カーブ
            if (apexOffset > 12)
            {
                // コーナー手前（アプローチ）：わずかにアウト側に振る
                targetLane = -cornerDir * 1.5f;
            }
            else
            {
                // エペックス付近〜通過：ドリフト中は中央寄り(0.8m)、通常旋回時は適度にイン(1.8m)
                targetLane = cornerDir * (drifting ? 0.8f : 1.8f);
            }
        }
        else
        {
            // 直線：個別の走行レーンを維持
            targetLane = Mathf.Clamp(aiLane, -1.8f, 1.8f);
        }

        // 2b. アイテムボックス・ダッシュボード取得と、スリップストリーム／追い抜き・バナナ回避
        targetLane = ApplyTacticalLineAdjustments(curPos, currentLateral, targetLane, apexCurvature, apexBend);

        // 3. 曲率適応型ルックアヘッド & 非線形 Stanley 操舵制御
        // 直近(12m先)と中距離(24m先)のコース旋回角を取得
        float nearAngle = Vector3.SignedAngle(track.Dirs[Index], track.Dirs[track.Wrap(Index + 6)], Vector3.up);
        float futureAngle = Vector3.SignedAngle(track.Dirs[Index], track.Dirs[track.Wrap(Index + 12)], Vector3.up);
        float localCurv = Mathf.Abs(nearAngle) * Mathf.Deg2Rad / 12f;

        // カーブ中やドリフト中に先読み(look)が遠すぎると、視線(弦)がコーナー内側のダートや内壁をショートカットして貫通してしまう。
        // 直線では遠く(12〜16ステップ=24〜32m)を見て滑らかに、カーブやドリフト中は近く(5〜8ステップ=10〜16m)を見て路面中央に沿って旋回する。
        int baseLook = Mathf.Clamp(6 + Mathf.RoundToInt(v * 0.35f), 7, 16);
        int look = drifting
            ? Mathf.Clamp(Mathf.RoundToInt(8f - localCurv * 55f), 5, 8)
            : Mathf.Clamp(Mathf.RoundToInt(baseLook - localCurv * 140f), 6, 16);
        int targetIdx = track.Wrap(Index + look);
        Vector3 targetPos = track.PointAt(targetIdx, targetLane);

        // 車体鼻先(fwd)の方位角誤差と、実際の進行方向ベクトル(VelDir)の方位角誤差
        Vector3 toTarget = (targetPos - curPos).normalized;
        float headingError = Vector3.SignedAngle(fwd, toTarget, Vector3.up);
        float velHeadingError = Vector3.SignedAngle(VelDir, toTarget, Vector3.up);

        // 横偏差 e_lateral (目標レーシングラインからのズレ)
        float lateralError = currentLateral - targetLane;
        // Stanley クロストラック制御則: delta = arctan(k * e / (v + v0))
        float kCross = 1.0f;
        float crossSteer = -Mathf.Atan2(kCross * lateralError, Mathf.Max(v, 4.0f)) * Mathf.Rad2Deg;

        // 先読み曲率フィードフォワード（旋回遅れを緩やかに先回り補正）
        float ffSteer = Mathf.Clamp(futureAngle * 0.022f, -0.32f, 0.32f);

        // 操舵入力の合算（ゲインを落ち着かせ、過敏な急ハンドルを防止）
        float steerCmd = (headingError * 0.030f) + (crossSteer * 0.016f) + ffSteer;
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
            inp.throttle = 1f; // バックではなく前進で方向転換（壁スタック時は下のスタック脱出が優先される）
            inp.drift = false;
            inp.driftDown = false;
            aiSteer = inp.steer;
        }
        else
        {
            // 5. 自己駆動ドリフト制御 (Velocity-Vector Slip-Angle Drift Control)
            // ドリフト発動トリガー：
            // 中速〜急コーナー（24m先の旋回角 > 23度 または コーナー進入域で曲率 > 0.022）で積極的にホップ＆ドリフトを開始！
            float turnDir = Mathf.Sign(Mathf.Abs(futureAngle) > 8f ? futureAngle : nearAngle);
            float entryInwardLat = currentLateral * turnDir;
            bool cornerApproaching = (Mathf.Abs(futureAngle) > 23f || (apexCurvature > 0.022f && apexOffset <= 14))
                                  && Mathf.Abs(nearAngle) > 9f
                                  && Mathf.Sign(nearAngle) == turnDir;
            bool canStartDrift = !drifting
                && cornerApproaching
                && entryInwardLat < 4.2f
                && v > 14.0f
                && (headingError * turnDir) > 4f;

            if (canStartDrift)
            {
                inp.drift = true;
                // Kart.cs のドリフト開始条件 (|steer| > 0.3f) を確実に満たすよう、踏み切り時にしっかりインへステアを入れる
                if (inp.steer * turnDir < 0.48f) inp.steer = turnDir * 0.48f;
                if (aiSteer * turnDir < 0.42f) aiSteer = turnDir * 0.42f;
            }
            else if (drifting)
            {
                // ドリフト中は車体鼻先(fwd)が内側に傾く(スリップ角15〜25度)のが物理的に正常な状態（VelDir = Slerp(VelDir, fwd, 2.6*dt)）。
                // そのため鼻先(headingError)ではなく「実際の進行方向ベクトル(VelDir)」が目標ライン(toTarget)に沿っているかで制御する。
                float inwardLat = currentLateral * driftDir;
                float inwardVel = Vector3.Dot(VelDir, track.Rights[Index] * driftDir);
                float velInwardErr = velHeadingError * driftDir;

                // ドリフト継続条件：
                // 1) カーブがまだ続いている（または青火花 Level 1 直前でまだ緩やかなカーブが残っている）
                bool curveRemains = (Mathf.Abs(nearAngle) > 9f && Mathf.Sign(nearAngle) == driftDir)
                                 || (Mathf.Abs(futureAngle) > 13f && Mathf.Sign(futureAngle) == driftDir)
                                 || (DriftLevel < 1 && driftCharge > 0.5f && Mathf.Abs(futureAngle) > 7f && Mathf.Sign(futureAngle) == driftDir);

                // 2) 進行方向ベクトル(VelDir)ベースのイン巻き防止ガード：
                // イン側5.2m以内、かつイン側深くでさらに内側へ突っ込んでいないこと
                bool inwardSafe = inwardLat < 5.2f
                               && !(inwardLat > 3.6f && inwardVel > 0.20f)
                               && !(inwardLat > 2.2f && velInwardErr < -12f);

                // 3) アウト側ガード：コース外縁から1.4m以上内側
                bool outwardSafe = Mathf.Abs(currentLateral) < Track.HalfWidth - 1.4f;

                // 長いカーブ・ヘアピンでは Level 3（紫火花）、中程度のコーナーでは Level 2（オレンジ火花）まで溜める
                int targetDriftLv = (Mathf.Abs(nearAngle) > 18f && Mathf.Abs(futureAngle) > 30f && inwardLat < 3.0f) ? 3 : 2;

                if (curveRemains && inwardSafe && outwardSafe && DriftLevel < targetDriftLv)
                {
                    inp.drift = true;

                    // カスケード制御：
                    // ① 進行方向(VelDir)を目標ラインに沿わせるために必要な旋回角速度 desiredVelTurnRate (deg/s)
                    float roadTurnRate = (Mathf.Max(0f, nearAngle * driftDir) / 12f) * v;
                    float laneInwardOffset = Mathf.Max(0f, (currentLateral - targetLane) * driftDir);
                    float desiredVelTurnRate = roadTurnRate + (velInwardErr * 2.2f) - (laneInwardOffset * 6.5f);

                    // ② VelDir = Slerp(VelDir, fwd, 2.6 * dt) より、desiredVelTurnRate を生むための目標スリップ角 desiredSlip
                    float desiredSlip = Mathf.Clamp(desiredVelTurnRate / 2.6f, 3f, 30f);
                    float currentSlip = Vector3.SignedAngle(VelDir, fwd, Vector3.up) * driftDir;

                    // ③ 現在のスリップ角を目標スリップ角へ追従させるための車体鼻先旋回角速度 desiredNoseTurnRate
                    float desiredNoseTurnRate = desiredVelTurnRate + (desiredSlip - currentSlip) * 4.5f;

                    // ④ Kart.cs のドリフト旋回式 [36, 112] deg/s にマッピングして [-1.0, +1.0] のステア入力を決定
                    float t01 = Mathf.Clamp01(((desiredNoseTurnRate / Mathf.Max(turnMul, 0.8f)) - 36f) / (112f - 36f));
                    float u = Mathf.Lerp(-1.0f, 1.0f, t01);
                    inp.steer = driftDir * u;
                }
                else
                {
                    // ドリフト解除！ミニターボ発動！
                    inp.drift = false;
                }
            }

            // 6. コントロール・バリア・ファンクション (CBF / 滑らかな境界反発)
            float safeBoundary = Track.HalfWidth - 1.3f; // 7.7m（ダート手前1.3mで反発）
            float distToEdge = safeBoundary - Mathf.Abs(currentLateral);
            float outwardVelocity = Vector3.Dot(VelDir, track.Rights[Index] * Mathf.Sign(currentLateral));
            bool boundaryRepelling = false;

            if (distToEdge < 2.2f && outwardVelocity > 0.04f)
            {
                boundaryRepelling = true;
                // 距離に応じた滑らかな比例反発
                float repelIntensity = Mathf.Clamp01((2.2f - distToEdge) / 1.7f);
                float repelSteer = -Mathf.Sign(currentLateral) * repelIntensity * 0.80f;
                inp.steer = Mathf.Clamp(inp.steer + repelSteer, -1f, 1f);
                if (distToEdge < 1.0f) inp.drift = false; // ダート直前まで迫った場合はドリフトを即解除
                if (distToEdge < 0.6f && outwardVelocity > 0.15f && Speed > 7.0f)
                {
                    inp.throttle = -1f; // 十分な前進速度がある場合のみ減速ブレーキ
                }
            }

            // ステアリングの滑らかな補間（ドリフト中・境界反発中はカウンターステアが遅れないよう高速追従）
            float steerRate = (drifting || boundaryRepelling) ? 10.0f : 5.0f;
            aiSteer = Mathf.MoveTowards(aiSteer, inp.steer, dt * steerRate);
            inp.steer = aiSteer;

            // 最終的なステア量がドリフト開始閾値を満たし、かつ境界反発等で解除されていない場合のみドリフトジャンプ開始
            inp.driftDown = canStartDrift && inp.drift && Mathf.Abs(inp.steer) > 0.32f && Speed > 10.5f;
        }

        // 7. スタック脱出 (Anti-Stuck Logic)
        // スタート直後（レース開始後3秒以内）は絶対にスタック判定を行わない！
        // また、前進入力中 (inp.throttle > 0) なのに壁に引っかかって車速がほぼゼロ (< 3.5m/s) の状態が
        // 続いた場合のみ、一時的なバック脱出を行う。
        if (rm != null && rm.RaceRunning && rm.RaceTime > 3.0f && Mathf.Abs(Speed) < 3.5f && (inp.throttle > 0 || forwardDot < -0.1f))
        {
            aiStuck += dt;
        }
        else
        {
            aiStuck = 0;
        }

        float stuckLimit = forwardDot < -0.1f ? 0.5f : 2.5f;
        if (aiStuck > stuckLimit)
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
            inp.driftDown = false;
        }

        // 8. 戦略的アイテム使用 (Strategic Item Usage)
        if (!Finished && Item != ItemType.None && RouletteTimer <= 0 && rm != null)
        {
            aiItemTimer -= dt;
            bool canUse = false;
            bool urgentUse = false;

            switch (Item)
            {
                case ItemType.Missile:
                    canUse = rm.KartAheadWithin(this, 60f);
                    break;
                case ItemType.Banana:
                    // 後方にライバルが迫っている時、または急コーナーのエペックス上に罠を仕掛ける時
                    canUse = rm.KartBehindWithin(this, 22f)
                          || (apexCurvature > 0.025f && apexOffset <= 6 && rm.KartBehindWithin(this, 45f));
                    break;
                case ItemType.Turbo:
                    // オフロード（ダート）に飛び出して減速した時は即座にターボで復帰！
                    if (Offroad && !Boosting)
                    {
                        urgentUse = true;
                    }
                    else
                    {
                        canUse = !Boosting && Mathf.Abs(futureAngle) < 15f && Mathf.Abs(currentLateral) < Track.HalfWidth - 1.5f;
                    }
                    break;
                case ItemType.Star:
                    // ミサイル接近時は即座にスター無敵を発動して防御！
                    if (rm.IsMissileApproaching(this, out float mDist) && mDist < 34f)
                    {
                        urgentUse = true;
                    }
                    else
                    {
                        // 周囲にライバルがいて体当たりできる時、または直線で一気に加速したい時
                        canUse = rm.KartBehindWithin(this, 12f) || rm.KartAheadWithin(this, 18f);
                    }
                    break;
                case ItemType.Bomb:
                    // 前方にライバルがいる場合（前方投擲）、または後方に迫っている時（後方投下）
                    canUse = rm.KartAheadWithin(this, 35f) || rm.KartBehindWithin(this, 18f);
                    break;
                case ItemType.Thunder:
                    // サンダーは下位からの大逆転アイテム。前方にカートがいれば確実に発動！
                    canUse = rm.KartAheadWithin(this, 120f);
                    break;
            }

            float forceUseThreshold = Item == ItemType.Star ? -4.5f : -3.5f;
            if (urgentUse || (aiItemTimer <= 0 && (canUse || aiItemTimer < forceUseThreshold)))
            {
                inp.useItem = true;
                if (Item == ItemType.Bomb && !rm.KartAheadWithin(this, 25f) && rm.KartBehindWithin(this, 18f))
                {
                    inp.throttle = -1f; // 後方に敵がいる場合は後方投下
                }
                aiItemTimer = Random.Range(1.5f, 3.0f);
            }
        }

        return inp;
    }

    // アイテムボックス・ダッシュボードへのライン寄せ、スリップストリーム／追い抜き、およびバナナ回避
    float ApplyTacticalLineAdjustments(Vector3 curPos, float currentLateral, float baseLane, float apexCurvature, float apexBend)
    {
        float lane = baseLane;
        float maxSafeLane = Track.HalfWidth - 1.6f; // 7.4m
        Vector3 trackFwd = track.Dirs[Index];

        // A. アイテムボックス & ダッシュボード取得（急カーブ・ドリフト中でない場合）
        if (!drifting && apexCurvature < 0.022f)
        {
            bool foundPickup = false;
            // アイテム未所持なら前方のアクティブなアイテムボックスを狙う
            if (Item == ItemType.None && RouletteTimer <= 0 && rm != null && track.ItemBoxSpots != null)
            {
                int bestSteps = 999;
                float bestLatDiff = 999f;
                float boxLane = lane;
                for (int i = 0; i < track.ItemBoxSpots.Count; i++)
                {
                    if (!rm.IsItemBoxActive(i)) continue;
                    var spot = track.ItemBoxSpots[i];
                    int dIdx = track.Wrap(spot.index - Index);
                    if (dIdx >= 3 && dIdx <= 18)
                    {
                        float latDiff = Mathf.Abs(spot.lateral - currentLateral);
                        if (latDiff < 4.2f && (dIdx < bestSteps || (dIdx == bestSteps && latDiff < bestLatDiff)))
                        {
                            bestSteps = dIdx;
                            bestLatDiff = latDiff;
                            boxLane = spot.lateral;
                            foundPickup = true;
                        }
                    }
                }
                if (foundPickup) lane = boxLane;
            }

            // ダッシュボード（BoostPad）が前方近くにあれば乗る
            if (!foundPickup && boostTimer < 0.4f && track.BoostPads != null)
            {
                int bestSteps = 999;
                for (int i = 0; i < track.BoostPads.Count; i++)
                {
                    var pad = track.BoostPads[i];
                    int dIdx = track.Wrap(pad.index - Index);
                    if (dIdx >= 3 && dIdx <= 16 && dIdx < bestSteps && Mathf.Abs(pad.lateral - currentLateral) < 3.8f)
                    {
                        bestSteps = dIdx;
                        lane = pad.lateral;
                    }
                }
            }
        }

        // B. 前走車との駆け引き（スリップストリーム追従 & オーバーテイク追い抜き）
        if (rm != null && rm.Karts != null)
        {
            Kart closestAhead = null;
            float minFwdDist = 18.0f;
            for (int i = 0; i < rm.Karts.Count; i++)
            {
                var other = rm.Karts[i];
                if (other == this || other == null || !other.gameObject.activeSelf) continue;
                Vector3 toOther = other.transform.position - curPos;
                if (Mathf.Abs(toOther.y) > 2.2f) continue;
                float fwdDist = Vector3.Dot(toOther, trackFwd);
                if (fwdDist > 1.2f && fwdDist < minFwdDist)
                {
                    if (Mathf.Abs(other.Lateral - currentLateral) < 3.2f || Mathf.Abs(other.Lateral - lane) < 2.6f)
                    {
                        minFwdDist = fwdDist;
                        closestAhead = other;
                    }
                }
            }

            if (closestAhead != null)
            {
                bool shouldPass = minFwdDist < 7.5f
                               || closestAhead.Speed < Speed - 1.8f
                               || Boosting
                               || Slipstreaming;

                if (shouldPass)
                {
                    // 追突を避けて追い抜く：コース内側に余裕がある方、または現在の自分のオフセット側を選択
                    float passDir;
                    if (closestAhead.Lateral > 2.0f) passDir = -1f;
                    else if (closestAhead.Lateral < -2.0f) passDir = 1f;
                    else if (Mathf.Abs(apexBend) > 12f) passDir = Mathf.Sign(apexBend); // コーナーのイン側を突く
                    else passDir = currentLateral >= closestAhead.Lateral ? 1f : -1f;

                    lane = closestAhead.Lateral + passDir * 2.8f;
                }
                else if (!drifting && apexCurvature < 0.018f && minFwdDist >= 7.5f && minFwdDist <= 16.0f && Mathf.Abs(closestAhead.Lateral - currentLateral) < 2.4f)
                {
                    // 直線〜緩やかなカーブでは前走車の真後ろに入ってスリップストリーム（ドラフティング）を狙う
                    lane = Mathf.Lerp(lane, closestAhead.Lateral, 0.75f);
                }
            }
        }

        // C. コース上のバナナ（罠）の検知・回避（最優先）
        if (!StarInvincible && rm != null && rm.Bananas != null && rm.Bananas.Count > 0)
        {
            float closestBananaFwd = 26.0f;
            float threatBananaLat = 0f;
            bool hasThreat = false;

            for (int i = 0; i < rm.Bananas.Count; i++)
            {
                var b = rm.Bananas[i];
                if (b == null || (!b.Armed && b.Owner == this)) continue;
                Vector3 toB = b.transform.position - curPos;
                if (Mathf.Abs(toB.y) > 2.2f) continue;
                float fwdDist = Vector3.Dot(toB, trackFwd);
                if (fwdDist > 1.0f && fwdDist < closestBananaFwd)
                {
                    int bIdx = track.Wrap(Index + Mathf.RoundToInt(fwdDist / 2f));
                    float bLat = Vector3.Dot(b.transform.position - track.Pts[bIdx], track.Rights[bIdx]);
                    if (Mathf.Abs(bLat - lane) < 2.6f || Mathf.Abs(bLat - currentLateral) < 2.4f)
                    {
                        closestBananaFwd = fwdDist;
                        threatBananaLat = bLat;
                        hasThreat = true;
                    }
                }
            }

            if (hasThreat)
            {
                float avoidDir;
                if (threatBananaLat > 1.8f) avoidDir = -1f;
                else if (threatBananaLat < -1.8f) avoidDir = 1f;
                else avoidDir = currentLateral >= threatBananaLat ? 1f : -1f;

                lane = threatBananaLat + avoidDir * 2.9f;
            }
        }

        if (drifting)
        {
            // ドリフト中はイン側へ1.2m以上深いラインを目標にしない（イン巻きダート突入の防止）
            if (driftDir > 0) lane = Mathf.Min(lane, 1.2f);
            else lane = Mathf.Max(lane, -1.2f);
        }

        return Mathf.Clamp(lane, -maxSafeLane, maxSafeLane);
    }

    float AISpeedFactor()
    {
        if (IsPlayer && !Finished && !rm.Demo) return 1f;
        float f = aiSkill;
        if (rm != null)
        {
            // 1P / 2P の人間プレイヤーのうち、最も距離が近いプレイヤーを基準にラバーバンド補正を行う
            Kart refHuman = null;
            float bestDist = float.MaxValue;
            if (rm.Player != null && !rm.Player.Finished && rm.Player != this)
            {
                refHuman = rm.Player;
                bestDist = Mathf.Abs(rm.Player.RaceDistance - RaceDistance);
            }
            if (rm.TwoPlayer && rm.Player2 != null && !rm.Player2.Finished && rm.Player2 != this)
            {
                float d2 = Mathf.Abs(rm.Player2.RaceDistance - RaceDistance);
                if (d2 < bestDist)
                {
                    refHuman = rm.Player2;
                }
            }
            if (refHuman != null)
            {
                float diff = refHuman.RaceDistance - RaceDistance;
                f += Mathf.Clamp(diff / 220f, -0.06f, 0.08f);
            }
        }
        return Mathf.Clamp(f, 0.90f, 1.05f);
    }
}
