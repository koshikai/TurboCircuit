using System.Linq;
using UnityEngine;

public partial class RaceManager
{
    void RemoveBananaById(int id)
    {
        for (int i = bananas.Count - 1; i >= 0; i--)
            if (bananas[i] != null && bananas[i].NetId == id)
            {
                Destroy(bananas[i].gameObject);
                bananas.RemoveAt(i);
            }
    }

    void ExplodeBombById(int id)
    {
        for (int i = bombs.Count - 1; i >= 0; i--)
            if (bombs[i] != null && bombs[i].NetId == id)
            {
                bombs[i].Explode();
            }
    }

    void UpdatePlaces()
    {
        placeOrder.Sort((a, b) =>
        {
            bool ha = a.gameObject.activeSelf, hb = b.gameObject.activeSelf;
            if (ha != hb) return ha ? -1 : 1; // 非表示（タイムアタック中のCPU）は最下位
            return ComparePlaces(a, b);
        });
        for (int i = 0; i < placeOrder.Count; i++) placeOrder[i].Place = i + 1;
    }

    void KartCollisions()
    {
        for (int i = 0; i < Karts.Count; i++)
        for (int j = i + 1; j < Karts.Count; j++)
        {
            var a = Karts[i]; var b = Karts[j];
            if (!a.gameObject.activeSelf || !b.gameObject.activeSelf) continue;
            if (Mathf.Abs(a.transform.position.y - b.transform.position.y) > 2.0f) continue;
            var d = a.transform.position - b.transform.position;
            d.y = 0;
            float m = d.magnitude, min = Kart.Radius * 2f;
            if (m >= min || m < 0.001f) continue;
            var n = d / m;
            // 重いカートほど押し返されにくい（ミニ化中は軽くなり弾かれやすい）
            float massA = a.Mass * (a.Shocked ? 0.35f : 1.0f);
            float massB = b.Mass * (b.Shocked ? 0.35f : 1.0f);
            float wa = massB / (massA + massB), wb = 1f - wa;
            a.transform.position += n * (min - m) * wa;
            b.transform.position -= n * (min - m) * wb;

            if (a.Shielded && !b.Shielded) { if (b.Spin()) AddBattleHit(a, b, ItemType.Star); continue; }
            if (b.Shielded && !a.Shielded) { if (a.Spin()) AddBattleHit(b, a, ItemType.Star); continue; }
            if (!a.Shocked && b.Shocked) { b.Spin(); continue; }
            if (!b.Shocked && a.Shocked) { a.Spin(); continue; }

            // 相対接近速度を計算し、擦れ続けではなく実際に衝突した瞬間のみ強さに応じたバンプ音を鳴らす
            float relVel = Vector3.Dot(b.VelDir * b.Speed - a.VelDir * a.Speed, n);
            // 押し合い：横方向に少し弾く
            a.VelDir = (a.VelDir + n * 0.5f * wa).normalized;
            b.VelDir = (b.VelDir - n * 0.5f * wb).normalized;
            float damp = Mathf.Pow(0.985f, Time.deltaTime * 60f); // フレームレート非依存
            a.Speed *= damp;
            b.Speed *= damp;
            if ((a.IsPlayer || b.IsPlayer) && relVel > 2.8f)
                Audio.Bump(Mathf.Clamp01(relVel / 18f));
        }
    }

    void CheckItemBoxes()
    {
        foreach (var box in boxes)
        {
            if (!box.Active) continue;
            foreach (var k in Karts)
            {
                if (!k.gameObject.activeSelf) continue;
                if (Mathf.Abs(k.transform.position.y - box.transform.position.y) > 2.2f) continue;
                var d = k.transform.position - box.transform.position;
                d.y = 0;
                if (d.magnitude > 2.2f) continue;
                box.Break();
                if (k.IsRemote) break; // 相手側のカートのアイテムは相手が決める
                if (k.GiveItem(RollItem(k)) && k.IsPlayer) Audio.Pickup();
                break;
            }
        }
    }

    void CheckBananas()
    {
        for (int i = bananas.Count - 1; i >= 0; i--)
        {
            var b = bananas[i];
            foreach (var k in Karts)
            {
                if (k.IsRemote || !k.gameObject.activeSelf) continue; // 当たり判定は持ち主側で行う
                if (k == b.Owner && !b.Armed) continue;
                if (Mathf.Abs(k.transform.position.y - b.transform.position.y) > 2.0f) continue;
                var d = k.transform.position - b.transform.position;
                d.y = 0;
                if (d.magnitude > 1.5f) continue;
                bool spun = !k.Shielded && k.Spin();
                if (k.Shielded || spun)
                {
                    if (spun) AddBattleHit(b.Owner, k, ItemType.Banana);
                    Fx.Burst(b.transform.position, new Color(1f, 0.95f, 0.3f), 12, 5f, 0.4f);
                    bananas.RemoveAt(i);
                    if (Online) Net.SendBananaGone(b.NetId);
                    Destroy(b.gameObject);
                    break;
                }
            }
        }
    }

    public bool IsMissileApproaching(Kart k, out float distance)
    {
        distance = 999f;
        if (k == null || missiles.Count == 0) return false;
        foreach (var m in missiles)
        {
            if (m != null && m.Target == k)
            {
                float d = Vector3.Distance(m.transform.position, k.transform.position);
                if (d < 45f && d < distance)
                {
                    distance = d;
                }
            }
        }
        return distance < 45f;
    }

    ItemType RollItem(Kart k)
    {
        if (ItemDefs.Count == 0) return ItemType.Banana;
        int activeCount = Karts.Count(x => x.gameObject.activeSelf);
        float p = activeCount > 1 ? Mathf.Clamp01((k.Place - 1) / (float)(activeCount - 1)) : 0f;

        float totalWeight = 0f;
        for (int i = 0; i < ItemDefs.Count; i++)
        {
            totalWeight += ItemDefs[i].WeightFunc != null ? ItemDefs[i].WeightFunc(p) : 0.1f;
        }

        if (totalWeight <= 0f) return ItemType.Banana;

        float r = Random.value * totalWeight;
        for (int i = 0; i < ItemDefs.Count; i++)
        {
            float w = ItemDefs[i].WeightFunc != null ? ItemDefs[i].WeightFunc(p) : 0.1f;
            if ((r -= w) < 0f) return ItemDefs[i].Type;
        }

        return ItemDefs[ItemDefs.Count - 1].Type;
    }

    public void UseThunder(Kart user)
    {
        thunderCooldown = 28f;
        screenFlashTimer = 0.35f;
        Audio.Explode(1.2f);

        var tDef = GetItemDef(ItemType.Thunder);
        string uName = ShortKartName(user);
        AddBattleLog(
            $"{uName} -> ALL [{(tDef != null ? tDef.NameEn : "THUNDER")}]",
            $"{uName} → 全員 [{(tDef != null ? tDef.NameJp : "サンダー")}]",
            tDef != null ? tDef.AccentColor : Color.yellow);

        foreach (var k in Karts)
        {
            if (k == null || !k.gameObject.activeSelf || k == user) continue;
            k.Shock(6.5f);
        }

        if (Online && user != null && !user.IsRemote)
        {
            Net.SendThunder(Karts.IndexOf(user));
        }
    }

    struct BattleLogEntry
    {
        public string En;
        public string Ja;
        public Color Accent;
        public float ExpireAt;
    }

    readonly System.Collections.Generic.List<BattleLogEntry> battleLog = new System.Collections.Generic.List<BattleLogEntry>(4);
    const int MaxBattleLogEntries = 4;
    const float BattleLogDuration = 3.5f;

    public void AddBattleLog(string en, string ja, Color col)
    {
        if (!RaceRunning) return;
        if (battleLog.Count >= MaxBattleLogEntries) battleLog.RemoveAt(0);
        battleLog.Add(new BattleLogEntry { En = en, Ja = ja, Accent = col, ExpireAt = Time.time + BattleLogDuration });
    }

    static string ShortKartName(Kart k)
    {
        if (k == null || string.IsNullOrEmpty(k.Name)) return "?";
        int idx = k.Name.IndexOf(" (");
        return idx > 0 ? k.Name.Substring(0, idx) : k.Name;
    }

    public void AddBattleHit(Kart attacker, Kart victim, ItemType item)
    {
        if (victim == null) return;
        var def = GetItemDef(item);
        string itemEn = def != null ? def.NameEn : item.ToString().ToUpperInvariant();
        string itemJa = def != null ? def.NameJp : itemEn;
        Color col = def != null ? def.AccentColor : Color.white;
        string atkName = attacker != null ? ShortKartName(attacker) : "?";
        string vicName = ShortKartName(victim);
        AddBattleLog($"{atkName} -> {vicName} [{itemEn}]", $"{atkName} → {vicName} [{itemJa}]", col);
    }

    public void SpawnBanana(Vector3 pos, Kart owner)
    {
        int ownerId = Karts.IndexOf(owner);
        int id = ownerId * 100000 + (nextBananaId++ % 100000);
        SpawnBananaInternal(id, pos, owner);
        if (Online && !owner.IsRemote) Net.SendBanana(id, ownerId, pos);
    }

    void SpawnBananaInternal(int id, Vector3 pos, Kart owner)
    {
        var b = new GameObject("Banana").AddComponent<Banana>();
        b.Init(pos, owner, bananaMaterial);
        b.NetId = id;
        bananas.Add(b);
    }

    public void SpawnMissile(Kart owner)
    {
        var target = Karts.FirstOrDefault(k => k.gameObject.activeSelf && k.Place == owner.Place - 1);
        SpawnMissileInternal(owner, target);
        if (Online && !owner.IsRemote) Net.SendMissile(Karts.IndexOf(owner), target == null ? -1 : Karts.IndexOf(target));
    }

    void SpawnMissileInternal(Kart owner, Kart target)
    {
        var m = new GameObject("Missile").AddComponent<Missile>();
        m.Init(this, track, owner, target, missileMaterial);
        missiles.Add(m);
    }

    public void RemoveMissile(Missile m) => missiles.Remove(m);

    public void SpawnBomb(Vector3 pos, Vector3 vel, Kart owner)
    {
        int ownerId = Karts.IndexOf(owner);
        int id = ownerId * 100000 + (nextBombId++ % 100000);
        SpawnBombInternal(id, pos, vel, owner);
        if (Online && !owner.IsRemote) Net.SendBomb(id, ownerId, pos, vel);
    }

    void SpawnBombInternal(int id, Vector3 pos, Vector3 vel, Kart owner)
    {
        var b = new GameObject("Bomb").AddComponent<Bomb>();
        b.Init(this, track, owner, pos, vel, kartPaintMaterial);
        b.NetId = id;
        bombs.Add(b);
    }

    public void RemoveBomb(Bomb b) => bombs.Remove(b);

    public void TriggerBombChain(Bomb source, Vector3 pos, float radius)
    {
        for (int i = bombs.Count - 1; i >= 0; i--)
        {
            var b = bombs[i];
            if (b == null || b == source) continue;
            if (Vector3.Distance(b.transform.position, pos) < radius)
            {
                b.Explode();
            }
        }
    }

    public bool MissileHitsBanana(Vector3 pos)
    {
        for (int i = 0; i < bananas.Count; i++)
        {
            if (bananas[i] == null) continue;
            if ((bananas[i].transform.position + Vector3.up * 0.5f - pos).magnitude < 1.4f)
            {
                var b = bananas[i];
                Fx.Burst(b.transform.position, new Color(1f, 0.95f, 0.3f), 12, 5f, 0.4f);
                bananas.RemoveAt(i);
                if (Online) Net.SendBananaGone(b.NetId);
                Destroy(b.gameObject);
                return true;
            }
        }
        return false;
    }

    public bool KartBehindWithin(Kart k, float dist)
    {
        foreach (var o in Karts)
        {
            if (o == k || !o.gameObject.activeSelf) continue;
            float d = k.RaceDistance - o.RaceDistance;
            if (d > 0 && d < dist) return true;
        }
        return false;
    }

    public bool KartAheadWithin(Kart k, float dist)
    {
        foreach (var o in Karts)
        {
            if (o == k || !o.gameObject.activeSelf) continue;
            float d = o.RaceDistance - k.RaceDistance;
            if (d > 0 && d < dist) return true;
        }
        return false;
    }
}
