using System;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

// Attach to any GameObject in Level1.
// Wire the exit-gate keycard pickup's OnPickedUp event, or call TriggerLockdown() directly.
// The player must deactivate both assigned panels to lift the lockdown.
// ChainDoorController for the exit gate should have this checked via IsLockedDown.
public class SecurityLockdown : MonoBehaviour
{
    [Header("Lockdown Trigger")]
    [Tooltip("The keycard pickup that triggers the alarm when collected.")]
    public KeycardPickupTrigger exitKeycardPickup;

    [Header("Exit Gate")]
    [Tooltip("The exit gate door — locked while lockdown is active.")]
    public ChainDoorController exitGate;

    [Header("Security Panels")]
    [Tooltip("Both panels must be deactivated to lift the lockdown.")]
    public SecurityPanel panelA;
    public SecurityPanel panelB;

    [Header("Alarm Audio")]
    public AudioClip alarmLoop;
    [Range(0f, 1f)] public float alarmVolume = 0.6f;

    [Header("Style")]
    public Color lockdownColor  = new Color(1f, 0.2f, 0.1f, 1f);   // red
    public Color clearedColor   = new Color(0f, 1f, 0.55f, 1f);    // green

    // True while lockdown is active — ChainDoorController reads this.
    public bool IsLockedDown { get; private set; }

    // Fired when lockdown starts (keycard picked up).
    public event Action OnLockdownTriggered;

    // Fired when lockdown is lifted.
    public event Action OnLockdownLifted;

    AudioSource  _alarmSource;
    CanvasGroup  _bannerGroup;
    TextMeshProUGUI _bannerText;
    bool _triggered;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        BuildBanner();

        if (exitKeycardPickup != null)
            exitKeycardPickup.OnPickedUp += TriggerLockdown;
        else
            Debug.LogWarning("[SecurityLockdown] exitKeycardPickup not assigned.", this);

        if (panelA != null) panelA.OnDeactivated += CheckLiftLockdown;
        if (panelB != null) panelB.OnDeactivated += CheckLiftLockdown;
    }

    void OnDestroy()
    {
        if (exitKeycardPickup != null) exitKeycardPickup.OnPickedUp -= TriggerLockdown;
        if (panelA != null) panelA.OnDeactivated -= CheckLiftLockdown;
        if (panelB != null) panelB.OnDeactivated -= CheckLiftLockdown;
    }

    // ─────────────────────────────────────────────────────────────────────────
    public void TriggerLockdown()
    {
        if (_triggered) return;
        _triggered   = true;
        IsLockedDown = true;

        StartAlarm();
        ShowBanner("⚠  SECURITY LOCKDOWN ACTIVE", lockdownColor);

        if (exitGate != null)
            exitGate.SetLockdown(true);

        OnLockdownTriggered?.Invoke();
    }

    void CheckLiftLockdown()
    {
        if (!IsLockedDown) return;
        bool aCleared = panelA == null || panelA.IsDeactivated;
        bool bCleared = panelB == null || panelB.IsDeactivated;
        if (!aCleared || !bCleared) return;

        LiftLockdown();
    }

    void LiftLockdown()
    {
        IsLockedDown = false;
        StopAlarm();
        ShowBanner("✓  LOCKDOWN LIFTED — EXIT UNLOCKED", clearedColor);

        if (exitGate != null)
            exitGate.SetLockdown(false);

        OnLockdownLifted?.Invoke();

        // Auto-hide banner after 4 seconds.
        StartCoroutine(HideBannerAfter(4f));
    }

    // ─────────────────────────────────────────────────────────────────────────
    void StartAlarm()
    {
        if (alarmLoop == null) return;
        var go = new GameObject("AlarmSource");
        _alarmSource          = go.AddComponent<AudioSource>();
        _alarmSource.clip     = alarmLoop;
        _alarmSource.loop     = true;
        _alarmSource.volume   = alarmVolume;
        _alarmSource.spatialBlend = 0f; // 2D
        _alarmSource.Play();
    }

    void StopAlarm()
    {
        if (_alarmSource == null) return;
        _alarmSource.Stop();
        Destroy(_alarmSource.gameObject);
        _alarmSource = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    void ShowBanner(string msg, Color color)
    {
        if (_bannerText == null) return;
        _bannerText.text  = msg;
        _bannerText.color = color;

        var img = _bannerGroup.GetComponent<Image>();
        if (img != null)
            img.color = new Color(color.r * 0.08f, color.g * 0.08f, color.b * 0.08f, 0.92f);

        _bannerGroup.alpha = 1f;
    }

    System.Collections.IEnumerator HideBannerAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            _bannerGroup.alpha = Mathf.Lerp(1f, 0f, t / 0.6f);
            yield return null;
        }
        _bannerGroup.alpha = 0f;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  UI BANNER — top-centre
    // ─────────────────────────────────────────────────────────────────────────
    void BuildBanner()
    {
        Canvas canvas = FindCanvas();

        var bannerGO = new GameObject("LockdownBanner");
        bannerGO.transform.SetParent(canvas.transform, false);

        _bannerGroup               = bannerGO.AddComponent<CanvasGroup>();
        _bannerGroup.alpha         = 0f;
        _bannerGroup.blocksRaycasts = false;

        var bannerRect              = bannerGO.AddComponent<RectTransform>();
        bannerRect.anchorMin        = new Vector2(0.5f, 1f);
        bannerRect.anchorMax        = new Vector2(0.5f, 1f);
        bannerRect.pivot            = new Vector2(0.5f, 1f);
        bannerRect.anchoredPosition = new Vector2(0f, -20f);
        bannerRect.sizeDelta        = new Vector2(560f, 58f);

        bannerGO.AddComponent<Image>().color = new Color(0.08f, 0.01f, 0.01f, 0.92f);

        // Top accent line
        AddRect("TLine", bannerRect,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 3f),
            lockdownColor);

        // Left bar
        AddRect("LBar", bannerRect,
            new Vector2(0f, 0.1f), new Vector2(0f, 0.9f),
            new Vector2(0f, 0.5f), Vector2.zero, new Vector2(4f, 0f),
            lockdownColor);

        // Right bar
        AddRect("RBar", bannerRect,
            new Vector2(1f, 0.1f), new Vector2(1f, 0.9f),
            new Vector2(1f, 0.5f), Vector2.zero, new Vector2(4f, 0f),
            lockdownColor);

        var textGO = new GameObject("BannerText");
        textGO.transform.SetParent(bannerGO.transform, false);
        var textRect    = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 0f);
        textRect.offsetMax = new Vector2(-16f, 0f);

        _bannerText                 = textGO.AddComponent<TextMeshProUGUI>();
        _bannerText.fontSize        = 18f;
        _bannerText.fontStyle       = FontStyles.Bold;
        _bannerText.alignment       = TextAlignmentOptions.Midline;
        _bannerText.characterSpacing = 2f;
    }

    static void AddRect(string name, RectTransform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt              = go.AddComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;
        go.AddComponent<Image>().color = color;
    }

    Canvas FindCanvas()
    {
        var go = GameObject.Find("CombatHUDCanvas");
        if (go != null)
        {
            var c = go.GetComponent<Canvas>();
            if (c != null) return c;
        }
        var cgo    = new GameObject("LockdownCanvas");
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 52;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();
        return canvas;
    }
}
