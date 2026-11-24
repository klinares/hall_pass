// Phase 8.2 builder B: preview renders of any place scene (needs a GPU, so no -nographics). Used for Place_Theater, Place_Festival and
// Place_Arena. Writes PNGs and puts QualitySettings and GraphicsSettings back byte for byte.
//
//   Unity.exe -batchmode -projectPath world_tour_unity -executeMethod PlaceTheaterPreview.Run -previewScene <scene path> -previewOut <dir>
//       [-previewSize 1366x768] [-previewFans 3000] [-previewShots spawn,crew1,crew2,crew3,stagecam,overview,...] [-previewWalk x:z,...]
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using WorldTour.World;
using WorldTour.World.Gameplay;

public static class PlaceTheaterPreview
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
            EditorSceneManager.OpenScene(Arg("-previewScene"), OpenSceneMode.Single);

            var cam = Camera.main;
            var follow = cam.GetComponent<FollowCamera>();
            var rt = new RenderTexture(size[0], size[1], 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            foreach (var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) ps.Simulate(9f, true, true);
            var idle = WorldArtBuilder.KayClip("Idle_A");
            foreach (var a in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
                if (a.avatar != null && a.avatar.isHuman) idle.SampleAnimation(a.gameObject, 0.4f);
            var crowdGo = new GameObject("CrowdPreview");
            var crowdField = crowdGo.AddComponent<CrowdField>();
            var crowdArea = GameObject.Find("CrowdArea").transform;
            var stageSpot = GameObject.Find("StageSpot").transform;
            crowdField.Setup(crowdArea, stageSpot.position, WorldGameplayAssets.Load(), 7);
            crowdField.SetFans(int.Parse(Arg("-previewFans") ?? "3000"));
            foreach (Transform c in crowdGo.transform) c.localScale = Vector3.one * 0.85f;

            void Shot(string name, Vector3 pos, Quaternion rot, float fov = 44f, float lens = 0f)
            {
                if (want != null && !want.Contains(name) && !name.StartsWith("walk_")) return;
                cam.transform.SetPositionAndRotation(pos, rot);
                cam.fieldOfView = fov;
                cam.ResetProjectionMatrix();
                if (lens != 0f) { var m = cam.projectionMatrix; m.m02 = lens * 2f; cam.projectionMatrix = m; }
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

            Vector3 spawn = GameObject.Find("PlayerSpawn").transform.position;
            var walker = GameObject.Find("Player");
            void WalkShot(string name, Vector3 at)
            {
                if (want != null && !want.Contains(name) && !name.StartsWith("walk_")) return;
                walker.transform.position = new Vector3(at.x, 0f, at.z);
                follow.target = walker.transform;
                follow.SnapForPreview();
                Shot(name, follow.transform.position, follow.transform.rotation, 44f);
            }
            WalkShot("spawn", spawn);
            for (int i = 1; i <= 4; i++)
            {
                var crew = GameObject.Find("Crew_" + i);
                if (crew == null) continue;
                // she stands about 3.4 m south of the crew member, as she would when she walks up
                WalkShot("crew" + i, crew.transform.position + new Vector3(0.6f, 0, -3.4f));
            }
            WalkShot("stage", stageSpot.position + new Vector3(0, 0, -5.5f));
            WalkShot("exit", GameObject.Find("ExitSpot").transform.position + new Vector3(-2.5f, 0, 0));
            walker.transform.position = spawn;
            follow.SnapForPreview();

            // walk shots: where she stands, as the follow camera would see it
            string walkSpec = Arg("-previewWalk");
            if (walkSpec != null && walker != null && follow != null)
            {
                foreach (string pair in walkSpec.Split(','))
                {
                    string[] xz = pair.Split(':');
                    float wx = float.Parse(xz[0], System.Globalization.CultureInfo.InvariantCulture), wz = float.Parse(xz[1], System.Globalization.CultureInfo.InvariantCulture);
                    WalkShot("walk_" + xz[0] + "_" + xz[1], new Vector3(wx, 0, wz));
                    Debug.Log("WT_WALK " + pair + " hidden=" + follow.HiddenCount);
                }
            }

            // the gameplay's stage camera (WorldGame.ComputeStageCamera, constants copied) with her on the spot and the lights flared, panel lens shift
            var plr = GameObject.Find("Player");
            if (plr != null)
            {
                Vector3 stageP = stageSpot.position, crowdP = crowdArea.position;
                Vector3 dirS = crowdP - stageP; dirS.y = 0; dirS.Normalize();
                Vector3 camP = crowdP + dirS * 7f + Vector3.up * 6.5f;
                Quaternion camR = Quaternion.LookRotation((stageP + Vector3.up * 3f + dirS * 2.5f) - camP, Vector3.up);
                Vector3 keep = plr.transform.position; Quaternion keepR = plr.transform.rotation;
                plr.transform.position = new Vector3(stageP.x, 0, stageP.z);
                plr.transform.rotation = Quaternion.LookRotation(-dirS);
                var st = plr.GetComponentInChildren<StandOnHeight>();
                float h = HeightZone.Sample(stageP);
                if (st != null) st.transform.localPosition = new Vector3(0, h, 0);
                Shot("stagecam", camP, camR, 57f, 0.246f);
                Shot("stagecam_full", camP, camR, 57f, 0f);
                var props = new MaterialPropertyBlock();
                foreach (var beam in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => r.name == "Beam"))
                { props.SetColor("_BaseColor", Color.white * 2.1f); beam.SetPropertyBlock(props); }
                Shot("stagecam_flare", camP, camR, 57f, 0.246f);
                plr.transform.position = keep; plr.transform.rotation = keepR;
                if (st != null) st.transform.localPosition = Vector3.zero;
            }
            Shot("overview", spawn + new Vector3(0, 34, -30), Quaternion.Euler(40, 0, 0), 45f);
                        rt.Release();
        }
        catch (Exception e) { Debug.LogError("PlaceTheaterPreview failed: " + e); }
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
