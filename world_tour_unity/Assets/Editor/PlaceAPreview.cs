// Phase 8.2 builder A: preview renders of a place scene straight from the Editor (needs a GPU, so no -nographics). Project settings are restored byte for byte.
//
//   Unity.exe -batchmode -projectPath world_tour_unity -executeMethod PlaceAPreview.Run -previewPlace records -previewOut <dir> [-previewSize 1366x768]
//       [-previewShots spawn,crew1,crew2,crew3,stage,stage_wide,overview,walk_x_z] [-previewFans 3000]
//   Shots: spawn (the follow camera at PlayerSpawn), crewN (the follow camera standing at that crew member's talking spot), stage (the stage camera
//   the show uses, with the docked-panel lens shift, the Star on the spot, a crowd), stage_wide (the same without the shift), overview (high),
//   walk_x_z (the follow camera with her at x, z), cam_x_y_z_pitch_yaw_fov (a free camera).
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using WorldTour.World;
using WorldTour.World.Gameplay;

public static class PlaceAPreview
{
    static string Arg(string name)
    {
        var a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return null;
    }

    static float F(string s) { return float.Parse(s, System.Globalization.CultureInfo.InvariantCulture); }

    public static void Run()
    {
        string quality = "ProjectSettings/QualitySettings.asset", graphics = "ProjectSettings/GraphicsSettings.asset";
        RenderPipelineAsset oldQ = null, oldG = null; int oldAA = 0;
        byte[] qb = File.ReadAllBytes(quality), gb = File.ReadAllBytes(graphics);
        try
        {
            string place = Arg("-previewPlace") ?? "records";
            string scenePath = PlaceKit.SceneDir + WorldPlace.SceneName(place) + ".unity";
            string outDir = Arg("-previewOut") ?? "Temp/preview";
            Directory.CreateDirectory(outDir);
            var size = (Arg("-previewSize") ?? "1366x768").Split('x').Select(int.Parse).ToArray();
            var want = (Arg("-previewShots") ?? "spawn,crew1,crew2,crew3,stage,stage_wide,overview").Split(',');
            oldAA = QualitySettings.antiAliasing; oldQ = QualitySettings.renderPipeline; oldG = GraphicsSettings.defaultRenderPipeline;
            var pipe = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(WorldSpikeBuilder.PipelinePath);
            QualitySettings.renderPipeline = pipe;
            GraphicsSettings.defaultRenderPipeline = pipe;
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var cam = Camera.main;
            var follow = cam.GetComponent<FollowCamera>();
            var rt = new RenderTexture(size[0], size[1], 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            foreach (var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) ps.Simulate(9f, true, true);
            var idle = WorldArtBuilder.KayClip("Idle_A");
            foreach (var a in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
                if (a.avatar != null && a.avatar.isHuman) idle.SampleAnimation(a.gameObject, 0.4f);

            Transform T(string n) { var g = GameObject.Find(n); return g != null ? g.transform : null; }
            var crowdGo = new GameObject("CrowdPreview");
            var crowdField = crowdGo.AddComponent<CrowdField>();
            var crowdArea = T("CrowdArea"); var stageSpot = T("StageSpot");
            crowdField.Setup(crowdArea, stageSpot.position, WorldGameplayAssets.Load(), 7);
            crowdField.SetFans(int.Parse(Arg("-previewFans") ?? "3000"));
            foreach (Transform c in crowdGo.transform) c.localScale = Vector3.one * 0.85f;

            void Shot(string name, Vector3 pos, Quaternion rot, float fov = 44f, float shift = 0f)
            {
                cam.transform.SetPositionAndRotation(pos, rot);
                cam.fieldOfView = fov;
                cam.ResetProjectionMatrix();
                if (shift != 0f) { var m = cam.projectionMatrix; m.m02 = shift * 2f; cam.projectionMatrix = m; }
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
                cam.ResetProjectionMatrix();
            }

            Vector3 off = follow != null ? follow.offset : new Vector3(0, 6.6f, -11.4f);
            Quaternion look = follow != null ? follow.FollowRotation : Quaternion.LookRotation(-off, Vector3.up);
            var player = T("Player");
            Vector3 spawn = T("PlayerSpawn").position;
            if (want.Contains("spawn")) Shot("spawn", spawn + off, look);
            for (int i = 1; i <= 3; i++)
            {
                var c = T("Crew_" + i);
                if (c == null || !want.Contains("crew" + i)) continue;
                // she stands 1.7 m short of them, on the camera side, as a click on them would leave her
                Vector3 stand = c.position + Vector3.back * 1.7f;
                if (player != null) player.position = stand;
                Shot("crew" + i, stand + off, look);
            }
            if (player != null) player.position = spawn;

            // the show's stage camera (WorldGame.ComputeStageCamera, constants copied) with her on the spot and the panel's lens shift
            if (want.Contains("stage") || want.Contains("stage_wide"))
            {
                Vector3 stageP = stageSpot.position, crowdP = crowdArea.position;
                Vector3 dir = crowdP - stageP; dir.y = 0; dir.Normalize();
                Vector3 camP = crowdP + dir * 7f + Vector3.up * 6.5f;
                Quaternion camR = Quaternion.LookRotation((stageP + Vector3.up * 3.0f + dir * 2.5f) - camP, Vector3.up);
                float h = 0f;
                foreach (var z in UnityEngine.Object.FindObjectsByType<HeightZone>(FindObjectsSortMode.None)) h = Mathf.Max(h, z.height);
                if (player != null)
                {
                    player.position = new Vector3(stageP.x, 0, stageP.z);
                    player.rotation = Quaternion.LookRotation(-dir);
                    var st = player.GetComponentInChildren<StandOnHeight>();
                    if (st != null) st.transform.localPosition = new Vector3(0, h, 0);
                }
                foreach (var lights in UnityEngine.Object.FindObjectsByType<MarkerBob>(FindObjectsSortMode.None)) lights.gameObject.SetActive(false);
                if (want.Contains("stage")) Shot("stage", camP, camR, 57f, 0.246f);
                if (want.Contains("stage_wide")) Shot("stage_wide", camP, camR, 57f, 0f);
                foreach (var m in UnityEngine.Object.FindObjectsByType<MarkerBob>(FindObjectsSortMode.None)) m.gameObject.SetActive(true);
                if (player != null) player.position = spawn;
            }
            foreach (string w in want)
            {
                if (w.StartsWith("walk_"))
                {
                    var p = w.Split('_');
                    if (player != null) { player.position = new Vector3(F(p[1]), 0, F(p[2])); }
                    Shot(w, new Vector3(F(p[1]), 0, F(p[2])) + off, look);
                }
                else if (w.StartsWith("cam_"))
                {
                    var p = w.Split('_');
                    Shot(w, new Vector3(F(p[1]), F(p[2]), F(p[3])), Quaternion.Euler(F(p[4]), F(p[5]), 0), F(p[6]));
                }
            }
            if (want.Contains("overview"))
            {
                Shot("overview", new Vector3(0, 30, -34), Quaternion.Euler(33, 0, 0), 40f);
                Shot("overview_n", new Vector3(0, 8, -12), Quaternion.Euler(14, 0, 0), 60f);
            }
            rt.Release();
        }
        catch (Exception e) { Debug.LogError("PlaceAPreview failed: " + e); }
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
}
