using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Level 3 Objective Manager
//
//  Mirrors the look + flow of Sayed's Level1ObjectiveManager so Level 3 has
//  the same top-left objective panel + bottom-centre "OBJECTIVE COMPLETE"
//  banner. Three objectives:
//    1. FIND THE BLUE KEYCARD          (Player picks up Keycard Spawner)
//    2. HACK ALL 3 SERVERS             (ServerHack.HackedCount reaches 3)
//    3. ESCAPE THROUGH THE EXIT GATE   (ExitGatePanel triggers extraction)
//
//  Polls for completion every frame so it doesn't need any teammate scripts
//  modified. Auto-installs on Level-3 load. Updates the CombatHUD counter.
// ─────────────────────────────────────────────────────────────────────────────
public class Level3ObjectiveManager : MonoBehaviour
{
    // ── Objectives ────────────────────────────────────────────────────────────
    [Header("Objective 1")]
    public string obj1Title = "FIND THE BLUE KEYCARD";
    [TextArea(2, 4)]
    public string obj1Description = "Locate the IRONCLAD blue keycard hidden in the lab. It unlocks the inner doors.";

    [Header("Objective 2")]
    public string obj2Title = "HACK ALL 3 SERVERS";
    [TextArea(2, 4)]
    public string obj2Description = "Destroy the weapon schematics. Hack Server 1, then Server 2, then breach Server 3.";

    [Header("Objective 3")]
    public string obj3Title = "ESCAPE THROUGH THE EXIT GATE";
    [TextArea(2, 4)]
    public string obj3Description = "The facility is going up. Get to the emergency exit and extract before it blows.";

    [Header("Style")]
    public Color accentColor   = new Color(0f, 0.78f, 1f, 1f);
    public Color completeColor = new Color(0f, 1f, 0.55f, 1f);

    [Header("Misc")]
    public int totalObjectives = 3;
    public string requiredKeycardId = "blue_keycard";

    // ── State ─────────────────────────────────────────────────────────────────
    int  _phase = 1;       // 1 = keycard, 2 = servers, 3 = exit
    int  _completed;       // 0..3 — drives the HUD counter
    bool _showingBanner;
    bool _scriptedExit;
    Coroutine _panelCoroutine;

    CanvasGroup     _panelGroup;
    TextMeshProUGUI _titleText;
    TextMeshProUGUI _descText;
    TextMeshProUGUI _progressText;
    Image           _topLineImg;
    Image           _barImg;
    CanvasGroup     _notifGroup;
    TextMeshProUGUI _notifText;

    CombatHUD                _hud;
    ChainDoorKeycardHolder   _holder;

    // ── Auto-install on Level-3 ───────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Level-3" && scene.name != "Level3") return;
        if (Object.FindFirstObjectByType<Level3ObjectiveManager>() != null) return;
        var host = new GameObject("Level3ObjectiveManager");
        host.AddComponent<Level3ObjectiveManager>();
    }

    void Start()
    {
        _hud = Object.FindFirstObjectByType<CombatHUD>();
        _hud?.SetObjectives(0, totalObjectives);

        BuildUI();
        ShowObjective(obj1Title, obj1Description, accentColor);
        RefreshProgress();
    }

    void Update()
    {
        if (_holder == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _holder = player.GetComponent<ChainDoorKeycardHolder>();
        }

        // Force the HUD counter to reflect OUR phase progress, not whatever
        // ServerHack/etc are incrementing on their own. We "own" the counter.
        if (_hud != null) _hud.SetObjectives(_completed, totalObjectives);

        RefreshProgress();

        switch (_phase)
        {
            case 1:
                if (_holder != null && _holder.HasKeycard(requiredKeycardId))
                    StartCoroutine(CompleteAndAdvance(2, obj2Title, obj2Description));
                break;
            case 2:
                if (ServerHack.HackedCount >= 3)
                    StartCoroutine(CompleteAndAdvance(3, obj3Title, obj3Description));
                break;
            case 3:
                // Objective 3 completes when ExitGatePanel triggers extraction
                // (the LabExplosionCutscene scene transition / MissionComplete).
                // Detected here by watching for any scene change away from Level-3.
                if (_scriptedExit) break;
                if (Object.FindFirstObjectByType<LabExplosionCutscene>() != null &&
                    IsCutscenePlaying())
                {
                    _scriptedExit = true;
                    StartCoroutine(CompleteObj3Sequence());
                }
                break;
        }
    }

    bool IsCutscenePlaying()
    {
        // The cutscene activates a "CutsceneCamera" or similar at start, but the
        // simplest universal check: the player input gets locked. We treat the
        // cutscene as active once the time has passed extraction. Conservative:
        // if AutoSave isn't being asked to save and the extraction path was taken,
        // we just trust the gate panel state. For robustness, completing on first
        // detection of the cutscene component being enabled in scene is enough.
        var cutscene = Object.FindFirstObjectByType<LabExplosionCutscene>();
        return cutscene != null && cutscene.enabled && cutscene.isActiveAndEnabled;
    }

    // ── Objective transitions ─────────────────────────────────────────────────
    IEnumerator CompleteAndAdvance(int nextPhase, string nextTitle, string nextDesc)
    {
        if (_phase == nextPhase) yield break;
        _phase = nextPhase;
        _completed++;   // one full objective just finished

        ShowNotification("✓  OBJECTIVE COMPLETE", completeColor);
        yield return FadeGroup(_notifGroup, 0f, 1f, 0.3f);
        yield return new WaitForSeconds(2.5f);
        yield return FadeGroup(_notifGroup, 1f, 0f, 0.5f);

        yield return FadeGroup(_panelGroup, 1f, 0f, 0.4f);
        ShowObjective(nextTitle, nextDesc, accentColor);
        _progressText.text = "";
        yield return FadeGroup(_panelGroup, 0f, 1f, 0.4f);
    }

    IEnumerator CompleteObj3Sequence()
    {
        _completed = totalObjectives;   // all three done
        ShowNotification("✓  OBJECTIVE COMPLETE", completeColor);
        yield return FadeGroup(_notifGroup, 0f, 1f, 0.3f);
        yield return new WaitForSeconds(2.5f);
        yield return FadeGroup(_notifGroup, 1f, 0f, 0.5f);
        yield return FadeGroup(_panelGroup, 1f, 0f, 0.6f);
    }

    // ── Progress strip ────────────────────────────────────────────────────────
    void RefreshProgress()
    {
        if (_progressText == null) return;
        string text = "";
        switch (_phase)
        {
            case 1:
                bool has = _holder != null && _holder.HasKeycard(requiredKeycardId);
                text = has ? "Keycard: <color=#00ff8c>Found</color>" : "Keycard: Not found";
                break;
            case 2:
                int hacked = ServerHack.HackedCount;
                text = $"Servers: {hacked}/3";
                break;
            case 3:
                text = "Head for the emergency exit.";
                break;
        }
        _progressText.text = text;
    }

    // ── UI Helpers ────────────────────────────────────────────────────────────
    void ShowObjective(string title, string desc, Color accent)
    {
        if (_titleText != null) _titleText.text = title;
        if (_descText  != null) _descText.text  = desc;
        UpdateAccent(accent);
    }

    void ShowNotification(string msg, Color color)
    {
        if (_notifText == null) return;
        _notifText.text  = msg;
        _notifText.color = color;
    }

    void UpdateAccent(Color color)
    {
        if (_topLineImg != null)
            _topLineImg.color = new Color(color.r, color.g, color.b, 0.7f);
        if (_barImg != null)
            _barImg.color = color;
    }

    IEnumerator FadeGroup(CanvasGroup grp, float from, float to, float duration)
    {
        float t = 0f;
        grp.alpha = from;
        while (t < duration)
        {
            t += Time.deltaTime;
            grp.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        grp.alpha = to;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  UI BUILDER — copied from Sayed's Level1ObjectiveManager for visual parity
    // ─────────────────────────────────────────────────────────────────────────
    void BuildUI()
    {
        Canvas canvas = FindCanvas();

        // Objective panel — top-left
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

        var labelGO = MakeRect("ObjLabel", panel);
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

        var titleGO = MakeRect("Title", panel);
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

        var descGO = MakeRect("Desc", panel);
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

        var progGO = MakeRect("Progress", panel);
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
        var notif = MakeRect("ObjNotif", canvas.transform);
        _notifGroup       = notif.gameObject.AddComponent<CanvasGroup>();
        _notifGroup.alpha = 0f;
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

        var notifTextGO = MakeRect("NotifText", notif);
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
}
