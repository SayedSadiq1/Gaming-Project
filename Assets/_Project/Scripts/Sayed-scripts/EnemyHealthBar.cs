using UnityEngine;
using UnityEngine.UI;
using Unity.FPS.Game;

// Attach to the enemy root GameObject (same one that has Health).
// Builds a world-space health bar above the enemy's head at runtime.
public class EnemyHealthBar : MonoBehaviour
{
    [Header("Position")]
    [Tooltip("How far above the enemy root the bar floats.")]
    public float heightOffset = 2.2f;

    [Header("Size")]
    public float barWidth  = 1.2f;
    public float barHeight = 0.12f;

    [Header("Colors")]
    public Color fullColor = new Color(0f, 1f, 0.45f, 1f);   // green
    public Color lowColor  = new Color(1f, 0.22f, 0.22f, 1f); // red
    [Range(0f, 1f)] public float lowThreshold = 0.35f;

    [Header("Visibility")]
    [Tooltip("Only show the bar when the enemy has been damaged at least once.")]
    public bool hideUntilDamaged = true;
    [Tooltip("Seconds to keep the bar visible after the last hit. 0 = always visible once shown.")]
    public float hideDelay = 3f;

    Health       _health;
    Canvas       _canvas;
    RectTransform _fillRect;
    Image        _fillImg;
    Transform    _cam;
    bool         _everDamaged;
    float        _hideTimer;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        _health = GetComponent<Health>();
        if (_health == null)
        {
            Debug.LogWarning("[EnemyHealthBar] No Health component found on " + gameObject.name, this);
            enabled = false;
            return;
        }

        _health.OnDamaged += OnDamaged;
        _health.OnDie     += OnDie;

        _cam = Camera.main != null ? Camera.main.transform : null;

        BuildBar();

        if (hideUntilDamaged)
            _canvas.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (_health != null)
        {
            _health.OnDamaged -= OnDamaged;
            _health.OnDie     -= OnDie;
        }
        if (_canvas != null)
            Destroy(_canvas.gameObject);
    }

    // ─────────────────────────────────────────────────────────────────────────
    void LateUpdate()
    {
        if (_canvas == null) return;

        // Keep above enemy head.
        _canvas.transform.position = transform.position + Vector3.up * heightOffset;

        // Face camera.
        if (_cam != null)
            _canvas.transform.rotation = Quaternion.LookRotation(
                _canvas.transform.position - _cam.position);

        // Auto-hide after delay.
        if (hideUntilDamaged && _everDamaged && hideDelay > 0f)
        {
            _hideTimer -= Time.deltaTime;
            if (_hideTimer <= 0f)
                _canvas.gameObject.SetActive(false);
        }

        RefreshFill();
    }

    // ─────────────────────────────────────────────────────────────────────────
    void OnDamaged(float dmg, GameObject src)
    {
        if (!_everDamaged)
        {
            _everDamaged = true;
            _canvas.gameObject.SetActive(true);
        }
        _hideTimer = hideDelay;
    }

    void OnDie()
    {
        if (_canvas != null)
            _canvas.gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    void RefreshFill()
    {
        if (_health == null || _fillRect == null) return;

        float ratio = Mathf.Clamp01(_health.CurrentHealth / _health.MaxHealth);
        _fillRect.localScale = new Vector3(ratio, 1f, 1f);

        if (_fillImg != null)
            _fillImg.color = Color.Lerp(lowColor, fullColor,
                Mathf.InverseLerp(0f, lowThreshold, ratio) );
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  BAR BUILDER
    // ─────────────────────────────────────────────────────────────────────────
    void BuildBar()
    {
        // World-space canvas — not parented to the enemy so it doesn't inherit scale.
        var cgo    = new GameObject("EnemyHPBar_" + gameObject.name);
        _canvas    = cgo.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.WorldSpace;
        _canvas.sortingOrder = 10;
        cgo.AddComponent<CanvasScaler>();   // keeps pixel density sane

        var crt           = (RectTransform)_canvas.transform;
        crt.sizeDelta     = new Vector2(barWidth, barHeight + 0.06f);
        crt.localScale    = Vector3.one;

        // Background (dark).
        var bgGO  = new GameObject("BG");
        bgGO.transform.SetParent(cgo.transform, false);
        var bgRT          = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin    = Vector2.zero;
        bgRT.anchorMax    = Vector2.one;
        bgRT.sizeDelta    = Vector2.zero;
        bgGO.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.85f);

        // Fill root — full width, anchored left so we scale from left edge.
        var fillRootGO = new GameObject("FillRoot");
        fillRootGO.transform.SetParent(cgo.transform, false);
        var fillRootRT        = fillRootGO.AddComponent<RectTransform>();
        fillRootRT.anchorMin  = Vector2.zero;
        fillRootRT.anchorMax  = Vector2.one;
        fillRootRT.sizeDelta  = new Vector2(-0.04f, -0.03f);
        fillRootRT.pivot      = new Vector2(0f, 0.5f);
        fillRootRT.anchoredPosition = new Vector2(0.02f, 0f);

        // Actual fill image — pivot left, scaled by ratio.
        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillRootGO.transform, false);
        _fillRect             = fillGO.AddComponent<RectTransform>();
        _fillRect.anchorMin   = Vector2.zero;
        _fillRect.anchorMax   = Vector2.one;
        _fillRect.sizeDelta   = Vector2.zero;
        _fillRect.pivot       = new Vector2(0f, 0.5f);
        _fillRect.anchoredPosition = Vector2.zero;
        _fillImg = fillGO.AddComponent<Image>();
        _fillImg.color = fullColor;

        RefreshFill();
    }
}
