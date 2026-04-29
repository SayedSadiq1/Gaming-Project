// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Main Menu Builder
//  Usage: top menu → Facility Breach → Build Main Menu
//  Output: Assets/_Project/Scenes/MainMenu.unity
//
//  Black-Ops-3-style dark/cyan menu, including:
//    • Animated background (rising particles, scanlines, pulsing haze)
//    • Continue / New Game / Settings / Credits / Quit
//    • Settings panel (volume, music, sfx, sensitivity, controls, reset)
//    • Controls panel (rebind every key, persists via PlayerPrefs)
//    • Credits panel (Level creators + assets)
//
//  Auto-adds MainMenu + Test-Scene to Build Settings.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;
using System.Collections.Generic;

public static class MainMenuBuilder
{
    static readonly Color C_BG_DARK   = new Color32(0x05, 0x08, 0x10, 0xFF);
    static readonly Color C_BG_HAZE   = new Color32(0x0A, 0x18, 0x28, 0xCC);
    static readonly Color C_CYAN      = new Color32(0x00, 0xC8, 0xFF, 0xFF);
    static readonly Color C_CYAN_DIM  = new Color(0f, 0.78f, 1f, 0.35f);
    static readonly Color C_WHITE     = Color.white;
    static readonly Color C_GRAY      = new Color32(0x88, 0x88, 0x95, 0xFF);
    static readonly Color C_BTN_BG    = new Color(0f, 0f, 0f, 0.45f);
    static readonly Color C_PANEL_BG  = new Color(0.02f, 0.04f, 0.08f, 0.92f);
    static readonly Color C_DANGER    = new Color32(0xFF, 0x4D, 0x4D, 0xFF);

    static AudioSource s_sfx;
    static AudioSource s_music;
    static MainMenuController s_ctrl;

    [MenuItem("Facility Breach/Build Main Menu")]
    public static void Build()
    {
        EnsureFolder("Assets/_Project", "Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BuildCamera();
        BuildEventSystem();
        BuildAudio(out s_music, out s_sfx);

        var canvas = BuildCanvas();
        var hazeImage = BuildBackground(canvas.transform);
        BuildBackgroundFX(canvas.transform, hazeImage);
        BuildTitle(canvas.transform);

        // Main panel + buttons
        var mainPanel = BuildMainPanel(canvas.transform,
            out Button btnContinue, out Button btnPlay, out Button btnSettings,
            out Button btnCredits, out Button btnQuit);

        // Controller (created BEFORE sub-panels so back-buttons can wire to it)
        var ctrlGO = new GameObject("MainMenuController");
        s_ctrl = ctrlGO.AddComponent<MainMenuController>();
        s_ctrl.mainPanel       = mainPanel;
        s_ctrl.continueButton  = btnContinue;
        s_ctrl.level1SceneName = "Test-Scene";

        var settingsPanel = BuildSettingsPanel(canvas.transform);
        var controlsPanel = BuildControlsPanel(canvas.transform);
        var creditsPanel  = BuildCreditsPanel(canvas.transform);
        settingsPanel.SetActive(false);
        controlsPanel.SetActive(false);
        creditsPanel.SetActive(false);

        s_ctrl.settingsPanel = settingsPanel;
        s_ctrl.controlsPanel = controlsPanel;
        s_ctrl.creditsPanel  = creditsPanel;

        // PERSISTENT listeners — runtime AddListener is wiped on scene save
        UnityEventTools.AddPersistentListener(btnContinue.onClick, s_ctrl.OnContinue);
        UnityEventTools.AddPersistentListener(btnPlay.onClick,     s_ctrl.OnPlay);
        UnityEventTools.AddPersistentListener(btnSettings.onClick, s_ctrl.OnSettings);
        UnityEventTools.AddPersistentListener(btnCredits.onClick,  s_ctrl.OnCredits);
        UnityEventTools.AddPersistentListener(btnQuit.onClick,     s_ctrl.OnQuit);

        // Save & build settings
        string scenePath = "Assets/_Project/Scenes/MainMenu.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        AddScenesToBuildSettings(scenePath, "Assets/_Project/Scenes/Test-Scene.unity");

        Debug.Log("[MainMenuBuilder] Done — scene saved at " + scenePath);
    }

    // ── CAMERA ────────────────────────────────────────────────────────────
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
        AttachInputModule(go);
    }

    static void AttachInputModule(GameObject go)
    {
        var newType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (newType != null) go.AddComponent(newType);
        else go.AddComponent<StandaloneInputModule>();
    }

    [MenuItem("Facility Breach/Fix Main Menu Input")]
    static void FixInput()
    {
        var es = Object.FindFirstObjectByType<EventSystem>();
        if (es == null) { Debug.LogError("[MainMenuBuilder] No EventSystem in scene."); return; }
        foreach (var mod in es.GetComponents<BaseInputModule>()) Object.DestroyImmediate(mod);
        AttachInputModule(es.gameObject);
        EditorSceneManager.MarkSceneDirty(es.gameObject.scene);
        Debug.Log("[MainMenuBuilder] Input module fixed. Save & Play.");
    }

    // ── AUDIO ─────────────────────────────────────────────────────────────
    static void BuildAudio(out AudioSource music, out AudioSource sfx)
    {
        var musicGO = new GameObject("Music");
        music = musicGO.AddComponent<AudioSource>();
        music.playOnAwake = false; music.loop = true; music.volume = 0f;
        musicGO.AddComponent<MainMenuMusic>().source = music;

        var sfxGO = new GameObject("SFX");
        sfx = sfxGO.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
    }

    // ── CANVAS ────────────────────────────────────────────────────────────
    static Canvas BuildCanvas()
    {
        var go     = new GameObject("MainMenuCanvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler        = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    // ── BACKGROUND (returns the haze Image so FX can pulse it) ───────────
    static Image BuildBackground(Transform parent)
    {
        var bg = NewImage("Background", parent, C_BG_DARK);
        Stretch(bg.rectTransform);

        var haze = NewImage("BG_Haze", parent, C_BG_HAZE);
        Stretch(haze.rectTransform);

        var topLine = NewImage("TopAccentLine", parent, C_CYAN);
        var rt = topLine.rectTransform;
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot     = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, -8);
        rt.sizeDelta        = new Vector2(0, 2);

        var botLine = NewImage("BottomAccentLine", parent, C_CYAN_DIM);
        var rt2 = botLine.rectTransform;
        rt2.anchorMin = new Vector2(0, 0);
        rt2.anchorMax = new Vector2(1, 0);
        rt2.pivot     = new Vector2(0.5f, 0);
        rt2.anchoredPosition = new Vector2(0, 8);
        rt2.sizeDelta        = new Vector2(0, 2);

        var ft = NewText("FooterText", parent, "FACILITY BREACH  ·  SPRINT BUILD", 18, C_GRAY);
        var ftRT = ft.rectTransform;
        ftRT.anchorMin = new Vector2(1, 0);
        ftRT.anchorMax = new Vector2(1, 0);
        ftRT.pivot     = new Vector2(1, 0);
        ftRT.anchoredPosition = new Vector2(-40, 30);
        ftRT.sizeDelta        = new Vector2(500, 30);
        ft.alignment = TextAlignmentOptions.Right;
        ft.characterSpacing = 8;

        return haze;
    }

    // ── BACKGROUND FX (animated particles + scanlines + haze pulse) ──────
    static void BuildBackgroundFX(Transform parent, Image haze)
    {
        var go = new GameObject("BackgroundFX", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());
        go.transform.SetSiblingIndex(2);   // above background, below UI panels

        var fx = go.AddComponent<MenuBackgroundFX>();
        fx.hazeImage = haze;
    }

    // ── TITLE ─────────────────────────────────────────────────────────────
    static void BuildTitle(Transform parent)
    {
        var title = NewText("Title", parent, "FACILITY BREACH", 110, C_WHITE);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 8;
        title.outlineWidth = 0.18f;
        title.outlineColor = C_CYAN;
        var rt = title.rectTransform;
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot     = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(120, -100);
        rt.sizeDelta        = new Vector2(1200, 140);
        title.alignment = TextAlignmentOptions.Left;

        var sub = NewText("Subtitle", parent, "TACTICAL OPS  ·  CLASSIFIED", 22, C_CYAN);
        sub.characterSpacing = 14;
        var rt2 = sub.rectTransform;
        rt2.anchorMin = new Vector2(0, 1);
        rt2.anchorMax = new Vector2(0, 1);
        rt2.pivot     = new Vector2(0, 1);
        rt2.anchoredPosition = new Vector2(125, -230);
        rt2.sizeDelta        = new Vector2(900, 30);
        sub.alignment = TextAlignmentOptions.Left;
    }

    // ── MAIN PANEL  (5 buttons: Continue / New Game / Settings / Credits / Quit) ──
    static GameObject BuildMainPanel(Transform parent,
        out Button btnContinue, out Button btnPlay, out Button btnSettings,
        out Button btnCredits, out Button btnQuit)
    {
        var panel = new GameObject("MainPanel", typeof(RectTransform));
        panel.transform.SetParent(parent, false);
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0.5f);
        rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot     = new Vector2(0, 0.5f);
        rt.anchoredPosition = new Vector2(120, -80);
        rt.sizeDelta        = new Vector2(440, 460);

        var vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 14;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = false;

        btnContinue = NewMenuButton(panel.transform, "CONTINUE");
        btnPlay     = NewMenuButton(panel.transform, "NEW GAME");
        btnSettings = NewMenuButton(panel.transform, "SETTINGS");
        btnCredits  = NewMenuButton(panel.transform, "CREDITS");
        btnQuit     = NewMenuButton(panel.transform, "QUIT");

        return panel;
    }

    // ── SETTINGS PANEL ────────────────────────────────────────────────────
    static GameObject BuildSettingsPanel(Transform parent)
    {
        var panel = NewPanel("SettingsPanel", parent, new Vector2(740, 640));

        var head = NewText("Header", panel.transform, "SETTINGS", 48, C_CYAN);
        head.fontStyle = FontStyles.Bold;
        head.characterSpacing = 6;
        var headRT = head.rectTransform;
        headRT.anchorMin = new Vector2(0, 1);
        headRT.anchorMax = new Vector2(1, 1);
        headRT.pivot     = new Vector2(0.5f, 1);
        headRT.anchoredPosition = new Vector2(0, -28);
        headRT.sizeDelta        = new Vector2(0, 60);
        head.alignment = TextAlignmentOptions.Center;

        var sm = panel.AddComponent<SettingsMenu>();
        sm.musicSource = s_music;

        sm.masterSlider      = AddSliderRow(panel.transform, "MASTER VOLUME",     -110, out sm.masterValue);
        sm.musicSlider       = AddSliderRow(panel.transform, "MUSIC VOLUME",      -170, out sm.musicValue);
        sm.sfxSlider         = AddSliderRow(panel.transform, "SFX VOLUME",        -230, out sm.sfxValue);
        sm.sensitivitySlider = AddSliderRow(panel.transform, "MOUSE SENSITIVITY", -290, out sm.sensValue);
        sm.sensitivitySlider.minValue = 0.5f;
        sm.sensitivitySlider.maxValue = 3.0f;

        // ── Bottom row: CONTROLS  |  RESET  |  BACK ──
        var btnControls = NewMenuButton(panel.transform, "CONTROLS");
        ConfigBottomBtn(btnControls.GetComponent<RectTransform>(), -160f, 30f, new Vector2(220, 60));
        UnityEventTools.AddPersistentListener(btnControls.onClick, s_ctrl.OnControls);

        var btnReset = NewMenuButton(panel.transform, "RESET");
        ConfigBottomBtn(btnReset.GetComponent<RectTransform>(), 80f, 30f, new Vector2(160, 60));
        // Tint the accent bar red to mark it as destructive
        var fx = btnReset.GetComponent<ButtonHoverFX>();
        if (fx != null)
        {
            fx.accentNormal = new Color(1f, 0.30f, 0.30f, 0.4f);
            fx.accentHover  = C_DANGER;
        }
        UnityEventTools.AddPersistentListener(btnReset.onClick, sm.ResetAll);

        var btnBack = NewMenuButton(panel.transform, "BACK");
        ConfigBottomBtn(btnBack.GetComponent<RectTransform>(), 260f, 30f, new Vector2(160, 60));
        UnityEventTools.AddPersistentListener(btnBack.onClick, s_ctrl.OnBackToMain);

        return panel;
    }

    static void ConfigBottomBtn(RectTransform rt, float x, float y, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0);
        rt.anchorMax = new Vector2(0.5f, 0);
        rt.pivot     = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta        = size;
    }

    // ── CONTROLS PANEL  (rebind keys) ─────────────────────────────────────
    static GameObject BuildControlsPanel(Transform parent)
    {
        var panel = NewPanel("ControlsPanel", parent, new Vector2(720, 760));

        var head = NewText("Header", panel.transform, "CONTROLS", 48, C_CYAN);
        head.fontStyle = FontStyles.Bold;
        head.characterSpacing = 6;
        var headRT = head.rectTransform;
        headRT.anchorMin = new Vector2(0, 1);
        headRT.anchorMax = new Vector2(1, 1);
        headRT.pivot     = new Vector2(0.5f, 1);
        headRT.anchoredPosition = new Vector2(0, -28);
        headRT.sizeDelta        = new Vector2(0, 60);
        head.alignment = TextAlignmentOptions.Center;

        var hint = NewText("Hint", panel.transform, "Click a key to rebind  ·  ESC to cancel", 18, C_GRAY);
        var hRT = hint.rectTransform;
        hRT.anchorMin = new Vector2(0, 1);
        hRT.anchorMax = new Vector2(1, 1);
        hRT.pivot     = new Vector2(0.5f, 1);
        hRT.anchoredPosition = new Vector2(0, -90);
        hRT.sizeDelta        = new Vector2(0, 24);
        hint.alignment = TextAlignmentOptions.Center;
        hint.characterSpacing = 4;

        var ctrl = panel.AddComponent<ControlsBindingMenu>();

        // Build one row per action (12 actions, 40px each = 480px)
        float startY = -140f;
        float rowH   = 44f;
        for (int i = 0; i < KeyBindings.ActionOrder.Length; i++)
        {
            string action = KeyBindings.ActionOrder[i];
            string label  = KeyBindings.Display[action];
            float y = startY - i * rowH;

            AddBindingRow(panel.transform, action, label, y, ctrl);
        }

        // Bottom: BACK + RESET CONTROLS
        var btnBack = NewMenuButton(panel.transform, "BACK");
        ConfigBottomBtn(btnBack.GetComponent<RectTransform>(), -110f, 28f, new Vector2(200, 56));
        UnityEventTools.AddPersistentListener(btnBack.onClick, s_ctrl.OnBackToSettings);

        var btnResetCtrl = NewMenuButton(panel.transform, "RESET CONTROLS");
        ConfigBottomBtn(btnResetCtrl.GetComponent<RectTransform>(), 130f, 28f, new Vector2(260, 56));
        var fx = btnResetCtrl.GetComponent<ButtonHoverFX>();
        if (fx != null)
        {
            fx.accentNormal = new Color(1f, 0.30f, 0.30f, 0.4f);
            fx.accentHover  = C_DANGER;
        }
        UnityEventTools.AddPersistentListener(btnResetCtrl.onClick, ctrl.ResetControls);

        return panel;
    }

    static void AddBindingRow(Transform parent, string action, string display, float y, ControlsBindingMenu ctrl)
    {
        var row = new GameObject(action + "_Row", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var rrt = row.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0.5f, 1);
        rrt.anchorMax = new Vector2(0.5f, 1);
        rrt.pivot     = new Vector2(0.5f, 1);
        rrt.anchoredPosition = new Vector2(0, y);
        rrt.sizeDelta        = new Vector2(620, 40);

        // Action label (left)
        var lbl = NewText("Label", row.transform, display.ToUpper(), 20, C_WHITE);
        var lrt = lbl.rectTransform;
        lrt.anchorMin = new Vector2(0, 0.5f);
        lrt.anchorMax = new Vector2(0, 0.5f);
        lrt.pivot     = new Vector2(0, 0.5f);
        lrt.anchoredPosition = new Vector2(0, 0);
        lrt.sizeDelta        = new Vector2(320, 30);
        lbl.alignment = TextAlignmentOptions.Left;
        lbl.characterSpacing = 4;

        // Key button (right)
        var btnGO = new GameObject("KeyBtn", typeof(RectTransform));
        btnGO.transform.SetParent(row.transform, false);
        var brt = btnGO.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0, 0.5f);
        brt.anchorMax = new Vector2(0, 0.5f);
        brt.pivot     = new Vector2(0, 0.5f);
        brt.anchoredPosition = new Vector2(340, 0);
        brt.sizeDelta        = new Vector2(260, 36);

        var bg = NewImage("BG", btnGO.transform, new Color(0f, 0f, 0f, 0.5f));
        Stretch(bg.rectTransform);

        var accent = NewImage("Accent", btnGO.transform, C_CYAN_DIM);
        var aRT = accent.rectTransform;
        aRT.anchorMin = new Vector2(0, 0);
        aRT.anchorMax = new Vector2(0, 1);
        aRT.pivot     = new Vector2(0, 0.5f);
        aRT.sizeDelta = new Vector2(4, 0);

        var keyLabel = NewText("KeyLabel", btnGO.transform, "—", 18, C_CYAN);
        var krt = keyLabel.rectTransform;
        krt.anchorMin = Vector2.zero; krt.anchorMax = Vector2.one;
        krt.offsetMin = new Vector2(12, 0); krt.offsetMax = new Vector2(-8, 0);
        keyLabel.alignment = TextAlignmentOptions.Left;
        keyLabel.fontStyle = FontStyles.Bold;

        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = bg;
        var fx = btnGO.AddComponent<ButtonHoverFX>();
        fx.target = brt; fx.accentBar = accent; fx.background = bg; fx.sfxSource = s_sfx;
        fx.hoverScale = 1.02f;

        ctrl.AddRow(action, btn, keyLabel);
    }

    // ── CREDITS PANEL ─────────────────────────────────────────────────────
    static GameObject BuildCreditsPanel(Transform parent)
    {
        var panel = NewPanel("CreditsPanel", parent, new Vector2(720, 620));

        var head = NewText("Header", panel.transform, "CREDITS", 48, C_CYAN);
        head.fontStyle = FontStyles.Bold;
        head.characterSpacing = 6;
        var headRT = head.rectTransform;
        headRT.anchorMin = new Vector2(0, 1);
        headRT.anchorMax = new Vector2(1, 1);
        headRT.pivot     = new Vector2(0.5f, 1);
        headRT.anchoredPosition = new Vector2(0, -28);
        headRT.sizeDelta        = new Vector2(0, 60);
        head.alignment = TextAlignmentOptions.Center;

        string body =
            "<b><color=#00C8FF>FACILITY BREACH</color></b>\n" +
            "<size=20>Y3S2 Game Development Project</size>\n\n" +
            "<b><color=#00C8FF>LEVEL CREATORS</color></b>\n" +
            "Level 1     Ali Manaf\n" +
            "Level 2     <color=#888888>(Available)</color>\n" +
            "Level 3     Ammar Rabeea\n" +
            "Level 4     Sayed Hussain\n" +
            "Level 5     Sayed Sadiq\n\n" +
            "<b><color=#00C8FF>ASSETS</color></b>\n" +
            "FPS Microgame — Unity Technologies\n" +
            "Low Poly Weapons VOL.1\n" +
            "Military Base Pack — Tiny Teacup Studio\n" +
            "HQ Rocks\n" +
            "Low Poly Insurgent — ArtStore3D\n" +
            "TextMesh Pro";

        var txt = NewText("CreditsBody", panel.transform, body, 22, C_WHITE);
        var rt = txt.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, 30);
        rt.sizeDelta        = new Vector2(620, 460);
        txt.alignment   = TextAlignmentOptions.Top;
        txt.lineSpacing = 6;

        var back = NewMenuButton(panel.transform, "BACK");
        ConfigBottomBtn(back.GetComponent<RectTransform>(), 0, 30, new Vector2(280, 60));
        UnityEventTools.AddPersistentListener(back.onClick, s_ctrl.OnBackToMain);

        return panel;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────────────

    static GameObject NewPanel(string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = size;

        var bg = go.AddComponent<Image>();
        bg.color = C_PANEL_BG;

        AddBorderLine(rt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -1), new Vector2(0, 2));
        AddBorderLine(rt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0,  1), new Vector2(0, 2));
        AddBorderLine(rt, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0,    0.5f), new Vector2( 1, 0), new Vector2(2, 0));
        AddBorderLine(rt, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1,    0.5f), new Vector2(-1, 0), new Vector2(2, 0));

        return go;
    }

    static void AddBorderLine(RectTransform parent, Vector2 aMin, Vector2 aMax, Vector2 piv, Vector2 pos, Vector2 size)
    {
        var line = NewImage("Border", parent, C_CYAN);
        var rt = line.rectTransform;
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = piv;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static Slider AddSliderRow(Transform parent, string label, float yPos, out TextMeshProUGUI valueLabel)
    {
        var row = new GameObject(label + "_Row", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var rrt = row.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0.5f, 1);
        rrt.anchorMax = new Vector2(0.5f, 1);
        rrt.pivot     = new Vector2(0.5f, 1);
        rrt.anchoredPosition = new Vector2(0, yPos);
        rrt.sizeDelta        = new Vector2(640, 50);

        var lbl = NewText("Label", row.transform, label, 22, C_WHITE);
        var lrt = lbl.rectTransform;
        lrt.anchorMin = new Vector2(0, 0.5f); lrt.anchorMax = new Vector2(0, 0.5f);
        lrt.pivot = new Vector2(0, 0.5f);
        lrt.anchoredPosition = new Vector2(0, 0);
        lrt.sizeDelta        = new Vector2(290, 30);
        lbl.alignment = TextAlignmentOptions.Left;
        lbl.characterSpacing = 4;

        var slider = CreateSlider(row.transform);
        var srt = slider.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0, 0.5f); srt.anchorMax = new Vector2(0, 0.5f);
        srt.pivot = new Vector2(0, 0.5f);
        srt.anchoredPosition = new Vector2(290, 0);
        srt.sizeDelta        = new Vector2(280, 18);

        valueLabel = NewText("Value", row.transform, "100%", 20, C_CYAN);
        var vrt = valueLabel.rectTransform;
        vrt.anchorMin = new Vector2(0, 0.5f); vrt.anchorMax = new Vector2(0, 0.5f);
        vrt.pivot = new Vector2(0, 0.5f);
        vrt.anchoredPosition = new Vector2(580, 0);
        vrt.sizeDelta        = new Vector2(60, 30);
        valueLabel.alignment = TextAlignmentOptions.Left;

        return slider;
    }

    static Slider CreateSlider(Transform parent)
    {
        var sliderGO = new GameObject("Slider", typeof(RectTransform));
        sliderGO.transform.SetParent(parent, false);

        var bg = NewImage("Background", sliderGO.transform, new Color(1, 1, 1, 0.15f));
        Stretch(bg.rectTransform);

        var fillArea = new GameObject("FillArea", typeof(RectTransform));
        fillArea.transform.SetParent(sliderGO.transform, false);
        var faRT = fillArea.GetComponent<RectTransform>();
        faRT.anchorMin = new Vector2(0, 0); faRT.anchorMax = new Vector2(1, 1);
        faRT.offsetMin = new Vector2(8, 0); faRT.offsetMax = new Vector2(-8, 0);

        var fill = NewImage("Fill", fillArea.transform, C_CYAN);
        Stretch(fill.rectTransform);

        var handleArea = new GameObject("HandleArea", typeof(RectTransform));
        handleArea.transform.SetParent(sliderGO.transform, false);
        var haRT = handleArea.GetComponent<RectTransform>();
        haRT.anchorMin = new Vector2(0, 0); haRT.anchorMax = new Vector2(1, 1);
        haRT.offsetMin = new Vector2(8, 0); haRT.offsetMax = new Vector2(-8, 0);

        var handle = NewImage("Handle", handleArea.transform, C_WHITE);
        var hRT = handle.rectTransform;
        hRT.sizeDelta = new Vector2(18, 28);

        var slider = sliderGO.AddComponent<Slider>();
        slider.fillRect      = fill.rectTransform;
        slider.handleRect    = hRT;
        slider.targetGraphic = handle;
        slider.direction     = Slider.Direction.LeftToRight;
        slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;

        return slider;
    }

    static Button NewMenuButton(Transform parent, string label)
    {
        var go = new GameObject(label + "_Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(420, 70);

        var bg = NewImage("BG", go.transform, C_BTN_BG);
        Stretch(bg.rectTransform);

        var accent = NewImage("Accent", go.transform, C_CYAN_DIM);
        var aRT = accent.rectTransform;
        aRT.anchorMin = new Vector2(0, 0); aRT.anchorMax = new Vector2(0, 1);
        aRT.pivot     = new Vector2(0, 0.5f);
        aRT.anchoredPosition = new Vector2(0, 0);
        aRT.sizeDelta        = new Vector2(6, 0);

        var txt = NewText("Label", go.transform, label, 32, C_WHITE);
        txt.fontStyle = FontStyles.Bold;
        txt.characterSpacing = 6;
        var trt = txt.rectTransform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(28, 0); trt.offsetMax = new Vector2(-10, 0);
        txt.alignment = TextAlignmentOptions.Left;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;
        var fx = go.AddComponent<ButtonHoverFX>();
        fx.target = rt; fx.accentBar = accent; fx.background = bg; fx.sfxSource = s_sfx;

        return btn;
    }

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

    static void AddScenesToBuildSettings(params string[] paths)
    {
        var existing = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool changed = false;
        foreach (var p in paths)
        {
            if (string.IsNullOrEmpty(p)) continue;
            if (!System.IO.File.Exists(p)) continue;
            if (existing.Exists(s => s.path == p)) continue;
            if (p.EndsWith("MainMenu.unity")) existing.Insert(0, new EditorBuildSettingsScene(p, true));
            else                              existing.Add(new EditorBuildSettingsScene(p, true));
            changed = true;
        }
        if (changed) EditorBuildSettings.scenes = existing.ToArray();
    }
}
