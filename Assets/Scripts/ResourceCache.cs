using System;
using System.Collections.Generic;
using UnityEngine;

// Resources.Load の結果を静的辞書にキャッシュし、レース中のディスク/アセット検索を防ぐ軽量キャッシュ
public static class ResourceCache
{
    static readonly Dictionary<(Type, string), UnityEngine.Object> s_cache = new Dictionary<(Type, string), UnityEngine.Object>();
    static bool s_preloaded;

    public static T Load<T>(string path) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(path)) return null;
        var key = (typeof(T), path);
        if (s_cache.TryGetValue(key, out var cached))
        {
            if (object.ReferenceEquals(cached, null)) return null;
            if (cached != null) return cached as T;
            s_cache.Remove(key);
        }
        var loaded = Resources.Load<T>(path);
        s_cache[key] = loaded;
        return loaded;
    }

    public static void Register<T>(string path, T asset) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(path) || asset == null) return;
        s_cache[(typeof(T), path)] = asset;
    }

    public static void PreloadRaceAssets()
    {
        if (s_preloaded) return;
        s_preloaded = true;

        // オーディオ
        Load<AudioClip>("Audio/sfx_missile");
        Load<AudioClip>("Audio/sfx_engine");

        // カート・ホイール
        Load<GameObject>("Karts/kart-oobi");
        Load<GameObject>("Karts/kart-oodi");
        Load<GameObject>("Karts/kart-ooli");
        Load<GameObject>("Karts/kart-oopi");
        Load<GameObject>("Karts/kart-oozi");
        Load<GameObject>("Karts/wheel-racing");
        Load<GameObject>("Karts/wheel-default");

        // 動物レーサー・コース動物
        string[] animals =
        {
            "bunny", "penguin", "polar", "deer", "fox", "lion",
            "cow", "cat", "dog", "crab", "chick", "parrot"
        };
        for (int i = 0; i < animals.Length; i++)
        {
            Load<GameObject>("Animals/animal-" + animals[i]);
        }

        // 共通プロップ・テクスチャ
        Load<Texture2D>("Props/Textures/checkers");
        Load<GameObject>("Props/lightPostLarge");
        Load<GameObject>("Props/cone");
        Load<GameObject>("Props/flagCheckers");
        Load<GameObject>("Props/tent");
    }

    public static void Clear()
    {
        s_cache.Clear();
        s_preloaded = false;
    }
}
