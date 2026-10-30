using System.Collections.Generic;
using UnityEngine;

// スタイライズドアニマルの配置
public partial class Track
{
    // コースに生き生きとした息吹を与えるスタイライズドアニマルシステム (Kenney Cube Pets, CC0)
    void BuildAnimals(RaceManager rm, TrackDef def)
    {
        string dir = Application.dataPath + "/Resources/Animals/";
        var colormapTex = RaceManager.LoadTexture("Animals/Textures/colormap", dir + "Textures/colormap.png");
        if (colormapTex == null) return;

        var animalMat = new Material(rm.wallMaterial)
        {
            mainTexture = colormapTex,
            color = Color.white
        };
        animalMat.SetColor("_BaseColor", Color.white);
        animalMat.SetFloat("_Smoothness", 0.15f);
        createdMaterials.Add(animalMat);

        var prefabBunny = Resources.Load<GameObject>("Animals/animal-bunny");
        var prefabDeer = Resources.Load<GameObject>("Animals/animal-deer");
        var prefabPenguin = Resources.Load<GameObject>("Animals/animal-penguin");
        var prefabPolar = Resources.Load<GameObject>("Animals/animal-polar");
        var prefabFox = Resources.Load<GameObject>("Animals/animal-fox");
        var prefabLion = Resources.Load<GameObject>("Animals/animal-lion");
        var prefabCrab = Resources.Load<GameObject>("Animals/animal-crab");
        var prefabCat = Resources.Load<GameObject>("Animals/animal-cat");
        var prefabDog = Resources.Load<GameObject>("Animals/animal-dog");
        var prefabCow = Resources.Load<GameObject>("Animals/animal-cow");
        var prefabBird = Resources.Load<GameObject>("Animals/animal-parrot");
        var prefabChick = Resources.Load<GameObject>("Animals/animal-chick");

        var rng = new System.Random(def.SceneryTheme * 777 + 42);
        int theme = def.SceneryTheme;

        // 1. 全コース共通：空を優雅に飛ぶ鳥（旋回飛行）
        if (prefabBird != null)
        {
            int birdFlocks = (theme == 3) ? 1 : 2;
            for (int f = 0; f < birdFlocks; f++)
            {
                int spotIdx = (int)(Count * ((f * 0.45f + 0.2f) % 1f));
                Vector3 center = Pts[spotIdx];
                float radius = 70f + f * 40f;
                float altitude = (theme == 3 ? 42f : 24f) + f * 6f;
                float speed = 18f + f * 3f;
                for (int b = 0; b < 3; b++)
                {
                    var birdGo = SpawnFlyingBird(prefabBird, center + new Vector3(b * 4f, 0, b * 3f), radius + b * 5f, altitude + b * 1.5f, speed, animalMat);
                    if (birdGo != null) birdGo.transform.localScale = Vector3.one * 1.5f;
                }
            }
        }

        // 2. テーマ別地上動物の配置
        if (theme == 0) // TURBO CIRCUIT: 豊かな芝生丘陵
        {
            // スタート直後（右側テント横）のウサギ応援スポット（HopBounce）
            if (prefabBunny != null)
            {
                // スタート直後の特等席
                for (int b = 0; b < 3; b++)
                {
                    Vector3 p = PointAt(7 + b * 2, WallOffset + 4.2f + (float)rng.NextDouble() * 2.5f);
                    p.y = GetTerrainHeight(p.x, p.z, def);
                    SpawnAnimal(prefabBunny, p, Quaternion.Euler(0, -70f + (float)rng.NextDouble() * 30f, 0), 1.5f, animalMat, AnimalBehavior.BehaviorType.HopBounce);
                }
                // コース全体の群れ
                for (int g = 1; g < 4; g++)
                {
                    int idx = Wrap(Count * g / 4 + 18);
                    float side = (g % 2 == 0) ? 1f : -1f;
                    Vector3 basePos = PointAt(idx, side * (WallOffset + 5.5f + (float)rng.NextDouble() * 6f));
                    for (int b = 0; b < 3; b++)
                    {
                        Vector3 p = basePos + new Vector3((float)rng.NextDouble() * 4f - 2f, 0, (float)rng.NextDouble() * 4f - 2f);
                        p.y = GetTerrainHeight(p.x, p.z, def);
                        Quaternion rot = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                        SpawnAnimal(prefabBunny, p, rot, 1.4f, animalMat, AnimalBehavior.BehaviorType.HopBounce);
                    }
                }
            }

            // シカ（草を食む IdleGraze）
            if (prefabDeer != null)
            {
                // スタート直後の木陰
                Vector3 p0 = PointAt(14, WallOffset + 12f);
                p0.y = GetTerrainHeight(p0.x, p0.z, def);
                SpawnAnimal(prefabDeer, p0, Quaternion.Euler(0, -110f, 0), 2.2f, animalMat, AnimalBehavior.BehaviorType.IdleGraze);

                for (int d = 1; d < 3; d++)
                {
                    int idx = Wrap(Count * d / 3 + 35);
                    float side = (d % 2 == 0) ? -1f : 1f;
                    Vector3 p = PointAt(idx, side * (WallOffset + 14f + (float)rng.NextDouble() * 10f));
                    p.y = GetTerrainHeight(p.x, p.z, def);
                    Quaternion rot = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                    SpawnAnimal(prefabDeer, p, rot, 2.2f, animalMat, AnimalBehavior.BehaviorType.IdleGraze);
                }
            }

            // ヒヨコ（スタート左側芝生・花壇近く）
            if (prefabChick != null)
            {
                for (int c = 0; c < 4; c++)
                {
                    Vector3 p = PointAt(6 + c * 2, -(WallOffset + 3.8f + (float)rng.NextDouble() * 2f));
                    p.y = GetTerrainHeight(p.x, p.z, def);
                    SpawnAnimal(prefabChick, p, Quaternion.Euler(0, 80f + (float)rng.NextDouble() * 40f, 0), 1.3f, animalMat, AnimalBehavior.BehaviorType.HopBounce);
                }
            }
        }
        else if (theme == 1) // SUNSET DUNES: 夕焼け砂漠・キャニオン
        {
            // スタート直後の岩の上に砂漠のライオン（IdleGraze）
            if (prefabLion != null)
            {
                Vector3 pStart = PointAt(10, WallOffset + 6.5f);
                pStart.y = GetTerrainHeight(pStart.x, pStart.z, def);
                SpawnAnimal(prefabLion, pStart, Quaternion.Euler(0, -85f, 0), 2.4f, animalMat, AnimalBehavior.BehaviorType.IdleGraze);

                for (int l = 1; l < 3; l++)
                {
                    int idx = Wrap(Count * l / 3 + 30);
                    Vector3 p = PointAtOuterWall(idx, -1f, 14f + l * 6f);
                    p.y = GetTerrainHeight(p.x, p.z, def);
                    Quaternion rot = Quaternion.LookRotation(Dirs[idx], Vector3.up);
                    SpawnAnimal(prefabLion, p, rot, 2.4f, animalMat, AnimalBehavior.BehaviorType.IdleGraze);
                }
            }

            // 砂漠キツネ（LookAround）
            if (prefabFox != null)
            {
                // スタート直後の左側
                Vector3 pFox = PointAt(7, -(WallOffset + 5f));
                pFox.y = GetTerrainHeight(pFox.x, pFox.z, def);
                SpawnAnimal(prefabFox, pFox, Quaternion.Euler(0, 75f, 0), 1.7f, animalMat, AnimalBehavior.BehaviorType.LookAround);

                for (int f = 1; f < 4; f++)
                {
                    int idx = Wrap(Count * f / 4 + 22);
                    float side = (f % 2 == 0) ? 1f : -1f;
                    Vector3 p = PointAtOuterWall(idx, side, 8f + (float)rng.NextDouble() * 8f);
                    p.y = GetTerrainHeight(p.x, p.z, def);
                    Quaternion rot = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                    SpawnAnimal(prefabFox, p, rot, 1.7f, animalMat, AnimalBehavior.BehaviorType.LookAround);
                }
            }

            // カニ（砂地の横歩き CrabSidestep）
            if (prefabCrab != null)
            {
                // スタート直後
                Vector3 pCrab = PointAt(12, WallOffset + 4.2f);
                pCrab.y = GetTerrainHeight(pCrab.x, pCrab.z, def);
                SpawnAnimal(prefabCrab, pCrab, Quaternion.Euler(0, -90f, 0), 1.8f, animalMat, AnimalBehavior.BehaviorType.CrabSidestep);

                for (int c = 1; c < 5; c++)
                {
                    int idx = Wrap(Count * c / 5 + 18);
                    Vector3 p = PointAtOuterWall(idx, 1f, 5f + (float)rng.NextDouble() * 6f);
                    p.y = GetTerrainHeight(p.x, p.z, def);
                    Quaternion rot = Quaternion.LookRotation(-Rights[idx], Vector3.up);
                    SpawnAnimal(prefabCrab, p, rot, 1.8f, animalMat, AnimalBehavior.BehaviorType.CrabSidestep);
                }
            }
        }
        else if (theme == 2) // FROST PEAK: 白銀の雪山・氷河
        {
            // ペンギン応援団（PenguinWaddle）！スタート直後・コーナー外側にずらり
            if (prefabPenguin != null)
            {
                // スタート直後の特等席！右側フェンス際によちよち並ぶ
                for (int p = 0; p < 4; p++)
                {
                    Vector3 pos = PointAt(6 + p * 2, WallOffset + 3.6f);
                    pos.y = GetTerrainHeight(pos.x, pos.z, def);
                    Quaternion rot = Quaternion.Euler(0, -90f + (p % 2 == 0 ? 15f : -15f), 0);
                    SpawnAnimal(prefabPenguin, pos, rot, 1.7f, animalMat, AnimalBehavior.BehaviorType.PenguinWaddle);
                }

                // コース全体の群れ
                for (int g = 1; g < 4; g++)
                {
                    int idx = Wrap(Count * g / 4 + 16);
                    float side = (g % 2 == 0) ? 1f : -1f;
                    Vector3 basePos = PointAt(idx, side * (WallOffset + 4.5f + (float)rng.NextDouble() * 3f));
                    for (int p = 0; p < 3; p++)
                    {
                        Vector3 pos = basePos + new Vector3((p % 2) * 2.2f - 1.1f, 0, (p / 2) * 2.2f);
                        pos.y = GetTerrainHeight(pos.x, pos.z, def);
                        Quaternion rot = Quaternion.LookRotation(-side * Rights[idx], Vector3.up) * Quaternion.Euler(0, (float)rng.NextDouble() * 24f - 12f, 0);
                        SpawnAnimal(prefabPenguin, pos, rot, 1.6f, animalMat, AnimalBehavior.BehaviorType.PenguinWaddle);
                    }
                }
            }

            // シロクマ / ホッキョクグマ（IdleGraze）
            if (prefabPolar != null)
            {
                // スタート直後の左側雪丘
                Vector3 pPolar0 = PointAt(10, -(WallOffset + 9f));
                pPolar0.y = GetTerrainHeight(pPolar0.x, pPolar0.z, def);
                SpawnAnimal(prefabPolar, pPolar0, Quaternion.Euler(0, 95f, 0), 2.5f, animalMat, AnimalBehavior.BehaviorType.IdleGraze);

                for (int pb = 1; pb < 3; pb++)
                {
                    int idx = Wrap(Count * pb / 3 + 28);
                    float side = (pb % 2 == 0) ? -1f : 1f;
                    Vector3 p = PointAt(idx, side * (WallOffset + 16f + (float)rng.NextDouble() * 10f));
                    p.y = GetTerrainHeight(p.x, p.z, def);
                    Quaternion rot = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                    SpawnAnimal(prefabPolar, p, rot, 2.5f, animalMat, AnimalBehavior.BehaviorType.IdleGraze);
                }
            }

            // 雪山のキタキツネ（LookAround）
            if (prefabFox != null)
            {
                for (int f = 0; f < 3; f++)
                {
                    int idx = Wrap(Count * f / 3 + 24);
                    Vector3 p = PointAt(idx, WallOffset + 8f + (float)rng.NextDouble() * 6f);
                    p.y = GetTerrainHeight(p.x, p.z, def);
                    Quaternion rot = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                    SpawnAnimal(prefabFox, p, rot, 1.7f, animalMat, AnimalBehavior.BehaviorType.LookAround);
                }
            }
        }
        else if (theme == 3) // NEON METROPOLIS: サイバー未来都市
        {
            // サイバーキャット（LookAround）- ビルのエントランスやサイバーツリーのふもと
            if (prefabCat != null)
            {
                Vector3 pCat0 = PointAt(8, WallOffset + 3.8f);
                pCat0.y = -0.3f;
                SpawnAnimal(prefabCat, pCat0, Quaternion.Euler(0, -80f, 0), 1.6f, animalMat, AnimalBehavior.BehaviorType.LookAround);

                for (int c = 1; c < 4; c++)
                {
                    int idx = Wrap(Count * c / 4 + 16);
                    Vector3 p = PointAt(idx, WallOffset + 4.2f + (float)rng.NextDouble() * 3f);
                    p.y = -0.3f;
                    Quaternion rot = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                    SpawnAnimal(prefabCat, p, rot, 1.6f, animalMat, AnimalBehavior.BehaviorType.LookAround);
                }
            }

            // サイバードッグ（LookAround）- 歩道沿い
            if (prefabDog != null)
            {
                Vector3 pDog0 = PointAt(12, -(WallOffset + 4.0f));
                pDog0.y = -0.3f;
                SpawnAnimal(prefabDog, pDog0, Quaternion.Euler(0, 85f, 0), 1.7f, animalMat, AnimalBehavior.BehaviorType.LookAround);

                for (int d = 1; d < 3; d++)
                {
                    int idx = Wrap(Count * d / 3 + 32);
                    Vector3 p = PointAt(idx, -(WallOffset + 4.5f + (float)rng.NextDouble() * 3f));
                    p.y = -0.3f;
                    Quaternion rot = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                    SpawnAnimal(prefabDog, p, rot, 1.7f, animalMat, AnimalBehavior.BehaviorType.LookAround);
                }
            }
        }
        else if (theme == 4) // HOKKAIDO CAMPUS: 北海道大学キャンパス
        {
            // ホルスタイン牛（第2農場の放牧地）- キュートなKenney Cow！
            if (prefabCow != null)
            {
                int pastureStart = (int)(0.68f * Count);
                int pastureEnd = (int)(0.76f * Count);
                for (int i = pastureStart; i <= pastureEnd; i += 6)
                {
                    Vector3 cowPos = PointAt(i, -(WallOffset + 12f + (i % 3) * 3.5f));
                    cowPos.y = GetTerrainHeight(cowPos.x, cowPos.z, def);
                    Quaternion cowRot = Quaternion.Euler(0, (i * 47) % 360, 0);
                    SpawnAnimal(prefabCow, cowPos, cowRot, 2.4f, animalMat, AnimalBehavior.BehaviorType.IdleGraze);
                }
            }

            // エゾシカ（中央ローン広場やエルムの森の奥）
            if (prefabDeer != null)
            {
                for (int d = 0; d < 3; d++)
                {
                    int idx = (int)(Count * (0.09f + d * 0.12f));
                    Vector3 p = PointAt(idx, -(WallOffset + 12f + d * 5f));
                    p.y = GetTerrainHeight(p.x, p.z, def);
                    Quaternion rot = Quaternion.Euler(0, 60f + d * 30f, 0);
                    SpawnAnimal(prefabDeer, p, rot, 2.2f, animalMat, AnimalBehavior.BehaviorType.IdleGraze);
                }
            }

            // エゾリス / ウサギ（イチョウ並木や大野池周辺）
            if (prefabBunny != null)
            {
                for (int b = 0; b < 5; b++)
                {
                    int idx = (int)(Count * (0.07f + b * 0.08f));
                    Vector3 p = PointAt(idx, WallOffset + 4.5f + (float)rng.NextDouble() * 5f);
                    p.y = GetTerrainHeight(p.x, p.z, def);
                    Quaternion rot = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                    SpawnAnimal(prefabBunny, p, rot, 1.5f, animalMat, AnimalBehavior.BehaviorType.HopBounce);
                }
            }

            // 北キツネ（ポプラ並木の木陰）
            if (prefabFox != null)
            {
                int popIdx = (int)(0.84f * Count);
                Vector3 p = PointAt(popIdx, WallOffset + 7.5f);
                p.y = GetTerrainHeight(p.x, p.z, def);
                SpawnAnimal(prefabFox, p, Quaternion.Euler(0, 160f, 0), 1.7f, animalMat, AnimalBehavior.BehaviorType.LookAround);
            }
        }
    }

    GameObject SpawnAnimal(GameObject prefab, Vector3 pos, Quaternion rot, float scale, Material mat, AnimalBehavior.BehaviorType bType)
    {
        if (prefab == null) return null;
        var go = Instantiate(prefab, pos, rot, transform);
        go.transform.localScale = Vector3.one * scale;
        StripColliders(go);
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            r.sharedMaterial = mat;
        var beh = go.AddComponent<AnimalBehavior>();
        beh.behavior = bType;
        return go;
    }

    GameObject SpawnFlyingBird(GameObject prefab, Vector3 center, float radius, float altitude, float speed, Material mat)
    {
        if (prefab == null) return null;
        var go = Instantiate(prefab, center + Vector3.up * altitude, Quaternion.identity, transform);
        go.transform.localScale = Vector3.one * 1.5f;
        StripColliders(go);
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            r.sharedMaterial = mat;
        var bird = go.AddComponent<FlyingBird>();
        bird.centerPos = center;
        bird.radius = radius;
        bird.altitude = altitude;
        bird.speed = speed;
        return go;
    }
}
