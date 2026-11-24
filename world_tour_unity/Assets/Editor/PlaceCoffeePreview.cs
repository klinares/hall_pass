// Phase 8.1 preview renders of Place_Coffee and its characters straight from the Editor (needs a GPU, so no -nographics).
// Writes PNGs and leaves the project settings as they were (QualitySettings and GraphicsSettings are restored byte for byte).
//
//   Unity.exe -batchmode -projectPath world_tour_unity -executeMethod PlaceCoffeePreview.Run -previewOut <dir> [-previewSize 1366x768] [-previewShots a,b,c]
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WorldTour.World;

public static class PlaceCoffeePreview
{
    static string Arg(string name)
    {
        var a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return null;
    }

    public static void Run()
    {
        string quality = "ProjectSettings/QualitySettings.asset", graphics = "ProjectSettings/GraphicsSettings.asset";
        RenderPipelineAsset oldQ = null, oldG = null; int oldAA = 0;
        byte[] qb = File.ReadAllBytes(quality), gb = File.ReadAllBytes(graphics);
        try
        {
            string outDir = Arg("-previewOut") ?? "Temp/preview";
            Directory.CreateDirectory(outDir);
            var size = (Arg("-previewSize") ?? "1366x768").Split('x').Select(int.Parse).ToArray();
            var only = Arg("-previewShots");
            var want = only == null ? null : only.Split(',');
            oldAA = QualitySettings.antiAliasing; oldQ = QualitySettings.renderPipeline; oldG = GraphicsSettings.defaultRenderPipeline;
            var pipe = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(WorldSpikeBuilder.PipelinePath);
            QualitySettings.renderPipeline = pipe;
            GraphicsSettings.defaultRenderPipeline = pipe;
            EditorSceneManager.OpenScene(PlaceCoffeeBuilder.ScenePath, OpenSceneMode.Single);

            var cam = Camera.main;
            var follow = cam.GetComponent<FollowCamera>();
            var rt = new RenderTexture(size[0], size[1], 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            foreach (var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) ps.Simulate(9f, true, true);
            PoseEveryone();
            // the crowd the gameplay would add at run time (36 figures), so the stage shots show it
            var crowdGo = new GameObject("CrowdPreview");
            var crowdField = crowdGo.AddComponent<WorldTour.World.Gameplay.CrowdField>();
            var crowdArea = GameObject.Find("CrowdArea").transform;
            crowdField.Setup(crowdArea, GameObject.Find("StageSpot").transform.position, WorldTour.World.Gameplay.WorldGameplayAssets.Load(), 7);
            crowdField.SetFans(int.Parse(Arg("-previewFans") ?? "3000"));
            foreach (Transform c in crowdGo.transform) c.localScale = Vector3.one * 0.85f;

            void Shot(string name, Vector3 pos, Quaternion rot, float fov = 44f)
            {
                if (want != null && !want.Contains(name)) return;
                cam.transform.SetPositionAndRotation(pos, rot);
                cam.fieldOfView = fov;
                foreach (var mb in UnityEngine.Object.FindObjectsByType<MarkerBob>(FindObjectsSortMode.None)) mb.transform.rotation = cam.transform.rotation * Quaternion.Euler(0, 180, 0);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var t = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                t.Apply();
                File.WriteAllBytes(Path.Combine(outDir, name + ".png"), t.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(t);
                RenderTexture.active = null;
                cam.targetTexture = null;
            }

            Vector3 off = follow != null ? follow.offset : new Vector3(0, 6.6f, -11.4f);
            Quaternion look = follow != null ? follow.FollowRotation : Quaternion.LookRotation(-off, Vector3.up);
            Shot("spawn", new Vector3(0, 0, -7.6f) + off, look);
            Shot("park_w", new Vector3(-7, 0, -9.5f) + off, look);
            Shot("park_e", new Vector3(7, 0, -9.5f) + off, look);
            Shot("shop", new Vector3(-6, 0, 3) + off, look);
            Shot("shop2", new Vector3(-6, 0, -1) + off, look);
            Shot("stage", new Vector3(9, 0, 5.5f) + off, look);
            Shot("stage_on", new Vector3(9, 0, 9.2f) + off, look);
            Shot("plaza", new Vector3(0, 0, 2) + off, look);
            Shot("crew3", new Vector3(3.0f, 0, 3.4f) + off, look);
            Shot("exit", new Vector3(11.5f, 0, -9.5f) + off, look);
            Shot("spawn_11", new Vector3(0, 0, -7.6f) + off, look);
            // the gameplay's stage camera (WorldGame.ComputeStageCamera) with her on the spot
            var plr = GameObject.Find("Player");
            if (plr != null)
            {
                Vector3 stageP = GameObject.Find("StageSpot").transform.position, crowdP = GameObject.Find("CrowdArea").transform.position;
                Vector3 dirS = crowdP - stageP; dirS.y = 0; dirS.Normalize();
                Vector3 camP = crowdP + dirS * 9f + Vector3.up * 6.5f;
                Vector3 keep = plr.transform.position; Quaternion keepR = plr.transform.rotation;
                plr.transform.position = new Vector3(stageP.x, 0, stageP.z);
                plr.transform.rotation = Quaternion.LookRotation(-dirS);
                var st = plr.GetComponentInChildren<StandOnHeight>();
                if (st != null) st.transform.localPosition = new Vector3(0, 0.5f, 0);
                Shot("stagecam", camP, Quaternion.LookRotation((stageP + Vector3.up * 1.2f) - camP, Vector3.up));
                plr.transform.position = keep; plr.transform.rotation = keepR;
                if (st != null) st.transform.localPosition = Vector3.zero;
            }
            // overview from high above the south edge
            Shot("overview", new Vector3(0, 30, -34), Quaternion.Euler(33, 0, 0), 40f);
            Shot("overview_n", new Vector3(0, 6, -16), Quaternion.Euler(12, 0, 0), 55f);
            // close-ups
            foreach (var n in new[] { "Crew_1", "Crew_2", "Crew_3" })
            {
                var g = GameObject.Find(n);
                if (g == null) continue;
                Vector3 p = g.transform.position + Vector3.up * 1.7f;
                Shot("close_" + n, p + new Vector3(0, 0.6f, -4.2f), Quaternion.LookRotation(-new Vector3(0, 0.6f, -4.2f)), 30f);
            }
            var pl = GameObject.Find("Player");
            if (pl != null) Shot("close_star", pl.transform.position + new Vector3(0, 1.5f, 4.2f), Quaternion.Euler(6, 180, 0), 30f);

            // line-up of every figure and pose
            Lineup(outDir, cam, rt, size, want);
            rt.Release();
        }
        catch (Exception e) { Debug.LogError("PlaceCoffeePreview failed: " + e); }
        finally
        {
            QualitySettings.renderPipeline = oldQ;
            QualitySettings.antiAliasing = oldAA;
            GraphicsSettings.defaultRenderPipeline = oldG;
            File.WriteAllBytes(quality, qb);
            File.WriteAllBytes(graphics, gb);
        }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static void PoseEveryone()
    {
        var idle = WorldArtBuilder.KayClip("Idle_A");
        foreach (var a in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
        {
            if (a.avatar != null && a.avatar.isHuman) idle.SampleAnimation(a.gameObject, 0.4f);
        }
    }

    static void Lineup(string outDir, Camera cam, RenderTexture rt, int[] size, string[] want)
    {
        if (want != null && !want.Contains("lineup") && !want.Contains("poses")) return;
        // a row of copies far from the street, one per pose
        var star = AssetDatabase.LoadAssetAtPath<GameObject>(WorldArtBuilder.StarPrefab);
        var crews = WorldArtBuilder.CrewPrefabs.Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p)).ToArray();
        var riff = AssetDatabase.LoadAssetAtPath<GameObject>(WorldArtBuilder.RiffPrefab);
        var root = new GameObject("Lineup");
        root.transform.position = new Vector3(0, 0, -60);
        var figs = new[] { star, crews[0], crews[1], crews[2] };
        for (int i = 0; i < figs.Length; i++)
        {
            var g = (GameObject)PrefabUtility.InstantiatePrefab(figs[i], root.transform);
            g.transform.localPosition = new Vector3(-3.6f + i * 2.4f, 0, 0);
            g.transform.localRotation = Quaternion.identity;
            WorldArtBuilder.KayClip("Idle_A").SampleAnimation(g, 0.4f);
        }
        var d = (GameObject)PrefabUtility.InstantiatePrefab(riff, root.transform);
        d.transform.localPosition = new Vector3(4.2f, 0, 0);
        d.transform.localRotation = Quaternion.Euler(0, 160f, 0);
        var dogClip = AssetDatabase.LoadAllAssetsAtPath("Assets/World/Art/Quaternius_Dog/Dog.fbx").OfType<AnimationClip>().First(c => c.name == "DogArmature|Idle");
        dogClip.SampleAnimation(d.GetComponentInChildren<Animator>().gameObject, 0.3f);
        void Shot(string name, float fov = 24f, Vector3? pos = null, Quaternion? rot = null)
        {
            if (want != null && !want.Contains("lineup") && !want.Contains(name)) return;
            cam.transform.SetPositionAndRotation(pos ?? new Vector3(0.3f, 1.5f, -60 + 11f), rot ?? Quaternion.Euler(4, 180, 0));
            cam.fieldOfView = fov;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            t.Apply();
            File.WriteAllBytes(Path.Combine(outDir, name + ".png"), t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
            RenderTexture.active = null;
            cam.targetTexture = null;
        }
        Shot("lineup_idle");
        d.transform.localRotation = Quaternion.identity;
        Vector3 dp = d.transform.position + Vector3.up * 0.5f;
        Shot("dog_side", 25f, dp + new Vector3(3.2f, 0.2f, 0), Quaternion.LookRotation(new Vector3(-1, -0.05f, 0)));
        Shot("dog_front", 25f, dp + new Vector3(0, 0.3f, 3.2f), Quaternion.LookRotation(new Vector3(0, -0.1f, -1)));
        Shot("dog_top", 25f, dp + new Vector3(0, 3.2f, 0.2f), Quaternion.LookRotation(new Vector3(0, -1, 0), Vector3.forward));
        d.transform.localRotation = Quaternion.Euler(0, 160f, 0);
        foreach (var (state, speed, shot) in new[] { ("Move", 2f, "lineup_walk"), ("Move", 5f, "lineup_run"), ("Cheer", 0f, "lineup_cheer"), ("Talk", 0f, "lineup_interact"), ("Listen", 0f, "lineup_idleB") })
        {
            for (int i = 0; i < figs.Length; i++)
            {
                var an = root.transform.GetChild(i).GetComponent<Animator>();
                an.Rebind();
                an.SetFloat("Speed", speed);
                an.SetBool("Listen", state == "Listen");
                an.Play(state, 0, 0.3f);
                an.Update(0f);
                an.Update(0.2f);
            }
            Shot(shot);
        }
    }
}
