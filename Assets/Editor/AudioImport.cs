using UnityEditor;
using UnityEngine;

// BGM は長いのでストリーミング再生にして、メモリ消費とロード時間を抑える
public class AudioImport : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/Resources/Audio/")) return;
        var importer = (AudioImporter)assetImporter;
        var settings = importer.defaultSampleSettings;
        if (System.IO.Path.GetFileName(assetPath).StartsWith("bgm_"))
        {
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
        }
        else
        {
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
        }
        importer.defaultSampleSettings = settings;
        importer.forceToMono = false;
    }
}
