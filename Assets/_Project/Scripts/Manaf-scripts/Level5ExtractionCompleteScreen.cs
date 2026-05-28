using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Level 5 Extraction Complete Screen
//
//  Final-level end screen. Unlike the shared MissionCompleteScreen (one
//  "NEXT LEVEL" button), this has a single "RETURN TO MAIN MENU" button since
//  Level 5 is the last mission.
//
//  Style mirrors MissionCompleteScreen (dark / cyan) so it matches the rest of
//  the game. Spawned at runtime, DontDestroyOnLoad, top-most canvas.
//
//      Level5ExtractionCompleteScreen.Show("MISSION COMPLETE", "GHOST ESCAPED", stats);
// ─────────────────────────────────────────────────────────────────────────────
public class Level5ExtractionCompleteScreen : MonoBehaviour
{
    static Level5ExtractionCompleteScreen _instance;

    static readonly Color C_BG_DARK  = new Color(0.02f, 0.03f, 0.06f, 1f);
    static readonly Color C_PANEL_BG = new Color(0.02f, 0.04f, 0.08f, 0.92f);
    static readonly Color C_CYAN     = new Color32(0x00, 0xC8, 0xFF, 0xFF);
    static readonly Color C_CYAN_DIM = new Color(0f, 0.78f, 1f, 0.35f);
    static readonly Color C_WHITE    = Color.white;
    static readonly Color C_BTN_BG   = new Color(0f, 0f, 0f, 0.45f);
    static readonly Color C_GRAY     = new Color32(0x88, 0x88, 0x95, 0xFF);

    CanvasGroup     _group;
    TextMeshProUGUI _titleText;
    TextMeshProUGUI _headlineText;
    TextMeshProUGUI _statsText;

    public static void Show(string title, string headline, string stats, float fadeDuration = 1.8f)
    {
        EnsureInstance();
        _instance.StartCoroutine(_instance.ShowRoutine(title, headline, stats, fadeDuration));
    }

    static void EnsureInstance()
    {
        if (_instance != null) return;
        EnsureEventSystem();
        var go = new GameObject("Level5ExtractionCompleteCanvas");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<Level5ExtractionCompleteScreen>();
        _instance.Build();
    }

    // Project runs on the new Input System only (activeInputHandler = 1). UI
    // clicks need an EventSystem with InputSystemUIInputModule — Level 5's scene
    // has none, so the button silently swallowed clicks. Guarantee a working one.
    static void EnsureEventSystem()
    {
        var es = Object.FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(go);
            return;
        }

        // A legacy StandaloneInputModule throws under the new Input System and
        // blocks pointer events — disable it and ensure the new module is present.
        var legacy = es.GetComponent<StandaloneInputModule>();
        if (legacy != null) legacy.enabled = false;
        if (es.GetComponent<InputSystemUIInputModule>() == null)
            es.gameObject.AddComponent<InputSystemUIInputModule>();
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha          = 0f;
        _group.blocksRaycasts = false;
        _group.interactable   = false;

        var bg = NewImage("Background", transform, C_BG_DARK);
        Stretch(bg.rectTransform);

        // Accent lines
        var topLine = NewImage("TopAccentLine", transform, C_CYAN);
        var tlRT = topLine.rectTransform;
        tlRT.anchorMin = new Vector2(0, 1); tlRT.anchorMax = new Vector2(1, 1);
        tlRT.pivot = new Vector2(0.5f, 1); tlRT.anchoredPosition = new Vector2(0, -8);
        tlRT.sizeDelta = new Vector2(0, 2);

        var botLine = NewImage("BottomAccentLine", transform, C_CYAN_DIM);
        var blRT = botLine.rectTransform;
        blRT.anchorMin = new Vector2(0, 0); blRT.anchorMax = new Vector2(1, 0);
        blRT.pivot = new Vector2(0.5f, 0); blRT.anchoredPosition = new Vector2(0, 8);
        blRT.sizeDelta = new Vector2(0, 2);

        // Footer
        var foot = NewText("Footer", transform, "FACILITY BREACH  ·  EXTRACTION", 18, C_GRAY);
        var fRT = foot.rectTransform;
        fRT.anchorMin = new Vector2(1, 0); fRT.anchorMax = new Vector2(1, 0);
        fRT.pivot = new Vector2(1, 0);
        fRT.anchoredPosition = new Vector2(-40, 30);
        fRT.sizeDelta = new Vector2(560, 30);
        foot.alignment = TextAlignmentOptions.Right;
        foot.characterSpacing = 8;

        // Title
        var titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(transform, false);
        var tRT = titleGO.GetComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0.5f, 1); tRT.anchorMax = new Vector2(0.5f, 1);
        tRT.pivot = new Vector2(0.5f, 1);
        tRT.anchoredPosition = new Vector2(0, -140);
        tRT.sizeDelta = new Vector2(1600, 130);
        _titleText = titleGO.AddComponent<TextMeshProUGUI>();
        _titleText.text            = "MISSION COMPLETE";
        _titleText.fontSize        = 90;
        _titleText.fontStyle       = FontStyles.Bold;
        _titleText.alignment       = TextAlignmentOptions.Center;
        _titleText.color           = C_WHITE;
        _titleText.characterSpacing = 8;
        _titleText.outlineWidth    = 0.18f;
        _titleText.outlineColor    = C_CYAN;

        // Headline (e.g. "GHOST ESCAPED")
        var headGO = new GameObject("Headline", typeof(RectTransform));
        headGO.transform.SetParent(transform, false);
        var hRT = headGO.GetComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0.5f, 1); hRT.anchorMax = new Vector2(0.5f, 1);
        hRT.pivot = new Vector2(0.5f, 1);
        hRT.anchoredPosition = new Vector2(0, -270);
        hRT.sizeDelta = new Vector2(1400, 50);
        _headlineText = headGO.AddComponent<TextMeshProUGUI>();
        _headlineText.text            = "GHOST ESCAPED";
        _headlineText.fontSize        = 34;
        _headlineText.fontStyle       = FontStyles.Bold;
        _headlineText.alignment       = TextAlignmentOptions.Center;
        _headlineText.color           = C_CYAN;
        _headlineText.characterSpacing = 14;

        // Stats panel
        var statsPanel = NewPanel("StatsPanel", transform, new Vector2(760, 400));
        var spRT = statsPanel.GetComponent<RectTransform>();
        spRT.anchorMin = new Vector2(0.5f, 0.5f); spRT.anchorMax = new Vector2(0.5f, 0.5f);
        spRT.pivot = new Vector2(0.5f, 0.5f);
        spRT.anchoredPosition = new Vector2(0, -10);

        var statsGO = new GameObject("Stats", typeof(RectTransform));
        statsGO.transform.SetParent(statsPanel.transform, false);
        var stRT = statsGO.GetComponent<RectTransform>();
        stRT.anchorMin = Vector2.zero; stRT.anchorMax = Vector2.one;
        stRT.offsetMin = new Vector2(40, 30); stRT.offsetMax = new Vector2(-40, -30);
        _statsText = statsGO.AddComponent<TextMeshProUGUI>();
        _statsText.text            = "";
        _statsText.fontSize        = 30;
        _statsText.alignment       = TextAlignmentOptions.Center;
        _statsText.color           = C_WHITE;
        _statsText.richText        = true;
        _statsText.lineSpacing     = 10;
        _statsText.characterSpacing = 3;

        // Single centered RETURN TO MAIN MENU button
        var btnRow = new GameObject("ButtonRow", typeof(RectTransform));
        btnRow.transform.SetParent(transform, false);
        var brRT = btnRow.GetComponent<RectTransform>();
        brRT.anchorMin = new Vector2(0.5f, 0); brRT.anchorMax = new Vector2(0.5f, 0);
        brRT.pivot = new Vector2(0.5f, 0); brRT.anchoredPosition = new Vector2(0, 110);
        brRT.sizeDelta = new Vector2(520, 90);

        var menuBtn = BuildButton(btnRow.transform, "RETURN TO MAIN MENU", Vector2.zero);
        menuBtn.onClick.AddListener(OnMainMenu);
    }

    IEnumerator ShowRoutine(string title, string headline, string stats, float duration)
    {
        if (_titleText    != null) _titleText.text    = title    ?? "MISSION COMPLETE";
        if (_headlineText != null) _headlineText.text = headline ?? "";
        if (_statsText    != null) _statsText.text    = stats    ?? "";

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        Time.timeScale   = 1f;

        _group.blocksRaycasts = true;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Clamp01(t / duration);
            yield return null;
        }
        _group.alpha        = 1f;
        _group.interactable = true;
    }

    void OnMainMenu()
    {
        SaveSystem.ContinueRequested = false;
        _instance = null;
        Destroy(gameObject);
        LoadingScreen.Load("MainMenu");
    }

    // ── Helpers (mirrored from MissionCompleteScreen so the style is identical) ──
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
    static GameObject NewPanel(string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        var bg = go.AddComponent<Image>();
        bg.color = C_PANEL_BG;
        AddBorder(rt, new Vector2(0,1), new Vector2(1,1), new Vector2(0.5f,1), new Vector2(0,-1), new Vector2(0,2));
        AddBorder(rt, new Vector2(0,0), new Vector2(1,0), new Vector2(0.5f,0), new Vector2(0, 1), new Vector2(0,2));
        AddBorder(rt, new Vector2(0,0), new Vector2(0,1), new Vector2(0,0.5f), new Vector2( 1,0), new Vector2(2,0));
        AddBorder(rt, new Vector2(1,0), new Vector2(1,1), new Vector2(1,0.5f), new Vector2(-1,0), new Vector2(2,0));
        return go;
    }
    static void AddBorder(RectTransform parent, Vector2 aMin, Vector2 aMax, Vector2 piv, Vector2 pos, Vector2 size)
    {
        var line = NewImage("Border", parent, C_CYAN);
        var rt = line.rectTransform;
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = piv;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }
    static Button BuildButton(Transform parent, string label, Vector2 anchoredPos)
    {
        var go = new GameObject(label + "_Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(520, 80);

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
        // Built-in colour transition for hover (no external ButtonHoverFX dependency).
        var colors = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(0.6f, 0.92f, 1f, 1f);
        colors.pressedColor     = C_CYAN;
        colors.selectedColor    = new Color(0.6f, 0.92f, 1f, 1f);
        colors.fadeDuration     = 0.12f;
        btn.colors = colors;
        return btn;
    }
}
