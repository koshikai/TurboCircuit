---
name: unity-multiplayer-patterns
description: >-
  Use this skill when implementing or modifying networked multiplayer features, Netcode for GameObjects (NGO),
  Unity Relay/Lobby integration, or kart state synchronization (e.g., NetSession.cs, Kart.Net.cs, RaceManager.Online.cs).
---

# Unity マルチプレイヤー＆ネットワーク設計ガイド (unity-multiplayer-patterns)

Turbo Circuit における Unity Netcode for GameObjects (NGO) および Unity Relay を利用したマルチプレイヤー機能（`NetSession.cs`、`Kart.Net.cs`、`RaceManager.Online.cs`）の実装・保守指針です。

---

## 1. 状態同期 (`NetworkVariable`) vs イベント (`RPC`) の使い分け

* **`NetworkVariable<T>` (永続的な状態同期)**:
  * 途中参加したプレイヤー（Late Joiner）にも同期されるべき「現在の状態」（ラップ数、レース順位、スコア、プレイヤーの準備状態）に使用する。
  * **厳禁**: `Update()` 内で値が変わっていないのに毎フレーム `NetworkVariable.Value = ...` を代入すること。無駄なパケット送信と帯域圧迫の原因になる。
* **`Rpc` (一過性のイベント通知)**:
  * 保持する必要のない「瞬間的なイベント」（アイテム発射音、被弾エフェクト、ゴール通過通知）に使用する。
  * `[ServerRpc]`: クライアントからホスト/サーバーへ要求を送る。必ずサーバー側で入力値の妥当性検証（バリデーション）を行うこと。
  * `[ClientRpc]`: サーバーから全クライアント（または特定クライアント）へ確定結果を通知する。

---

## 2. カート物理と補間同期 (Interpolation & Extrapolation)

レースゲームにおける高速移動体のネットワーク同期は、過度なサーバー権限による入力遅延と、過度なクライアント信頼による同期ズレのバランスが重要です。

* **位置・回転の同期**:
  * 毎フレーム Transform をそのまま送るのではなく、送信レート（例: 20Hz〜30Hz）を制限する。
  * 受信側は受信した直近のスナップショット間を線形補間（`Vector3.Lerp`）やスプライン補間し、カクつき（Jitter）を吸収する。
  * レイテンシが大きい場合は、前回の速度ベクトル（Velocity）を加味したデッドレコニング（外挿補正）を組み合わせる。
* **物理演算の二重適用防止**:
  * リモートのカート（非ローカルプレイヤー）は Rigidbody や物理コントローラーの駆動を無効化し、受信した位置・姿勢の補間反映に専念させる。

---

## 3. データシリアライズと帯域削減

* **プリミティブの軽量化**:
  * 単純な状態フラグは `bool` を並べるのではなく、ビットマスク（`byte` / `enum Flags`）にまとめる。
  * 浮動小数点数（Float）は、範囲が限定されている場合（0〜1 のスロットル量など）、固定小数点や `byte` / `short`（量子化）への変換を検討する。
* **`INetworkSerializable` の実装**:
  * 複数プロパティをまとめるパケット構造体には `INetworkSerializable` を実装し、必要な最小限のバイト列のみを `serializer.SerializeValue(...)` する。

---

## 4. Unity Relay & セッションライフサイクル

* **安全な切断とクリーンアップ**:
  * ホスト切断時（Host Migration がない場合）やクライアント切断時は、残存オブジェクトの二重破棄や NullReferenceException を防ぐため、明示的なクリーンアップシーケンスを実行すること。
  * `NetworkManager.Singleton.Shutdown()` を呼び、購読したイベントハンドラ（`OnClientConnectedCallback` 等）は確実に解除する。
* **ルームコードと NAT 越え**:
  * 直接 IP 接続（Direct IP）と Unity Relay（Join Code による NAT 越え）の両対応を維持する。
  * Relay の Join Code は 6 桁の英数字。UI 上でコピペしやすく視認性の高いフォントで表示する。
