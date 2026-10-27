---
name: add-game-content
description: >-
  Use this skill when adding new game content to Turbo Circuit: a new course (Track.DefaultCourses / CourseData),
  a new driver (KartCharacterDef / Kart.Visual BuildCustomDriver), a new kart machine (KartMachineDef / BuildCustomKartParts),
  or a new item (ItemType / ItemDef / Items.cs / AI / NetSession). Lists exactly which files and methods to touch,
  the shared pitfalls (ScriptableObject file names, ExportAllData overwrite, docs counts), and how to verify.
---

# ゲームコンテンツ追加ガイド (コース / ドライバー / マシン / アイテム)

コードを実際に読んで書いた手順。過去の追加コミット（CANYON SPLIT = `2ee8298` ほか、Bomb = `a87a2a3`、Thunder = `b57c182`）を参照している。
**「要確認」と書いた箇所は未検証 or コードから断定できなかった点**なので、実装時に必ず確認すること。

作業前提（`AGENTS.md` / [`github-workflow`](../github-workflow/SKILL.md)）: 機能追加は `feat/〇〇` ブランチ + Issue/PR。`main` へ直接コミットしない。

---

## 0. 共通の落とし穴（全コンテンツ共通）

* [ ] **ScriptableObject は必ずクラス名と同名のファイル**に置く（`KartData.cs` / `KartMachineData.cs` / `CourseData.cs`）。別名ファイルだと `.asset` の `m_Script` が `{fileID: 0}` になり、`Resources.LoadAll` が missing 警告を出して読み込まれない。新しい `ScriptableObject` 型を作るときも同様。
* [ ] **`.asset` は手編集しても残らない**。`BuildTool.Setup()`（`Turbo Circuit > Setup Scene`、`Build Windows`、`verify.ps1 -Build` 経由すべて）が `DataAssetGenerator.ExportAllData()` を呼び、
  `RaceManager.DefaultKartCharacters` / `DefaultKartMachines` / `Track.DefaultCourses`（コード側の既定値）で `Assets/Resources/Data/{Karts,Machines,Courses}/*.asset` を**毎回上書き**する。
  → **データの正は常にコード側の `Default*` 配列**。`.asset` はビルド/Setup 後に生成・更新された結果をコミットに含める。
* [ ] **`ExportAllData` は削除をしない**。ファイル名は `Kart_{i:D2}_{name}.asset` / `Machine_{i:D2}_{name(空白→_)}.asset` / `Course_{i:D2}_{Name(空白→_)}.asset`。
  名前や並び順を変えると旧 `.asset`（と `.meta`）が残り、`Resources.LoadAll` が両方読む（ファイル名 `string.CompareOrdinal` 昇順でソートされる）。**改名・並べ替えをしたら古い `.asset`/`.meta` を手で削除**する。
* [ ] **インデックス＝ID**。コース/ドライバー/マシンは配列インデックスが各所のキーになっている（下記）。**途中挿入は避け、末尾に追加**する。挿入すると既存のゴースト・ベスト記録・装飾調整・Loc 配列がずれる。
  * コース番号キー: `GhostReplay` の `ghost_{n}.json` / `tc_ghost_{n}`、`RaceManager.GetBestTime/GetBestLap` の PlayerPrefs `tc_rec_time_{n}`、`CourseDecorData.ResourcePath` の `Data/CourseDecor/Course_{n:D2}`、`RaceAudio.bgm[n]`、`RaceManager.trackBadgeTex[n]`、`Loc` の各コース配列、`RaceManager.Intro.cs` の `CourseIntroCuts[n]`、ネット同期のコース番号（1 byte）。
* [ ] **ドキュメントの数値・一覧を更新**（過去に更新漏れが実在）:
  * `README.md`: 特徴欄の「〇つの本格サーキット」(51行付近)、コース紹介節「全〇コース」(90行付近、**現状「全6コース」のまま 7 コース目が追加されており古い**)、各コース節、ドライバー/マシン一覧表と見出しの人数、アイテム一覧表と「〇種類のアイテム」(57行付近)。
  * `AGENTS.md`: 「1. プロジェクト概要」の特徴リスト（コース数と名前の列挙、ドライバー/マシン名の列挙）、必要なら 3.x のファイル説明。
  * `README.md` の操作説明表に影響する変更（キー追加など）はそこも更新（`AGENTS.md` 6-3）。
  * README のステータス表は **コード側 `DefaultKartCharacters` / `DefaultKartMachines` と数値が食い違っている箇所がある**（例: OOLI の speed が README 8/8、コード 7）。表を書くときはコードの値（合算は `GetCombinedStats`）を正とし、既存行の不一致は要確認。
* [ ] **検証**: [`build-and-verify`](../build-and-verify/SKILL.md) の `verify.ps1` を使う。コンパイルだけ見たいときは [`unity-compile-and-test`](../unity-compile-and-test/SKILL.md) の `compile-check.ps1`。
  Unity エディタが同プロジェクトを開いていると batch は動かない（`unity-compile-and-test` §0 参照）。
* [ ] **IMGUI**: UI を触るときは `OnGUI` 内で `new GUIStyle()` 禁止、`St(...)` キャッシュ（`AGENTS.md` §4 / [`imgui-ui-design`](../imgui-ui-design/SKILL.md)）。
* [ ] **日本語/英語**: 表示文字列は `Loc.T(en, ja)` か `Loc.cs` の配列で両言語を用意する。
* [ ] **URP/プロシージャル**: モデル・メッシュ・マテリアルを増やすときは [`urp-graphics-optimization`](../urp-graphics-optimization/SKILL.md)。ネットワークに触れるときは [`unity-multiplayer-patterns`](../unity-multiplayer-patterns/SKILL.md)（ただし実装は NGO ではなく独自の `NetSession` + Unity Transport なので、`NetSession.cs` の実コードを必ず読むこと。要確認: スキル本文と実装の差）。

---

## 1. 新コース追加

参考コミット: `2ee8298`（CANYON SPLIT 初出）、`98156b4`（レイアウト改修）、`43401b1`（ジャンプ台調整）、`6469bb2`（分岐AI修正）。

### 1-1. コース定義（必須）
* [ ] `Assets/Scripts/Track.cs` の `Track.DefaultCourses`（`TrackDef[]`）の**末尾に** `new TrackDef { ... }` を追加。
  * `Name`（大文字英語。`.asset` ファイル名と `Loc.CourseName` のフォールバックになる）、`Description`（英語。日本語は後述 Loc）。
  * `Control`: `Vector3[]` の制御点。**閉ループ**の Catmull-Rom（`BuildPath` が `control[(i+1)%n]` で循環参照）。`[0]` がスタート/フィニッシュ。y は標高。既存コースは 14〜17 点。点間隔や曲率が極端だと路面メッシュが破綻するので、`verify.ps1 -Course N` で全周確認する。
  * `Ramps`: `JumpRampDef { Ratio(0..1 の進行度), Power, Lateral(横オフセット), Width(0 で既定=HalfWidth*1.7) }`。`Lateral` が ±1 より大きいとき、`Kart.cs` の判定許容幅が 4.8m に狭まる（通常 7.8m）。
  * `Tunnels`（任意）: `TunnelDef { Start, End }`（進行度）。`BuildTunnels` が生成。
  * `Branches`（任意）: 二股分岐。下記 1-6。
  * 環境色一式: `SkyTint, GroundColor, SunColor, SunIntensity, SunRotation, FogColor, FogDistance, RoadColor, CurbColorA/B, WallColorA/B, ShoulderColor`。
  * `SceneryTheme`: 0=Circuit, 1=Desert, 2=Snow, 3=City, 4=Campus, 5=Overpass。
    * **既存テーマの再利用が最も安全**（CANYON SPLIT は theme 1 = Desert を流用）。
    * 新テーマ（6 以上）を作る場合は `theme`/`SceneryTheme` で分岐している箇所をすべて追加対応する必要がある:
      `Track.Visuals.cs`（`BuildVisuals` の `groundTex`、`BuildScenery`、`BuildSponsorBoards`、`BuildCircuitProps`、`BuildStartLine` 周辺の `SceneryTheme == n` 分岐）、
      `Track.Nature.cs`（`BuildThemedMountains` / `BuildSkyClouds` / `BuildProceduralTerrain` の `theme` 分岐）、`Track.Animals.cs`（`BuildAnimals` の theme 分岐。現状 0〜4 のみ確認、5 の扱いは要確認）、`Track.Scenery.cs`（City/Campus/Overpass 専用ビルダー）。
      さらに `CourseData.cs` の `[Range(0, 5)] sceneryTheme` とヘッダー文字列も更新する。
* [ ] `TrackDef`/`CourseData` に**新フィールドを足す場合**は 3 箇所を同期: `Track.cs` の `TrackDef` struct、`CourseData.cs`（フィールド + `ToDef()`）、`Assets/Editor/BuildTool.cs` の `DataAssetGenerator.ExportAllData` のコース節（`asset.xxx = def.Xxx;`）。CANYON SPLIT で `Branches` を足した際は `BuildTool.cs` に `asset.branches = def.Branches;` の1行が必要だった。**忘れると実行時（`.asset` ロード経路）で値が空になる**。
* [ ] `.asset` が増える: `Assets/Resources/Data/Courses/Course_NN_NAME.asset`（+ `.meta`）。Setup/ビルドで生成されるのでコミットに含める。
  `Track.Courses` は `Resources.LoadAll<CourseData>("Data/Courses")` が 1 件でもあれば `.asset` を優先し、無いときだけ `DefaultCourses` にフォールバックする。つまり **`.asset` が古いままだと新コースが出ない**。

### 1-2. 自動で追随するもの（コード変更不要、動作確認のみ）
* `Track.BuildPath`（スプライン化・2m 等間隔サンプル・`Pts/Dirs/Rights/Normals/Dist`）、`Track.Locate`（進行度・横ずれ）。AI は専用ウェイポイントを持たず、この `Pts/Dirs` を追従する（`Kart.AI.cs`）。
* ミニマップ `Track.MakeMinimap`（bounds から自動。分岐区間は左右2本描画）。
* ゴースト `GhostReplay`（コース番号ごとにファイル/PlayerPrefs）、ベスト記録（`tc_rec_time_{n}`）。
* コース切替 `RaceManager.SwitchCourse` / `LoadCourse`（`Track.Courses.Length` で循環）。ホストのコース変更はネットワークで `Net.SendCourse(int)`（**1 byte**）、Welcome/Start でもコース番号を送るので、**256 コース未満なら同期用のコース ID 追加作業は不要**（要確認: 8 コース目以降でクライアント側 UI に出る固定長配列がないか。`trackBadgeTex` は `Mathf.Max(Track.Courses.Length, 6)` で拡張される）。
* `CourseDecorWindow`（`Track.Courses` を列挙）。

### 1-3. 見た目の生成（コース固有ギミックがある場合）
`Track.Visuals.cs` の `BuildVisuals` が以下を順に呼ぶ。コース固有処理は `def.Xxx` / `ActiveCourseIndex` / `SceneryTheme` で分岐する。
* [ ] 路面・縁石・壁: `Strip(...)`。`Branches` があるコースは `BuildSplitTrack` に切り替わる。
* [ ] 地形: `BuildProceduralTerrain`（`Track.Nature.cs`）。
* [ ] `BuildStartLine`, `BuildBoostPads`, `PlanItemBoxes`, `BuildBridgePillars`, `BuildJumpRamps`, `BuildTunnels`。
  * **`BuildBoostPads` / `PlanItemBoxes` はコース別データを持たず**、`Branches` の有無で「分岐コース用配置」か「汎用配置（Count の 22/48/81% など）」かを決めるだけ。コース固有の配置にしたい場合は、この2メソッドに分岐を足す（`ActiveCourseIndex` か新フィールド）。分岐コースは `Branches[0]` 固定で CANYON SPLIT 向けの割合がハードコードされているため、**2 つ目の分岐コースを作る場合は要改修**。
* [ ] `BuildScenery` / `BuildSponsorBoards` / `BuildCircuitProps` → `ApplyDecorOverrides`（手動調整の適用）→ `BuildAnimals`。
  * 装飾の手動調整は `Track.Decor.cs` の仕組み。`decorStart`〜`ApplyDecorOverrides` の範囲内で生成する。生成は決定的に（`UnityEngine.Random`/`Time` を使わず `System.Random(seed)`）。詳細は [`course-decor-editor`](../course-decor-editor/SKILL.md)。
* [ ] 専用テクスチャが要る場合は `TextureGen.cs` に `public static Texture2D Xxx()`（`cache` 辞書でキャッシュする既存パターン）を追加。
* [ ] 動くギミックの MonoBehaviour（`RiverFlowAnimator` / `JumpRampAnimator` 等）は `Track.Visuals.cs` 末尾付近のクラス。これを付けたオブジェクトは `ApplyDecorOverrides` の対象外になる。

### 1-4. UI・演出（コース番号インデックスの配列）
* [ ] `Assets/Scripts/Loc.cs`: `CourseNamesJa`, `CourseDescsJa`, `CourseDiffEn`, `CourseDiffJa`, `CourseShortDiffEn`, `CourseShortDiffJa` の**末尾に 1 要素ずつ**追加（範囲外だと `Loc.CourseName` 等は英語/先頭要素にフォールバックするだけでエラーにならないので、漏れに気づきにくい）。
* [ ] `Assets/Scripts/RaceManager.Intro.cs`: `CourseIntroCuts[]` に `CourseIntroCut`（`camPosStart/End`, `lookTargetStart/End`, `fovStart/End`）を末尾追加。**無いと最後の要素に Clamp され別コースの空撮になる**（`UpdateIntroCamera` は `Mathf.Clamp(SelectedCourse, 0, CourseIntroCuts.Length - 1)`）。座標はワールド座標なので `Control` に合わせて決める。
* [ ] コース選択 UI `RaceManager.UI.cs`（タイトルの左カード、`SelectedCourse` 参照箇所）:
  * 難易度ピルの色 `diffBg` は各コースに応じたカラー（5=マリーナブルー、6=キャニオンテラコッタ等）を指定。独自色が欲しければここを編集。
  * バッジ画像 `trackBadgeTex[SelectedCourse]`（`RaceManager.Assets.cs`）: `Resources/UI/badge_track_{i}_pop.png` → `badge_track_{i}.png` の順に探し、無ければ 3〜6 は `TextureGen.CityBadge/HokkaidoBadge/OverpassBadge/CanyonBadge` にフォールバック。新コースでバッジを足す際は PNG を追加するか `TextureGen` に生成関数を足して `RaceManager.Assets.cs` のフォールバック行に追加。
* [ ] オンライン側の表示（`RaceManager.Online.cs` の `Loc.CourseName(SelectedCourse, ...)`, `Loc.CourseShortDifficulty`）は配列追加で自動対応。
* [ ] **BGM**: `Assets/Scripts/RaceAudio.cs` の `bgm` は `new AudioClip[N]`（現在 7）で `Resources.Load("Audio/bgm_" + i)` を読み、`SetCourseMusic/RestartMusic` は `Mathf.Clamp(course, 0, bgm.Length - 1)`。新コース用楽曲を追加する際は `Assets/Resources/Audio/bgm_N.ogg`（および `.meta`）を追加し、`bgm` 配列長を増やし、`Assets/Resources/Audio/CREDITS.md` の Music 表にクレジットを追記する。

### 1-5. 検証
```powershell
# コースを数地点まとめて撮影（ビルドあり。コード変更後は -Build 必須）
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -Build -Course 7 -Progress "0.0,0.25,0.5,0.75" -NoHud
# タイトル（コース選択カード・バッジ・難易度ピル）
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -ShotUi "title,hud1p,results"
```
* `-Course` は `Track.Courses.Length-1` に Clamp される（`build-and-verify` SKILL.md の「0〜5」は古い記述）。
* 分岐やジャンプ台など特定地点は `-Progress` に `Branches`/`Ramps` の Ratio を指定。
* AI の挙動（壁への張り付き、コース逸脱）は静止画では分からない。実際に Demo/レースを走らせる必要がある（過去に `6469bb2` で分岐区間の AI 異常が後から見つかった）。自動検証手段は要確認。
* 装飾を手で調整する場合は Course Decor Editor（[`course-decor-editor`](../course-decor-editor/SKILL.md)）。保存先は `Assets/Resources/Data/CourseDecor/Course_NN.asset`（現状 `Course_04.asset` のみ存在）。

### 1-6. 分岐（二股）コースの場合の追加ポイント
* `BranchDef { Start, End, LeftOffset, RightOffset, LaneWidth, DividerWidth, RightHeight }`。遷移区間は `Track.IsInBranch` 内の `transLen = 0.035f`（進行度）。
* 触るコード（CANYON SPLIT 初出コミットで変更された箇所）:
  * `Track.cs`: `IsInBranch`, `PointAt`（右車線の持ち上げ）, `MakeMinimap`。
  * `Track.Visuals.cs`: `BuildSplitTrack`, `BuildSplitFeatures`, `BuildBoostPads`, `PlanItemBoxes`。
  * `Kart.cs`: 壁判定（中央分離帯 `dividerLimit` と外壁 `limit`）、ジャンプ台の `Lateral` 対応。
  * `Kart.AI.cs`: ルート選択（`aiBranchChoice`、`PlayerIndex % 2` で左右を振り分け）、分岐区間の境界反発。分岐接近の補正は **`Branches[0]` のみ**対象。
* コース全体の `Lateral` 前提（`Track.HalfWidth` を使う箇所）が分岐区間で崩れないか、ジャンプ台・ブーストパッド・アイテムボックスの横位置が車線内かを `-Progress` で確認する（`43401b1` は右車線内への精密調整）。

---

## 2. 新ドライバー追加

現状: 10 人。index 0〜4 = Kenney 製モデルのキャラクター(OOBI/OODI/OOLI/OOPI/OOZI)、index 5〜9 = 動物(PANDA/PENGUIN/FOX/BUNNY/LION)。

* [ ] **データ**: `Assets/Scripts/RaceManager.cs` の `DefaultKartCharacters`（`KartCharacterDef { name, driver(二つ名), trait, color, speed/accel/handling/weight (1〜8) }`）の**末尾に追加**。
  性能はマシンと合算（`GetCombinedStats` = `ドライバー + マシン − 5` を 1〜8 に Clamp）。`KartData.cs` は項目の型定義のみなので通常は触らない。
  `.asset` は `Kart_NN_NAME.asset` が増える（§0 の上書き注意）。`KartCharacters` getter は `Resources.LoadAll<KartData>("Data/Karts")` が `DefaultKartCharacters.Length` 以上のときだけ `.asset` を採用する。
* [ ] **モデル**: `Assets/Scripts/Kart.Visual.cs`。
  * 配列 `KenneyKarts`（`"kart-oobi"`…）と `AnimalDrivers`（`"panda"`,`"penguin"`…）。`BuildCustomDriver(driverIdx, liftY)` は `driverIdx < KenneyKarts.Length` なら Kenney キャラ、それ以外は `driverIdx - KenneyKarts.Length` で `AnimalDrivers` を引く（`Mathf.Clamp` されるので**配列に足し忘れると最後の動物で表示される**）。
  * 動物の追加: `Assets/Resources/Animals/animal-<name>.fbx` が存在すること（`animal-beaver`, `-elephant`, `-giraffe` など多数が既にある）→ `AnimalDrivers` に名前を追加 → 必要なら同メソッド内の `aScale`（`bunny 0.66 / lion 0.70 / その他 0.72`）の分岐を追加。`leg-*` 子は座り姿勢のため自動で除去される。動物共通マテリアルは `GetAnimalDriverMaterial()`（`colormap.png`）。
  * 新種の Kenney キャラ（FBX）追加は `Assets/Resources/Karts/kart-xxx.fbx` と `KenneyKarts` への追加で、その FBX に `character` 子オブジェクトがあることが前提（`BuildCustomDriver` が `Find("character")` する）。
  * **ドライバー数のハードコード**: `Kart.Visual.cs` の `TryBuildKenneyModel` で AI フォールバックの `(slotFallback + 1) % 10`（ドライバー数）/`(slotFallback + 2) % 8`（マシン数）が直書き。人数が変わったら `RaceManager.KartCharacters.Length` / `KartMachines.Length` 参照に直す（要確認: 他に `10` 直書きがないか grep）。
* [ ] **選択 UI**: `RaceManager.UI.cs` は `KartCharacters.Length` を参照するので 1P/2P の選択は自動対応。ただし**ガイド文字列が直書き**: `RaceManager.UI.cs` の `"[W][S] Driver (10)   •   [Q][E] Kart (8)"` / `"... ドライバー (10) ... マシン (8)"`（775 行付近）。人数に合わせて更新。
* [ ] **ローカライズ**: `Loc.cs` の `DriverNamesJa`, `DriverSubsJa`（"二つ名  •  特徴" 形式）を**同じ順序で**追加。
* [ ] **ネットワーク**: `RaceManager.PackDriverKart/UnpackDriverKart` は driver/kart を各 **4bit（0〜15）** に詰めて 1 byte で送る。**ドライバー/マシンはそれぞれ最大 16 種まで**。それ以上は要設計変更（`NetSession` の `Hello`/`LobbySync` の `kartChar` が byte）。
* [ ] **AI の割り当て**: `RaceManager.cs` の AI は `aiDrv = (ai + 3) % KartCharacters.Length`, `aiKrt = (ai + 1) % KartMachines.Length`。人数が増えると AI の顔ぶれが変わる（意図通りか確認）。AI 名は `AiNames`（`"Blaze"`…7 個）+ `Loc.AiNamesJa` で、ドライバー数とは無関係。
* [ ] **検証**:
```powershell
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -Build -ShotModel "driver:10"   # 追加した index
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -ShotModel "driver:all"
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -ShotUi "title,title2p,lobby8p"
```
  `-ShotModel driver:N` は `RaceManager.Verify.cs` の `category == "driver"` で、マシン index 0（STANDARD）に載せて撮影（`KartCharacters.Length` で Clamp）。`verify_model_bounds.txt` に Bounds が出る。動物の単体確認は `animal:<name>`。
* [ ] ドキュメント: README のドライバー一覧表・見出し「10人の個性派ドライバー」「(10 Characters)」・組み合わせ数「全80通り」、`AGENTS.md` §1 のドライバー列挙。`AppUpdater.cs` の `SimulateUpdateAvailableForTesting` 内の文言は**テスト用ダミー**で更新不要。

---

## 3. 新カートマシン追加

現状: 8 機（0 STANDARD, 1 PIPE BUGGY, 2 MACH FORMULA, 3 MONSTER, 4 CYBER GT, 5 ROADSTER, 6 MUSCLE V8, 7 JET POD）。

* [ ] **データ**: `RaceManager.cs` の `DefaultKartMachines`（`KartMachineDef { name, code("XXX-NN"), trait, color(車体色), speed/accel/handling/weight }`）の**末尾に追加**。`KartMachineData.cs` は型のみ。`Machine_NN_NAME.asset` が増える（空白は `_` に置換される）。
* [ ] **モデル**: `Assets/Scripts/Kart.Visual.cs`
  * `TryBuildKenneyModel`: 共通シャシー（`Karts/kart-oobi`）+ ホイール + ドライバーを組み立てる。`liftY = (kartIdx == 3) ? 0.10f : 0f`（MONSTER のみハイリフト）。ホイールの特殊装飾は `kartIdx == 4`（ネオンリング）、`kartIdx == 3 || 5`（クロームハブ）の**index 直書き**。
  * `BuildCustomKartParts(kartIdx, ...)` の `switch (kartIdx)` に `case N:` を追加。`Part(PrimitiveType, pos, scale, material, mpb, rot)` でプリミティブを組む（`rm.kartPaintMaterial`/`chromeMaterial`/`glowMaterial`/`visorMaterial`/`taillightMaterial`、`paint`/`dark`/`accent` の `MaterialPropertyBlock`）。`wheelPos[0..3]`（前左/前右/後左/後右）、`useRacingWheel`、`frontScale/rearScale` を `out` で調整できる。**`switch` に無い index は共通シャシーのみになる**ので追加漏れに注意（現状 1〜4 の case も存在する）。
  * 車体色は `Color`（= `KartMachines[i].color`）。ドライバー色 `DriverColor` はアクセントに使われる。
  * AI フォールバック `% 8` の直書き（§2 参照）。
* [ ] **選択 UI**: `KartMachines.Length` を参照するため自動対応。ガイド文字列 `"Kart (8)"` / `"マシン (8)"` を更新。
* [ ] **ローカライズ**: `Loc.cs` の `KartNamesJa`, `KartTraitsJa`（`Loc.KartSub` が `"{code}  •  {trait}"` を作る）を同順で追加。
* [ ] **ネットワーク**: ドライバーと同じく kart index は 4bit（最大 16 種）。
* [ ] **検証**:
```powershell
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -Build -ShotModel "kart:0:8"   # kart:<driver>:<machine>
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -ShotModel "kart:all"          # 全マシン一覧
```
  4面図（斜め前/正面/左側面/斜め後ろ）と `verify_model_bounds.txt`（寸法・接地判定）を確認。ホイールが地面に接しているか、ドライバーがシートに収まっているかを見る。
* [ ] ドキュメント: README マシン一覧表・見出し「8種類」「(8 Kart Machines)」・「全80通り」、`AGENTS.md` §1 のマシン列挙・`3.2` の説明。

---

## 4. 新アイテム追加

参考: Bomb(`a87a2a3`)、Thunder(`b57c182`、`ItemDef` マスター化後の追加例)。現状 `enum ItemType { None, Turbo, Banana, Missile, Star, Bomb, Thunder }`（`Kart.cs` 冒頭）。
HUD のアイテム枠・ルーレット・出現確率は `ItemDef` マスターから自動解決されるので **HUD 側の編集は原則不要**（`RaceManager.HUD.cs` は `GetItemDef(shown)` を使う。Star のみ虹色の特別扱いあり）。

* [ ] **種類の追加**: `Assets/Scripts/Kart.cs` の `enum ItemType` の**末尾に追加**（既存値の並びを変えない）。
* [ ] **マスター定義**: `Assets/Scripts/RaceManager.Assets.cs`
  * アイコン読込: `iconXxx = LoadTexture("Icons/item_xxx_pop", iconDir + "item_xxx_pop.png");`（既存の読込行の並び）と、`RaceManager.cs` のフィールド宣言 `Texture2D iconTurbo, iconBanana, ...` に追加。
  * `InitItemDefs()` に `ItemDefs.Add(new ItemDef { Type, NameEn, NameJp, AccentColor, Icon, WeightFunc = p => ... })`。`p` は順位比率（0.0=1位、1.0=最下位）。条件付き出現は Thunder のようにクールダウン/順位条件をラムダ内に書く。`RollItem`（`RaceManager.cs`）が全 `ItemDefs` の重みから抽選する。
  * アイコン PNG: `Assets/Resources/Icons/item_xxx_pop.png`（256x256 透過、`.meta` 付き）。README 用に `Screenshots/item_xxx_pop.png` も置く運用（既存アイテム全てに存在）。アイコンが無い場合 HUD は色付き矩形+名前にフォールバックする。
* [ ] **使用ロジック**: `Kart.cs` の `UseItem(KartInput inp)` の `switch (it)` に `case ItemType.Xxx:` を追加。効果の実体は
  * 飛び道具/設置物 → `Items.cs` に MonoBehaviour クラス（既存: `ItemBox`, `Banana`, `Missile`, `Bomb`）+ `RaceManager.cs` に `Spawn*` / `Remove*` / リスト（`bananas`, `missiles`, `bombs`）。**リストは `LoadCourse` と レース再開処理（`bombs.Clear()` が 2 箇所: コース読込時と再スタート時）で `Destroy`+`Clear` が必要**。
  * 全体効果 → Thunder のように `RaceManager.UseThunder` + `Kart.Shock` 的な状態メソッド/タイマー（`shockTimer` 等）。
  * 被弾状態を足す場合は `Kart.Tick`（速度上限・見た目・タイマー減算）、`RaceManager.KartCollisions`（質量・衝突時スピン）も確認する。
  * ネットワーク状態に影響する自己状態（`FBoost`/`FShield`/`FStar` 等のフラグ）が増える場合は `NetKartState.Flags`（byte、`FStar` と `FShield` が同値 4 で兼用になっている点に注意）の空きビットを要確認。
* [ ] **AI の使用判断**: `Kart.AI.cs` のアイテム使用 `switch (Item)` に `case ItemType.Xxx: canUse = ...;`（`rm.KartAheadWithin(this, dist)` / `rm.KartBehindWithin` / `rm.IsMissileApproaching` 等のヘルパー）。**case を足さないと `canUse=false` のまま `aiItemTimer < forceUseThreshold`（-3.5 秒）で強制使用**になる。Star のみ閾値が -4.5。
* [ ] **ネット同期**（オンライン時にリモートでも効果を出す必要があるアイテムのみ）: `Assets/Scripts/NetSession.cs`
  * `enum Msg : byte` に新メッセージ（現状 `ThunderUse = 12` まで）を追加。
  * `Handle()` 冒頭の最小ペイロード長テーブル（`switch (type)` の `need = ...`）に追加（**これに無い型は `default: return;` で破棄される**）。受信側 `case Msg.Xxx:` でパースし `OnXxx?.Invoke(...)`、ホストなら `BroadcastExcept(sourceConn, true, ...)` で中継（Thunder/Bomb の実装を踏襲）。
  * 送信メソッド `SendXxx(...)`（ホストは `Broadcast`、クライアントは `Open(clientConn, ...)`→`driver.EndSend(w)`）、コールバック `public Action<...> OnXxx`。
  * `RaceManager.cs` の `Awake` で `Net.OnXxx = ...` を設定（Thunder: `Net.OnThunder`）、使用側（`UseXxx`/`SpawnXxx`）で `if (Online && !owner.IsRemote) Net.SendXxx(...)`。ローカルプレイヤー自身の分は二重適用しない（Thunder は `userSlot` のカートを除外）。
  * 設計規約は [`unity-multiplayer-patterns`](../unity-multiplayer-patterns/SKILL.md)。**既存メッセージの意味・長さは変えない**（後方互換。バージョン間の互換方針は要確認）。
* [ ] **効果音**: `RaceAudio.cs` に専用 SE が要る場合のみ（既存は `rm.Audio.Drop()/Boost()/Missile()/Star()/Explode()/Hit()` などを流用）。
* [ ] **検証用ショット**:
  * `RaceManager.Verify.cs` の `-shot-ui` の `hud1p` / `hud2p` ケースで `SetPreviewSpeedAndBoost(..., ItemType.Xxx)` を渡すと HUD のアイテム枠を確認できる（Thunder 追加コミットではここの引数を差し替えた）。
  * 3D モデルがあるアイテムは同ファイルの `category == "item"` ブロックに `shotXxx` を追加すると `verify.ps1 -ShotModel "item:xxx"` で4面図が撮れる（現状 banana/missile/bomb/box。`item:all` で全部）。
```powershell
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -Build -ShotUi "hud1p,hud2p"
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -ShotModel "item:all"
```
* [ ] ドキュメント: README「〇種類のアイテム」(57行付近) の文言、アイテム一覧表（`Screenshots/item_xxx_pop.png` 付き）、`AGENTS.md` §3.4 の `Items.cs` 説明（"ロケット、バナナ、スター等"）。

---

## 5. 最終チェックリスト（PR 前）

* [ ] `Default*` 配列（コード）と `Assets/Resources/Data/**.asset`（Setup 後の生成物）の両方がコミットに入っている。古い `.asset` が残っていない。
* [ ] 追加物に対応する `Loc` 配列（コース名/説明/難易度/ドライバー名/サブ/マシン名/特性）が日英とも同じ順序・同じ要素数。
* [ ] `compile-check.ps1` または `verify.ps1 -Build` が通る。
* [ ] `verify.ps1`（`-Course` / `-ShotModel` / `-ShotUi`、広範囲なら `-AutoShot`）の画像を実際に見て、文字被り・崩れ・接地を確認した。
* [ ] README / AGENTS.md の数値・一覧を更新した（§0）。
* [ ] 作業手順が再利用できる形で新しく分かったことは、該当スキルに追記した（`AGENTS.md` 6-4）。
* [ ] Issue → 作業ブランチ → PR（`Closes #n`）。リリースのバージョン更新は別 PR（[`github-workflow`](../github-workflow/SKILL.md)）。
