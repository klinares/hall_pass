using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WorldTour.World.Audio;

/// <summary>
/// Phase 8.3: import settings for the clips in Assets/World/Audio/Clips (the five CC0 Kenney cues; outside Resources on purpose, see WorldAudioClips). Everything is mono and
/// compressed Vorbis for the web player; these are all short, so they are decoded on load (no delay on a click).
/// </summary>
public class WorldAudioImport : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        if (!assetPath.Replace((char)92, '/').Contains("/World/Audio/Clips/")) return;
        var imp = (AudioImporter)assetImporter;
        imp.forceToMono = true;
        imp.loadInBackground = false;
        foreach (string platform in new[] { "WebGL", "Standalone" })
        {
            var s = imp.GetOverrideSampleSettings(platform);
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.5f;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            imp.SetOverrideSampleSettings(platform, s);
        }
        var d = imp.defaultSampleSettings;
        d.loadType = AudioClipLoadType.DecompressOnLoad;
        d.compressionFormat = AudioCompressionFormat.Vorbis;
        d.quality = 0.5f;
        imp.defaultSampleSettings = d;
    }
}

/// <summary>Makes the WorldAudioBank scene (Assets/World/Scenes) that holds the cue clips. Menu: World Tour/Make world audio bank scene; batch: -executeMethod WorldAudioBankBuilder.Run.</summary>
public static class WorldAudioBankBuilder
{
    public const string ScenePath = "Assets/World/Scenes/WorldAudioBank.unity";

    [MenuItem("World Tour/Make world audio bank scene")]
    public static void Run()
    {
        try
        {
            Make();
            Debug.Log("WorldAudioBankBuilder: done");
        }
        catch (System.Exception e)
        {
            Debug.LogError("WorldAudioBankBuilder failed: " + e);
        }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static AudioClip Load(string name)
    {
        var c = AssetDatabase.LoadAssetAtPath<AudioClip>(WorldAudio.ClipDir + name + ".ogg");
        if (c == null) throw new System.Exception("missing clip " + name);
        return c;
    }

    public static void Make()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var go = new GameObject("WorldAudioClips");
        var b = go.AddComponent<WorldAudioClips>();
        b.open = Load("ui_open"); b.close = Load("ui_close"); b.whoosh = Load("ui_whoosh");
        b.done = Load("jingle_done"); b.unlock = Load("jingle_unlock");
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);
    }
}
