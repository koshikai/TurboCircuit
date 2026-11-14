using System.Collections.Generic;
using UnityEngine;

public partial class Track
{
    // 高架橋・立体交差セクションの橋脚（ピラー）
    void BuildBridgePillars(RaceManager rm, TrackDef def)
    {
        var pillarMat = rm.standMaterial;
        int step = 12;
        float beamH = 1.4f;

        for (int i = 0; i < Count; i += step)
        {
            var p = Pts[i];
            Vector3 beamPos = p - Normals[i] * (0.35f + beamH * 0.5f);
            float groundY = GetTerrainHeight(beamPos.x, beamPos.z, def);

            // ビーム中心から地面までの高低差
            float topY = beamPos.y;
            float colH = topY - groundY;

            // 地面から実際に 2.8m 以上浮いている空中区間（都市高架等）のみ橋脚とビームを配置
            if (colH > 2.8f)
            {
                // 下を別の道路が通っているか（立体交差）を判定
                bool crossesOverTrack = false;
                for (int j = 0; j < Count; j += 4)
                {
                    int indexDiff = Mathf.Abs(i - j);
                    if (indexDiff > 40 && indexDiff < Count - 40)
                    {
                        float hDistSqr = (Pts[j].x - beamPos.x) * (Pts[j].x - beamPos.x) + (Pts[j].z - beamPos.z) * (Pts[j].z - beamPos.z);
                        if (hDistSqr < (WallOffset + 5f) * (WallOffset + 5f) && Pts[j].y < beamPos.y - 3.0f)
                        {
                            crossesOverTrack = true;
                            break;
                        }
                    }
                }

                if (crossesOverTrack)
                {
                    // 立体交差部（下の道路を跨ぐ区間）：
                    // 下の道路の走行路面を完全にクリアに保つため、道路上には柱を一切置かない（オープンスパン構造）
                    // 道路底面に接する頑丈なスチール横桁ビームのみ配置
                    var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(beam.GetComponent<Collider>());
                    beam.name = "OverpassSpanBeam_" + i;
                    beam.transform.SetParent(transform, false);
                    beam.transform.position = beamPos;
                    beam.transform.rotation = Quaternion.LookRotation(Dirs[i], Normals[i]);
                    beam.transform.localScale = new Vector3(WallOffset * 2.2f, beamH, 4.6f);
                    beam.GetComponent<Renderer>().sharedMaterial = pillarMat;
                }
                else
                {
                    // 道路底面（-0.35f）に接するように横ビームを配置
                    var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(beam.GetComponent<Collider>());
                    beam.name = "BridgeBeam_" + i;
                    beam.transform.SetParent(transform, false);
                    beam.transform.position = beamPos;
                    beam.transform.rotation = Quaternion.LookRotation(Dirs[i], Normals[i]);
                    beam.transform.localScale = new Vector3(WallOffset * 1.8f, beamH, 3.8f);
                    beam.GetComponent<Renderer>().sharedMaterial = pillarMat;

                    // コンクリート橋脚（上端はビーム中心高さに接続し、路面を絶対に貫通しない）
                    var col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(col.GetComponent<Collider>());
                    col.name = "BridgePillar_" + i;
                    col.transform.SetParent(transform, false);
                    col.transform.position = new Vector3(beamPos.x, groundY + colH * 0.5f, beamPos.z);
                    col.transform.localScale = new Vector3(3.4f, colH * 0.5f, 3.4f);
                    col.GetComponent<Renderer>().sharedMaterial = pillarMat;
                }
            }
        }
    }

    // ビルまたは木造納屋を貫通する屋根付き区間
    void BuildTunnels(RaceManager rm, TrackDef def)
    {
        if (def.Tunnels == null) return;
        bool isCampus = def.SceneryTheme == 4;
        const float H = 10f;
        // 壁・天井マテリアル（北大キャンパスは木造下見板張りのオフホワイト、都市はコンクリート）
        var wallTunnelMat = new Material(rm.wallMaterial) {
            mainTexture = isCampus ? TextureGen.BarnWoodWhite() : TextureGen.Concrete(),
            color = Color.white
        };
        var lightMat = new Material(rm.glowMaterial) { color = isCampus ? new Color(1f, 0.78f, 0.38f) : new Color(1f, 0.92f, 0.75f) };
        var trimA = new Material(rm.wallMaterial) { color = isCampus ? new Color(0.12f, 0.48f, 0.24f) : def.CurbColorA };
        var trimB = new Material(rm.wallMaterial) { color = isCampus ? new Color(0.12f, 0.48f, 0.24f) : def.CurbColorB };
        var barnRedMat = new Material(rm.wallMaterial) { color = new Color(0.76f, 0.18f, 0.12f) };
        var woodDarkMat = new Material(rm.trunkMaterial) { color = new Color(0.34f, 0.22f, 0.15f) };
        createdMaterials.Add(wallTunnelMat);
        createdMaterials.Add(lightMat);
        createdMaterials.Add(trimA);
        createdMaterials.Add(trimB);
        createdMaterials.Add(barnRedMat);
        createdMaterials.Add(woodDarkMat);
        var facade = isCampus ? null : CityMaterials(rm);

        for (int t = 0; t < def.Tunnels.Length; t++)
        {
            int i0 = Mathf.Clamp(Mathf.RoundToInt(def.Tunnels[t].Start * Count), 0, Count - 2);
            int i1 = Mathf.Clamp(Mathf.RoundToInt(def.Tunnels[t].End * Count), i0 + 1, Count - 1);
            int segs = i1 - i0;
            string id = "Tunnel" + t;

            Strip(id + "Ceiling", new Vector2(WallOffset, H), new Vector2(-WallOffset, H), wallTunnelMat, 8f, i0, segs);
            // 側壁は両面
            Strip(id + "WallL", new Vector2(-WallOffset, 0f), new Vector2(-WallOffset, H), wallTunnelMat, 8f, i0, segs);
            Strip(id + "WallL2", new Vector2(-WallOffset, H), new Vector2(-WallOffset, 0f), wallTunnelMat, 8f, i0, segs);
            Strip(id + "WallR", new Vector2(WallOffset, H), new Vector2(WallOffset, 0f), wallTunnelMat, 8f, i0, segs);
            Strip(id + "WallR2", new Vector2(WallOffset, 0f), new Vector2(WallOffset, H), wallTunnelMat, 8f, i0, segs);

            // 壁際のトリム（キャンパスは木製巾木、都市はネオン）
            Strip(id + "TrimL", new Vector2(-WallOffset + 0.1f, 3.6f), new Vector2(-WallOffset + 0.1f, 4.1f), trimA, 8f, i0, segs);
            Strip(id + "TrimR", new Vector2(WallOffset - 0.1f, 4.1f), new Vector2(WallOffset - 0.1f, 3.6f), trimB, 8f, i0, segs);

            for (int i = i0; i <= i1; i += 4)
            {
                var rot = Quaternion.LookRotation(Dirs[i], Normals[i]);
                if (!isCampus && (i - i0) % 8 == 0)
                    Box("TunnelLight", Pts[i] + Normals[i] * (H - 0.15f), new Vector3(WallOffset * 1.2f, 0.25f, 0.8f), rot, lightMat);

                // 天井の太い木造梁トラス & 温かい納屋ランタン照明
                if (isCampus && (i - i0) % 4 == 0)
                {
                    var beam = Box("BarnBeam", Pts[i] + Normals[i] * (H - 0.5f), new Vector3(WallOffset * 2f, 0.7f, 0.7f), rot, woodDarkMat);
                    // 左右の柱
                    Box("BarnPostL", Pts[i] - Rights[i] * (WallOffset - 0.35f) + Vector3.up * (H * 0.5f), new Vector3(0.7f, H, 0.7f), rot, woodDarkMat);
                    Box("BarnPostR", Pts[i] + Rights[i] * (WallOffset - 0.35f) + Vector3.up * (H * 0.5f), new Vector3(0.7f, H, 0.7f), rot, woodDarkMat);

                    // 天井梁から吊り下がるクラシック納屋ランタン照明（8ステップごと）
                    if ((i - i0) % 8 == 0)
                    {
                        Vector3 lampPos = Pts[i] + Normals[i] * (H - 1.4f);
                        Box("BarnLanternCap", lampPos + Vector3.up * 0.2f, new Vector3(0.8f, 0.25f, 0.8f), rot, woodDarkMat);
                        var bulb = Box("BarnLanternBulb", lampPos, new Vector3(0.6f, 0.6f, 0.6f), rot, lightMat, PrimitiveType.Sphere);

                        var ltGo = new GameObject("BarnLight");
                        ltGo.transform.SetParent(bulb.transform, false);
                        var lt = ltGo.AddComponent<Light>();
                        lt.type = LightType.Point;
                        lt.color = new Color(1f, 0.72f, 0.35f);
                        lt.range = 18f;
                        lt.intensity = 2.6f;
                    }
                }
            }

            if (isCampus)
            {
                // 北大第2農場モデルバーン：優美な赤屋根マンサード（腰折れ屋根）と換気クーポラ
                for (int i = i0; i <= i1; i += 6)
                {
                    var rot = Quaternion.LookRotation(Dirs[i], Vector3.up);
                    float w = WallOffset * 2f + 4f, len = 15f;
                    // 下段急勾配屋根
                    Box("BarnMansardLower", Pts[i] + Vector3.up * (H + 3.2f), new Vector3(w, 6.5f, len), rot, barnRedMat);
                    // 上段緩勾配屋根
                    Box("BarnMansardUpper", Pts[i] + Vector3.up * (H + 7.5f), new Vector3(w * 0.65f, 4.5f, len), rot, barnRedMat);
                }

                // 屋根の上の換気小塔（クーポラ / 鐘楼）3基
                int[] cupolaIdxs = { i0 + (int)(segs * 0.25f), i0 + (int)(segs * 0.5f), i0 + (int)(segs * 0.75f) };
                foreach (int ci in cupolaIdxs)
                {
                    var rot = Quaternion.LookRotation(Dirs[ci], Vector3.up);
                    Vector3 cPos = Pts[ci] + Vector3.up * (H + 10.5f);
                    Box("BarnCupolaBase", cPos, new Vector3(3.2f, 3.5f, 3.2f), rot, wallTunnelMat);
                    Box("BarnCupolaRoof", cPos + Vector3.up * 2.8f, new Vector3(3.8f, 2.2f, 3.8f), rot, barnRedMat);
                }

                // 入口・出口の大破風トラスファサード（白下見板壁、赤屋根破風、X字トラス木組み）
                foreach (int portalIdx in new[] { i0, i1 })
                {
                    var rot = Quaternion.LookRotation(Dirs[portalIdx], Vector3.up);
                    Vector3 pPos = Pts[portalIdx];
                    float pW = WallOffset * 2f + 4.5f;
                    // 妻壁ベース（白木造）
                    Box("BarnPortalWall", pPos + Vector3.up * (H + 4.0f), new Vector3(pW, 8.0f, 1.2f), rot, wallTunnelMat);
                    // 赤屋根の大破風
                    Box("BarnPortalGable", pPos + Vector3.up * (H + 7.8f), new Vector3(pW + 1.2f, 2.5f, 1.8f), rot, barnRedMat);
                    // X字木造トラスブレース（左右の交差斜め梁）
                    Box("BarnPortalTruss1", pPos + Vector3.up * (H + 4.0f) + rot * Vector3.forward * 0.7f, new Vector3(0.55f, 7.5f, 0.55f), rot * Quaternion.Euler(0, 0, 36f), woodDarkMat);
                    Box("BarnPortalTruss2", pPos + Vector3.up * (H + 4.0f) + rot * Vector3.forward * 0.7f, new Vector3(0.55f, 7.5f, 0.55f), rot * Quaternion.Euler(0, 0, -36f), woodDarkMat);
                    // 開拓使の赤い星印（シンボルエンブレム）
                    Box("BarnPortalStar", pPos + Vector3.up * (H + 5.2f) + rot * Vector3.forward * 0.8f, new Vector3(1.4f, 1.4f, 0.2f), rot, barnRedMat);
                }
            }
            else
            {
                // 上に載るビル本体（路面に沿って並べる）
                for (int i = i0; i <= i1; i += 8)
                {
                    float bh = 28f + ((i * 37) % 23);
                    var rot = Quaternion.LookRotation(Dirs[i], Vector3.up);
                    float w = WallOffset * 2f + 2f, len = 18f;
                    var b = Box("TunnelBuilding", Pts[i] + Vector3.up * (H + bh * 0.5f), new Vector3(w, bh, len), rot, facade[(t + i / 8) % facade.Length]);
                    TileFacade(b, (w + len) * 0.5f, bh);
                }
            }
        }
    }

    // 立体ジャンプ台（カタパルトランプ）
    void BuildJumpRamps(RaceManager rm)
    {
        if (JumpRamps.Count == 0) return;

        // ジャンプ台スロープ面用マテリアル（アーケードシェブロン＆発光）
        var rampSurfaceMat = new Material(rm.boostPadMaterial)
        {
            mainTexture = TextureGen.JumpRamp(),
            color = Color.white
        };
        rampSurfaceMat.SetTexture("_BaseMap", TextureGen.JumpRamp());
        rampSurfaceMat.EnableKeyword("_EMISSION");
        rampSurfaceMat.SetTexture("_EmissionMap", TextureGen.JumpRampEmission());
        rampSurfaceMat.SetColor("_EmissionColor", new Color(1.2f, 0.9f, 0.5f));
        createdMaterials.Add(rampSurfaceMat);

        // ジャンプ台台座・側面用マテリアル（堅牢なハザードストライプ＆スチール）
        var rampBaseMat = new Material(rm.wallMaterial)
        {
            mainTexture = TextureGen.HazardStripes(),
            color = Color.white
        };
        rampBaseMat.SetTexture("_BaseMap", TextureGen.HazardStripes());
        rampBaseMat.SetFloat("_Smoothness", 0.35f);
        createdMaterials.Add(rampBaseMat);

        // ポール頂点の発光警告ビーコン用マテリアル
        var beaconMat = new Material(rm.glowMaterial)
        {
            color = new Color(1f, 0.75f, 0.1f)
        };
        createdMaterials.Add(beaconMat);

        var curDef = Courses[ActiveCourseIndex];
        for (int r = 0; r < JumpRamps.Count; r++)
        {
            var ramp = JumpRamps[r];
            int i = ramp.index;
            var pos = PointAt(i, ramp.lateral);
            var fwd = Dirs[i];
            var up = Normals[i];

            var rampGo = new GameObject("JumpRamp_" + r);
            rampGo.transform.SetParent(transform, false);
            rampGo.transform.position = pos;
            rampGo.transform.rotation = Quaternion.LookRotation(fwd, up);

            float w = (curDef.Ramps != null && r < curDef.Ramps.Length && curDef.Ramps[r].Width > 0f) ? curDef.Ramps[r].Width : HalfWidth * 1.7f;
            float len = 8.5f;
            float h = 1.6f;

            // 1. スロープ面（上部走行面）
            var slopeMesh = new Mesh();
            slopeMesh.name = "RampSlopeMesh_" + r;
            createdMeshes.Add(slopeMesh);
            slopeMesh.vertices = new[]
            {
                new Vector3(-w * 0.5f, 0.06f, -len * 0.5f), // 0: 手前左
                new Vector3(w * 0.5f, 0.06f, -len * 0.5f),  // 1: 手前右
                new Vector3(-w * 0.5f, h, len * 0.5f),      // 2: 奥左上
                new Vector3(w * 0.5f, h, len * 0.5f)        // 3: 奥右上
            };
            slopeMesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            slopeMesh.triangles = new[]
            {
                0, 2, 1,
                1, 2, 3
            };
            slopeMesh.RecalculateNormals();
            slopeMesh.RecalculateBounds();

            var slopeGo = new GameObject("RampSlope");
            slopeGo.transform.SetParent(rampGo.transform, false);
            slopeGo.AddComponent<MeshFilter>().sharedMesh = slopeMesh;
            slopeGo.AddComponent<MeshRenderer>().sharedMaterial = rampSurfaceMat;

            // 2. 台座・側面・背面フレーム（ハザードストライプ）
            var baseMesh = new Mesh();
            baseMesh.name = "RampBaseMesh_" + r;
            createdMeshes.Add(baseMesh);

            baseMesh.vertices = new[]
            {
                // 左側面（三角）
                new Vector3(-w * 0.5f, 0f, -len * 0.5f),
                new Vector3(-w * 0.5f, h, len * 0.5f),
                new Vector3(-w * 0.5f, 0f, len * 0.5f),

                // 右側面（三角）
                new Vector3(w * 0.5f, 0f, -len * 0.5f),
                new Vector3(w * 0.5f, 0f, len * 0.5f),
                new Vector3(w * 0.5f, h, len * 0.5f),

                // 背面（四角）
                new Vector3(-w * 0.5f, 0f, len * 0.5f),
                new Vector3(-w * 0.5f, h, len * 0.5f),
                new Vector3(w * 0.5f, h, len * 0.5f),
                new Vector3(w * 0.5f, 0f, len * 0.5f)
            };

            baseMesh.uv = new[]
            {
                // 左側面
                new Vector2(0f, 0f), new Vector2(3f, 1f), new Vector2(3f, 0f),
                // 右側面
                new Vector2(0f, 0f), new Vector2(3f, 0f), new Vector2(3f, 1f),
                // 背面
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(4f, 1f), new Vector2(4f, 0f)
            };

            baseMesh.triangles = new[]
            {
                // 左側面
                0, 1, 2,
                // 右側面
                3, 4, 5,
                // 背面
                6, 7, 8,
                6, 8, 9
            };
            baseMesh.RecalculateNormals();
            baseMesh.RecalculateBounds();

            var baseGo = new GameObject("RampBase");
            baseGo.transform.SetParent(rampGo.transform, false);
            baseGo.AddComponent<MeshFilter>().sharedMesh = baseMesh;
            baseGo.AddComponent<MeshRenderer>().sharedMaterial = rampBaseMat;

            // 3. ランプ先端の両脇に警告サインポール＆発光ビーコン
            var poleMat = rm.chromeMaterial;
            foreach (float side in new[] { -1f, 1f })
            {
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                SafeDestroy(pole.GetComponent<Collider>());
                pole.transform.SetParent(rampGo.transform, false);
                pole.transform.localPosition = new Vector3(side * (w * 0.5f), h + 0.8f, len * 0.5f);
                pole.transform.localScale = new Vector3(0.24f, 0.8f, 0.24f);
                pole.GetComponent<Renderer>().sharedMaterial = poleMat;

                var beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                SafeDestroy(beacon.GetComponent<Collider>());
                beacon.transform.SetParent(rampGo.transform, false);
                beacon.transform.localPosition = new Vector3(side * (w * 0.5f), h + 1.65f, len * 0.5f);
                beacon.transform.localScale = Vector3.one * 0.55f;
                beacon.GetComponent<Renderer>().sharedMaterial = beaconMat;
            }

            // 4. スロープ先端の離陸リップフレーム（テイクオフエッジバー）
            float slopeAngle = Mathf.Atan2(h - 0.06f, len) * Mathf.Rad2Deg;
            float slopeLen = Mathf.Sqrt(len * len + (h - 0.06f) * (h - 0.06f));

            var lipGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SafeDestroy(lipGo.GetComponent<Collider>());
            lipGo.name = "TakeoffLip";
            lipGo.transform.SetParent(rampGo.transform, false);
            lipGo.transform.localPosition = new Vector3(0f, h + 0.04f, len * 0.5f);
            lipGo.transform.localRotation = Quaternion.Euler(-slopeAngle, 0f, 0f);
            lipGo.transform.localScale = new Vector3(w + 0.05f, 0.12f, 0.35f);
            lipGo.GetComponent<Renderer>().sharedMaterial = rampBaseMat;

            // 5. スロープ左右のサイドガードレール（走行安全バー）
            foreach (float side in new[] { -1f, 1f })
            {
                var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SafeDestroy(rail.GetComponent<Collider>());
                rail.transform.SetParent(rampGo.transform, false);
                rail.transform.localPosition = new Vector3(side * (w * 0.5f + 0.1f), (h + 0.06f) * 0.5f + 0.15f, 0f);
                rail.transform.localRotation = Quaternion.Euler(-slopeAngle, 0f, 0f);
                rail.transform.localScale = new Vector3(0.18f, 0.35f, slopeLen);
                rail.GetComponent<Renderer>().sharedMaterial = rm.standMaterial;
            }

            // 最初のジャンプ台にアニメーション制御コンポーネントをアタッチ（共有マテリアルを一括駆動）
            if (r == 0)
            {
                var anim = rampGo.AddComponent<JumpRampAnimator>();
                anim.Init(rampSurfaceMat, beaconMat);
            }
        }
    }
}
