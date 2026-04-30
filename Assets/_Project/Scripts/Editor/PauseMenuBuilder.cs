// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Pause Menu Builder
//  Usage: top menu → Facility Breach → Build Pause Menu
//
//  Builds a complete pause-menu overlay into the current scene.
//  Visually consistent with the Main Menu's dark/cyan theme but simpler,
//  since this is an in-game overlay, not a full-screen menu.
//
//  Creates:
//    • Full-screen dimmed overlay with subtle FX
//    • Main pause panel (Resume / Settings / Restart / Main Menu)
//    • Settings sub-panel (Master + Music volume sliders)
//    • All button wiring via persistent listeners
//
//  Does NOT create a separate scene — adds to whatever scene is open.
//  Run this with sayedhussainscene open.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public static class PauseMenuBuilder
{
    // ── COLOURS (same palette as Main Menu) ──────────────────────────────────
    static readonly Color C_OVERLAY    = new Color(0.02f, 0.04f, 0.07f, 0.82f);
    static readonly Color C_PANEL_BG   = new Color(0.02f, 0.04f, 0.08f, 0.94f);
    static readonly Color C_CYAN       = new Color32(0x00, 0xC8, 0xFF, 0xFF);
    static readonly Color C_CYAN_DIM   = new Color(0f, 0.78f, 1f, 0.35f);
    static readonly Color C_WHITE      = Color.white;
    static readonly Color C_GRAY       = new Color32(0x88, 0x88, 0x95, 0xFF);
    static readonly Color C_BTN_BG     = new Color(0f, 0f, 0f, 0.50f);
    static readonly Color C_DANGER     = new Color32(0xFF, 0x60, 0x60, 0xFF);
    static readonly Color C_DANGER_DIM = new Color(1f, 0.30f, 0.30f, 0.35f);

    static AudioSource s_sfx;

    // ─────────────────────────────────────────────────────────────────────────
    //  ENTRY POINT
    // ─────────────────────────────────────────────────────────────────────────
    [MenuItem("Facility Breach/Build Pause Menu")]
    public static void Build()
    {
        // If there's already a PauseMenuCanvas, remove it to avoid duplicates
        var existing = GameObject.Find("PauseMenuCanvas");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
            Debug.Log("[PauseMenuBuilder] Removed old PauseMenuCanvas.");
        }

        // Create a shared SFX source (optional — hover/click sounds)
        var sfxGO = new GameObject("PauseSFX");
        s_sfx = sfxGO.AddComponent<AudioSource>();
        s_sfx.playOnAwake = false;

        // Ensure an EventSystem exists
        EnsureEventSystem();

        // ── Canvas ──────────────────────────────────────────────────────────
        var canvasGO = new GameObject("PauseMenuCanvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;   // render above gameplay UI
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Parent the SFX source under the canvas for tidiness
        sfxGO.transform.SetParent(canvasGO.transform, false);

        // ── Pause Root (the full-screen overlay that gets toggled) ───────────
        var pauseRoot = new GameObject("PauseRoot", typeof(RectTransform));
        pauseRoot.transform.SetParent(canvasGO.transform, false);
        Stretch(pauseRoot.GetComponent<RectTransform>());

        // Background dim + haze
        var dimBG = NewImage("DimBackground", pauseRoot.transform, C_OVERLAY);
        dimBG.raycastTarget = true;   // block clicks to gameplay behind the overlay
        Stretch(dimBG.rectTransform);

        // FX layer (particles / scanlines)
        var fxGO = new GameObject("PauseFX", typeof(RectTransform));
        fxGO.transform.SetParent(pauseRoot.transform, false);
        Stretch(fxGO.GetComponent<RectTransform>());
        var fx = fxGO.AddComponent<PauseBackgroundFX>();
        fx.hazeImage = dimBG;

        // Top accent line
        var topLine = NewImage("TopAccent", pauseRoot.transform, C_CYAN);
        var tlRT = topLine.rectTransform;
        tlRT.anchorMin = new Vector2(0, 1);
        tlRT.anchorMax = new Vector2(1, 1);
        tlRT.pivot     = new Vector2(0.5f, 1);
        tlRT.anchoredPosition = Vector2.zero;
        tlRT.sizeDelta = new Vector2(0, 2);

        // Bottom accent line
        var botLine = NewImage("BottomAccent", pauseRoot.transform, C_CYAN_DIM);
        var blRT = botLine.rectTransform;
        blRT.anchorMin = new Vector2(0, 0);
        blRT.anchorMax = new Vector2(1, 0);
        blRT.pivot     = new Vector2(0.5f, 0);
        blRT.anchoredPosition = Vector2.zero;
        blRT.sizeDelta = new Vector2(0, 2);

        // ── "PAUSED" title ──────────────────────────────────────────────────
        var title = NewText("PausedTitle", pauseRoot.transform, "PAUSED", 72, C_WHITE);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 12;
        title.outlineWidth = 0.15f;
        title.outlineColor = C_CYAN;
        var titleRT = title.rectTransform;
        titleRT.anchorMin = new Vector2(0.5f, 1);
        titleRT.anchorMax = new Vector2(0.5f, 1);
        titleRT.pivot     = new Vector2(0.5f, 1);
        titleRT.anchoredPosition = new Vector2(0, -140);
        titleRT.sizeDelta = new Vector2(600, 90);
        title.alignment = TextAlignmentOptions.Center;

        // Subtle subtitle
        var subtitle = NewText("PausedSubtitle", pauseRoot.transform,
            "GAME PAUSED  ·  PRESS TAB TO RESUME", 16, C_GRAY);
        subtitle.characterSpacing = 8;
        var stRT = subtitle.rectTransform;
        stRT.anchorMin = new Vector2(0.5f, 1);
        stRT.anchorMax = new Vector2(0.5f, 1);
        stRT.pivot     = new Vector2(0.5f, 1);
        stRT.anchoredPosition = new Vector2(0, -228);
        stRT.sizeDelta = new Vector2(600, 24);
        subtitle.alignment = TextAlignmentOptions.Center;

        // ── Main Pause Panel (4 buttons) ────────────────────────────────────
        var mainPanel = BuildMainPanel(pauseRoot.transform,
            out Button btnResume, out Button btnSettings,
            out Button btnRestart, out Button btnMainMenu);

        // ── Settings Panel ──────────────────────────────────────────────────
        var settingsPanel = BuildSettingsPanel(pauseRoot.transform, out Button btnBackToPause);
        settingsPanel.SetActive(false);

        // ── Controller ──────────────────────────────────────────────────────
        var ctrl = canvasGO.AddComponent<PauseMenuController>();
        ctrl.pauseRoot         = pauseRoot;
        ctrl.mainPausePanel    = mainPanel;
        ctrl.settingsPanel     = settingsPanel;
        ctrl.mainMenuSceneName = "MainMenu";

        // Assign button references — controller wires onClick at runtime
        ctrl.resumeButton      = btnResume;
        ctrl.settingsButton    = btnSettings;
        ctrl.restartButton     = btnRestart;
        ctrl.mainMenuButton    = btnMainMenu;
        ctrl.backToPauseButton = btnBackToPause;

        // Start hidden
        pauseRoot.SetActive(false);

        // Mark scene dirty so the user can save
        EditorSceneManager.MarkSceneDirty(canvasGO.scene);

        Debug.Log("[PauseMenuBuilder] ✓ Pause Menu built successfully. Save your scene!");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  MAIN PAUSE PANEL (Resume / Settings / Restart / Main Menu)
    // ─────────────────────────────────────────────────────────────────────────
    static GameObject BuildMainPanel(Transform parent,
        out Button btnResume, out Button btnSettings,
        out Button btnRestart, out Button btnMainMenu)
    {
        var panel = new GameObject("MainPausePanel", typeof(RectTransform));
        panel.transform.SetParent(parent, false);
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, -40);
        rt.sizeDelta = new Vector2(520, 480);

        var vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 16;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = false;
        vlg.childAlignment         = TextAnchor.MiddleCenter;
        vlg.padding = new RectOffset(0, 0, 10, 10);

        btnResume   = NewMenuButton(panel.transform, "RESUME",    false);
        btnSettings = NewMenuButton(panel.transform, "SETTINGS",  false);
        btnRestart  = NewMenuButton(panel.transform, "RESTART",   false);
        btnMainMenu = NewMenuButton(panel.transform, "MAIN MENU", true);

        return panel;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  SETTINGS PANEL (Master Volume + Music Volume)
    // ─────────────────────────────────────────────────────────────────────────
    static GameObject BuildSettingsPanel(Transform parent, out Button btnBack)
    {
        var panel = NewPanel("PauseSettingsPanel", parent, new Vector2(580, 400));

        // Header
        var head = NewText("Header", panel.transform, "SETTINGS", 40, C_CYAN);
        head.fontStyle = FontStyles.Bold;
        head.characterSpacing = 6;
        var headRT = head.rectTransform;
        headRT.anchorMin = new Vector2(0, 1);
        headRT.anchorMax = new Vector2(1, 1);
        headRT.pivot     = new Vector2(0.5f, 1);
        headRT.anchoredPosition = new Vector2(0, -24);
        headRT.sizeDelta = new Vector2(0, 50);
        head.alignment = TextAlignmentOptions.Center;

        // Attach SettingsMenu component (reuse the existing one — it persists
        // settings via PlayerPrefs with the same FB_ keys as the main menu)
        var sm = panel.AddComponent<SettingsMenu>();
        sm.musicSource = null;   // no dedicated music source in pause; AudioListener.volume handles master

        // Sliders
        sm.masterSlider = AddSliderRow(panel.transform, "MASTER VOLUME", -100, out sm.masterValue);
        sm.musicSlider  = AddSliderRow(panel.transform, "MUSIC VOLUME",  -160, out sm.musicValue);
        sm.sfxSlider    = AddSliderRow(panel.transform, "SFX VOLUME",    -220, out sm.sfxValue);

        // Note: sensitivity slider is omitted to keep pause settings minimal.
        // The full settings are available in the Main Menu.

        // Back button
        btnBack = NewMenuButton(panel.transform, "BACK", false);
        var brt = btnBack.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0);
        brt.anchorMax = new Vector2(0.5f, 0);
        brt.pivot     = new Vector2(0.5f, 0);
        brt.anchoredPosition = new Vector2(0, 28);
        brt.sizeDelta = new Vector2(280, 70);

        return panel;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  UI FACTORY HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Creates a styled button matching the Main Menu theme.</summary>
    /// <param name="danger">If true, uses red accent (for destructive actions like Main Menu).</param>
    static Button NewMenuButton(Transform parent, string label, bool danger)
    {
        var go = new GameObject(label + "_Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(480, 90);

        // Dark semi-transparent background (raycast ON so clicks register)
        var bg = NewImage("BG", go.transform, C_BTN_BG);
        bg.raycastTarget = true;
        Stretch(bg.rectTransform);

        // Left accent bar (cyan or red)
        var accent = NewImage("Accent", go.transform, danger ? C_DANGER_DIM : C_CYAN_DIM);
        var aRT = accent.rectTransform;
        aRT.anchorMin = new Vector2(0, 0);
        aRT.anchorMax = new Vector2(0, 1);
        aRT.pivot     = new Vector2(0, 0.5f);
        aRT.anchoredPosition = Vector2.zero;
        aRT.sizeDelta = new Vector2(6, 0);

        // Button text
        var txt = NewText("Label", go.transform, label, 34, C_WHITE);
        txt.fontStyle = FontStyles.Bold;
        txt.characterSpacing = 6;
        var trt = txt.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(30, 0);
        trt.offsetMax = new Vector2(-10, 0);
        txt.alignment = TextAlignmentOptions.Left;

        // Button component — explicit navigation off to avoid conflicts
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };

        // Proper color tint so the button visually responds to clicks
        var colors = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor     = new Color(0.7f, 0.7f, 0.7f, 1f);
        colors.selectedColor    = Color.white;
        colors.disabledColor    = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        colors.fadeDuration     = 0.08f;
        btn.colors = colors;

        // Hover FX (reuse the same ButtonHoverFX as the Main Menu)
        var hfx = go.AddComponent<ButtonHoverFX>();
        hfx.target     = rt;
        hfx.accentBar  = accent;
        hfx.background = bg;
        hfx.sfxSource  = s_sfx;
        hfx.hoverScale = 1.03f;

        if (danger)
        {
            hfx.accentNormal = C_DANGER_DIM;
            hfx.accentHover  = C_DANGER;
        }

        return btn;
    }

    /// <summary>Creates a bordered panel (same style as Main Menu sub-panels).</summary>
    static GameObject NewPanel(string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, -20);
        rt.sizeDelta = size;

        var bg = go.AddComponent<Image>();
        bg.color = C_PANEL_BG;

        // Cyan border lines (top, bottom, left, right)
        AddBorderLine(rt, new Vector2(0,1), new Vector2(1,1), new Vector2(0.5f,1), new Vector2(0,-1), new Vector2(0,2));
        AddBorderLine(rt, new Vector2(0,0), new Vector2(1,0), new Vector2(0.5f,0), new Vector2(0, 1), new Vector2(0,2));
        AddBorderLine(rt, new Vector2(0,0), new Vector2(0,1), new Vector2(0,0.5f), new Vector2( 1,0), new Vector2(2,0));
        AddBorderLine(rt, new Vector2(1,0), new Vector2(1,1), new Vector2(1,0.5f), new Vector2(-1,0), new Vector2(2,0));

        return go;
    }

    static void AddBorderLine(RectTransform parent, Vector2 aMin, Vector2 aMax,
                               Vector2 piv, Vector2 pos, Vector2 size)
    {
        var line = NewImage("Border", parent, C_CYAN);
        var rt = line.rectTransform;
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = piv;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    /// <summary>Creates a slider row with label + slider + value text.</summary>
    static Slider AddSliderRow(Transform parent, string label, float yPos,
                                out TextMeshProUGUI valueLabel)
    {
        var row = new GameObject(label + "_Row", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var rrt = row.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0.5f, 1);
        rrt.anchorMax = new Vector2(0.5f, 1);
        rrt.pivot     = new Vector2(0.5f, 1);
        rrt.anchoredPosition = new Vector2(0, yPos);
        rrt.sizeDelta = new Vector2(500, 48);

        // Label
        var lbl = NewText("Label", row.transform, label, 20, C_WHITE);
        var lrt = lbl.rectTransform;
        lrt.anchorMin = new Vector2(0, 0.5f);
        lrt.anchorMax = new Vector2(0, 0.5f);
        lrt.pivot     = new Vector2(0, 0.5f);
        lrt.anchoredPosition = Vector2.zero;
        lrt.sizeDelta = new Vector2(220, 28);
        lbl.alignment = TextAlignmentOptions.Left;
        lbl.characterSpacing = 3;

        // Slider
        var slider = CreateSlider(row.transform);
        var srt = slider.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0, 0.5f);
        srt.anchorMax = new Vector2(0, 0.5f);
        srt.pivot     = new Vector2(0, 0.5f);
        srt.anchoredPosition = new Vector2(220, 0);
        srt.sizeDelta = new Vector2(220, 16);

        // Value label
        valueLabel = NewText("Value", row.transform, "100%", 18, C_CYAN);
        var vrt = valueLabel.rectTransform;
        vrt.anchorMin = new Vector2(0, 0.5f);
        vrt.anchorMax = new Vector2(0, 0.5f);
        vrt.pivot     = new Vector2(0, 0.5f);
        vrt.anchoredPosition = new Vector2(450, 0);
        vrt.sizeDelta = new Vector2(55, 28);
        valueLabel.alignment = TextAlignmentOptions.Left;

        return slider;
    }

    /// <summary>Creates a styled slider (same look as Main Menu sliders).</summary>
    static Slider CreateSlider(Transform parent)
    {
        var sliderGO = new GameObject("Slider", typeof(RectTransform));
        sliderGO.transform.SetParent(parent, false);

        // Track background
        var bg = NewImage("Background", sliderGO.transform, new Color(1, 1, 1, 0.12f));
        Stretch(bg.rectTransform);

        // Fill area
        var fillArea = new GameObject("FillArea", typeof(RectTransform));
        fillArea.transform.SetParent(sliderGO.transform, false);
        var faRT = fillArea.GetComponent<RectTransform>();
        faRT.anchorMin = Vector2.zero; faRT.anchorMax = Vector2.one;
        faRT.offsetMin = new Vector2(6, 0); faRT.offsetMax = new Vector2(-6, 0);

        var fill = NewImage("Fill", fillArea.transform, C_CYAN);
        Stretch(fill.rectTransform);

        // Handle area
        var handleArea = new GameObject("HandleArea", typeof(RectTransform));
        handleArea.transform.SetParent(sliderGO.transform, false);
        var haRT = handleArea.GetComponent<RectTransform>();
        haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one;
        haRT.offsetMin = new Vector2(6, 0); haRT.offsetMax = new Vector2(-6, 0);

        var handle = NewImage("Handle", handleArea.transform, C_WHITE);
        var hRT = handle.rectTransform;
        hRT.sizeDelta = new Vector2(16, 26);

        // Slider component
        var slider = sliderGO.AddComponent<Slider>();
        slider.fillRect      = fill.rectTransform;
        slider.handleRect    = hRT;
        slider.targetGraphic = handle;
        slider.direction     = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value    = 1f;

        return slider;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  LOW-LEVEL HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static TextMeshProUGUI NewText(string name, Transform parent, string text,
                                    float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.enableWordWrapping = true;
        t.raycastTarget = false;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();

        // Use new Input System module if available (Unity 6 requirement)
        var newType = System.Type.GetType(
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (newType != null) go.AddComponent(newType);
        else go.AddComponent<StandaloneInputModule>();
    }
}
