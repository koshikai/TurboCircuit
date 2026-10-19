---
name: urp-graphics-optimization
description: >-
  Use this skill when modifying materials, shaders, procedural meshes, or rendering performance
  under Universal Render Pipeline (URP) (e.g., Track.Visuals.cs, Kart.Visual.cs, procedural textures, draw calls).
---

# URP グラフィックス＆描画最適化ガイド (urp-graphics-optimization)

Turbo Circuit (Unity 6 / URP) におけるシェーダー、マテリアル、プロシージャルメッシュ生成、およびレンダリング負荷最適化の指針です。

---

## 1. ドローコール削減とバッチング（SRP Batcher / GPU Instancing）

* **SRP Batcher の維持**:
  * URP の標準シェーダー（Universal Render Pipeline/Lit または Simple Lit）を使用し、シェーダープロパティは定数バッファ（`cbuffer UnityPerMaterial`）に宣言されたものを使用する。
  * **厳禁**: `renderer.material` をランタイムで頻繁に参照・変更すること。マテリアルのインスタンス（クローン）が生成され、メモリリークと SRP Batcher / GPU Instancing の破壊を引き起こす。
* **マテリアルプロパティの動的変更 (`MaterialPropertyBlock`)**:
  * カートごとのカラー変更や点滅エフェクトなど、オブジェクト個別のプロパティ変更には必ず `MaterialPropertyBlock` を使用する。
  ```csharp
  // キャッシュしたブロックを使い回す
  private MaterialPropertyBlock _propBlock;
  if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

  _propBlock.SetColor("_BaseColor", teamColor);
  renderer.SetPropertyBlock(_propBlock);
  ```

---

## 2. プロシージャルメッシュと動的リソース管理

* **メッシュの安全な破棄**:
  * `Track.Visuals.cs` などでプロシージャル生成されたメッシュ（`MeshFilter.mesh`）は、再生成時やシーン破棄時にガベージコレクタ（GC）で自動解放されない（ネイティブメモリに残る）。
  * 古いメッシュは明示的に `Destroy(mesh)`（エディタ実行時は `SafeDestroy` または `DestroyImmediate`）を呼んでリークを防ぐこと。
* **頂点バッファとインデックスの最適化**:
  * 頂点データ構造（Position, Normal, Tangent, UV）は不要なチャネルを含めない。
  * 頂点数が 65,535 以下の場合は `mesh.indexFormat = IndexFormat.UInt16` を維持し、メモリ帯域を節約する。

---

## 3. オーバードローと半透明（Transparency）の抑制

* **アルファクリッピング (Alpha Cutout) vs 半透明 (Alpha Blending)**:
  * 樹木の葉や金網フェンスなどの装飾は、半透明ブレンドではなく可能な限りアルファクリッピング（Cutoff）を使用する。
  * 半透明ブレンドは前後ソートの計算負荷と重なりによる過度なオーバードロー（GPU Fill-rate 逼迫）を招く。
* **画面分割 2P 時の負荷考慮**:
  * Turbo Circuit は画面分割 2P 対戦（カメラ 2 台）に対応しているため、カメラごとにカリングと描画パスが走る。
  * パーティクルエフェクト（ドリフト火花、煙）の最大粒子数（Max Particles）は抑えめに設計する。

---

## 4. UI とテクスチャメモリ

* **IMGUI (`OnGUI`) の GC 削減**:
  * `RaceManager.UI.cs` のように、毎フレームの `new GUIStyle()` や文字列結合（`$"{time}..."`）は GC Alloc の主因となる。
  * スタイルは `St(...)` キャッシュ辞書を利用し、文字列はフォーマット済みバッファを意識する。
* **プロシージャルテクスチャのキャッシュ**:
  * `TextureGen.cs` で生成した角丸カードやグラデーションテクスチャは、Dictionary 等にキャッシュし、同一解像度・スタイルのテクスチャを二重生成しない。
