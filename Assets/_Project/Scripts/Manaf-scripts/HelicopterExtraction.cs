using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Level 5 Helicopter Extraction
//
//  Attach to the eurocopter_tiger_attack_helicopter GameObject.
//  • Player walks within triggerDistance → a "HOLD E" prompt fades in.
//  • Player holds E for holdDuration seconds → a progress bar fills.
//  • On completion → the player is extracted and the Level 5 end screen shows.
//
//  Self-contained: builds its own prompt UI, uses pure proximity (not look-at),
//  and reads E directly via the new Input System (PlayerInteractor's shared
//  hold system uses F, so this stays out of its way).
// ─────────────────────────────────────────────────────────────────────────────
public class HelicopterExtraction : MonoBehaviour
{
    [Header("Interaction")]
    [Tooltip("How close (metres) the player must be for the prompt to appear.")]
    public float triggerDistance = 6f;

    [Tooltip("Seconds the player must hold the key to be extracted.")]
    public float holdDuration = 3f;

    [Tooltip("Key to hold. E by default, per design.")]
    public Key interactKey = Key.E;

    [Header("Prompt")]
    public string promptText = "HOLD  E  TO BOARD EXTRACTION";

    [Header("Completion")]
    [Tooltip("Seconds to wait after the hold completes before the end screen shows. " +
             "~3.5s lets the 'OBJECTIVE COMPLETE' banner play out first.")]
    public float delayBeforeShow = 3.5f;

    [Tooltip("Title shown on the Level 5 end screen.")]
    public string completeTitle = "MISSION COMPLETE";

    [Tooltip("Big line under the title.")]
    public string completeHeadline = "GHOST ESCAPED";

    [Header("Style")]
    public Color accentColor = new Color(0f, 0.78f, 1f, 1f);
    public float fadeSpeed = 12f;

    // Fires once when the player finishes boarding (the level-win moment).
    // Level5ObjectiveManager listens to this to complete its objective.
    public static event System.Action OnExtractionComplete;

    Transform     _player;
    CanvasGroup   _group;
    RectTransform _fillRT;
    TextMeshProUGUI _label;

    float _holdTimer;
    bool  _triggered;

    void Start()
    {
        _player = ResolvePlayer();
        BuildUI();
    }

    void Update()
    {
        if (_group == null || _triggered) return;

        float dist = _player != null
            ? Vector3.Distance(transform.position, _player.position)
            : float.MaxValue;

        bool inRange = dist <= triggerDistance;
        _group.alpha = Mathf.Lerp(_group.alpha, inRange ? 1f : 0f, Time.deltaTime * fadeSpeed);

        if (!inRange)
        {
            _holdTimer = 0f;
            SetProgress(0f);
            return;
        }

        bool held = Keyboard.current != null && Keyboard.current[interactKey].isPressed;
        if (held)
        {
            _holdTimer += Time.deltaTime;
            SetProgress(_holdTimer / holdDuration);
            if (_holdTimer >= holdDuration)
                Extract();
        }
        else
        {
            _holdTimer = 0f;
            SetProgress(0f);
        }
    }

    void Extract()
    {
        if (_triggered) return;
        _triggered = true;

        if (_label != null) _label.text = "EXTRACTING...";
        SetProgress(1f);

        OnExtractionComplete?.Invoke();

        Invoke(nameof(ShowEndScreen), Mathf.Max(0f, delayBeforeShow));
    }

    void ShowEndScreen()
    {
        if (_group != null) _group.alpha = 0f;

        int kills = ScoreManager.Instance != null ? ScoreManager.Instance.Kills : 0;
        string stats =
            "<color=#00C8FF>OBJECTIVES</color>\n3 / 3 COMPLETE\n\n" +
            $"<color=#00C8FF>KILLS</color>\n{kills}\n\n" +
            "<color=#00C8FF>STATUS</color>\nEXTRACTED";

        Level5ExtractionCompleteScreen.Show(completeTitle, completeHeadline, stats);
    }

    void OnDestroy()
    {
        if (_group != null) Destroy(_group.gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.25f);
        Gizmos.DrawWireSphere(transform.position, triggerDistance);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    Transform ResolvePlayer()
    {
        var go = GameObject.FindWithTag("Player");
        if (go != null) return go.transform;
        Debug.LogWarning("[HelicopterExtraction] No GameObject tagged 'Player' found.", this);
        return null;
    }

    void SetProgress(float t)
    {
        if (_fillRT == null) return;
        t = Mathf.Clamp01(t);
        var aMax = _fillRT.anchorMax;
        aMax.x = t;
        _fillRT.anchorMax = aMax;
    }

    void BuildUI()
    {
        Canvas canvas = FindOrCreateCanvas();

        var panelGO = new GameObject($"HelicopterPrompt_{gameObject.name}", typeof(RectTransform));
        panelGO.transform.SetParent(canvas.transform, false);

        _group = panelGO.AddComponent<CanvasGroup>();
        _group.alpha          = 0f;
        _group.blocksRaycasts = false;
        _group.interactable   = false;

        var panelRect              = (RectTransform)panelGO.transform;
        panelRect.anchorMin        = new Vector2(0.5f, 0f);
        panelRect.anchorMax        = new Vector2(0.5f, 0f);
        panelRect.pivot            = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 200f);
        panelRect.sizeDelta        = new Vector2(480f, 64f);

        var bg   = panelGO.AddComponent<Image>();
        bg.color = new Color(0.03f, 0.04f, 0.08f, 0.88f);

        // Top accent line
        UIRect("TopLine", panelGO.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 2f),
            new Color(accentColor.r, accentColor.g, accentColor.b, 0.6f));

        // Left accent bar
        UIRect("AccentBar", panelGO.transform,
            new Vector2(0f, 0.30f), new Vector2(0f, 0.95f),
            new Vector2(0f, 0.5f), Vector2.zero, new Vector2(4f, 0f),
            accentColor);

        // Label
        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(panelGO.transform, false);
        var labelRect = (RectTransform)labelGO.transform;
        labelRect.anchorMin = new Vector2(0f, 0.30f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(20f, 0f);
        labelRect.offsetMax = new Vector2(-18f, 0f);

        _label                  = labelGO.AddComponent<TextMeshProUGUI>();
        _label.text             = promptText.ToUpper();
        _label.fontSize         = 18f;
        _label.fontStyle        = FontStyles.Bold;
        _label.color            = Color.white;
        _label.alignment        = TextAlignmentOptions.MidlineLeft;
        _label.characterSpacing = 3f;
        _label.overflowMode     = TextOverflowModes.Ellipsis;

        // Progress bar track (bottom of panel)
        var trackGO = new GameObject("ProgressTrack", typeof(RectTransform));
        trackGO.transform.SetParent(panelGO.transform, false);
        var trackRT = (RectTransform)trackGO.transform;
        trackRT.anchorMin = new Vector2(0f, 0f);
        trackRT.anchorMax = new Vector2(1f, 0f);
        trackRT.pivot     = new Vector2(0.5f, 0f);
        trackRT.anchoredPosition = new Vector2(0f, 6f);
        trackRT.sizeDelta = new Vector2(-32f, 6f);
        trackGO.AddComponent<Image>().color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.18f);

        // Progress fill (grows left → right via anchorMax.x)
        var fillGO = new GameObject("ProgressFill", typeof(RectTransform));
        fillGO.transform.SetParent(trackGO.transform, false);
        _fillRT = (RectTransform)fillGO.transform;
        _fillRT.anchorMin = new Vector2(0f, 0f);
        _fillRT.anchorMax = new Vector2(0f, 1f);
        _fillRT.pivot     = new Vector2(0f, 0.5f);
        _fillRT.offsetMin = Vector2.zero;
        _fillRT.offsetMax = Vector2.zero;
        fillGO.AddComponent<Image>().color = accentColor;
    }

    Canvas FindOrCreateCanvas()
    {
        var combatHUD = GameObject.Find("CombatHUDCanvas");
        if (combatHUD != null)
        {
            var c = combatHUD.GetComponent<Canvas>();
            if (c != null) return c;
        }

        var go     = new GameObject("HelicopterPromptCanvas", typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 51;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    static void UIRect(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;
        go.AddComponent<Image>().color = color;
    }
}
