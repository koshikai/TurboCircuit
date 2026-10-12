## 概要 (Summary)
<!-- このPRで変更・追加した内容を簡潔に書いてください -->

## 関連 Issue (Related Issue)
<!-- バグ修正や対応するIssueがある場合は番号を記載してください（例: Closes #12） -->
Closes #

## 変更の種類 (Type of Change)
- [ ] 🏎️ カート挙動 / AI / アイテム (`Kart*.cs`, `Items.cs`)
- [ ] 🗺️ コース / 背景 / 装飾 (`Track*.cs`, `CourseDecor/Course_XX.asset`)
- [ ] 🎨 UI / HUD / カメラ (`RaceManager.UI.cs`, `RaceManager.HUD.cs`, `RaceManager.Camera.cs`)
- [ ] 🌐 オンライン / ネットワーク (`NetSession.cs`, `RaceManager.Online.cs`, `Kart.Net.cs`)
- [ ] 🔊 サウンド / アセット追加 (`RaceAudio.cs`, `Assets/Resources/`)
- [ ] 📝 ドキュメント / ツール / その他

## スクリーンショット・検証結果 (Verification)
<!-- UI変更・モデル変更・コース変更がある場合は verify.ps1 で撮影した画像を貼ってください -->
<!-- 例: pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -Build -ShotUi "title,results" -->


## チェックリスト (Checklist)
- [ ] ローカルでビルドが通り、エラーがないことを確認した（`verify.ps1 -Build` または Unity Editor）
- [ ] アセットやスクリプトを追加・削除した場合、対応する `.meta` ファイルも一緒にコミットしている
- [ ] `Assets/Resources/Data/` 直下の `.asset`（`KartData` / `KartMachineData` / `CourseData`）を手編集していない（※コード側の既定値で変更すること。`CourseDecor/Course_XX.asset` はOK）
- [ ] `OnGUI` 内で毎フレーム `new GUIStyle()` を生成せず、`St(...)` キャッシュを使用している
- [ ] 新機能や操作キーの変更がある場合、`README.md` もあわせて更新した
