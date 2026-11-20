// Builds the phase 2 UI scaffolding by code, so no scene, prefab or material is edited by hand.
//
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod UiBuilder.BuildAll [-uiSummary <file>]
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod UiBuilder.WireTheme [-uiSummary <file>]
//
// BuildAll (re-runnable; each run replaces what it made before):
//   Assets/Resources/WorldTour/UiGreyLocked.mat, UiGreyUnearned.mat   greyscale UI materials (Resources keeps the shader in the build)
//   Assets/Resources/WorldTour/Theme.asset                             fonts, decor sprites and materials by role (WireTheme)
//   Assets/Game/Ui/Prefabs/StickerButton.prefab, Screen_Empty.prefab
//   -uiGlyphProbe adds a throwaway text line with the glyph sprites to Screen_Empty (screenshot check; rerun without it)
//   Assets/Scenes/Main.unity     camera, EventSystem (Input System UI module), Canvas (Overlay, 1366x768, match 0.5),
//                                background, screen stack, MainRoot with the empty screen as home
//   Assets/Scenes/Boot.unity     adds an EventSystem if missing and the start card canvas; assigns Boot.startCard when
//                                Boot has that field (see the 2.2 hand-back for the Boot change)
//   Build list: Main after Boot.
// WireTheme only re-wires the theme asset, for after task 2.1's fonts and sprites land.
//
// Every run writes a summary JSON (default Temp/UiBuilder-summary.json) and ends in EditorApplication.Exit(0 or 1) in
// batch mode. The Editor can crash at shutdown on this machine (DECISIONS D1), so the summary is the evidence.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WorldTour.Game;
using WorldTour.Game.Mascot;
using WorldTour.Game.Ui;

public static class UiBuilder
{
    const string ShaderPath = "Assets/Game/Ui/Shaders/UiGreyscale.shader";
    const string ResDir = "Assets/Resources/WorldTour";
    const string LockedMatPath = ResDir + "/UiGreyLocked.mat";
    const string UnearnedMatPath = ResDir + "/UiGreyUnearned.mat";
    const string ThemePath = ResDir + "/Theme.asset";
    const string PrefabDir = "Assets/Game/Ui/Prefabs";
    const string ButtonPrefabPath = PrefabDir + "/StickerButton.prefab";
    const string ScreenPrefabPath = PrefabDir + "/Screen_Empty.prefab";
    const string MainScenePath = "Assets/Scenes/Main.unity";
    const string BootScenePath = "Assets/Scenes/Boot.unity";
    const string FontsDir = "Assets/Fonts";
    const string DecorDir = "Assets/Art/Sprites/decor";
    const string DogArtPath = "Assets/Art/Mascot/DogArt.asset";
    static readonly Vector2 Reference = new Vector2(1366, 768);

    [Serializable]
    public class Summary
    {
        public string method;
        public string startedAt;
        public string finishedAt;
        public bool ok;
        public List<string> created = new List<string>();
        public List<string> wired = new List<string>();
        public List<string> warnings = new List<string>();
        public List<string> errors = new List<string>();
    }

    static Summary s_Summary;

    // ------------------------------------------------------------------ entry points

    public static void BuildAll() { Run("BuildAll", () =>
    {
        EnsureFolder(ResDir);
        EnsureFolder(PrefabDir);
        Material locked, unearned;
        MakeMaterials(out locked, out unearned);
        ThemeAsset theme = MakeTheme(locked, unearned);
        StickerButton button = MakeButtonPrefab(theme);
        GameScreen screen = MakeScreenPrefab(theme);
        MakeMainScene(screen);
        AddToBuildList(MainScenePath);
        AddStartCardToBoot(button, theme);
    }); }

    public static void WireTheme() { Run("WireTheme", () =>
    {
        EnsureFolder(ResDir);
        Material locked, unearned;
        MakeMaterials(out locked, out unearned);
        MakeTheme(locked, unearned);
    }); }

    [MenuItem("World Tour/UI/Build scaffolding (2.2)")]
    static void BuildAllMenu() { BuildAll(); }

    [MenuItem("World Tour/UI/Re-wire theme")]
    static void WireThemeMenu() { WireTheme(); }

    static void Run(string method, Action body)
    {
        s_Summary = new Summary { method = method, startedAt = DateTime.UtcNow.ToString("o") };
        try
        {
            body();
            AssetDatabase.SaveAssets();
            s_Summary.ok = s_Summary.errors.Count == 0;
        }
        catch (Exception e)
        {
            s_Summary.errors.Add(e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
            s_Summary.ok = false;
        }
        finally
        {
            s_Summary.finishedAt = DateTime.UtcNow.ToString("o");
            string path = Arg("-uiSummary") ?? Path.Combine("Temp", "UiBuilder-summary.json");
            try
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, JsonUtility.ToJson(s_Summary, true));
                Debug.Log("[UiBuilder] " + method + (s_Summary.ok ? " ok" : " FAILED") + ", summary: " + Path.GetFullPath(path));
            }
            catch (Exception e) { Debug.LogError("[UiBuilder] could not write the summary: " + e.Message); }
            if (Application.isBatchMode) EditorApplication.Exit(s_Summary.ok ? 0 : 1);
        }
    }

    static string Arg(string name)
    {
        string[] a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return null;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // ------------------------------------------------------------------ materials and theme

    static void MakeMaterials(out Material locked, out Material unearned)
    {
        AssetDatabase.ImportAsset(ShaderPath, ImportAssetOptions.ForceSynchronousImport);
        Shader sh = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (sh == null) throw new Exception("greyscale shader missing at " + ShaderPath);
        ShaderInfo info = ShaderUtil.GetShaderInfo(sh);
        if (info.hasErrors) s_Summary.errors.Add("the greyscale shader has compile errors (see the Editor log)");
        // screens.css: .venue.locked .vart and .toearn .stk
        locked = SaveMaterial(LockedMatPath, sh, contrast: 0.7f, brightness: 1.08f, fade: 0.5f);
        unearned = SaveMaterial(UnearnedMatPath, sh, contrast: 1f, brightness: 1f, fade: 0.45f);
    }

    static Material SaveMaterial(string path, Shader sh, float contrast, float brightness, float fade)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = m == null;
        if (isNew) m = new Material(sh);
        m.shader = sh;
        m.SetFloat("_Grey", 1f);
        m.SetFloat("_Contrast", contrast);
        m.SetFloat("_Brightness", brightness);
        m.SetFloat("_Fade", fade);
        if (isNew) { AssetDatabase.CreateAsset(m, path); s_Summary.created.Add(path); }
        else EditorUtility.SetDirty(m);
        return m;
    }

    static ThemeAsset MakeTheme(Material locked, Material unearned)
    {
        ThemeAsset t = AssetDatabase.LoadAssetAtPath<ThemeAsset>(ThemePath);
        if (t == null)
        {
            t = ScriptableObject.CreateInstance<ThemeAsset>();
            AssetDatabase.CreateAsset(t, ThemePath);
            s_Summary.created.Add(ThemePath);
        }

        t.display = FindFont(FontsDir + "/CaveatBrush", bold: false, "display");
        t.body = FindFont(FontsDir + "/CourierPrime", bold: false, "body");
        t.bodyBold = FindFont(FontsDir + "/CourierPrime", bold: true, "bodyBold");

        t.paperPanel = FindDecor("paperPanel", "paper_torn", "torn", "scrap", "paper");
        t.tape = FindDecor("tape", "tape_a", "tape", "washi");
        t.polaroidFrame = FindDecor("polaroidFrame", "polaroid");
        t.halftone = FindDecor("halftone", "halftone_tr", "halftone", "dots");
        t.ticketStub = FindDecor("ticketStub", "ticket_stub", "ticket", "stub", "perf");
        t.roundedRect = FindDecor("roundedRect", "rounded", "round_rect", "roundrect");
        if (t.roundedRect == null)
        {
            t.roundedRect = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            s_Summary.wired.Add("roundedRect = uGUI built-in UISprite (fallback)");
        }

        t.greyLocked = locked;
        t.greyUnearned = unearned;
        t.iconSound = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/ui/icon_sound.png");
        t.iconMute = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/ui/icon_mute.png");
        if (t.iconSound == null || t.iconMute == null) s_Summary.warnings.Add("ui/icon_sound or icon_mute missing; the sound toggle has no icon");
        else s_Summary.wired.Add("iconSound, iconMute = Assets/Art/Sprites/ui/icon_sound.png, icon_mute.png");
        t.dogArt = AssetDatabase.LoadAssetAtPath<DogArt>(DogArtPath);
        if (t.dogArt == null) s_Summary.warnings.Add("no " + DogArtPath + " yet (run MascotArtBuilder.BuildBatch); riff will not show");
        else s_Summary.wired.Add("dogArt = " + DogArtPath);
        EditorUtility.SetDirty(t);
        AssetDatabase.SaveAssets();
        return t;
    }

    static TMP_FontAsset FindFont(string folder, bool bold, string role)
    {
        if (!AssetDatabase.IsValidFolder(folder)) { s_Summary.warnings.Add("font " + role + ": " + folder + " not found yet (task 2.1); TMP default used"); return null; }
        var fonts = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal).ToList();
        string pick = fonts.FirstOrDefault(p => (Path.GetFileName(p).IndexOf("bold", StringComparison.OrdinalIgnoreCase) >= 0) == bold);
        if (pick == null && !bold) pick = fonts.FirstOrDefault();
        if (pick == null) { s_Summary.warnings.Add("font " + role + ": no TMP font asset in " + folder + " yet; TMP default used"); return null; }
        s_Summary.wired.Add("font " + role + " = " + pick);
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(pick);
    }

    /// <summary>A decor sprite by role: an exact file name match first, then keywords in priority order (first keyword wins).</summary>
    static Sprite FindDecor(string role, params string[] keywords)
    {
        if (!AssetDatabase.IsValidFolder(DecorDir)) { s_Summary.warnings.Add("decor " + role + ": " + DecorDir + " not found yet (task 2.1)"); return null; }
        var paths = AssetDatabase.FindAssets("t:Sprite", new[] { DecorDir })
            .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList();
        string pick = paths.FirstOrDefault(p => string.Equals(Path.GetFileNameWithoutExtension(p), role, StringComparison.OrdinalIgnoreCase));
        foreach (string k in keywords)
        {
            if (pick != null) break;
            pick = paths.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p).IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);
        }
        if (pick == null) { if (role != "roundedRect") s_Summary.warnings.Add("decor " + role + ": no sprite in " + DecorDir + " matches " + string.Join("/", keywords)); return null; }
        s_Summary.wired.Add("decor " + role + " = " + pick + " (check this is the right piece)");
        return AssetDatabase.LoadAssetAtPath<Sprite>(pick);
    }

    // ------------------------------------------------------------------ prefabs

    static StickerButton MakeButtonPrefab(ThemeAsset theme)
    {
        GameObject root = NewUi("StickerButton", null);
        var rt = (RectTransform)root.transform;
        rt.sizeDelta = new Vector2(240, Tokens.ButtonMinHeight);
        Image hit = root.AddComponent<Image>();
        hit.sprite = theme.roundedRect; hit.type = Image.Type.Sliced;
        hit.color = new Color(1, 1, 1, 0);
        hit.raycastTarget = true;
        root.GetComponent<CanvasRenderer>().cullTransparentMesh = true;

        GameObject body = NewUi("Body", root.transform);
        Stretch(body, 0);
        body.AddComponent<CanvasGroup>();

        float ring = Tokens.StickerPad + Tokens.BorderWidth;
        Image focus = Layer("FocusRing", body.transform, theme, ring + Tokens.FocusOffset + Tokens.FocusWidth, Vector2.zero);
        focus.enabled = false;
        Image shadow = Layer("Shadow", body.transform, theme, ring, new Vector2(Tokens.ShadowMd, -Tokens.ShadowMd));
        Image ringImg = Layer("Ring", body.transform, theme, ring, Vector2.zero);
        Image pad = Layer("Pad", body.transform, theme, Tokens.StickerPad, Vector2.zero);
        Image face = Layer("Face", body.transform, theme, 0, Vector2.zero);

        GameObject labelGo = NewUi("Label", body.transform);
        var lrt = (RectTransform)labelGo.transform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(Tokens.ButtonPadX, 4); lrt.offsetMax = new Vector2(-Tokens.ButtonPadX, -4);
        var label = labelGo.AddComponent<TextMeshProUGUI>();
        if (theme.display != null) label.font = theme.display;
        label.text = "button";
        label.fontSize = Tokens.FontButton;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;

        var b = root.AddComponent<StickerButton>();
        b.transition = Selectable.Transition.None;
        b.targetGraphic = hit;
        var so = new SerializedObject(b);
        Set(so, "body", body.transform);
        Set(so, "shadow", shadow.transform);
        Set(so, "shadowImage", shadow);
        Set(so, "ring", ringImg);
        Set(so, "pad", pad);
        Set(so, "face", face);
        Set(so, "focusRing", focus);
        Set(so, "label", label);
        so.ApplyModifiedPropertiesWithoutUndo();
        b.Paint();

        StickerButton saved = SavePrefab(root, ButtonPrefabPath).GetComponent<StickerButton>();
        return saved;
    }

    static GameScreen MakeScreenPrefab(ThemeAsset theme)
    {
        GameObject root = NewUi("Screen_Empty", null);
        Stretch(root, 0);
        root.AddComponent<CanvasGroup>();
        root.AddComponent<GameScreen>();

        GameObject labelGo = NewUi("Label", root.transform);
        Stretch(labelGo, Tokens.Gutter);
        var label = labelGo.AddComponent<TextMeshProUGUI>();
        if (theme.display != null) label.font = theme.display;
        label.text = "world tour";
        label.fontSize = Tokens.Fs5;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        labelGo.AddComponent<ThemeColor>().role = ColorRole.Ink;

        if (Environment.GetCommandLineArgs().Contains("-uiGlyphProbe"))
        {
            GameObject probe = NewUi("GlyphProbe", root.transform);
            var prt = (RectTransform)probe.transform;
            prt.anchorMin = new Vector2(0, 0); prt.anchorMax = new Vector2(1, 0);
            prt.pivot = new Vector2(0.5f, 0);
            prt.sizeDelta = new Vector2(0, 80);
            prt.anchoredPosition = new Vector2(0, 80);
            var pt = probe.AddComponent<TextMeshProUGUI>();
            // "stop 2 of 8 * tonight -> ^ star heart", from code points so this file stays ASCII
            pt.text = "stop 2 of 8 " + char.ConvertFromUtf32(0x2736) + " tonight " + char.ConvertFromUtf32(0x2192) + " "
                + char.ConvertFromUtf32(0x2191) + " " + char.ConvertFromUtf32(0x2605) + " " + char.ConvertFromUtf32(0x2665);
            pt.fontSize = 40;
            pt.alignment = TextAlignmentOptions.Center;
            pt.raycastTarget = false;
            probe.AddComponent<ThemeColor>().role = ColorRole.Ink;
            s_Summary.warnings.Add("Screen_Empty: TEMPORARY glyph probe line added; rerun BuildAll without -uiGlyphProbe");
        }

        return SavePrefab(root, ScreenPrefabPath).GetComponent<GameScreen>();
    }

    static GameObject SavePrefab(GameObject root, string path)
    {
        bool existed = File.Exists(path);
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
        UnityEngine.Object.DestroyImmediate(root);
        if (!success || saved == null) throw new Exception("could not save " + path);
        if (existed) s_Summary.warnings.Add("replaced " + path); else s_Summary.created.Add(path);
        return saved;
    }

    static Image Layer(string name, Transform parent, ThemeAsset theme, float grow, Vector2 shift)
    {
        GameObject go = NewUi(name, parent);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-grow, -grow) + shift;
        rt.offsetMax = new Vector2(grow, grow) + shift;
        Image img = go.AddComponent<Image>();
        img.sprite = theme.roundedRect;
        img.type = Image.Type.Sliced;
        img.raycastTarget = false;
        return img;
    }

    // ------------------------------------------------------------------ scenes

    static void MakeMainScene(GameScreen homeRef)
    {
        bool existed = File.Exists(MainScenePath);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // A scene switch can unload in-memory asset objects, so assets are reloaded by path after it.
        GameScreen home = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPrefabPath).GetComponent<GameScreen>();

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        Camera cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Theme.ToColor(Palettes.TourPink[ColorRole.Paper]);
        camGo.transform.position = new Vector3(0, 0, -10);
        Type urpCam = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urpCam != null) camGo.AddComponent(urpCam);
        else s_Summary.warnings.Add("URP camera data type not found; URP adds it at runtime");
        EnsureOneAudioListener(cam);

        AddEventSystem();

        GameObject canvasGo = MakeCanvas("Canvas", 0);
        GameObject bg = NewUi("Background", canvasGo.transform);
        Stretch(bg, 0);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.raycastTarget = false;
        bg.AddComponent<ThemeColor>().role = ColorRole.Paper;

        GameObject screens = NewUi("Screens", canvasGo.transform);
        Stretch(screens, 0);
        ScreenStack stack = screens.AddComponent<ScreenStack>();
        var sso = new SerializedObject(stack);
        Set(sso, "container", screens.transform);
        sso.ApplyModifiedPropertiesWithoutUndo();

        GameObject fx = NewUi("Fx", canvasGo.transform);
        Stretch(fx, 0);

        var rootGo = new GameObject("MainRoot");
        MainRoot mr = rootGo.AddComponent<MainRoot>();
        var mso = new SerializedObject(mr);
        Set(mso, "stack", stack);
        Set(mso, "home", home);
        mso.ApplyModifiedPropertiesWithoutUndo();

        if (!EditorSceneManager.SaveScene(scene, MainScenePath)) throw new Exception("could not save " + MainScenePath);
        if (existed) s_Summary.warnings.Add("replaced " + MainScenePath); else s_Summary.created.Add(MainScenePath);
    }

    /// <summary>
    /// Exactly one AudioListener per scene, on its camera (2.7 tester: the game was silent because neither scene had
    /// one). Any other listener in the open scene is removed, so a rebuild never leaves two.
    /// </summary>
    static void EnsureOneAudioListener(Camera cam)
    {
        foreach (AudioListener other in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (other.gameObject != cam.gameObject) UnityEngine.Object.DestroyImmediate(other);
        if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
        s_Summary.wired.Add(cam.gameObject.scene.name + ": one AudioListener on " + cam.name);
    }

    static void AddStartCardToBoot(StickerButton buttonRef, ThemeAsset themeRef)
    {
        if (!File.Exists(BootScenePath)) throw new Exception(BootScenePath + " not found");
        Scene scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
        StickerButton buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath).GetComponent<StickerButton>();
        ThemeAsset theme = AssetDatabase.LoadAssetAtPath<ThemeAsset>(ThemePath);

        Camera bootCam = Camera.main != null ? Camera.main : UnityEngine.Object.FindFirstObjectByType<Camera>();
        if (bootCam != null) EnsureOneAudioListener(bootCam);
        else s_Summary.warnings.Add("Boot has no camera for the AudioListener");

        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null) AddEventSystem();
        else
        {
            EventSystem es = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (es.GetComponent<InputSystemUIInputModule>() == null)
            {
                foreach (BaseInputModule m in es.GetComponents<BaseInputModule>()) UnityEngine.Object.DestroyImmediate(m);
                es.gameObject.AddComponent<InputSystemUIInputModule>();
                s_Summary.wired.Add("Boot EventSystem: switched to InputSystemUIInputModule");
            }
        }

        foreach (GameObject old in scene.GetRootGameObjects().Where(g => g.name == "StartCanvas").ToList())
        {
            UnityEngine.Object.DestroyImmediate(old);
            s_Summary.warnings.Add("replaced Boot/StartCanvas");
        }

        GameObject canvasGo = MakeCanvas("StartCanvas", 10);
        GameObject cardRoot = NewUi("StartCard", canvasGo.transform);
        Stretch(cardRoot, 0);
        CanvasGroup group = cardRoot.AddComponent<CanvasGroup>();
        // A paper page behind the card covers Boot's phase 0 title label; hidden with the card until Show().
        Image backdrop = cardRoot.AddComponent<Image>();
        backdrop.raycastTarget = true;
        cardRoot.AddComponent<ThemeColor>().role = ColorRole.Paper;

        GameObject card = NewUi("Card", cardRoot.transform);
        var crt = (RectTransform)card.transform;
        crt.sizeDelta = new Vector2(560, 340);
        crt.localEulerAngles = new Vector3(0, 0, StickerMath.Tilt(StickerMath.Seed("StartCard")) * 0.5f);
        Image panel = card.AddComponent<Image>();
        panel.sprite = theme.paperPanel != null ? theme.paperPanel : theme.roundedRect;
        panel.type = panel.sprite != null && panel.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        panel.raycastTarget = false;
        card.AddComponent<ThemeColor>().role = ColorRole.Card;

        GameObject titleGo = NewUi("Title", card.transform);
        var trt = (RectTransform)titleGo.transform;
        trt.anchorMin = new Vector2(0, 0.45f); trt.anchorMax = new Vector2(1, 1);
        trt.offsetMin = new Vector2(Tokens.Gutter, 0); trt.offsetMax = new Vector2(-Tokens.Gutter, -Tokens.Gutter);
        var title = titleGo.AddComponent<TextMeshProUGUI>();
        if (theme.display != null) title.font = theme.display;
        title.text = "world tour";
        title.fontSize = Tokens.Fs5;
        title.alignment = TextAlignmentOptions.Center;
        title.raycastTarget = false;
        titleGo.AddComponent<ThemeColor>().role = ColorRole.Ink;

        var btnGo = (GameObject)PrefabUtility.InstantiatePrefab(buttonPrefab.gameObject, card.transform);
        btnGo.name = "StartButton";
        var brt = (RectTransform)btnGo.transform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.25f);
        brt.anchoredPosition = Vector2.zero;
        brt.sizeDelta = new Vector2(240, Tokens.ButtonMinHeight + 8);
        StickerButton btn = btnGo.GetComponent<StickerButton>();
        var bso = new SerializedObject(btn);
        Set(bso, "seed", StickerMath.Seed("StartButton"));
        bso.ApplyModifiedPropertiesWithoutUndo();
        btnGo.GetComponentInChildren<TMP_Text>().text = "start";

        StartCard sc = cardRoot.AddComponent<StartCard>();
        var so = new SerializedObject(sc);
        Set(so, "group", group);
        Set(so, "card", card.transform);
        Set(so, "title", title);
        Set(so, "startButton", btn);
        so.ApplyModifiedPropertiesWithoutUndo();

        Boot boot = UnityEngine.Object.FindFirstObjectByType<Boot>();
        if (boot == null) s_Summary.warnings.Add("no Boot component in the Boot scene");
        else
        {
            var bootSo = new SerializedObject(boot);
            SerializedProperty p = bootSo.FindProperty("startCard");
            if (p == null) s_Summary.warnings.Add("Boot has no 'startCard' field yet: apply the Boot change from the 2.2 hand-back, then run BuildAll again");
            else { p.objectReferenceValue = sc; bootSo.ApplyModifiedPropertiesWithoutUndo(); s_Summary.wired.Add("Boot.startCard = StartCanvas/StartCard"); }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("could not save " + BootScenePath);
        s_Summary.wired.Add("Boot scene: StartCanvas added");
    }

    static void AddEventSystem()
    {
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    static GameObject MakeCanvas(string name, int order)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = order;
        CanvasScaler sc = go.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = Reference;
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return go;
    }

    static void AddToBuildList(string path)
    {
        var list = EditorBuildSettings.scenes.ToList();
        if (list.Any(s => s.path == path)) { s_Summary.wired.Add("build list already has " + path); return; }
        list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
        s_Summary.wired.Add("build list: " + string.Join(", ", list.Select(s => s.path)));
    }

    // ------------------------------------------------------------------ helpers

    static GameObject NewUi(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    static void Stretch(GameObject go, float inset)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
    }

    static void Set(SerializedObject so, string field, UnityEngine.Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null) throw new Exception(so.targetObject.GetType().Name + " has no serialized field '" + field + "'");
        p.objectReferenceValue = value;
    }

    static void Set(SerializedObject so, string field, int value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null) throw new Exception(so.targetObject.GetType().Name + " has no serialized field '" + field + "'");
        p.intValue = value;
    }
}
