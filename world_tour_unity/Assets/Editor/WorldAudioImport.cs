using UnityEditor;
using UnityEngine;

/// <summary>
/// Phase 8.3: import settings for the clips in Assets/Resources/WorldAudio. Everything is mono (the beds, the steps and the cues have no use for stereo) and
/// compressed Vorbis for the web player; short clips are decoded on load (no delay on a footstep), the long loops stay compressed in memory.
/// </summary>
public class WorldAudioImport : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        if (!assetPath.Replace((char)92, '/').Contains("/Resources/WorldAudio/")) return;
        var imp = (AudioImporter)assetImporter;
        imp.forceToMono = true;
        imp.loadInBackground = false;
        bool loop = assetPath.Contains("amb_");
        foreach (string platform in new[] { "WebGL", "Standalone" })
        {
            var s = imp.GetOverrideSampleSettings(platform);
            s.loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.5f;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            imp.SetOverrideSampleSettings(platform, s);
        }
        var d = imp.defaultSampleSettings;
        d.loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
        d.compressionFormat = AudioCompressionFormat.Vorbis;
        d.quality = 0.5f;
        imp.defaultSampleSettings = d;
    }
}
