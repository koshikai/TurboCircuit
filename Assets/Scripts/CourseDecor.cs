using UnityEngine;

// コース装飾（木・岩・看板・建物など）の手動調整データの要素。
// 自動生成された装飾物を「位置/回転/スケールの上書き」「削除」「複製」として保存し、
// 実行時の Track.BuildVisuals が生成直後に適用する（Track.Decor.cs / CourseDecorData.cs）。
// 編集は Unity メニュー Turbo Circuit > Course Decor Editor から行う。
[System.Serializable]
public struct DecorEntry
{
    public string key;          // 生成時の「名前@位置」で作る安定キー
    public bool deleted;
    public Vector3 position;    // ワールド座標
    public Quaternion rotation;
    public Vector3 scale;
}

[System.Serializable]
public struct DecorClone
{
    public string sourceKey;    // 複製元のキー
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;
}
