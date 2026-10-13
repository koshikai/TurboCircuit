using UnityEngine;

// 日英ローカライズ管理クラス（UI・HUD・コース・キャラクター・マシンの多言語テキスト定義）
public static class Loc
{
    public enum Lang
    {
        Ja = 0,
        En = 1
    }

    public static Lang Current { get; private set; } = Lang.Ja;
    public static bool IsJa => Current == Lang.Ja;

    static bool initialized;

    public static void Init(string[] args = null)
    {
        if (initialized && args == null) return;
        initialized = true;

        int saved = PlayerPrefs.GetInt("tc_lang", -1);
        if (saved == 0 || saved == 1)
        {
            Current = (Lang)saved;
        }
        else
        {
            Current = Lang.Ja;
        }

        if (args != null)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-lang" && i + 1 < args.Length)
                {
                    string v = args[i + 1].Trim().ToLowerInvariant();
                    if (v == "en" || v == "english") Current = Lang.En;
                    else if (v == "ja" || v == "jp" || v == "japanese") Current = Lang.Ja;
                }
                else if (args[i] == "-en")
                {
                    Current = Lang.En;
                }
                else if (args[i] == "-ja" || args[i] == "-jp")
                {
                    Current = Lang.Ja;
                }
            }
        }
    }

    public static void Set(Lang lang)
    {
        Current = lang;
        PlayerPrefs.SetInt("tc_lang", (int)lang);
        PlayerPrefs.Save();
    }

    public static void Toggle()
    {
        Set(Current == Lang.Ja ? Lang.En : Lang.Ja);
    }

    public static string T(string en, string ja) => Current == Lang.Ja ? ja : en;

    static readonly string[] CourseNamesJa =
    {
        "ターボ・サーキット",
        "サンセット・デューンズ",
        "フロスト・ピーク",
        "ネオン・メトロポリス",
        "北大札幌キャンパス",
        "スカイライン・オーバーパス"
    };

    static readonly string[] CourseDescsJa =
    {
        "立体交差ブリッジと高速コーナー、スタジアムの大ジャンプ台を備えた王道グランプリサーキット。",
        "夕日に染まる巨大砂丘を一気に駆け上がり、古代ピラミッド遺跡とオアシスへ急降下する砂漠コース。",
        "標高差35mの白銀アルペン雪山コース！連続ヘアピンと氷河スロープ、断崖クレバスの大ジャンプが待ち受ける。",
        "ネオン輝く黄昏のサイバー都市！摩天楼の谷間や高架ハイウェイ、ビル群を貫通する近未来トンネルを疾走せよ。",
        "北海道大学札幌キャンパスを完全再現！メインストリート、クラーク像、黄金のイチョウ並木、第2農場モデルバーン、ポプラ並木を巡る特別コース。",
        "海岸ベイエリアを舞台にした8の字立体交差ハイウェイ！下層アンダーパスと上空15mの巨大斜張橋が交差するスリリングなコース。"
    };

    static readonly string[] CourseDiffEn =
    {
        "★☆☆  NOVICE",
        "★★☆  ADVANCED",
        "★★★  EXPERT",
        "★★☆  URBAN",
        "★★☆  CAMPUS",
        "★★★  OVERPASS"
    };

    static readonly string[] CourseDiffJa =
    {
        "★☆☆  初級・王道",
        "★★☆  中級・砂漠",
        "★★★  上級・雪山",
        "★★☆  中級・都市",
        "★★☆  中級・北大",
        "★★★  上級・高架橋"
    };

    static readonly string[] CourseShortDiffEn =
    {
        "★☆☆ NOVICE",
        "★★☆ ADVANCED",
        "★★★ EXPERT",
        "★★☆ URBAN",
        "★★☆ CAMPUS",
        "★★★ OVERPASS"
    };

    static readonly string[] CourseShortDiffJa =
    {
        "★☆☆ 初級",
        "★★☆ 中級",
        "★★★ 上級",
        "★★☆ 都市",
        "★★☆ 北大",
        "★★★ 高架橋"
    };

    static readonly string[] DriverNamesJa =
    {
        "ウービィ",
        "ウーディ",
        "ウーリィ",
        "ウーピィ",
        "ウージィ",
        "パンダ",
        "ペンギン",
        "フォックス",
        "バニー",
        "ライオン"
    };

    static readonly string[] DriverSubsJa =
    {
        "エイリアンエース  •  万能オールラウンダー",
        "ピンクダッシュ  •  高加速＆ターボドリフト",
        "エアロソニック  •  最高速特化タイプ",
        "レーサースウィフト  •  シャープ旋回タイプ",
        "クールタイタン  •  重量級・高グリップ",
        "バンブーボス  •  パワー重量級",
        "アイススライダー  •  滑らかドリフター",
        "ニトロキツネ  •  俊敏スピードスター",
        "ホッパースター  •  超軽量ロケット加速",
        "ゴールデンキング  •  百獣の王・最高速"
    };

    static readonly string[] KartNamesJa =
    {
        "スタンダード",
        "パイプバギー",
        "マッハフォーミュラ",
        "モンスター",
        "サイバーGT",
        "ロードスター",
        "マッスルV8",
        "ジェットポッド"
    };

    static readonly string[] KartTraitsJa =
    {
        "バランス型オールラウンダー",
        "軽量・ロケット加速",
        "超高速エアロウイング",
        "重戦車オフロードタイヤ",
        "ネオンドリフトマシン",
        "クラシックスムーズ旋回",
        "V8スーパーチャージャー",
        "ツインジェットタービン"
    };

    static readonly string[] AiNamesJa =
    {
        "ブレイズ",
        "ノヴァ",
        "レックス",
        "キキ",
        "ボルト",
        "モチ",
        "タロウ"
    };

    public static string CourseName(int idx, string fallback)
    {
        if (IsJa && idx >= 0 && idx < CourseNamesJa.Length) return CourseNamesJa[idx];
        return fallback != null ? fallback.ToUpperInvariant() : "";
    }

    public static string CourseDesc(int idx, string fallback)
    {
        if (IsJa && idx >= 0 && idx < CourseDescsJa.Length) return CourseDescsJa[idx];
        return fallback ?? "";
    }

    public static string CourseDifficulty(int idx)
    {
        var arr = IsJa ? CourseDiffJa : CourseDiffEn;
        return (idx >= 0 && idx < arr.Length) ? arr[idx] : arr[0];
    }

    public static string CourseShortDifficulty(int idx)
    {
        var arr = IsJa ? CourseShortDiffJa : CourseShortDiffEn;
        return (idx >= 0 && idx < arr.Length) ? arr[idx] : arr[0];
    }

    public static string DriverName(int idx, string fallback)
    {
        if (IsJa && idx >= 0 && idx < DriverNamesJa.Length) return DriverNamesJa[idx];
        return fallback ?? "";
    }

    public static string DriverSub(int idx, string enDriver, string enTrait)
    {
        if (IsJa && idx >= 0 && idx < DriverSubsJa.Length) return DriverSubsJa[idx];
        return $"{enDriver}  •  {enTrait}";
    }

    public static string KartName(int idx, string fallback)
    {
        if (IsJa && idx >= 0 && idx < KartNamesJa.Length) return KartNamesJa[idx];
        return fallback ?? "";
    }

    public static string KartSub(int idx, string code, string enTrait)
    {
        if (IsJa && idx >= 0 && idx < KartTraitsJa.Length) return $"{code}  •  {KartTraitsJa[idx]}";
        return $"{code}  •  {enTrait}";
    }

    public static string AiName(int idx, string fallback)
    {
        if (IsJa && idx >= 0 && idx < AiNamesJa.Length) return AiNamesJa[idx];
        return fallback ?? "CPU";
    }

    static readonly string[] OrdinalsEn = { "", "1ST", "2ND", "3RD", "4TH", "5TH", "6TH", "7TH", "8TH" };
    public static string PlaceText(int n)
    {
        if (IsJa) return $"{n}位";
        return (n >= 1 && n <= 8) ? OrdinalsEn[n] : (n + "TH");
    }
}
