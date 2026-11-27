// Phase 8.3: renders the wardrobe straight from the Editor (needs a GPU, so no -nographics). Project settings are restored byte for byte.
//
//   Unity.exe -batchmode -projectPath world_tour_unity -executeMethod WardrobePreview.Run -wplace coffee -wout <dir> [-wsize 1366x768]
//       -wshots "name|id,id,id|view;name2|..."      ids are item ids ("outfit:peach-navy"), view is star | game | stage | trunk | dog
//   star = the shop's close-up of the Star and riff, game = the follow camera, stage = the show's stage camera with the Star on the stage (guitar in her hands),
//   trunk = the follow camera standing at the wardrobe trunk, dog = a tight shot of riff.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;
using WorldTour.World;
using WorldTour.World.Gameplay;
using WorldTour.World.Wardrobe;

public static class WardrobePreview
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
            string place = Arg("-wplace") ?? "coffee";
            string outDir = Arg("-wout") ?? "Temp/wardrobe";
            Directory.CreateDirectory(outDir);
            var size = (Arg("-wsize") ?? "1366x768").Split('x').Select(int.Parse).ToArray();
            oldAA = QualitySettings.antiAliasing; oldQ = QualitySettings.renderPipeline; oldG = GraphicsSettings.defaultRenderPipeline;
            var pipe = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(WorldSpikeBuilder.PipelinePath);
            QualitySettings.renderPipeline = pipe;
            GraphicsSettings.defaultRenderPipeline = pipe;
            EditorSceneManager.OpenScene("Assets/World/Scenes/" + WorldPlace.SceneName(place) + ".unity", OpenSceneMode.Single);

            var cam = Camera.main;
            var follow = cam.GetComponent<FollowCamera>();
            var sheet = new System.Collections.Generic.List<Texture2D>();
            var rt = new RenderTexture(size[0], size[1], 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            foreach (var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) ps.Simulate(9f, true, true);
            var idle = WorldArtBuilder.KayClip("Idle_A");
            foreach (var a in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
                if (a.avatar != null && a.avatar.isHuman) idle.SampleAnimation(a.gameObject, 0.4f);

            Transform T(string n) { var g = GameObject.Find(n); return g != null ? g.transform : null; }
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            var dogF = UnityEngine.Object.FindFirstObjectByType<DogFollower>();
            var holder = new GameObject("WardrobePreview");
            var rig = new WardrobeRig(holder);
            rig.Bind();
            Vector3 spawn = T("PlayerSpawn").position;
            Vector3 stageP = T("StageSpot").position, crowdP = T("CrowdArea").position;
            var crowdGo = new GameObject("CrowdPreview");
            var crowdField = crowdGo.AddComponent<CrowdField>();
            crowdField.Setup(T("CrowdArea"), stageP, WorldGameplayAssets.Load(), 7);
            crowdField.SetFans(int.Parse(Arg("-wfans") ?? "9000"));
            foreach (Transform c in crowdGo.transform) c.localScale = Vector3.one * 0.85f;
            foreach (var m in UnityEngine.Object.FindObjectsByType<MarkerBob>(FindObjectsSortMode.None)) m.transform.rotation = cam.transform.rotation * Quaternion.Euler(0, 180, 0);

            // the wardrobe trunk, as WorldShop sets it out at run time (a stand-in position for the preview: the first free spot beside the spawn)
            string trunkAt = Arg("-wtrunk");
            GameObject trunk = null;
            Vector3 trunkPos = spawn + new Vector3(4.2f, 0f, 0.6f);
            if (trunkAt != null) { var p = trunkAt.Split(','); trunkPos = new Vector3(float.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture), 0f, float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture)); }
            if (Arg("-wnotrunk") == null) trunk = WardrobeTrunk.Build(trunkPos, WardrobeAssets.Load());

            void Shot(string name, Vector3 pos, Quaternion rot, float fov)
            {
                cam.transform.SetPositionAndRotation(pos, rot);
                cam.fieldOfView = fov;
                cam.ResetProjectionMatrix();
                float shift = Arg("-wshift") != null ? float.Parse(Arg("-wshift"), System.Globalization.CultureInfo.InvariantCulture) : 0f;
                if (shift != 0f) { var m = cam.projectionMatrix; m.m02 = shift * 2f; cam.projectionMatrix = m; }
                foreach (var mb in UnityEngine.Object.FindObjectsByType<MarkerBob>(FindObjectsSortMode.None)) mb.transform.rotation = cam.transform.rotation * Quaternion.Euler(0, 180, 0);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var t = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                t.Apply();
                File.WriteAllBytes(Path.Combine(outDir, name + ".png"), t.EncodeToPNG());
                if (Arg("-wsheet") != null) sheet.Add(UnityEngine.Object.Instantiate(t));
                UnityEngine.Object.DestroyImmediate(t);
                RenderTexture.active = null;
                cam.targetTexture = null;
                cam.ResetProjectionMatrix();
            }

            Vector3 off = follow != null ? follow.offset : new Vector3(0, 6.6f, -11.4f);
            Quaternion look = follow != null ? follow.FollowRotation : Quaternion.LookRotation(-off, Vector3.up);
            foreach (string spec in (Arg("-wshots") ?? "base||star").Split(';'))
            {
                var parts = spec.Split('|');
                string name = parts[0], view = parts.Length > 2 ? parts[2] : "star";
                var wl = new WardrobeLook { Outfit = "outfit:tour", Hair = "hair:chestnut", Guitar = "guitar:classic", Dog = "dog:none", Lights = "lights:tour" };
                if (parts.Length > 1 && parts[1].Length > 0) foreach (string id in parts[1].Split(',')) wl.Set(id);
                rig.Apply(AvatarFromSpec(parts.Length > 1 ? parts[1] : ""));
                bool guitar = view == "stage" || parts.Length > 1 && parts[1].Contains("guitar:");
                rig.ShowGuitar(guitar);
                player.Place(spawn);
                if (trunk != null) trunk.SetActive(true);
                Transform starT = player.transform;
                Vector3 s = new Vector3(spawn.x, 0f, spawn.z);
                Vector3 pos; Quaternion rot; Vector3 dp; Quaternion dr;
                switch (view)
                {
                    case "game":
                        starT.rotation = Quaternion.identity;
                        dogF.transform.position = spawn + new Vector3(-1.8f, 0f, -0.6f);
                        Shot(name, s + off, look, 44f);
                        break;
                    case "trunk":
                        player.Place(new Vector3(trunkPos.x, 0f, trunkPos.z - 1.9f));
                        starT.rotation = Quaternion.identity;
                        dogF.transform.position = starT.position + new Vector3(-1.5f, 0f, -0.4f);
                        Shot(name, starT.position + off, look, 44f);
                        break;
                    case "stage":
                        {
                            Vector3 dir = WorldShop.StagePose(stageP, crowdP, out pos, out rot);
                            float h = 0f;
                            foreach (var z in UnityEngine.Object.FindObjectsByType<HeightZone>(FindObjectsSortMode.None)) h = Mathf.Max(h, z.height);
                            player.Place(new Vector3(stageP.x, 0f, stageP.z));
                            starT.rotation = Quaternion.LookRotation(dir);
                            var st = starT.GetComponentInChildren<StandOnHeight>();
                            if (st != null) st.transform.localPosition = new Vector3(0, h, 0);
                            WorldShop.DogPose(stageP, dir, rig.Dog.NoseYaw, out dp, out dr);
                            dogF.transform.SetPositionAndRotation(dp, dr);
                            foreach (var m in UnityEngine.Object.FindObjectsByType<MarkerBob>(FindObjectsSortMode.None)) m.gameObject.SetActive(false);
                            if (trunk != null) trunk.SetActive(false);
                            Shot(name, pos, rot, WorldShop.StageFovValue);
                            foreach (var m in UnityEngine.Object.FindObjectsByType<MarkerBob>(FindObjectsSortMode.None)) m.gameObject.SetActive(true);
                            if (st != null) st.transform.localPosition = Vector3.zero;
                        }
                        break;
                    case "dog":
                        {
                            starT.rotation = Quaternion.LookRotation(view == "back" ? Vector3.forward : Vector3.back);
                            WorldShop.DogPose(s, Vector3.back, rig.Dog.NoseYaw, out dp, out dr);
                            dogF.transform.SetPositionAndRotation(dp, dr);
                            Vector3 nose = rig.Dog.NoseDirection; nose.y = 0f; nose.Normalize();
                            Vector3 target = dp + Vector3.up * 0.62f;
                            Vector3 cp = target + nose * 1.4f + Vector3.Cross(Vector3.up, nose) * 0.55f + Vector3.up * 0.25f;
                            Shot(name, cp, Quaternion.LookRotation(target - cp, Vector3.up), 45f);
                        }
                        break;
                    case "torso":
                        {
                            starT.rotation = Quaternion.LookRotation(Vector3.back);
                            dogF.transform.position = spawn + new Vector3(-6f, 0f, 3f);
                            Vector3 target = s + Vector3.up * 0.85f, cp = target + new Vector3(0f, 0.05f, -3.2f);
                            Shot(name, cp, Quaternion.LookRotation(target - cp, Vector3.up), 26f);
                        }
                        break;
                    case "head": case "headback": case "headside":
                        {
                            // a tight shot of the head: front, from behind or from the side (hair, hats and the face)
                            bool back = view == "headback";
                            starT.rotation = Quaternion.LookRotation(Vector3.back);
                            dogF.transform.position = spawn + new Vector3(-6f, 0f, 3f);
                            Vector3 target = s + Vector3.up * 1.45f;
                            Vector3 dir = view == "headside" ? new Vector3(-1f, 0.04f, -0.2f) : new Vector3(0.0f, 0.06f, back ? 1f : -1f);
                            Vector3 cp = target + dir.normalized * 3.2f;
                            Shot(name, cp, Quaternion.LookRotation(target - cp, Vector3.up), 24f);
                        }
                        break;
                    case "crew0": case "crew1": case "crew2":
                        {
                            // a tight shot of one of the crew (the three NPC figures with LookAtPlayer) from the front
                            var crew = UnityEngine.Object.FindObjectsByType<WorldTour.World.LookAtPlayer>(FindObjectsSortMode.None).OrderBy(c => c.name).ToArray();
                            if (crew.Length == 0) break;
                            Transform ct = crew[Mathf.Min(view[4] - '0', crew.Length - 1)].transform;
                            Vector3 target = ct.position + Vector3.up * 1.35f;
                            Vector3 fwd = ct.forward; fwd.y = 0f; fwd.Normalize();
                            Vector3 cp = target + (fwd * 3.0f) + Vector3.up * 0.1f;
                            Shot(name, cp, Quaternion.LookRotation(target - cp, Vector3.up), 24f);
                        }
                        break;
                    default:   // star
                        {
                            starT.rotation = Quaternion.LookRotation(view == "back" ? Vector3.forward : Vector3.back);
                            WorldShop.DogPose(s, Vector3.back, rig.Dog.NoseYaw, out dp, out dr);
                            dogF.transform.SetPositionAndRotation(dp, dr);
                            WorldShop.ClosePose(s, Vector3.back, out pos, out rot);
                            Shot(name, pos, rot, WorldShop.CloseFovValue);
                        }
                        break;
                }
            }
            rt.Release();
            string sheetName = Arg("-wsheet");
            if (sheetName != null && sheet.Count > 0)
            {
                int cols = int.Parse(Arg("-wcols") ?? "4"), rows = (sheet.Count + cols - 1) / cols, cw = sheet[0].width, ch = sheet[0].height;
                var big = new Texture2D(cw * cols, ch * rows, TextureFormat.RGB24, false);
                for (int i = 0; i < sheet.Count; i++) big.SetPixels((i % cols) * cw, (rows - 1 - i / cols) * ch, cw, ch, sheet[i].GetPixels());
                big.Apply();
                File.WriteAllBytes(Path.Combine(outDir, sheetName + ".png"), big.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(big);
                foreach (var s2 in sheet) UnityEngine.Object.DestroyImmediate(s2);
            }
        }
        catch (Exception e) { Debug.LogError("WardrobePreview failed: " + e); }
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

    /// <summary>Phase 10B.1: an avatar look from "top:hoodie:3:1,hat:cap:7,skin=3,hair=15" (item id, colour, pattern; the rest stay the default look).</summary>
    static AvatarLook AvatarFromSpec(string spec)
    {
        AvatarLook l = AvatarLook.Default();
        foreach (string tok in spec.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (tok.StartsWith("skin=")) { l.Skin = int.Parse(tok.Substring(5)); continue; }
            if (tok.StartsWith("hair=")) { l.HairColor = int.Parse(tok.Substring(5)); l.Hair.Color = l.HairColor; continue; }
            string[] p = tok.Split(':');
            if (p.Length < 2) continue;
            AvatarItem it = AvatarCatalogue.ById(p[0] + ":" + p[1]);
            if (it == null) continue;
            AvatarPick old = l.Get(it.Slot);
            l.Set(it.Slot, new AvatarPick(it.Id, p.Length > 2 ? int.Parse(p[2]) : old.Color, p.Length > 3 ? int.Parse(p[3]) : 0));
        }
        return l;
    }
}
