---
name: imgui-ui-design
description: >-
  Turbo Circuit の IMGUI UI（RaceManager.UI.cs / RaceManager.HUD.cs / RaceManager.Online.cs）に
  画面・メニュー項目・HUD要素を追加/変更するときに使う。DrawPopCard / DrawPopRibbon / DrawModernButton /
  DrawPopPill の使い方、St(...) styleCache、720p基準の座標系、2P画面分割HUD、キーボード/パッド/マウス入力
  （PollMenuNav と各フォーカス変数）、ホワイトカード調のデザイン規約、README 操作表の更新、
  verify.ps1 -ShotUi による目視検証までの手順をまとめる。
---

# IMGUI UI の追加・変更ガイド

対象: `Assets/Scripts/RaceManager.UI.cs`, `RaceManager.HUD.cs`, `RaceManager.Online.cs`（および入力側の `RaceManager.cs`）。
以下はすべてコードを読んで確認した事実。行番号は執筆時点のもので、変更で前後する。

## 1. 座標系・解像度スケーリング

`OnGUI()`（UI.cs 冒頭付近）で次のように設定される。

```csharp
float scale = Screen.height / 720f;
GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
float w = Screen.width / scale, h = 720f;
```

- **高さは常に 720 の論理座標**。幅 `w` は画面アスペクト比で変わる（16:9 なら約 1280）。
- 各 `DrawXxx(float w, float h)` には論理座標の `w`/`h` が渡る。ピクセル直書きではなく `w`/`h` 基準で配置する（中央寄せ例: `new Rect((w - 480) * 0.5f, (h - 440) * 0.5f, 480, 440)`）。
- 3D のワールド座標を UI に載せる場合は `scale` で割る（`DrawLobby` の `gx = sp.x / scale, gy = (Screen.height - sp.y) / scale`）。
- 幅 `w` が可変なので、左右端に貼るものは `w - 値` 起点、画面端のはみ出しは `Mathf.Clamp` で守る（DrawLobby の名前タグ）。
- 検証撮影や `previewSpectatorsRequested` / `suppressVerifyGui` が true のときは `OnGUI` が即 return する（`-NoHud` 用）。

## 2. 共通部品（RaceManager.UI.cs）の正確なシグネチャ

| 部品 | シグネチャ | 備考 |
|---|---|---|
| カード | `void DrawPopCard(Rect r)` | 角丸 18、下に影（y+4.5）。白ベースのカード |
| リボン | `void DrawPopRibbon(Rect r, Texture2D tex, string text)` | `tex == ribbonTrackTex` なら青、それ以外（`ribbonDriverTex` など）はオレンジ。文字は白＋黒アウトライン（sMid 14, Bold） |
| ボタン | `bool DrawModernButton(Rect r, string label, string shortcut = "", Color? accentColor = null, bool isPrimary = false, bool focused = false)` | クリックされたフレームだけ true。`shortcut` は右寄せ表示（例 `"[ESC / B]"`）。`focused` は パッド/キーボード選択の発光＋`DrawFocusHighlight`。`GUI.enabled == false` のときホバー扱いにならない |
| ピル | `void DrawPopPill(Rect r, string text, Color bg)` | 小さなラベル/バッジ（sSmall 12 Bold、白＋アウトライン） |
| フォーカス枠 | `void DrawFocusHighlight(Rect r, float radius = 8f)` | 金⇔シアンに脈動する枠。スライダー行など独自行に使う（Settings の各行） |
| 統計ゲージ | `void DrawToonStatGauge(float x, float y, float w, string label, int value, int maxVal, Color barColor)` | ロビー/タイトルのステータス表示 |
| 枠線 | `static void DrawFrame(Rect r, float t, Color c)` | |
| 縁取り文字 | `static void Outlined(Rect r, string text, GUIStyle style, Color color, float thickness = 3f)` | 8方向に黒を描いてから本体。HUD/ボタン内文字の標準 |

使用例（ポーズ/設定/結果/オンラインメニューすべて同じ型）:

```csharp
var r = new Rect((w - 480) * 0.5f, (h - 440) * 0.5f, 480, 440);
DrawPopCard(r);
DrawPopRibbon(new Rect(r.x + 20, r.y + 14, r.width - 40, 36), ribbonDriverTex, Loc.T("★ TITLE ★", "★ タイトル ★"));
if (DrawModernButton(new Rect(r.x + 40, r.y + 82, r.width - 80, 46),
        Loc.T("►  DO IT", "►  実行"), "[ENTER / A]", new Color(0.18f, 0.78f, 0.42f), true, myFocus == 0))
{
    /* クリック時の処理 */
}
```

- カード内の本文ラベルは暗い青系（既存例: `new Color(0.2f, 0.25f, 0.4f)`、補足は `new Color(0.35f, 0.42f, 0.55f)`）を使う。白背景で読めない白文字にしない。
- パネルの背面暗転は `GUI.color = new Color(0,0,0,0.65f〜0.75f); GUI.DrawTexture(new Rect(0,0,w,h), Texture2D.whiteTexture); GUI.color = Color.white;` の形（Pause=0.65, Settings=0.75, Results=0.65, NetMenu=0.65）。
- `GUI.color` を変更したら必ず `Color.white` に戻す（`Outlined` は内部で保存・復元する）。
- 角丸テクスチャは `TextureGen`（`PopCard`, `PopButton`, `PopRibbon`, `PopPill`, `RoundedPanel(w,h,radius,fill,border,borderThick)`, `RoundedShadow`）が生成。サイズ(w,h)をキーにキャッシュされるため、**毎フレーム違う寸法を渡さない**（アニメで寸法を変える場合は `GUI.matrix` で拡縮する。`DrawLobby` の START ボタンの脈動が実例）。

## 3. GUIStyle は `St(...)` 経由のみ（`new GUIStyle()` 禁止）

```csharp
GUIStyle St(GUIStyle b, int size = 0, TextAnchor? anchor = null, FontStyle? fs = null, bool wrap = false, Color? text = null)
```

- `b` はベーススタイル: `sBig`(90) / `sMid`(38) / `sSmall`(22, 本文向き) / `sNum`(数字フォント) / `sMono`(等幅) / `sSys` / `sButton` / `sField`。null は `sBig`。
- 内部キー `(baseId, size, anchor, fontStyle, wrap, colorRGBA)` で `styleCache`（Dictionary）に保存し、同条件なら同じインスタンスを返す。
- **理由**: `OnGUI` は1フレームに複数回呼ばれ、`new GUIStyle()` の毎回生成はGC負荷になる（AGENTS.md §4）。
- 日本語時、`fontJpPop`/`fontJpRound` ベースでは `fs`（Bold等）が自動的に `FontStyle.Normal` へ置換される（元が極太で、疑似ボールドを重ねると漢字が潰れるため）。Bold を指定しても問題ない。
- 言語切替（`ApplyLanguageFonts`）で `sBig = null; styleCache.Clear();` され、次の `OnGUI` で作り直される。スタイルをフィールドに保持しない。`St(...)` を毎回呼ぶこと。
- 動的に変わる値（アニメ中のフォントサイズなど）は `int` に丸めると同キーに収束する。連続値を渡すとキャッシュが増え続けるので注意（`DrawBanner` は `(int)(72 * pop)` を渡している）。
- 文字列の毎フレーム結合も避ける。既存の `GetSpeedString` / `GetLapString` / `FormatTime`（`digits2` テーブル）が参考。

## 4. 言語（Loc）

- 表示文字列は必ず `Loc.T("English", "日本語")`。両言語を同時に書く。
- ドライバー名/マシン名/コース名は `Loc.DriverName` / `Loc.KartName` / `Loc.CourseName` / `Loc.PlaceText` などを使う（結果画面・ロビーの実例）。
- 日本語は英語より幅を取る場合がある。文字被りは両言語で確認する（§9）。

## 5. 入力処理の構造（描画と入力は別関数）

IMGUI の描画側は「マウスクリック」を `DrawModernButton` の戻り値で受ける。**キーボード/パッドの操作は `RaceManager.cs` の `Update()` 側で処理**する。

### 5.1 入力スナップショット

`Update()` が `PollMenuNav` で `MenuNav` を作る（1フレーム1回）:

- `moveX/moveY`: パッド（`InputSettings.GetMenuPadVector()`）または `Input.GetAxisRaw("Horizontal"/"Vertical")` を閾値 0.45/0.55 で -1/0/1 化。長押しリピート（初回 0.25s、以降 0.13s）。テキスト編集中は軸入力を無視。
- `confirm`: パッド A または Enter/KeypadEnter（テキスト編集中は Enter 無効）
- `cancel`: パッド B または Escape
- `tabPrev/tabNext`: パッド LB/RB
- `padStart/padBack/padX/padY`: START/BACK/X/Y

### 5.2 入力の優先順位（`Update()`）

```
showQuitConfirm → UpdateQuitModalInput
updater.ShowModal → UpdateModalInput
showSettings → UpdateSettingsInput
UpdateOverlayInput（オンラインメニュー / 接続中キャンセル / レース中のポーズ切替）
paused → UpdatePausedInput
state別: Title→UpdateTitleInput / Intro / Countdown / Racing / Results→UpdateResults
```

描画側 `OnGUI` の `overlayModalOpen = updateModalOpen || showSettings || paused || showQuitConfirm` と対応していて、モーダル表示中は背面を `GUI.enabled = false` にして背面ボタンがマウスクリックを奪わないようにしている。**新しいモーダル/オーバーレイを足したら、`Update()` の分岐・`OnGUI` の `overlayModalOpen`・`GUI.enabled` の三点を揃える**。

### 5.3 フォーカス変数（現状の定義、RaceManager.cs 134〜143行付近）

| 変数 | 意味 |
|---|---|
| `titleFocus` | 0=Start, 1=Drv1, 2=Krt1, 3=Drv2, 4=Krt2, 5=Course, 6=Mode, 7=1P/2P, 8=Online, 9=Options, 10=Updates, 11=Language, 12=Quit。移動順は `TitleFocusOrder1P` / `TitleFocusOrder2P` |
| `pauseFocus` | 0〜4（再開/やり直し/設定/タイトル/終了）。`% 5` で循環 |
| `settingsTab` / `settingsFocus` / `settingsCol` | タブ0=General(フォーカス0〜6、6=閉じる) / 1=Keys&Pad(0〜8、7=リセット、8=閉じる、`settingsCol` 0=キーボード 1=パッド) / 2=Updates(0〜3、3=閉じる) |
| `netFocus` | 0〜4（Host Relay / Join Relay / Host Direct / Join Direct / Close）。`% 5` |
| `lobbyFocus` | 0=Driver, 1=Kart, 2=Start(ホスト)/Copy(クライアント), 3=Leave。`% 4` |
| `resultsFocus` | 0=Race Again, 1=Back to Title |
| `updateModalFocus`, `quitModalFocus` | モーダルのボタン選択（`quitModalFocus` は 0=Quit, 1=Cancel、既定 1） |

`DrawModernButton(..., focused: xxxFocus == n)` と、`UpdateXxxInput` 内の `% N` の N、`closeFocusIdx` のような「閉じるボタンの番号」は**描画側と入力側で別々にハードコードされている**。項目を増やしたら両方の件数・番号を必ず同時に直す。

### 5.4 ショートカット表記

- ボタン右側の `shortcut` 文字列（`"[ESC / START / B]"`, `"[R / X]"`, `"[O]"`, `"[T / Y]"`, `"[Q / BACK]"` など）は**表示だけ**。実際のキー処理は `Update` 側（例: `UpdatePausedInput` の `Input.GetKeyDown(KeyCode.R) || nav.padX`）に書く。表記と実処理を必ず一致させる。
- 画面下部のガイド文（タイトルの `guideText`、ポーズのサブタイトル、設定のヘルプボックス）も手書き文字列なので、キーを変えたら一緒に更新する。
- テキスト入力欄（`GUI.TextField`、`sField`）がフォーカス中は `IsEditingTextField` が true になり、W/A/S/D/Q/E/L/P/O/U/M/Tab/Space などのショートカットを `!editTxt` で抑止している。`IsEditingTextField` は「設定タブ0」と「オンラインメニュー（非接続中）」のときだけ true を返す定義なので、**別の画面にテキスト欄を足す場合はこの定義と `PollMenuNav` 冒頭の `keyboardControl` 解除条件にも追加する**。欄外クリックで `GUIUtility.keyboardControl = 0` する処理（`DrawNetMenu`, `DrawSettings` 参照）も同様に入れる。

### 5.5 キーコンフィグ連動

- 走行用の操作（アクセル/ブレーキ/左右/ドリフト/アイテム/後方視点）は `InputSettings`（`Kart.Input.cs`）の `Key*`/`Pad*` を参照し、PlayerPrefs（`tc_key_*`, `tc_pad_*`, `tc_pad_deadzone`）に保存される。
- 設定画面の KEYS & PAD タブは `keyEntries` 配列（id: `accel`, `brake`, `left`, `right`, `drift`, `item`, `rear`）で行を描画し、`UpdateSettingsInput` の `ids` 配列と `DrawSettings` のキー待ち `switch (waitingKeyAction)` が同じ id を使う。**リバインド可能な操作を増やす場合は、`InputSettings`（フィールド/Load/Save/ResetDefaults）、`keyEntries`、`switch`、`ids`、フォーカス上限 `% 9`・`closeFocusIdx`・リセット(7)/閉じる(8)の番号をすべて揃える**。
- メニュー操作用の A/B/START などのパッド割り当ては `PadBind` 経由で読む（`InputSettings.GetSinglePadDown(PadBind.A, 0/1)`、パッド番号 0/1 は2台分）。
- メニュー内のW/S/Q/E/A/Dなどは現状**キーコンフィグを参照しないハードコード**（`UpdateTitleInput`）。走行キーの変更がメニューに影響しない点に注意。

## 6. 新しい画面/メニュー項目を追加する手順

### 6.1 既存画面への項目追加（例: ポーズメニューにボタンを足す）

1. 描画: 該当 `OnGUI` ブロックまたは `DrawXxx` に `DrawModernButton` を追加。カード高さ（例: ポーズ `480x440`、ボタン `bh=46, bGap=12`）が収まるか計算し、必要ならカードを拡大。
2. フォーカス: `xxxFocus` の項目数（`% 5` 等）を更新。
3. 入力: `UpdateXxxInput` に対応するキー/パッド処理を追加（`nav.confirm` 時の `xxxFocus == n` 分岐、ショートカットキー）。
4. 表記: `shortcut` 文字列、ガイド文、サブタイトルを更新。他の処理と**キーが衝突していないか**確認（ポーズでは O/Q/R/T が使用済み。タイトルでは W/A/S/D/Q/E/L/P/O/U/M/Tab/Space/Esc/Enter など）。
5. `Loc.T(en, ja)` で両言語。
6. README の操作表（§8）。
7. 検証（§9）。

### 6.2 新しい画面/状態の追加

- ゲーム状態は `enum State { Title, Intro, Countdown, Racing, Results }`（RaceManager.cs 65行）。メニュー類は新しい State ではなく、`showSettings` / `paused` / `netMenu` / `showQuitConfirm` / `updater.ShowModal` のようなフラグ＋`xxxFocus` で実装されている既存パターンに合わせるのが最小変更。
1. フラグ（`bool showFoo;`）と `fooFocus`、必要なら `Close` 関数（`CloseSettings` のように入力待ち状態・`GUIUtility.keyboardControl`・保存をまとめて片付ける）を `RaceManager.cs` に追加。
2. 描画関数 `DrawFoo(float w, float h)` を UI.cs か Online.cs に作り、`OnGUI` で `if (showFoo) { GUI.enabled = ...; DrawFoo(w, h); }` を、Settings/UpdateModal/QuitModal と同じ層構造で追加（前面ほど後ろに書く）。`overlayModalOpen` にも加える。
3. 入力関数 `UpdateFooInput(in MenuNav nav)` を作り、`Update()` の優先順位リストの適切な位置に `if (showFoo) { UpdateFooInput(nav); return; }` を追加。`Esc`/B で閉じる（`nav.cancel`）、`Audio.Tick()`（移動）/`Audio.Select()`（決定）を鳴らす（既存どおり `if (Audio != null)`）。
4. 他画面との整合:
   - ポーズ中(`paused`)に開く場合: `Time.timeScale = 0` なので `Time.unscaledTime` を使う（`DrawFocusHighlight` は `unscaledTime`）。`Update()` は `paused` 時に `UpdatePausedInput` のみ走る点に注意。
   - 設定を開いたまま他モーダルが出る場合の `GUI.enabled` の連鎖（`GUI.enabled = !updateModalOpen && !showQuitConfirm` の書き方）を踏襲。
   - `-shot-ui` 用の状態リセット（RaceManager.Verify.cs の `RunShotUiCoroutine` 冒頭で `showQuitConfirm/showSettings/paused/netMenu/updater.ShowModal` を false にしている）に、**新フラグを追加**し、撮影ターゲット（`case "foo":`）も追加する。
5. オンライン関連はロビー(`LobbyActive`)・`Net.Busy`（接続中はショートカット無効）・ホスト/クライアント差（`Net.IsHost`、`canRestart = !Online || Net.IsHost`）を考慮。同期設計は `.agents/skills/unity-multiplayer-patterns/SKILL.md` 参照。

## 7. 2P画面分割 HUD の注意（RaceManager.HUD.cs）

- `DrawHuds` は 2P 時、幅 `hw = w * 0.5f` の領域ごとに `GUI.BeginClip(new Rect(p * hw, 0, hw, h)) ... GUI.EndClip()` で囲み、**クリップ内はローカル座標で左上が (0,0)、幅は `hw`**。`DrawHud(kart, hw, h, true, p)` のように幅に `hw` を渡す。中央に黒い 4px の境界線を引く。
- `DrawHud(Kart pl, float w, float h, bool compact, int pi)` の `compact` が 2P 用レイアウトフラグ、`pi` がプレイヤー番号。要素ごとに `compact ? 小さい値 : 通常値` を切り替えている（タイム枠 `232x95` vs `260x100`、順位、スピード、ミニマップ `160` vs `230`、フォントサイズなど）。**新しい HUD 要素は必ず `compact` 分岐を用意し、半幅（約 640）に収まるか確認**。
- 右寄せは `w - 値`、下寄せは `h - 値` を使う（この `w` はクリップ後の半幅）。1P/2P で同じ式が通用する。
- 1P のときだけ全画面効果（`DrawVignette`, `DrawSpeedLines`）を `OnGUI` で描く（`state == Racing && !paused && !TwoPlayer`）。2P はクリップ内で `DrawVignette` を呼ぶ。
- 2P 識別バッジは `pi == 0` オレンジ、`pi == 1` 水色。
- 画面全体フラッシュ(`screenFlashTimer`)はクリップ外で全画面に描く。
- バナー/カウントダウンは `bannerTimes[p]` / `bannerTexts[p]` / `bannerColors[p]` が プレイヤーごと。フォントサイズは 2P で小さくしている（`TwoPlayer ? 54 : 72`）。
- HUD も `Outlined(...)`（黒縁取り文字）でレース背景に埋もれないようにするのが既存の書き方。

## 8. デザイン規約と README

- 世界観は「明るくポップで親しみやすいクリーンなトイ・アーケード調」。メニュー/ポーズ/設定/結果/オンラインは**白ベース角丸カード(`DrawPopCard`) ＋ 上部のカラフルなリボン(`DrawPopRibbon`) ＋ 立体ポップボタン(`DrawModernButton`)** で統一する。**暗いSF/サイバー調の背景は新規に使わない**（AGENTS.md §4）。
- ボタンは発色の良いアクセント色（既存: 緑 `0.18,0.78,0.42` / オレンジ `1,0.62,0.15` / 青 `0.18,0.62,0.95` / 紫 `0.65,0.35,0.92` / 赤 `0.95,0.32,0.32`）。主要アクションは `isPrimary = true`。
- 注意（現状コードの事実）: ロビー(`DrawLobby`)の上部バー/下部パネルは濃紺半透明の帯(`0.04,0.07,0.18,0.88`)、設定のタブバー/ガイドボックスも濃紺系の `RoundedPanel` を使っている既存箇所がある。新規画面はこれらをコピーせず、ホワイトカード調を基準にする。既存箇所を直す場合は別途判断。
- ステータス系は `DrawToonStatGauge`、バッジ/ラベルは `DrawPopPill`。順位メダル色（1位金 `0.96,0.72,0.08` / 2位銀 / 3位銅）は結果画面の実例。
- READMEの更新義務（AGENTS.md §6-3）: キーバインド変更・新機能追加時は `README.md` の「⌨️ 操作方法 (Controls)」節（`### メニュー操作` の箇条書きと `### レース中操作` の表、キーコンフィグ説明の注記）を更新する。オンライン手順（`[O]` キーの説明など）が変わるときは該当節も。
- `.agents/skills` への知識追記（AGENTS.md §6-4）: 新しい共通部品やフォーカス番号の変更をしたら、この SKILL.md も更新する。

## 9. 検証手順（build-and-verify スキルと整合）

コード変更後は `-Build` 付きで UI だけ高速撮影する（詳細は `.agents/skills/build-and-verify/SKILL.md`）。

```powershell
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -Build -ShotUi "title,results"
# ビルド済みで撮影だけやり直すとき
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -ShotUi "paused,settings,keys,netmenu,lobby4p,hud2p"
```

- 対応ターゲット（`RaceManager.Verify.cs` の `switch` で確認）: `title`, `title2p`, `settings`(`settings_general`), `keys`(`settings_keys`), `updates`(`settings_updates`), `updatemodal`(`update_modal`), `updatedownloading`(`update_downloading`), `netmenu`, `lobby`/`lobby4p`, `lobby8p`, `intro`/`intro_campus`/`intro_grid`, `countdown`, `racing`/`hud`/`hud1p`, `hud2p`/`racing2p`, `paused`(`quit`/`quitmodal` も同じ case に含まれる), `results`。build-and-verify SKILL.md に載っているのは一部（`title, title2p, settings, keys, updates, netmenu, lobby4p, lobby8p, countdown, hud1p, hud2p, paused, results`）なので、上記の別名を使うときは Verify.cs の該当 case を確認すること。
- 出力: `Build/Windows/TurboCircuit_Data/verify_ui_sheet.png`（複数指定時のコンタクトシート）と `verify_ui_<target>.png`。まず `verify_ui_sheet.png` を Read で開き、細部が必要なものだけ個別画像を開く。
- 新画面を追加したら `RunShotUiCoroutine` に `case` を追加（状態をそのターゲットに持っていくコード）し、冒頭の状態リセットに新フラグを足してから撮影する。
- 目視チェックリスト:
  1. 文字被り・はみ出し（特に日本語表示。`L` キーまたは設定で言語を切り替えて両言語で撮る）
  2. ボタン内ラベルと右側 `[ショートカット]` 表記の衝突（幅が狭いボタンは `shortcut` を空にしているものが多い）
  3. カード/リボン/ボタンが白カード調で統一され、暗い背景になっていないか
  4. フォーカス枠（`DrawFocusHighlight`）が対象を囲んでいるか
  5. 2P HUD（`hud2p`）で半幅に収まり、要素が中央境界をまたいでいないか
  6. 1P HUD（`hud1p`）が変わっていないか
  7. 結果画面は8台時と少数台で下部の空きの使い方が変わる（`btnY - tableBottom >= 66f` のとき統計ボックスを出す）ので行数が変わる変更では確認
- 広範囲の変更・リリース前は `-AutoShot`（全画面回帰、約25〜30秒）。
- 画像を見られない/ビルドできない場合は「未検証」と明記して報告する。

## 10. 実装チェックリスト

- [ ] `new GUIStyle()` を使わず `St(...)` のみ
- [ ] 座標は 720 基準の論理座標。`w` 可変を考慮し、はみ出しは `Clamp`
- [ ] 文字列は `Loc.T(en, ja)`、動的文字列の毎フレーム生成を避ける
- [ ] 表示ショートカットと `Update` 側の実処理が一致、他キーと衝突なし
- [ ] フォーカス数 `% N`・閉じるボタン番号が描画側と入力側で一致
- [ ] モーダル追加時は `Update()` 優先順位 / `overlayModalOpen` / `GUI.enabled` / `-shot-ui` のリセットを揃えた
- [ ] 2P HUD は `compact` 分岐あり
- [ ] `GUI.color` / `GUI.matrix` / `GUI.enabled` を元に戻した
- [ ] README 操作表を更新
- [ ] `verify.ps1 -Build -ShotUi ...` で目視確認（日本語/英語）
