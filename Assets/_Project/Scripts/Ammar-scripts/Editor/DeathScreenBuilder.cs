using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Death Screen Builder
//  Menu → Facility Breach → Build Death Screen
//
//  Generates Assets/_Project/Scenes/DeathScreen.unity with the same dark/cyan
//  style as the main menu, two buttons (PLAY AGAIN / MAIN MENU), and a
//  DeathScreenController wired with persistent listeners.
//
//  Replaces the FPS Microgame's default "LoseScene" — `LoseSceneRedirect`
//  patches GameFlowManager at runtime so death routes here.
// ─────────────────────────────────────────────────────────────────────────────
public static class DeathScreenBuilder
{
    static readonly Color C_BG_DARK   = new Color(0.02f, 0.03f, 0.06f, 1f);
    static readonly Color C_PANEL_BG  = new Color(0.02f, 0.04f, 0.08f, 0.92f);
    static readonly Color C_CYAN      = new Color32(0x00, 0xC8, 0xFF, 0xFF);
    static readonly Color C_CYAN_DIM  = new Color(0f, 0.78f, 1f, 0.35f);
    static readonly Color C_WHITE     = Color.white;
    static readonly Color C_GRAY      = new Color32(0x88, 0x88, 0x95, 0xFF);
    static readonly Color C_BTN_BG    = new Color(0f, 0f, 0f, 0.45f);
    static readonly Color C_DANGER    = new Color32(0xFF, 0x4D, 0x4D, 0xFF);

    [MenuItem("Facility Breach/Build Death Screen")]
    public static void Build()
    {
        EnsureFolder("Assets/_Project", "Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BuildCamera();
        BuildEventSystem();

        var canvas = BuildCanvas();
        BuildBackground(canvas.transform);
        BuildTitle(canvas.transform);
        var lastLevelLabel = BuildLastLevelLabel(canvas.transform);

        // Buttons row
        var btnRow = new GameObject("ButtonRow", typeof(RectTransform));
        btnRow.transform.SetParent(canvas.transform, false);
        var brRT = btnRow.GetComponent<RectTransform>();
        brRT.anchorMin = new Vector2(0.5f, 0.5f);
        brRT.anchorMax = new Vector2(0.5f, 0.5f);
        brRT.pivot     = new Vector2(0.5f, 0.5f);
        brRT.anchoredPosition = new Vector2(0, -80);
        brRT.sizeDelta        = new Vector2(960, 100);

        var btnPlay = BuildButton(btnRow.transform, "PLAY AGAIN", new Vector2(-220, 0));
        var btnMenu = BuildButton(btnRow.transform, "MAIN MENU",  new Vector2( 220, 0));

        // Controller + wiring (PERSISTENT listeners so they survive scene save)
        var ctrlGO = new GameObject("DeathScreenController");
        var ctrl   = ctrlGO.AddComponent<DeathScreenController>();
        ctrl.playAgainButton = btnPlay;
        ctrl.mainMenuButton  = btnMenu;
        ctrl.lastLevelLabel  = lastLevelLabel;
        ctrl.fallbackScene   = "Test-Scene";

        UnityEventTools.AddPersistentListener(btnPlay.onClick, ctrl.OnPlayAgain);
        UnityEventTools.AddPersistentListener(btnMenu.onClick, ctrl.OnMainMenu);

        // Save scene + add to Build Settings
        string scenePath = "Assets/_Project/Scenes/DeathScreen.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        AddSceneToBuildSettings(scenePath);

        Debug.Log("[DeathScreenBuilder] ✓ Done — scene saved at " + scenePath);
    }

    // ── Camera ────────────────────────────────────────────────────────────
    static void BuildCamera()
    {
        var go  = new GameObject("Main Camera");
        var cam = go.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = C_BG_DARK;
        cam.orthographic    = true;
        cam.cullingMask     = 0;
        go.AddComponent<AudioListener>();
        go.tag = "MainCamera";
    }

    static void BuildEventSystem()
    {
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        var newType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (newType != null) go.AddComponent(newType);
        else go.AddComponent<StandaloneInputModule>();
    }

    // ── Canvas ────────────────────────────────────────────────────────────
    static Canvas BuildCanvas()
    {
        var go     = new GameObject("DeathScreenCanvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler        = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    // ── Background (matches main menu) ───────────────────────────────────
    static void BuildBackground(Transform parent)
    {
        var bg = NewImage("Background", parent, C_BG_DARK);
        Stretch(bg.rectTransform);

        // Red-tinted haze instead of cyan, signals "bad outcome"
        var haze = NewImage("BG_Haze", parent, new Color(0.18f, 0.04f, 0.05f, 0.65f));
        Stretch(haze.rectTransform);

        // Top accent (red instead of cyan)
        var topLine = NewImage("TopAccentLine", parent, C_DANGER);
        var rt = topLine.rectTransform;
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
        rt.pivot     = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, -8);
        rt.sizeDelta        = new Vector2(0, 2);

        var botLine = NewImage("BottomAccentLine", parent, new Color(1f, 0.30f, 0.30f, 0.35f));
        var rt2 = botLine.rectTransform;
        rt2.anchorMin = new Vector2(0, 0); rt2.anchorMax = new Vector2(1, 0);
        rt2.pivot     = new Vector2(0.5f, 0);
        rt2.anchoredPosition = new Vector2(0, 8);
        rt2.sizeDelta        = new Vector2(0, 2);

        var foot = NewText("Footer", parent, "FACILITY BREACH  ·  MISSION FAILED", 18, C_GRAY);
        var fRT = foot.rectTransform;
        fRT.anchorMin = new Vector2(1, 0); fRT.anchorMax = new Vector2(1, 0);
        fRT.pivot = new Vector2(1, 0);
        fRT.anchoredPosition = new Vector2(-40, 30);
        fRT.sizeDelta = new Vector2(500, 30);
        foot.alignment = TextAlignmentOptions.Right;
        foot.characterSpacing = 8;
    }

    // ── Title ─────────────────────────────────────────────────────────────
    static void BuildTitle(Transform parent)
    {
        var title = NewText("Title", parent, "YOU DIED", 130, C_WHITE);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 14;
        title.outlineWidth = 0.20f;
        title.outlineColor = C_DANGER;
        var rt = title.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 1); rt.anchorMax = new Vector2(0.5f, 1);
        rt.pivot     = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, -180);
        rt.sizeDelta        = new Vector2(1600, 160);
        title.alignment = TextAlignmentOptions.Center;

        var sub = NewText("Subtitle", parent, "MISSION FAILED  ·  AGENT KIA", 24, C_DANGER);
        sub.characterSpacing = 14;
        sub.fontStyle = FontStyles.Bold;
        var rt2 = sub.rectTransform;
        rt2.anchorMin = new Vector2(0.5f, 1); rt2.anchorMax = new Vector2(0.5f, 1);
        rt2.pivot     = new Vector2(0.5f, 1);
        rt2.anchoredPosition = new Vector2(0, -340);
        rt2.sizeDelta        = new Vector2(1400, 30);
        sub.alignment = TextAlignmentOptions.Center;
    }

    static TextMeshProUGUI BuildLastLevelLabel(Transform parent)
    {
        var lbl = NewText("LastLevelLabel", parent, "Last level: <color=#00C8FF>—</color>", 26, C_WHITE);
        lbl.characterSpacing = 4;
        var rt = lbl.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, 60);
        rt.sizeDelta        = new Vector2(1200, 40);
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.richText  = true;
        return lbl;
    }

    // ── Button (same look as main menu) ───────────────────────────────────
    static Button BuildButton(Transform parent, string label, Vector2 anchoredPos)
    {
        var go = new GameObject(label + "_Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(380, 80);

        var bg = NewImage("BG", go.transform, C_BTN_BG);
        Stretch(bg.rectTransform);

        var accent = NewImage("Accent", go.transform, C_CYAN_DIM);
        var aRT = accent.rectTransform;
        aRT.anchorMin = new Vector2(0, 0); aRT.anchorMax = new Vector2(0, 1);
        aRT.pivot = new Vector2(0, 0.5f);
        aRT.sizeDelta = new Vector2(6, 0);

        var txt = NewText("Label", go.transform, label, 30, C_WHITE);
        txt.fontStyle = FontStyles.Bold;
        txt.characterSpacing = 6;
        var trt = txt.rectTransform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(28, 0); trt.offsetMax = new Vector2(-10, 0);
        txt.alignment = TextAlignmentOptions.Left;
        txt.raycastTarget = false;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;
        var fx = go.AddComponent<ButtonHoverFX>();
        fx.target = rt; fx.accentBar = accent; fx.background = bg;
        return btn;
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }
    static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = color;
        t.enableWordWrapping = true;
        return t;
    }
    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
    static void EnsureFolder(string parent, string folder)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + folder))
            AssetDatabase.CreateFolder(parent, folder);
    }
    static void AddSceneToBuildSettings(string path)
    {
        var existing = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (existing.Exists(s => s.path == path)) return;
        existing.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = existing.ToArray();
    }
}
