using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// レース中 HUD：ラップ/タイム、順位、アイテム、スピードメーター、ミニマップ、集中線、ビネット
public partial class RaceManager
{
    // 1P は全画面、2P は左右それぞれのビューにクリップして HUD・バナー・ビネットを描く
    void DrawHuds(float w, float h, bool countdown)
    {
        if (!TwoPlayer)
        {
            DrawHud(ViewKart, w, h, false, 0);
            if (countdown) DrawCountdown(w, h);
            DrawBanner(0, w, h);
            return;
        }

        float hw = w * 0.5f;
        for (int p = 0; p < 2; p++)
        {
            GUI.BeginClip(new Rect(p * hw, 0, hw, h));
            DrawVignette(HumanKart(p), hw, h);
            DrawHud(HumanKart(p), hw, h, true, p);
            if (countdown) DrawCountdown(hw, h);
            DrawBanner(p, hw, h);
            GUI.EndClip();
        }
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(hw - 2f, 0, 4f, h), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    void DrawBanner(int p, float w, float h)
    {
        float t = bannerTimes[p];
        if (t >= 1.6f) return;
        float a = Mathf.Clamp01((1.6f - t) / 0.3f);
        float pop = 1f + Mathf.Max(0, 0.3f - t) * 1.5f;
        var st = St(sBig, (int)((TwoPlayer ? 54 : 72) * pop));
        var c = bannerColors[p]; c.a = a;
        Outlined(new Rect(0, h * 0.28f, w, 120), bannerTexts[p], st, c);
    }

    void DrawCountdown(float w, float h)
    {
        float t = stateTime - 1f;
        if (t < 0) return;
        int n = 3 - Mathf.FloorToInt(t);
        float frac = t - Mathf.Floor(t);
        string text = n > 0 ? n.ToString() : "GO!";
        var st = St(sBig, (int)(150 * (1.3f - frac * 0.3f)));
        var c = n > 0 ? new Color(1f, 0.85f, 0.2f) : new Color(0.3f, 1f, 0.4f);
        c.a = 1f - frac * 0.3f;
        Outlined(new Rect(0, h * 0.25f, w, 200), text, st, c, 5);
    }

    void DrawHud(Kart pl, float w, float h, bool compact, int pi)
    {
        // ──────────────── アイテム枠（角丸ゴールド立体フレーム） ────────────────
        var slot = new Rect(24, 20, 114, 114);
        if (itemSlotTex != null)
        {
            GUI.DrawTexture(slot, itemSlotTex);
        }
        else
        {
            GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.85f);
            GUI.DrawTexture(slot, Texture2D.whiteTexture);
            GUI.color = Color.white;
            DrawFrame(slot, 3, new Color(1f, 0.85f, 0.25f));
        }

        ItemType shown = pl.Item;
        if (pl.RouletteTimer > 0) shown = (ItemType)(1 + (int)(Time.time * 16f) % 4);
        if (shown != ItemType.None)
        {
            Texture2D icon = null;
            Color ic; string label;
            switch (shown)
            {
                case ItemType.Turbo: icon = iconTurbo; ic = new Color(1f, 0.55f, 0.1f); label = "TURBO"; break;
                case ItemType.Banana: icon = iconBanana; ic = new Color(1f, 0.9f, 0.2f); label = "BANANA"; break;
                case ItemType.Missile: icon = iconMissile; ic = new Color(1f, 0.2f, 0.2f); label = "MISSILE"; break;
                default: icon = iconShield; ic = Color.HSVToRGB(Mathf.Repeat(Time.time, 1f), 0.6f, 1f); label = "SHIELD"; break;
            }

            if (icon != null)
            {
                GUI.DrawTexture(new Rect(slot.x + 10, slot.y + 10, slot.width - 20, slot.height - 20), icon, ScaleMode.ScaleToFit);
            }
            else
            {
                GUI.color = ic;
                GUI.DrawTexture(new Rect(slot.x + 14, slot.y + 14, slot.width - 28, 54), Texture2D.whiteTexture);
                GUI.color = Color.white;
                var ls = St(sSmall, 18);
                Outlined(new Rect(slot.x, slot.y + 14, slot.width, 54), label, ls, Color.white, 2);
            }

            if (pl.RouletteTimer <= 0)
                Outlined(new Rect(slot.x, slot.yMax - 26, slot.width, 22), "[E] USE", St(sSmall, 15), new Color(1f, 1f, 0.4f), 2);
        }

        // ──────────────── ドリフトミニターボゲージ ────────────────
        if (pl.Drifting && pl.Speed > 8f)
        {
            var dr = new Rect(slot.x, slot.yMax + 8, slot.width, 16);
            GUI.color = new Color(0.05f, 0.08f, 0.15f, 0.85f);
            GUI.DrawTexture(dr, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float chargeNorm = Mathf.Clamp01(pl.DriftCharge / 2.6f);
            Color turboCol = pl.DriftLevel switch
            {
                3 => new Color(0.85f, 0.2f, 1f),   // 紫 (ウルトラ)
                2 => new Color(1f, 0.55f, 0.05f),  // 橙 (スーパー)
                1 => new Color(0.1f, 0.8f, 1f),    // 青 (ミニ)
                _ => new Color(0.4f, 0.6f, 0.8f)   // チャージ中
            };
            GUI.color = turboCol;
            GUI.DrawTexture(new Rect(dr.x + 2, dr.y + 2, (dr.width - 4) * chargeNorm, dr.height - 4), Texture2D.whiteTexture);
            GUI.color = Color.white;
            DrawFrame(dr, 1, turboCol);

            string turboText = pl.DriftLevel switch
            {
                3 => "ULTRA TURBO!",
                2 => "SUPER TURBO!",
                1 => "MINI TURBO!",
                _ => "CHARGING..."
            };
            Outlined(new Rect(dr.x, dr.yMax + 2, dr.width, 18), turboText, St(sSmall, 11), turboCol, 1);
        }

        // ──────────────── 周回とタイム（スタイリッシュスラントカード） ────────────────
        var tr = new Rect(w - 280, 16, 260, 100);
        if (rankPlateTex != null)
        {
            GUI.color = new Color(0.04f, 0.08f, 0.18f, 0.85f);
            GUI.DrawTexture(tr, rankPlateTex);
            GUI.color = Color.white;
        }

        int lap = Mathf.Clamp(pl.MaxLap, 1, totalLaps);
        Outlined(new Rect(tr.x + 10, tr.y + 6, tr.width - 20, 36), "LAP " + lap + " / " + totalLaps, St(sMid, 28, TextAnchor.MiddleRight), Color.white, 2);
        Outlined(new Rect(tr.x + 10, tr.y + 42, tr.width - 20, 26), FormatTime(pl.Finished ? pl.FinishTime : raceTime), St(sNum, 22, TextAnchor.MiddleRight), new Color(1f, 0.95f, 0.4f), 2);
        if (pl == HumanKart(pi) && bestLap[pi] > 0)
            Outlined(new Rect(tr.x + 10, tr.y + 68, tr.width - 20, 20), "BEST " + FormatTime(bestLap[pi]), St(sSmall, 14, TextAnchor.MiddleRight), new Color(0.5f, 0.9f, 1f), 1.5f);

        // ──────────────── 後方確認 / 観戦バナー ────────────────
        if (isLookingBehind && pi == 0)
        {
            DrawPopPill(new Rect(w / 2 - 95, 20, 190, 28), "◄ REAR VIEW ►", new Color(0.95f, 0.22f, 0.22f));
        }
        else if (Spectating && pi == 0)
        {
            var sk = Karts[spectateIndex];
            Outlined(new Rect(0, 16, w, 32), $"► SPECTATING: {sk.Name} ({Ordinal(sk.Place)}) ◄", St(sMid, 24), new Color(1f, 0.85f, 0.2f), 2);
            GUI.Label(new Rect(0, 48, w, 22), "[A] / [D]  Switch Driver", St(sSmall, 14, TextAnchor.MiddleCenter, FontStyle.Normal, false, new Color(0.7f, 0.85f, 1f)));
        }

        // ──────────────── ミサイル接近警告アラート ────────────────
        if (RaceRunning && !pl.Finished && IsMissileApproaching(pl, out float mDist))
        {
            float pulse = Mathf.Repeat(Time.time * 6f, 1f);
            if (pulse < 0.65f)
            {
                var warnRect = new Rect(w / 2 - 160, h * 0.72f, 320, 42);
                GUI.color = new Color(0.9f, 0.1f, 0.1f, 0.85f);
                GUI.DrawTexture(warnRect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                DrawFrame(warnRect, 3, Color.yellow);
                Outlined(warnRect, $"⚠️ MISSILE {Mathf.RoundToInt(mDist)}m!", St(sMid, 24, TextAnchor.MiddleCenter), Color.yellow, 3);
            }
        }

        // ──────────────── 順位表示（スラントバッジプレート） ────────────────
        string place = Ordinal(pl.Place);
        Texture2D pPlate = pl.Place == 1 ? rankPlateGold : pl.Place == 2 ? rankPlateSilver : pl.Place == 3 ? rankPlateBronze : rankPlateTex;
        var pr = compact ? new Rect(w - 180, h - 110, 170, 95) : new Rect(w - 240, h - 140, 225, 125);
        if (pPlate != null)
        {
            GUI.DrawTexture(pr, pPlate);
        }
        var ps = St(sNum, compact ? 70 : 92, TextAnchor.MiddleCenter);
        Color pc = pl.Place == 1 ? new Color(0.25f, 0.12f, 0f) : pl.Place == 2 ? new Color(0.12f, 0.18f, 0.28f) : pl.Place == 3 ? new Color(0.28f, 0.12f, 0.05f) : Color.white;
        Outlined(new Rect(pr.x, pr.y - 6, pr.width, pr.height), place, ps, pc, 4);

        // ──────────────── スピードメーター & タコメーターバー ────────────────
        int spdKmh = Mathf.RoundToInt(Mathf.Abs(pl.Speed) * 3.6f);
        var sr = compact ? new Rect(185, h - 75, 180, 60) : new Rect(w / 2 - 120, h - 78, 240, 65);

        // 速度数値 (Russo One)
        Outlined(new Rect(sr.x, sr.y, sr.width, 38), spdKmh + " km/h", St(sNum, 32, TextAnchor.MiddleCenter), Color.white, 2.5f);

        // LEDタコメーターバー
        var barRect = new Rect(sr.x + 10, sr.y + 40, sr.width - 20, 12);
        GUI.color = new Color(0.08f, 0.12f, 0.2f, 0.8f);
        GUI.DrawTexture(barRect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        float speedRatio = Mathf.Clamp01(Mathf.Abs(pl.Speed) / Kart.MaxSpeed);
        Color barColor = pl.Boosting ? new Color(0.2f, 0.85f, 1f) : speedRatio > 0.85f ? new Color(1f, 0.3f, 0.2f) : speedRatio > 0.6f ? new Color(1f, 0.85f, 0.2f) : new Color(0.2f, 0.9f, 0.4f);
        GUI.color = barColor;
        GUI.DrawTexture(new Rect(barRect.x + 1, barRect.y + 1, (barRect.width - 2) * speedRatio, barRect.height - 2), Texture2D.whiteTexture);
        GUI.color = Color.white;
        DrawFrame(barRect, 1, barColor);

        // ──────────────── ミニマップ（角丸ダークガラス） ────────────────
        var mm = compact ? new Rect(14, h - 175, 160, 160) : new Rect(20, h - 250, 230, 230);
        if (minimapGlassTex != null)
        {
            GUI.DrawTexture(mm, minimapGlassTex);
        }
        else
        {
            GUI.color = new Color(0.05f, 0.08f, 0.16f, 0.75f);
            GUI.DrawTexture(mm, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        GUI.DrawTexture(mm, minimap);
        int playerIdx = Karts.IndexOf(pl);
        for (int n = 1; n <= Karts.Count; n++) // プレイヤーを最後（最前面）に描く
        {
            var k = Karts[(playerIdx + n) % Karts.Count];
            var uv = toMap(k.transform.position);
            var p = new Vector2(mm.x + uv.x * mm.width, mm.y + (1f - uv.y) * mm.height);
            float s = k == pl ? 14f : k.IsPlayer ? 12f : 10f;
            GUI.color = k == pl ? Color.white : k.IsPlayer ? new Color(1f, 0.85f, 0.2f) : Color.black;
            GUI.DrawTexture(new Rect(p.x - s / 2 - 2, p.y - s / 2 - 2, s + 4, s + 4), Texture2D.whiteTexture);
            GUI.color = k.Color;
            GUI.DrawTexture(new Rect(p.x - s / 2, p.y - s / 2, s, s), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;

        // ──────────────── 逆走警告 ────────────────
        if (RaceRunning && !pl.Finished && Vector3.Dot(pl.Forward, track.Dirs[pl.Index]) < -0.3f && pl.Speed > 3f && Mathf.Repeat(Time.time, 0.6f) < 0.4f)
            Outlined(new Rect(0, h * 0.42f, w, 80), "WRONG WAY!", St(sBig, compact ? 44 : 60), new Color(1f, 0.3f, 0.3f));
    }

    void DrawVignette(Kart pl, float w, float h)
    {
        if (vignetteTex == null) return;
        float speed01 = pl != null ? Mathf.Clamp01(pl.Speed / Kart.MaxSpeed) : 0f;
        bool boost = pl != null && pl.Boosting;
        float boostBonus = boost ? 0.35f : 0f;
        float alpha = Mathf.Clamp01(0.18f + speed01 * 0.25f + boostBonus);

        // ブースト時は周辺にサイバーブルーのエネルギーグローを薄く付加
        if (boost)
        {
            GUI.color = new Color(0.2f, 0.75f, 1f, alpha * 0.55f);
            GUI.DrawTexture(new Rect(0, 0, w, h), vignetteTex, ScaleMode.StretchToFill);
        }

        GUI.color = new Color(1, 1, 1, alpha);
        GUI.DrawTexture(new Rect(0, 0, w, h), vignetteTex, ScaleMode.StretchToFill);
        GUI.color = Color.white;
    }

    void DrawSpeedLines(float w, float h)
    {
        var vk = ViewKart;
        if (vk == null) return;
        float speedRatio = Mathf.Clamp01(vk.Speed / Kart.MaxSpeed);
        bool boost = vk.Boosting;
        if (speedRatio < 0.38f && !boost) return;

        // 速度比率 0.38〜1.0 を 0〜1 に正規化。ブースト時は 1.4
        float speedIntensity = Mathf.Clamp01((speedRatio - 0.38f) / 0.62f);
        float intensity = boost ? 1.4f : speedIntensity;
        if (intensity <= 0.01f) return;

        float scale = Screen.height / 720f;
        Matrix4x4 baseMatrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        // 消失点（Focus Point）: 画面中央・道路奥に安定配置
        float steerTilt = Mathf.Clamp(vk.Lateral * 2f, -22f, 22f);
        Vector2 focus = new Vector2(w * 0.5f + steerTilt, h * 0.46f);

        float time = Time.time;
        var strokeTex = speedLineTex != null ? speedLineTex : Texture2D.whiteTexture;

        // ─────────────────────────────────────────────
        // レイヤー1: 流れる高速ウィンドストリーム（滑らかに外から内へ疾走する空気の筋）
        // ─────────────────────────────────────────────
        int streamCount = (int)Mathf.Lerp(18f, boost ? 42f : 32f, intensity);
        float streamSpeed = 3.5f + speedRatio * 3.8f + (boost ? 5.2f : 0f);

        for (int i = 0; i < streamCount; i++)
        {
            float baseAngle = (i / (float)streamCount) * Mathf.PI * 2f;
            float seed = i * 137.5f;
            float angle = baseAngle + Mathf.Sin(seed) * 0.12f;

            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            // 画面境界までの距離
            float tx = cos > 0.0001f ? (w - focus.x) / cos : (cos < -0.0001f ? -focus.x / cos : 99999f);
            float ty = sin > 0.0001f ? (h - focus.y) / sin : (sin < -0.0001f ? -focus.y / sin : 99999f);
            float rEdge = Mathf.Min(tx, ty);
            if (rEdge < 30f) continue;

            // 視界保護セーフゾーン（楕円：横170px, 縦110px）
            float rSafe = Mathf.Sqrt(Mathf.Pow(cos * 170f, 2f) + Mathf.Pow(sin * 110f, 2f));

            // 外側から中心へ流れるフェーズ
            float phaseOffset = Mathf.Repeat(seed * 0.317f, 1f);
            float flow = Mathf.Repeat(time * streamSpeed * (0.85f + (i % 4) * 0.1f) + phaseOffset, 1f);

            // 画面外枠からセーフゾーン手前まで流れる
            float maxLineLen = Mathf.Lerp(150f, 320f, intensity) * (0.75f + (i % 3) * 0.25f);
            float currentR = Mathf.Lerp(rEdge + maxLineLen * 0.3f, rSafe + 30f, flow);
            float headR = currentR - maxLineLen;

            float alphaMod = 1f;
            if (currentR > rEdge) alphaMod *= Mathf.Clamp01((rEdge + maxLineLen * 0.3f - currentR) / (maxLineLen * 0.3f));
            if (headR < rSafe) alphaMod *= Mathf.Clamp01((headR - rSafe * 0.6f) / (rSafe * 0.4f));
            if (alphaMod <= 0.01f) continue;

            Vector2 p1 = new Vector2(focus.x + cos * currentR, focus.y + sin * currentR);
            Vector2 p2 = new Vector2(focus.x + cos * Mathf.Max(headR, rSafe * 0.5f), focus.y + sin * Mathf.Max(headR, rSafe * 0.5f));

            float thick = Mathf.Lerp(2.5f, boost ? 5.2f : 3.8f, intensity);

            Color col;
            if (boost)
            {
                col = (i % 3 == 0)
                    ? new Color(1f, 0.80f, 0.2f, 0.90f * intensity * alphaMod)
                    : (i % 3 == 1)
                        ? new Color(0.2f, 0.92f, 1f, 0.90f * intensity * alphaMod)
                        : new Color(1f, 1f, 1f, 0.95f * intensity * alphaMod);
            }
            else
            {
                // 明るい背景でもクッキリ見えるよう、鮮やかなシアンと白をブレンド
                col = (i % 2 == 0)
                    ? new Color(0.35f, 0.88f, 1f, 0.85f * intensity * alphaMod)
                    : new Color(1f, 1f, 1f, 0.80f * intensity * alphaMod);
            }

            DrawSpeedStroke(p1, p2, thick, col, strokeTex, baseMatrix);
        }

        // ─────────────────────────────────────────────
        // レイヤー2: 迫力の集中線（ダイナミック・インパクトスパイク）
        // ─────────────────────────────────────────────
        int spikeCount = (int)Mathf.Lerp(28f, boost ? 80f : 56f, intensity);
        int timeSlot = (int)(time * 22f); // 22Hzでリズミカルに切り替わる

        for (int i = 0; i < spikeCount; i++)
        {
            int hash = (i * 265443576 + timeSlot * 8235729) & 0x7FFFFFFF;
            float r0 = (hash % 1000) / 1000f;
            float r1 = ((hash / 1000) % 1000) / 1000f;
            float r2 = ((hash / 1000000) % 1000) / 1000f;

            // 角度の分散（360度全体に均一に分散）
            float baseAngle = (i / (float)spikeCount) * Mathf.PI * 2f;
            float angle = baseAngle + (r0 - 0.5f) * (Mathf.PI * 2f / spikeCount) * 1.15f;

            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            float tx = cos > 0.0001f ? (w - focus.x) / cos : (cos < -0.0001f ? -focus.x / cos : 99999f);
            float ty = sin > 0.0001f ? (h - focus.y) / sin : (sin < -0.0001f ? -focus.y / sin : 99999f);
            float rEdge = Mathf.Min(tx, ty);
            if (rEdge < 30f) continue;

            // 視界保護セーフゾーン
            float rSafe = Mathf.Sqrt(Mathf.Pow(cos * 160f, 2f) + Mathf.Pow(sin * 100f, 2f));

            // 外枠から中心方向へ伸びる長さ（3層のバリエーション）
            float rOuter = rEdge + 12f;
            float tier = (hash >> 5) % 3;
            float reach = tier == 0
                ? Mathf.Lerp(0.28f, 0.48f, intensity) * (0.8f + r1 * 0.4f)
                : tier == 1
                    ? Mathf.Lerp(0.50f, 0.75f, intensity) * (0.85f + r1 * 0.3f)
                    : Mathf.Lerp(0.75f, boost ? 0.96f : 0.90f, intensity);

            float rInner = Mathf.Max(rSafe, rOuter - (rOuter - rSafe) * reach);

            Vector2 p1 = new Vector2(focus.x + cos * rOuter, focus.y + sin * rOuter);
            Vector2 p2 = new Vector2(focus.x + cos * rInner, focus.y + sin * rInner);

            float thick = Mathf.Lerp(3.2f, boost ? 8.5f : 6.0f, intensity) * (0.65f + r2 * 0.7f);

            Color col;
            if (boost)
            {
                int kind = (hash >> 3) % 4;
                switch (kind)
                {
                    case 0: col = new Color(1f, 0.70f, 0.15f, 0.95f * intensity); break; // 炎ゴールド
                    case 1: col = new Color(0.20f, 0.95f, 1f, 0.95f * intensity); break;  // ネオンシアン
                    case 2: col = new Color(1f, 0.98f, 0.55f, 0.98f * intensity); break; // 高輝度イエロー
                    default: col = new Color(1f, 1f, 1f, 1.0f); break;                   // 白熱コア
                }
            }
            else
            {
                float a = Mathf.Clamp01(0.75f * intensity + r2 * 0.25f);
                col = (i % 3 == 0)
                    ? new Color(0.3f, 0.9f, 1f, a)
                    : (i % 3 == 1)
                        ? new Color(0.85f, 0.96f, 1f, a)
                        : new Color(1f, 1f, 1f, a);
            }

            DrawSpeedStroke(p1, p2, thick, col, strokeTex, baseMatrix);
        }

        GUI.matrix = baseMatrix;
    }

    void DrawSpeedStroke(Vector2 a, Vector2 b, float thickness, Color col, Texture2D tex, Matrix4x4 baseMatrix)
    {
        var d = b - a;
        float len = d.magnitude;
        if (len < 1f) return;
        float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        var prev = GUI.color;
        GUI.color = col;
        GUI.matrix = baseMatrix * Matrix4x4.TRS(new Vector3(a.x, a.y, 0f), Quaternion.Euler(0, 0, ang), Vector3.one);
        GUI.DrawTexture(new Rect(0, -thickness * 0.5f, len, thickness), tex);
        GUI.color = prev;
    }
}
