using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ProximityPrompt : MonoBehaviour
{
    [Header("Prompt")]
    [Tooltip("Text shown when the player is nearby.")]
    public string message = "PRESS E TO INTERACT";

    [Tooltip("Distance in metres that triggers the prompt.")]
    public float triggerDistance = 3f;

    [Header("Style")]
    [Tooltip("Accent colour — default matches CombatHUD cyan.")]
    public Color accentColor = new Color(0f, 0.78f, 1f, 1f);

    [Tooltip("Fade speed.")]
    public float fadeSpeed = 10f;

    CanvasGroup _group;
    Transform   _player;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        _player = ResolvePlayer();
        BuildUI();
    }

    void Update()
    {
        if (_group == null) return;

        float dist   = _player != null
            ? Vector3.Distance(transform.position, _player.position)
            : float.MaxValue;

        float target  = dist <= triggerDistance ? 1f : 0f;
        _group.alpha  = Mathf.Lerp(_group.alpha, target, Time.deltaTime * fadeSpeed);
    }

    void OnDestroy()
    {
        if (_group != null) Destroy(_group.gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.78f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, triggerDistance);
        Gizmos.color = new Color(0f, 0.78f, 1f, 0.06f);
        Gizmos.DrawSphere(transform.position, triggerDistance);
    }

    // ─────────────────────────────────────────────────────────────────────────
    Transform ResolvePlayer()
    {
        var go = GameObject.FindWithTag("Player");
        if (go != null) return go.transform;

        Debug.LogWarning("[ProximityPrompt] No GameObject tagged 'Player' found.", this);
        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    void BuildUI()
    {
        Canvas canvas = FindOrCreateCanvas();

        // ── Panel root ───────────────────────────────────────────────────────
        // IMPORTANT: create with RectTransform, not plain Transform
        var panelGO   = new GameObject($"ProximityPrompt_{gameObject.name}", typeof(RectTransform));
        panelGO.transform.SetParent(canvas.transform, false);

        _group = panelGO.AddComponent<CanvasGroup>();
        _group.alpha          = 0f;
        _group.blocksRaycasts = false;
        _group.interactable   = false;

        // Anchored to bottom-centre, 170 px above the bottom edge
        var panelRect              = (RectTransform)panelGO.transform;
        panelRect.anchorMin        = new Vector2(0.5f, 0f);
        panelRect.anchorMax        = new Vector2(0.5f, 0f);
        panelRect.pivot            = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 170f);
        panelRect.sizeDelta        = new Vector2(460f, 56f);

        // ── Dark background ──────────────────────────────────────────────────
        var bg   = panelGO.AddComponent<Image>();
        bg.color = new Color(0.03f, 0.04f, 0.08f, 0.88f);

        // ── Top accent line ──────────────────────────────────────────────────
        UIRect("TopLine", panelGO.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 2f),
            new Color(accentColor.r, accentColor.g, accentColor.b, 0.6f));

        // ── Left accent bar ──────────────────────────────────────────────────
        UIRect("AccentBar", panelGO.transform,
            new Vector2(0f, 0.12f), new Vector2(0f, 0.88f),
            new Vector2(0f, 0.5f), Vector2.zero, new Vector2(4f, 0f),
            accentColor);

        // ── Label ────────────────────────────────────────────────────────────
        var labelGO   = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(panelGO.transform, false);
        var labelRect  = (RectTransform)labelGO.transform;
        labelRect.anchorMin  = Vector2.zero;
        labelRect.anchorMax  = Vector2.one;
        labelRect.offsetMin  = new Vector2(18f, 0f);
        labelRect.offsetMax  = new Vector2(-18f, 0f);

        var tmp              = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text             = message.ToUpper();
        tmp.fontSize         = 17f;
        tmp.fontStyle        = FontStyles.Bold;
        tmp.color            = Color.white;
        tmp.alignment        = TextAlignmentOptions.MidlineLeft;
        tmp.characterSpacing = 3f;
        tmp.overflowMode     = TextOverflowModes.Ellipsis;

        // ── Right accent dot ─────────────────────────────────────────────────
        UIRect("Dot", panelGO.transform,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(6f, 6f),
            new Color(accentColor.r, accentColor.g, accentColor.b, 0.5f));
    }

    // ─────────────────────────────────────────────────────────────────────────
    Canvas FindOrCreateCanvas()
    {
        var combatHUD = GameObject.Find("CombatHUDCanvas");
        if (combatHUD != null)
        {
            var c = combatHUD.GetComponent<Canvas>();
            if (c != null) return c;
        }

        var go    = new GameObject("ProximityPromptCanvas", typeof(RectTransform));
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
        var go  = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt  = (RectTransform)go.transform;
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;
        go.AddComponent<Image>().color = color;
    }
}
