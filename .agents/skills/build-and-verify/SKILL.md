---
name: build-and-verify
description: >-
  Use this skill when building the Turbo Circuit Unity project in headless batch mode
  and capturing verification screenshots or 3D model 4-angle turnaround contact sheets
  using verify.ps1 (-shot-ui, -shot-spot, -shot-model, or -autoshot).
---

# Build and Visual Verification (`verify.ps1`)

コード変更・UI修正・コース装飾・3Dモデル追加を行った際、AIエージェントが最小限の待機時間と最小限のツール呼び出し（1枚のコンタクトシート画像）で視覚検証を行うための標準ワークフローです。

## 1. 統合スクリプト `verify.ps1` の使い方

プロジェクトルートから以下のコマンドを実行します。`-Build` を付けると Unity ヘッドレスバッチビルドを実行してから撮影します（コード変更時は `-Build` 必須、既にビルド済みで別アングルだけ撮る場合は省略可）。

### A. 3Dモデル 4面図スタジオ確認 (`-ShotModel`) — 推奨（約1秒）
1m基準グリッド床（0.5mサブグリッド、赤=X軸、青=Z軸）と2.0m高さルーラー付きのスタジオで、**斜め前・正面・左側面・斜め後ろの4面図を1枚の2×2画像（1024×1024）** に合成して出力します。また、モデルの正確なワールド `Bounds`（幅・高さ・奥行・接地底面Y座標）を `verify_model_bounds.txt` に出力します。

```powershell
# 特定のドライバー×マシン組み合わせ（例: Driver 5 = PANDA, Machine 2 = MACH FORMULA）
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -Build -ShotModel "kart:5:2"

# 全マシン一覧 / 全ドライバー一覧
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -ShotModel "kart:all"
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -ShotModel "driver:all"

# 動物モデル単体または全種（bunny, penguin, polar, deer, fox, lion, cow, cat, dog, crab, chick, parrot, panda）
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -ShotModel "animal:cow"

# 観客キャラクター（0〜5 または all）
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -ShotModel "fan:0"

# アイテム（バナナ・ミサイル）
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -ShotModel "item:all"
```

* **出力先**:
  * 画像: `Build/Windows/TurboCircuit_Data/verify_model_*.png`
  * 数値寸法・接地判定レポート: `Build/Windows/TurboCircuit_Data/verify_model_bounds.txt`

---

### B. コース任意地点ピンポイント確認 (`-Course` / `-Progress`) — 約1〜2秒
指定したコース（`0`〜`5`）の進行度（`0.0`〜`1.0`、カンマ区切りで複数指定可）へ即座にテレポートして撮影します。複数地点を指定すると **2×2コンタクトシート（`verify_spot_c<N>_sheet.png`）** も自動生成されるため、1回の `view_file` でコース4箇所を同時に確認できます。

```powershell
# コース4（北大キャンパス）の4地点をHUDなし・カメラ角度オフセット0度で一括撮影
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -Course 4 -Progress "0.106,0.29,0.555,0.82" -Yaw 0 -NoHud
```

* **出力先**:
  * まとめシート: `Build/Windows/TurboCircuit_Data/verify_spot_c4_sheet.png`
  * 個別画像: `Build/Windows/TurboCircuit_Data/verify_spot_c4_p*.png`

---

### C. UI画面ピンポイント高速確認 (`-ShotUi`) — 約1〜2秒
フル撮影（`-AutoShot`、約25秒）を回さず、変更したUI画面だけを即座に撮影します。複数指定時は `verify_ui_sheet.png` にも合成されます。

```powershell
# タイトル画面とリザルト画面だけ高速確認
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -ShotUi "title,results"
```

* **対応ターゲット名**:
  `title`, `title2p`, `settings`, `keys`, `updates`, `netmenu`, `lobby4p`, `lobby8p`, `countdown`, `hud1p`, `hud2p`, `paused`, `results`
* **出力先**:
  * まとめシート: `Build/Windows/TurboCircuit_Data/verify_ui_sheet.png`
  * 個別画像: `Build/Windows/TurboCircuit_Data/verify_ui_<target>.png`

---

### D. フル回帰スクリーンショット (`-AutoShot`) — 約25〜30秒
リリース前や広範囲のUI/コース変更時に全28画面を一括撮影します。

```powershell
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -AutoShot
```

## 2. 注意事項（トラブルシューティング）

* PowerShell から `Unity.exe` を直接 `&` で呼ぶと非同期プロセスとして即座に制御が返る場合があるため、必ず `verify.ps1` 経由（または `cmd.exe /c` 経由）で実行すること。
* `view_file` で画像を確認する際は、個別画像を何枚も開く前にまず `*_sheet.png`（コンタクトシート）と `verify_model_bounds.txt` を確認するとコンテキスト消費を最小化できる。
