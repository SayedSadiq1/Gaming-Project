using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Level 5 Objective Manager
//
//  Single-objective version of Sayed's Level1ObjectiveManager: same top-left
//  "OBJECTIVE" panel + centre completion banner + HUD counter, but Level 5 has
//  just one goal — reach the extraction helicopter and board it to win.
//
//  Completes when HelicopterExtraction.OnExtractionComplete fires (the player
//  finished the hold-E board). Self-installs on Level 5 load — no scene wiring.
// ─────────────────────────────────────────────────────────────────────────────
public class Level5ObjectiveManager : MonoBehaviour
{
    [Header("Objective")]
    public string objTitle = "REACH EXTRACTION";
    [TextArea(2, 4)]
    public string objDescription = "Get to the extraction helicopter.";

    [Header("HUD")]
    public CombatHUD combatHUD;
    public int totalObjectives = 1;

    [Header("Style")]
    public Color accentColor   = new Color(0f, 0.78f, 1f, 1f);
    public Color completeColor = new Color(0f, 1f, 0.55f, 1f);

    bool _done;

    CanvasGroup     _panelGroup;
    TextMeshProUGUI _titleText;
    TextMeshProUGUI _descText;
    TextMeshProUGUI _progressText;
    CanvasGroup     _notifGroup;
    TextMeshProUGUI _notifText;
    Image           _topLineImg;
    Image           _barImg;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Level5") return;
        if (Object.FindFirstObjectByType<Level5ObjectiveManager>() != null) return;
        new GameObject("Level5ObjectiveManager").AddComponent<Level5ObjectiveManager>();
    }

    void Start()
    {
        if (combatHUD == null) combatHUD = FindAnyObjectByType<CombatHUD>();
        combatHUD?.SetObjectives(0, totalObjectives);

        HelicopterExtraction.OnExtractionComplete += OnReachedHelicopter;

        BuildUI();
        ShowObjective(objTitle, objDescription, accentColor);
        if (_progressText != null) _progressText.text = "Get to the helicopter";
    }

    void OnDestroy()
    {
        HelicopterExtraction.OnExtractionComplete -= OnReachedHelicopter;
    }

    void OnReachedHelicopter()
    {
        if (_done) return;
        _done = true;
        StartCoroutine(CompleteSequence());
    }

    IEnumerator CompleteSequence()
    {
        combatHUD?.IncrementObjective();
        if (_progressText != null) _progressText.text = "<color=#00ff8c>Done</color>";

        ShowNotification("✓  OBJECTIVE COMPLETE", completeColor);
        yield return StartCoroutine(FadeGroup(_notifGroup, 0f, 1f, 0.3f));
        yield return new WaitForSeconds(2.5f);
        yield return StartCoroutine(FadeGroup(_notifGroup, 1f, 0f, 0.5f));

        // Hide the panel — objective done, end screen takes over.
        yield return StartCoroutine(FadeGroup(_panelGroup, 1f, 0f, 0.6f));
    }

    // ── UI (mirrors Level1ObjectiveManager so the style matches) ───────────────
    void ShowObjective(string title, string desc, Color accent)
    {
        if (_titleText != null) _titleText.text = title;
        if (_descText  != null) _descText.text  = desc;
        if (_topLineImg != null) _topLineImg.color = new Color(accent.r, accent.g, accent.b, 0.7f);
        if (_barImg != null) _barImg.color = accent;
    }

    void ShowNotification(string msg, Color color)
    {
        if (_notifText == null) return;
        _notifText.text  = msg;
        _notifText.color = color;
    }

    void BuildUI()
    {
        Canvas canvas = FindCanvas();

        var panel = MakeRect("L5ObjPanel", canvas.transform);
        _panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
        panel.anchorMin        = new Vector2(0f, 1f);
        panel.anchorMax        = new Vector2(0f, 1f);
        panel.pivot            = new Vector2(0f, 1f);
        panel.anchoredPosition = new Vector2(20f, -215f);
        panel.sizeDelta        = new Vector2(360f, 100f);
        panel.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.04f, 0.08f, 0.82f);

        _topLineImg = UIRect("TopLine", panel,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 2f),
            new Color(accentColor.r, accentColor.g, accentColor.b, 0.7f));

        _barImg = UIRect("Bar", panel,
            new Vector2(0f, 0.1f), new Vector2(0f, 0.9f),
            new Vector2(0f, 0.5f), Vector2.zero, new Vector2(4f, 0f),
            accentColor);

        var labelGO              = MakeRect("ObjLabel", panel);
        labelGO.anchorMin        = new Vector2(0f, 1f);
        labelGO.anchorMax        = new Vector2(1f, 1f);
        labelGO.pivot            = new Vector2(0f, 1f);
        labelGO.anchoredPosition = new Vector2(14f, -6f);
        labelGO.sizeDelta        = new Vector2(-14f, 18f);
        var labelTmp             = labelGO.gameObject.AddComponent<TextMeshProUGUI>();
        labelTmp.text            = "OBJECTIVE";
        labelTmp.fontSize        = 11f;
        labelTmp.fontStyle       = FontStyles.Bold;
        labelTmp.color           = new Color(accentColor.r, accentColor.g, accentColor.b, 0.9f);
        labelTmp.characterSpacing = 4f;
        labelTmp.alignment       = TextAlignmentOptions.BottomLeft;

        var titleGO              = MakeRect("Title", panel);
        titleGO.anchorMin        = new Vector2(0f, 1f);
        titleGO.anchorMax        = new Vector2(1f, 1f);
        titleGO.pivot            = new Vector2(0f, 1f);
        titleGO.anchoredPosition = new Vector2(14f, -26f);
        titleGO.sizeDelta        = new Vector2(-14f, 28f);
        _titleText               = titleGO.gameObject.AddComponent<TextMeshProUGUI>();
        _titleText.fontSize      = 18f;
        _titleText.fontStyle     = FontStyles.Bold;
        _titleText.color         = Color.white;
        _titleText.characterSpacing = 2f;
        _titleText.alignment     = TextAlignmentOptions.BottomLeft;

        var descGO               = MakeRect("Desc", panel);
        descGO.anchorMin         = new Vector2(0f, 1f);
        descGO.anchorMax         = new Vector2(1f, 1f);
        descGO.pivot             = new Vector2(0f, 1f);
        descGO.anchoredPosition  = new Vector2(14f, -56f);
        descGO.sizeDelta         = new Vector2(-14f, 30f);
        _descText                = descGO.gameObject.AddComponent<TextMeshProUGUI>();
        _descText.fontSize       = 13f;
        _descText.color          = new Color(0.8f, 0.8f, 0.8f, 1f);
        _descText.alignment      = TextAlignmentOptions.TopLeft;
        _descText.overflowMode   = TextOverflowModes.Ellipsis;
        _descText.enableWordWrapping = true;

        var progGO               = MakeRect("Progress", panel);
        progGO.anchorMin         = new Vector2(0f, 0f);
        progGO.anchorMax         = new Vector2(1f, 0f);
        progGO.pivot             = new Vector2(0f, 0f);
        progGO.anchoredPosition  = new Vector2(14f, 6f);
        progGO.sizeDelta         = new Vector2(-14f, 18f);
        _progressText            = progGO.gameObject.AddComponent<TextMeshProUGUI>();
        _progressText.fontSize   = 12f;
        _progressText.color      = new Color(0.6f, 0.6f, 0.6f, 1f);
        _progressText.alignment  = TextAlignmentOptions.BottomLeft;
        _progressText.richText   = true;

        // Notification banner — bottom-centre
        var notif          = MakeRect("L5ObjNotif", canvas.transform);
        _notifGroup        = notif.gameObject.AddComponent<CanvasGroup>();
        _notifGroup.alpha  = 0f;
        _notifGroup.blocksRaycasts = false;
        notif.anchorMin        = new Vector2(0.5f, 0f);
        notif.anchorMax        = new Vector2(0.5f, 0f);
        notif.pivot            = new Vector2(0.5f, 0f);
        notif.anchoredPosition = new Vector2(0f, 230f);
        notif.sizeDelta        = new Vector2(460f, 54f);
        notif.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.07f, 0.03f, 0.9f);

        UIRect("NTopLine", notif,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 2f),
            new Color(completeColor.r, completeColor.g, completeColor.b, 0.8f));

        UIRect("NBar", notif,
            new Vector2(0f, 0.12f), new Vector2(0f, 0.88f),
            new Vector2(0f, 0.5f), Vector2.zero, new Vector2(4f, 0f),
            completeColor);

        var notifTextGO        = MakeRect("NotifText", notif);
        notifTextGO.anchorMin  = Vector2.zero;
        notifTextGO.anchorMax  = Vector2.one;
        notifTextGO.offsetMin  = new Vector2(16f, 0f);
        notifTextGO.offsetMax  = new Vector2(-12f, 0f);
        _notifText             = notifTextGO.gameObject.AddComponent<TextMeshProUGUI>();
        _notifText.fontSize    = 18f;
        _notifText.fontStyle   = FontStyles.Bold;
        _notifText.alignment   = TextAlignmentOptions.MidlineLeft;
        _notifText.characterSpacing = 2f;
    }

    Canvas FindCanvas()
    {
        var go = GameObject.Find("CombatHUDCanvas");
        if (go != null)
        {
            var c = go.GetComponent<Canvas>();
            if (c != null) return c;
        }
        var cgo    = new GameObject("L5ObjCanvas");
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 51;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    static RectTransform MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    static Image UIRect(string name, RectTransform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta, Color color)
    {
        var rt = MakeRect(name, parent);
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;
        var img   = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    IEnumerator FadeGroup(CanvasGroup grp, float from, float to, float duration)
    {
        float t = 0f;
        grp.alpha = from;
        while (t < duration)
        {
            t        += Time.deltaTime;
            grp.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        grp.alpha = to;
    }
}
