using UnityEngine;

// エディタ編集用プレビューにだけ付くタグ。生成時の元の姿勢を保持して差分検出に使う。
public class DecorTag : MonoBehaviour
{
    public string key;
    public Vector3 origPosition;
    public Quaternion origRotation;
    public Vector3 origScale;
}
