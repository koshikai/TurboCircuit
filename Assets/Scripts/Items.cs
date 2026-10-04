using UnityEngine;

// アイテムボックス：触れるとランダムなアイテムがもらえる。数秒で復活。
public class ItemBox : MonoBehaviour
{
    public bool Active => respawn <= 0;

    float respawn, phase;
    Vector3 basePos;
    Renderer rend;
    MaterialPropertyBlock mpb;
    Transform label;
    Transform cube;

    public void Init(Vector3 pos, Material mat, Font font)
    {
        basePos = pos + Vector3.up * 1.1f;
        transform.position = basePos;
        phase = Random.value * 10f;

        var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(c.GetComponent<Collider>());
        c.transform.SetParent(transform, false);
        c.transform.localScale = Vector3.one * 1.3f;
        cube = c.transform;
        rend = c.GetComponent<Renderer>();
        rend.sharedMaterial = mat;
        mpb = new MaterialPropertyBlock();

        var t = new GameObject("?").AddComponent<TextMesh>();
        t.text = "?";
        t.font = font;
        t.fontSize = 64;
        t.characterSize = 0.06f;
        t.anchor = TextAnchor.MiddleCenter;
        t.alignment = TextAlignment.Center;
        t.fontStyle = FontStyle.Bold;
        t.color = Color.white;
        t.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        label = t.transform;
        label.SetParent(transform, false);
    }

    public void Break()
    {
        respawn = 2.5f;
        Fx.Burst(transform.position, Color.HSVToRGB(Random.value, 0.6f, 1f), 18, 6f, 0.5f);
    }

    void Update()
    {
        respawn -= Time.deltaTime;
        float grow = Mathf.Clamp01(-respawn / 0.4f);
        cube.gameObject.SetActive(respawn <= 0);
        label.gameObject.SetActive(respawn <= 0);
        if (respawn > 0) return;

        transform.position = basePos + Vector3.up * Mathf.Sin(Time.time * 2f + phase) * 0.2f;
        cube.localRotation = Quaternion.Euler(Time.time * 50f + phase, Time.time * 80f + phase * 20f, 20f);
        cube.localScale = Vector3.one * 1.3f * grow;

        var hue = Color.HSVToRGB(Mathf.Repeat(Time.time * 0.3f + phase, 1f), 0.75f, 1f);
        mpb.SetColor("_Color", hue * 0.6f + Color.white * 0.4f);
        mpb.SetColor("_EmissionColor", hue * 0.9f);
        rend.SetPropertyBlock(mpb);

        var cam = Camera.main;
        if (cam != null)
        {
            var toCam = (cam.transform.position - basePos).normalized;
            label.position = transform.position + toCam * 1.0f;
            label.rotation = Quaternion.LookRotation(-toCam, Vector3.up);
            label.localScale = Vector3.one * grow;
        }
    }
}

// バナナ：踏んだカートはスピンする
public class Banana : MonoBehaviour
{
    public Kart Owner;
    public int NetId;
    float age;
    public bool Armed => age > 0.4f;

    public void Init(Vector3 pos, Kart owner, Material mat)
    {
        Owner = owner;
        transform.position = new Vector3(pos.x, 0, pos.z);
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(transform, false);
        body.transform.localPosition = new Vector3(0, 0.35f, 0);
        body.transform.localRotation = Quaternion.Euler(0, 0, 70);
        body.transform.localScale = new Vector3(0.45f, 0.6f, 0.45f);
        body.GetComponent<Renderer>().sharedMaterial = mat;
        var tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(tip.GetComponent<Collider>());
        tip.transform.SetParent(transform, false);
        tip.transform.localPosition = new Vector3(0.55f, 0.6f, 0);
        tip.transform.localScale = new Vector3(0.12f, 0.2f, 0.12f);
        tip.GetComponent<Renderer>().sharedMaterial = mat;
        transform.rotation = Quaternion.Euler(0, Random.value * 360f, 0);
    }

    void Update()
    {
        age += Time.deltaTime;
        transform.Rotate(0, 40f * Time.deltaTime, 0);
    }
}

// ミサイル：前にいるカートを追いかける
public class Missile : MonoBehaviour
{
    public Kart Owner;
    Kart target;
    Track track;
    RaceManager rm;
    int index;
    float age, lateral;
    Vector3 dir;

    public void Init(RaceManager rm, Track track, Kart owner, Kart target, Material mat)
    {
        this.rm = rm;
        this.track = track;
        Owner = owner;
        this.target = target;
        index = owner.Index;
        dir = owner.Forward;
        lateral = owner.Lateral;
        transform.position = owner.transform.position + owner.Forward * 2.2f + Vector3.up * 0.7f;

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(transform, false);
        body.transform.localRotation = Quaternion.Euler(90, 0, 0);
        body.transform.localScale = new Vector3(0.45f, 0.6f, 0.45f);
        body.GetComponent<Renderer>().sharedMaterial = mat;
        foreach (float s in new[] { -1f, 1f })
        {
            var fin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(fin.GetComponent<Collider>());
            fin.transform.SetParent(transform, false);
            fin.transform.localPosition = new Vector3(0, 0, -0.45f);
            fin.transform.localRotation = Quaternion.Euler(0, 0, s > 0 ? 0 : 90);
            fin.transform.localScale = new Vector3(0.8f, 0.06f, 0.3f);
            fin.GetComponent<Renderer>().sharedMaterial = rm.chromeMaterial;
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt == 0) return;
        age += dt;
        if (age > 9f) { Explode(); return; }

        Vector3 aim;
        bool homing = target != null && (target.transform.position - transform.position).magnitude < 35f;
        if (homing) aim = target.transform.position + Vector3.up * 0.7f;
        else
        {
            lateral = Mathf.MoveTowards(lateral, 0, 4f * dt);
            aim = track.PointAt(index + 7, lateral) + Vector3.up * 0.7f;
        }
        dir = Vector3.RotateTowards(dir, (aim - transform.position).normalized, (homing ? 5f : 8f) * dt, 0f);
        transform.position += dir * 50f * dt;
        transform.rotation = Quaternion.LookRotation(dir);
        track.Locate(transform.position, ref index, out _, out _);

        Fx.Emit(transform.position - dir * 0.6f, -dir * 4f, new Color(1f, 0.5f, 0.2f), 0.5f, 0.25f, 1, 0.8f);
        Fx.Emit(transform.position - dir * 0.6f, -dir * 2f + Vector3.up, new Color(0.6f, 0.6f, 0.6f, 0.5f), 0.7f, 0.5f, 1, 0.5f);

        foreach (var k in rm.Karts)
        {
            if (k == Owner && age < 1.5f) continue;
            if ((k.transform.position + Vector3.up * 0.6f - transform.position).magnitude < 1.9f)
            {
                k.Spin();
                Explode();
                return;
            }
        }
        if (rm.MissileHitsBanana(transform.position)) Explode();
    }

    void Explode()
    {
        Fx.Burst(transform.position, new Color(1f, 0.45f, 0.1f), 30, 9f, 0.8f);
        Fx.Burst(transform.position, new Color(1f, 0.9f, 0.4f), 15, 5f, 0.6f);
        rm.RemoveMissile(this);
        Destroy(gameObject);
    }
}
