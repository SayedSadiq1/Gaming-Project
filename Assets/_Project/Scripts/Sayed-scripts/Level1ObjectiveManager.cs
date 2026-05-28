using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Attach to an empty GameObject in Level1 only.
public class Level1ObjectiveManager : MonoBehaviour
{
    // ── Objective 1 ──────────────────────────────────────────────────────────
    [Header("Objective 1")]
    public string obj1Title = "CLEAR PART 1";
    [TextArea(2, 4)]
    public string obj1Description = "Eliminate all enemies in Part 1 and open the exit gate.";
    [Tooltip("All enemy GameObjects in Part 1 that must be killed.")]
    public List<GameObject> part1Enemies = new List<GameObject>();
    [Tooltip("The gate the player must open to finish Objective 1.")]
    public ChainDoorController part1Gate;

    // ── Objective 2 ──────────────────────────────────────────────────────────
    [Header("Objective 2  (shown after Objective 1 completes)")]
    public string obj2Title = "RESTORE MAIN GATE POWER";
    [TextArea(2, 4)]
    public string obj2Description = "The exit gate is offline. Locate and restore the power supply in the area.";
    [Tooltip("The electricity source that powers the main exit gate.")]
    public ElectricitySource mainGateGenerator;

    // ── Objective 3 ──────────────────────────────────────────────────────────
    [Header("Objective 3  (shown after Objective 2 completes)")]
    public string obj3Title = "REACH THE EXIT";
    [TextArea(2, 4)]
    public string obj3Description = "Make your way to the main exit gate and get out.";

    // ── Objective 3b — lockdown phase (auto-shown when alarm triggers) ────────
    [Header("Objective 3  —  Lockdown phase")]
    public string obj3LockdownTitle = "DISABLE SECURITY LOCKDOWN";
    [TextArea(2, 4)]
    public string obj3LockdownDescription = "Security systems have been triggered. Locate and shut down both control panels to restore access.";
    [Tooltip("The SecurityLockdown manager in the scene.")]
    public SecurityLockdown securityLockdown;

    // ── HUD ──────────────────────────────────────────────────────────────────
    [Header("HUD")]
    [Tooltip("Auto-found if left empty.")]
    public CombatHUD combatHUD;
    [Tooltip("Total objectives in Level 1 (used for the HUD counter).")]
    public int totalObjectives = 3;

    // ── Style ─────────────────────────────────────────────────────────────────
    [Header("Style")]
    public Color accentColor   = new Color(0f, 0.78f, 1f, 1f);
    public Color completeColor = new Color(0f, 1f, 0.55f, 1f);
    public Color alarmColor    = new Color(1f, 0.2f, 0.1f, 1f);

    // ── Internal ─────────────────────────────────────────────────────────────
    int  _killedLast = -1;
    bool _gateOpen;
    bool _obj1Done;
    bool _obj2Done;
    bool _obj3Done;
    bool _inLockdown;

    Coroutine _panelCoroutine;

    CanvasGroup     _panelGroup;
    TextMeshProUGUI _titleText;
    TextMeshProUGUI _descText;
    TextMeshProUGUI _progressText;
    CanvasGroup     _notifGroup;
    TextMeshProUGUI _notifText;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        if (combatHUD == null)
            combatHUD = FindAnyObjectByType<CombatHUD>();

        combatHUD?.SetObjectives(0, totalObjectives);

        if (part1Gate != null)
            part1Gate.OnOpened += OnGateOpened;

        if (securityLockdown != null)
        {
            securityLockdown.OnLockdownTriggered += OnLockdownStarted;
            securityLockdown.OnLockdownLifted    += OnLockdownCleared;
        }

        BuildUI();
        ShowObjective(obj1Title, obj1Description, accentColor);
        RefreshProgress();
    }

    void OnDestroy()
    {
        if (securityLockdown != null)
        {
            securityLockdown.OnLockdownTriggered -= OnLockdownStarted;
            securityLockdown.OnLockdownLifted    -= OnLockdownCleared;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  OBJECTIVE 1
    // ─────────────────────────────────────────────────────────────────────────
    void Update()
    {
        if (_obj1Done) return;

        int dead = CountDead();
        if (dead != _killedLast)
        {
            _killedLast = dead;
            RefreshProgress();
            TryCompleteObj1();
        }
    }

    int CountDead()
    {
        int count = 0;
        foreach (var go in part1Enemies)
        {
            if (go == null || !go.activeInHierarchy)
                count++;
        }
        return count;
    }

    void OnGateOpened()
    {
        if (_obj1Done) return;
        _gateOpen = true;
        RefreshProgress();
        TryCompleteObj1();
    }

    void TryCompleteObj1()
    {
        if (_killedLast < part1Enemies.Count || !_gateOpen) return;
        _obj1Done = true;
        RunPanelSequence(Obj1CompleteSequence());
    }

    void RunPanelSequence(IEnumerator sequence)
    {
        if (_panelCoroutine != null) StopCoroutine(_panelCoroutine);
        _panelCoroutine = StartCoroutine(sequence);
    }

    IEnumerator Obj1CompleteSequence()
    {
        combatHUD?.IncrementObjective();

        ShowNotification("✓  OBJECTIVE COMPLETE", completeColor);
        yield return StartCoroutine(FadeGroup(_notifGroup, 0f, 1f, 0.3f));
        yield return new WaitForSeconds(2.5f);
        yield return StartCoroutine(FadeGroup(_notifGroup, 1f, 0f, 0.5f));

        yield return StartCoroutine(FadeGroup(_panelGroup, 1f, 0f, 0.4f));
        ShowObjective(obj2Title, obj2Description, accentColor);
        _progressText.text = "";
        yield return StartCoroutine(FadeGroup(_panelGroup, 0f, 1f, 0.4f));

        if (mainGateGenerator != null)
            mainGateGenerator.OnPowered += OnGeneratorPowered;
        else
            Debug.LogWarning("[Level1ObjectiveManager] mainGateGenerator not assigned — Objective 2 will never complete.", this);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  OBJECTIVE 2
    // ─────────────────────────────────────────────────────────────────────────
    void OnGeneratorPowered()
    {
        if (_obj2Done) return;
        _obj2Done = true;
        RunPanelSequence(Obj2CompleteSequence());
    }

    IEnumerator Obj2CompleteSequence()
    {
        combatHUD?.IncrementObjective();

        ShowNotification("✓  OBJECTIVE COMPLETE", completeColor);
        yield return StartCoroutine(FadeGroup(_notifGroup, 0f, 1f, 0.3f));
        yield return new WaitForSeconds(2.5f);
        yield return StartCoroutine(FadeGroup(_notifGroup, 1f, 0f, 0.5f));

        yield return StartCoroutine(FadeGroup(_panelGroup, 1f, 0f, 0.4f));
        ShowObjective(obj3Title, obj3Description, accentColor);
        _progressText.text = "";
        yield return StartCoroutine(FadeGroup(_panelGroup, 0f, 1f, 0.4f));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  OBJECTIVE 3 — trap / lockdown
    // ─────────────────────────────────────────────────────────────────────────
    void OnLockdownStarted()
    {
        if (_obj3Done || _inLockdown) return;
        _inLockdown = true;
        if (mainGateGenerator != null)
            mainGateGenerator.OnPowered -= OnGeneratorPowered;
        RunPanelSequence(LockdownStartSequence());
    }

    IEnumerator LockdownStartSequence()
    {
        _panelGroup.alpha = 0f;
        ShowObjective(obj3LockdownTitle, obj3LockdownDescription, alarmColor);
        _progressText.text = "";
        yield return StartCoroutine(FadeGroup(_panelGroup, 0f, 1f, 0.25f));

        ShowNotification("⚠  SECURITY LOCKDOWN ACTIVE", alarmColor);
        yield return StartCoroutine(FadeGroup(_notifGroup, 0f, 1f, 0.3f));
        yield return new WaitForSeconds(3f);
        yield return StartCoroutine(FadeGroup(_notifGroup, 1f, 0f, 0.5f));
    }

    void OnLockdownCleared()
    {
        if (_obj3Done) return;
        _obj3Done = true;
        RunPanelSequence(Obj3CompleteSequence());
    }

    IEnumerator Obj3CompleteSequence()
    {
        combatHUD?.IncrementObjective();

        ShowNotification("✓  OBJECTIVE COMPLETE", completeColor);
        yield return StartCoroutine(FadeGroup(_notifGroup, 0f, 1f, 0.3f));
        yield return new WaitForSeconds(2.5f);
        yield return StartCoroutine(FadeGroup(_notifGroup, 1f, 0f, 0.5f));

        // Hide the panel — all objectives done.
        yield return StartCoroutine(FadeGroup(_panelGroup, 1f, 0f, 0.6f));
    }

    // ─────────────────────────────────────────────────────────────────────────
    void RefreshProgress()
    {
        if (_obj1Done || _progressText == null) return;
        int total = part1Enemies.Count;
        int dead  = _killedLast < 0 ? 0 : _killedLast;
        string gate = _gateOpen ? "<color=#00ff8c>Done</color>" : "Not done";
        _progressText.text = $"Enemies: {dead}/{total}   Gate: {gate}";
    }

    void ShowObjective(string title, string desc, Color accent)
    {
        if (_titleText != null) _titleText.text = title;
        if (_descText  != null) _descText.text  = desc;

        // Recolor the accent bar and top line to match the current objective mood.
        UpdateAccent(accent);
    }

    void ShowNotification(string msg, Color color)
    {
        if (_notifText == null) return;
        _notifText.text  = msg;
        _notifText.color = color;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  UI BUILDER
    // ─────────────────────────────────────────────────────────────────────────
    Image _topLineImg;
    Image _barImg;

    void BuildUI()
    {
        Canvas canvas = FindCanvas();

        // ── Objective panel — top-left ────────────────────────────────────────
        var panel = MakeRect("Obj1Panel", canvas.transform);
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

        // "OBJECTIVE" label
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

        // Title
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

        // Description
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

        // Progress strip
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

        // ── Notification banner — bottom-centre ───────────────────────────────
        var notif          = MakeRect("ObjNotif", canvas.transform);
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

    void UpdateAccent(Color color)
    {
        if (_topLineImg != null)
            _topLineImg.color = new Color(color.r, color.g, color.b, 0.7f);
        if (_barImg != null)
            _barImg.color = color;
    }

    // ─────────────────────────────────────────────────────────────────────────
    Canvas FindCanvas()
    {
        var go = GameObject.Find("CombatHUDCanvas");
        if (go != null)
        {
            var c = go.GetComponent<Canvas>();
            if (c != null) return c;
        }
        var cgo    = new GameObject("ObjCanvas");
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
        var rt  = MakeRect(name, parent);
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
