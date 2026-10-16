using System;
using UnityEngine;

// アイテムのメタデータ（アイコン、名称、アクセントカラー、順位別出現ウェイト）を一元管理するマスター定義
public class ItemDef
{
    public ItemType Type;
    public string NameEn;
    public string NameJp;
    public Color AccentColor;
    public string IconResource;
    public Texture2D Icon;

    // 順位比率 p (0.0 = 1位, 1.0 = 最下位) から出現確率ウェイトを計算
    public Func<float, float> WeightFunc;

    public string LocalizedName => Loc.T(NameEn, NameJp);
}
