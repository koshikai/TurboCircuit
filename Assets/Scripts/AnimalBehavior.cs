using UnityEngine;

// 生き物たちのキュートな振る舞い・アニメーション
public class AnimalBehavior : MonoBehaviour
{
    public enum BehaviorType
    {
        IdleGraze,    // 草を食む・首を振る（シカ、ウシ、シロクマ、ラクダ等）
        HopBounce,    // ピョンピョン跳ねる（ウサギ、ヒヨコ）
        PenguinWaddle,// よちよち左右に揺れながらパタパタ（ペンギン）
        LookAround,   // きょろきょろ警戒・見回す（キツネ、ネコ、イヌ、ライオン）
        CrabSidestep, // ハサミをフリフリ横歩き（カニ）
    }

    public BehaviorType behavior = BehaviorType.IdleGraze;

    Vector3 initialPos;
    Quaternion initialRot;
    float timeOffset;

    // 視線・首振り
    float curYaw;
    float targetYaw;
    float nextLookChange;

    // ジャンプ・ホップ
    float hopTimer;
    float nextHopTime;
    bool isHopping;

    // 横歩き
    float sideOffset;

    void Start()
    {
        initialPos = transform.position;
        initialRot = transform.rotation;
        timeOffset = Random.Range(0f, 10f);
        nextLookChange = Random.Range(1f, 3f);
        nextHopTime = Random.Range(1.2f, 3.5f);
    }

    void Update()
    {
        float t = Time.time + timeOffset;

        switch (behavior)
        {
            case BehaviorType.HopBounce:
                UpdateHop(t);
                break;
            case BehaviorType.PenguinWaddle:
                UpdatePenguin(t);
                break;
            case BehaviorType.LookAround:
                UpdateLookAround(t);
                break;
            case BehaviorType.CrabSidestep:
                UpdateCrab(t);
                break;
            case BehaviorType.IdleGraze:
            default:
                UpdateGraze(t);
                break;
        }
    }

    // 草を食む・呼吸・ときどき首をかしげる
    void UpdateGraze(float t)
    {
        // 呼吸の上下ゆらぎ
        float breath = Mathf.Sin(t * 2.2f) * 0.05f;
        // 首のうなずき・左右の小刻みな揺れ
        float nod = Mathf.Sin(t * 1.4f) * 4f;

        // 定期的に首の向きを変える
        if (Time.time > nextLookChange)
        {
            targetYaw = (Random.value < 0.4f) ? 0f : Random.Range(-35f, 35f);
            nextLookChange = Time.time + Random.Range(2.5f, 6.0f);
        }
        curYaw = Mathf.Lerp(curYaw, targetYaw, Time.deltaTime * 3.5f);

        transform.position = initialPos + Vector3.up * breath;
        transform.rotation = initialRot * Quaternion.Euler(nod, curYaw, 0f);
    }

    // ウサギやヒヨコのピョンピョンジャンプ
    void UpdateHop(float t)
    {
        if (Time.time > nextHopTime && !isHopping)
        {
            isHopping = true;
            hopTimer = 0f;
        }

        float jumpY = 0f;
        float pitch = 0f;

        if (isHopping)
        {
            hopTimer += Time.deltaTime;
            float duration = 0.45f;
            float p = hopTimer / duration;
            if (p >= 1f)
            {
                isHopping = false;
                nextHopTime = Time.time + Random.Range(0.8f, 2.5f);
                // 着地時に少し方向を変える
                targetYaw = Random.Range(-40f, 40f);
            }
            else
            {
                // 放物線ジャンプ
                jumpY = Mathf.Sin(p * Mathf.PI) * 0.85f;
                // 空中での前傾・着地姿勢
                pitch = Mathf.Sin(p * Mathf.PI * 2f) * -12f;
            }
        }
        else
        {
            // 待機中の呼吸
            jumpY = Mathf.Abs(Mathf.Sin(t * 3f)) * 0.08f;
        }

        curYaw = Mathf.Lerp(curYaw, targetYaw, Time.deltaTime * 4f);
        transform.position = initialPos + Vector3.up * jumpY;
        transform.rotation = initialRot * Quaternion.Euler(pitch, curYaw, 0f);
    }

    // ペンギンのよちよち横揺れ＋パタパタ
    void UpdatePenguin(float t)
    {
        // 左右のよちよちロッキング（Roll）
        float waddle = Mathf.Sin(t * 3.6f) * 16f;
        // ピョコピョコ歩きの上下動
        float bob = Mathf.Abs(Mathf.Sin(t * 3.6f)) * 0.15f;
        // 定期的な嬉しそうな小ジャンプ
        if (Time.time > nextHopTime && !isHopping)
        {
            isHopping = true;
            hopTimer = 0f;
        }

        float jumpY = 0f;
        if (isHopping)
        {
            hopTimer += Time.deltaTime;
            float duration = 0.5f;
            float p = hopTimer / duration;
            if (p >= 1f)
            {
                isHopping = false;
                nextHopTime = Time.time + Random.Range(3f, 6f);
            }
            else
            {
                jumpY = Mathf.Sin(p * Mathf.PI) * 0.65f;
                waddle *= 0.3f;
            }
        }

        transform.position = initialPos + Vector3.up * (bob + jumpY);
        transform.rotation = initialRot * Quaternion.Euler(0f, Mathf.Sin(t * 0.8f) * 15f, waddle);
    }

    // キツネや猫のきょろきょろ警戒見回り
    void UpdateLookAround(float t)
    {
        if (Time.time > nextLookChange)
        {
            targetYaw = (Random.value < 0.35f) ? 0f : Random.Range(-55f, 55f);
            nextLookChange = Time.time + Random.Range(1.8f, 4.5f);
        }
        curYaw = Mathf.Lerp(curYaw, targetYaw, Time.deltaTime * 6f);

        // しっぽや体の微細な呼吸揺れ
        float breath = Mathf.Sin(t * 2.8f) * 0.04f;
        float tilt = Mathf.Sin(t * 1.5f) * 2.5f;

        transform.position = initialPos + Vector3.up * breath;
        transform.rotation = initialRot * Quaternion.Euler(tilt, curYaw, 0f);
    }

    // カニの横歩き＆ハサミフリフリ
    void UpdateCrab(float t)
    {
        // 左右のシャカシャカ横移動
        sideOffset = Mathf.Sin(t * 1.8f) * 1.2f;
        // 上下ボビング
        float bob = Mathf.Abs(Mathf.Sin(t * 7.2f)) * 0.08f;
        // 前後のハサミ威嚇傾き
        float rock = Mathf.Sin(t * 3.6f) * 8f;

        Vector3 right = initialRot * Vector3.right;
        transform.position = initialPos + right * sideOffset + Vector3.up * bob;
        transform.rotation = initialRot * Quaternion.Euler(rock, 0f, 0f);
    }
}

// コース上空を優雅に羽ばたいて旋回飛行する鳥たち
public class FlyingBird : MonoBehaviour
{
    public Vector3 centerPos;
    public float radius = 95f;
    public float altitude = 26f;
    public float speed = 19f;
    public float flapSpeed = 13f;
    public float bankAmount = 22f;

    float angle;
    float flapOffset;
    Vector3 baseScale;

    void Start()
    {
        angle = Random.Range(0f, Mathf.PI * 2f);
        flapOffset = Random.Range(0f, 10f);
        baseScale = transform.localScale;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        angle += (speed / radius) * dt;

        float x = centerPos.x + Mathf.Cos(angle) * radius;
        float z = centerPos.z + Mathf.Sin(angle) * (radius * 0.75f);
        // 上下にゆったり波打つ高度
        float y = altitude + Mathf.Sin(angle * 2.2f) * 3.5f;

        Vector3 nextPos = new Vector3(x, y, z);
        Vector3 forward = new Vector3(-Mathf.Sin(angle) * radius, Mathf.Cos(angle * 2.2f) * 1.5f, Mathf.Cos(angle) * (radius * 0.75f)).normalized;

        transform.position = nextPos;

        // 旋回に伴うバンク傾き（曲がる内側に傾く）
        Quaternion bank = Quaternion.Euler(0f, 0f, -bankAmount);
        transform.rotation = Quaternion.LookRotation(forward, Vector3.up) * bank;

        // 羽ばたき（上下スケーリング）
        float flap = Mathf.Sin((Time.time + flapOffset) * flapSpeed);
        transform.localScale = new Vector3(baseScale.x * (1f + flap * 0.15f), baseScale.y * (1f - flap * 0.22f), baseScale.z);
    }
}
