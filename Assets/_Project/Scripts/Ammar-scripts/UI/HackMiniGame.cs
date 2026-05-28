using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Hack Mini-Game (IP Lock-On Grid)
//
//  GTA-style mini-game. Player is shown a target IP and a grid of decoys.
//  Click the matching cell(s) before the timer runs out. Wrong clicks flash
//  red. Correct sequence completes the hack.
//
//  Difficulty:
//    1 (Server 1) — 8 cells (2x4),  10s, 1 target
//    2 (Server 2) — 12 cells (3x4),  8s, 1 target + reshuffle at 4s
//    3 (Server 3) — 16 cells (4x4), 12s, 3 targets in sequence
//
//  Usage:
//    HackMiniGame.Begin(serverId, onSuccess: () => { ... });
//
//  Pattern mirrors LoadingScreen / MissionCompleteScreen: spawns a
//  DontDestroyOnLoad canvas at runtime, builds its own UI, no scene setup.
// ─────────────────────────────────────────────────────────────────────────────
public class HackMiniGame : MonoBehaviour
{
    static HackMiniGame _instance;

    // ── Style ──────────────────────────────────────────────────────────────
    static readonly Color C_BG       = new Color(0.01f, 0.02f, 0.04f, 0.92f);
    static readonly Color C_CYAN     = new Color32(0x00, 0xC8, 0xFF, 0xFF);
    static readonly Color C_CYAN_DIM = new Color(0f, 0.78f, 1f, 0.35f);
    static readonly Color C_RED      = new Color(1f, 0.2f, 0.15f, 1f);
    static readonly Color C_GREEN    = new Color(0f, 1f, 0.55f, 1f);
    static readonly Color C_CELL_BG  = new Color(0f, 0.08f, 0.14f, 0.9f);
    static readonly Color C_GRAY     = new Color32(0x88, 0x88, 0x95, 0xFF);

    // ── UI refs (built at runtime) ─────────────────────────────────────────
    CanvasGroup       _group;
    TextMeshProUGUI   _titleText;
    TextMeshProUGUI   _targetText;
    TextMeshProUGUI   _hintText;
    RectTransform     _gridRT;
    Image             _timerBar;
    TextMeshProUGUI   _timerText;
    GridLayoutGroup   _grid;

    readonly List<Button>          _cellButtons = new List<Button>();
    readonly List<TextMeshProUGUI> _cellLabels  = new List<TextMeshProUGUI>();
    readonly List<Image>           _cellBgs     = new List<Image>();

    // ── State ──────────────────────────────────────────────────────────────
    int       _difficulty;
    Action    _onSuccess;
    Coroutine _runRoutine;

    int     _cellCount;
    float   _timeLimit;
    int     _targetsToClick;
    bool    _reshuffles;

    List<string> _targetSequence = new List<string>();
    int          _nextTargetIdx;
    bool         _isRunning;

    // ════════════════════════════════════════════════════════════════════════
    //  Public API
    // ════════════════════════════════════════════════════════════════════════
    public static void Begin(int difficulty, Action onSuccess)
    {
        EnsureInstance();
        _instance.StartGame(Mathf.Clamp(difficulty, 1, 3), onSuccess);
    }

    static void EnsureInstance()
    {
        if (_instance != null) return;
        var go = new GameObject("HackMiniGameCanvas");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<HackMiniGame>();
        _instance.Build();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Game flow
    // ════════════════════════════════════════════════════════════════════════
    void StartGame(int difficulty, Action onSuccess)
    {
        if (_isRunning) { Debug.Log("[HackMiniGame] Already running — ignoring."); return; }
        _difficulty = difficulty;
        _onSuccess  = onSuccess;

        ConfigureDifficulty();

        // Free cursor / unpause
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        Time.timeScale   = 1f;

        _group.alpha          = 1f;
        _group.blocksRaycasts = true;
        _group.interactable   = true;

        if (_runRoutine != null) StopCoroutine(_runRoutine);
        _runRoutine = StartCoroutine(GameRoutine());
    }

    void ConfigureDifficulty()
    {
        switch (_difficulty)
        {
            case 1:
                _cellCount      = 8;
                _timeLimit      = 10f;
                _targetsToClick = 1;
                _reshuffles     = false;
                _grid.constraintCount = 4;   // 4 cols, 2 rows
                _titleText.text = "SERVER 1 :: LOCK ONTO TARGET IP";
                break;
            case 2:
                _cellCount      = 12;
                _timeLimit      = 8f;
                _targetsToClick = 1;
                _reshuffles     = true;
                _grid.constraintCount = 4;   // 4 cols, 3 rows
                _titleText.text = "SERVER 2 :: LOCK ONTO TARGET IP";
                break;
            case 3:
            default:
                _cellCount      = 16;
                _timeLimit      = 12f;
                _targetsToClick = 3;
                _reshuffles     = false;
                _grid.constraintCount = 4;   // 4 cols, 4 rows
                _titleText.text = "SERVER 3 :: LOCK ONTO TARGET IPs IN SEQUENCE";
                break;
        }
    }

    IEnumerator GameRoutine()
    {
        _isRunning = true;
        while (_isRunning)
        {
            yield return RoundRoutine();
            if (_isRunning)
            {
                // Failed → small "RETRY" pause then go again
                _hintText.text  = "<color=#ff3826>HACK FAILED — RETRYING…</color>";
                yield return new WaitForSecondsRealtime(1.5f);
            }
        }

        // Success path
        _hintText.text = "<color=#00ff8c>ACCESS GRANTED</color>";
        yield return new WaitForSecondsRealtime(1.0f);

        _group.alpha          = 0f;
        _group.blocksRaycasts = false;
        _group.interactable   = false;

        // Re-lock cursor for FPS gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

        var cb = _onSuccess;
        _onSuccess = null;
        cb?.Invoke();
    }

    IEnumerator RoundRoutine()
    {
        BuildGrid();

        _nextTargetIdx = 0;
        UpdateTargetLabel();
        _hintText.text = $"CLICK MATCHING IP {(_targetsToClick > 1 ? "IN ORDER " : "")}— {_targetsToClick} TARGET(S)";

        float t = _timeLimit;
        bool reshuffled = false;
        bool won = false;

        while (t > 0f)
        {
            t -= Time.unscaledDeltaTime;
            _timerBar.fillAmount = Mathf.Clamp01(t / _timeLimit);
            _timerText.text      = t.ToString("0.0") + "s";

            if (_reshuffles && !reshuffled && t < _timeLimit * 0.5f)
            {
                reshuffled = true;
                ReshuffleGrid();
            }

            if (_nextTargetIdx >= _targetSequence.Count)
            {
                won = true;
                break;
            }

            yield return null;
        }

        if (won)
        {
            _isRunning = false;
            yield break;
        }
        // Time ran out — RoundRoutine ends, the GameRoutine loop will restart
    }

    // ── Cell click ────────────────────────────────────────────────────────
    void OnCellClicked(int index, string ip)
    {
        if (_nextTargetIdx >= _targetSequence.Count) return;

        string expected = _targetSequence[_nextTargetIdx];
        if (ip == expected)
        {
            _cellBgs[index].color = C_GREEN;
            _cellButtons[index].interactable = false;
            _nextTargetIdx++;
            UpdateTargetLabel();
            if (_nextTargetIdx >= _targetSequence.Count)
            {
                _hintText.text = "<color=#00ff8c>SEQUENCE LOCKED</color>";
            }
        }
        else
        {
            StartCoroutine(FlashRed(_cellBgs[index]));
        }
    }

    IEnumerator FlashRed(Image bg)
    {
        var orig = bg.color;
        bg.color = C_RED;
        yield return new WaitForSecondsRealtime(0.25f);
        bg.color = orig;
    }

    void UpdateTargetLabel()
    {
        if (_nextTargetIdx >= _targetSequence.Count)
        {
            _targetText.text = "<color=#00ff8c>ALL TARGETS LOCKED</color>";
            return;
        }

        if (_targetSequence.Count == 1)
        {
            _targetText.text = $"<color=#00C8FF>TARGET IP:</color> <b>{_targetSequence[_nextTargetIdx]}</b>";
        }
        else
        {
            string list = "";
            for (int i = 0; i < _targetSequence.Count; i++)
            {
                if (i > 0) list += "  →  ";
                if (i < _nextTargetIdx)
                    list += $"<color=#00ff8c><s>{_targetSequence[i]}</s></color>";
                else if (i == _nextTargetIdx)
                    list += $"<b>{_targetSequence[i]}</b>";
                else
                    list += $"<color=#666666>{_targetSequence[i]}</color>";
            }
            _targetText.text = $"<color=#00C8FF>SEQUENCE:</color> " + list;
        }
    }

    // ── Grid build / reshuffle ────────────────────────────────────────────
    static readonly string[] HOST_PREFIXES = { "192.168", "10.0.0", "172.16.0", "IRONCLAD", "ROOT@SECURE" };

    void BuildGrid()
    {
        // Clear old cells
        foreach (Transform child in _gridRT) Destroy(child.gameObject);
        _cellButtons.Clear();
        _cellLabels.Clear();
        _cellBgs.Clear();
        _targetSequence.Clear();

        // Generate IPs — unique strings
        var ips = new HashSet<string>();
        while (ips.Count < _cellCount) ips.Add(RandomIp());
        var ipList = new List<string>(ips);

        // Pick target sequence from the list
        var idxPool = new List<int>();
        for (int i = 0; i < ipList.Count; i++) idxPool.Add(i);
        for (int i = 0; i < _targetsToClick; i++)
        {
            int r = UnityEngine.Random.Range(0, idxPool.Count);
            int idx = idxPool[r];
            idxPool.RemoveAt(r);
            _targetSequence.Add(ipList[idx]);
        }

        // Build cells
        for (int i = 0; i < ipList.Count; i++)
        {
            int captureIdx = i;
            string ip      = ipList[i];

            var cellGO = new GameObject("Cell_" + i, typeof(RectTransform));
            cellGO.transform.SetParent(_gridRT, false);

            var bg = cellGO.AddComponent<Image>();
            bg.color = C_CELL_BG;
            bg.raycastTarget = true;

            // border
            var border = NewImage("Border", cellGO.transform, C_CYAN_DIM);
            var brt = border.rectTransform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            border.raycastTarget = false;
            // Outline via a slightly smaller inner panel — cheap border effect
            var inner = NewImage("Inner", cellGO.transform, C_CELL_BG);
            var irt = inner.rectTransform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(2, 2); irt.offsetMax = new Vector2(-2, -2);
            inner.raycastTarget = false;

            // Label
            var txt = NewText("IP", cellGO.transform, ip, 20, Color.white);
            txt.fontStyle = FontStyles.Bold;
            txt.alignment = TextAlignmentOptions.Center;
            txt.raycastTarget = false;
            var trt = txt.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(6, 6); trt.offsetMax = new Vector2(-6, -6);

            var btn = cellGO.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => OnCellClicked(captureIdx, ip));

            _cellButtons.Add(btn);
            _cellLabels.Add(txt);
            _cellBgs.Add(bg);
        }
    }

    void ReshuffleGrid()
    {
        // Capture which IPs were already correctly clicked so we keep them green/locked
        var locked = new Dictionary<string, bool>();
        for (int i = 0; i < _cellLabels.Count; i++)
        {
            if (!_cellButtons[i].interactable) locked[_cellLabels[i].text] = true;
        }

        var keep = _nextTargetIdx > 0 ? new List<string>(_targetSequence) : null;
        BuildGrid();

        // If we were partway through, force the unlocked target back into the new grid
        if (keep != null)
        {
            for (int i = _nextTargetIdx; i < keep.Count; i++)
            {
                int slot = UnityEngine.Random.Range(0, _cellLabels.Count);
                _cellLabels[slot].text = keep[i];
            }
            _targetSequence = keep;
            UpdateTargetLabel();
        }
    }

    static string RandomIp()
    {
        // 70% chance of a plausible IPv4, 30% chance of a fake hostname for flavor
        if (UnityEngine.Random.value < 0.7f)
        {
            int prefixIdx = UnityEngine.Random.Range(0, 3);
            string prefix = HOST_PREFIXES[prefixIdx];
            return $"{prefix}.{UnityEngine.Random.Range(0, 255)}.{UnityEngine.Random.Range(0, 255)}";
        }
        else
        {
            int prefixIdx = UnityEngine.Random.Range(3, HOST_PREFIXES.Length);
            return $"{HOST_PREFIXES[prefixIdx]}-{UnityEngine.Random.Range(10, 99)}";
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  UI builder
    // ════════════════════════════════════════════════════════════════════════
    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9500;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable   = false;

        // Full-screen dim background
        var dim = NewImage("Dim", transform, new Color(0, 0, 0, 0.78f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true; // block clicks behind

        // Center panel
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(transform, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot     = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(1100, 720);
        panel.AddComponent<Image>().color = C_BG;

        // Top accent line
        var topLine = NewImage("TopAccent", panel.transform, C_CYAN);
        var trt = topLine.rectTransform;
        trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1);
        trt.pivot = new Vector2(0.5f, 1);
        trt.sizeDelta = new Vector2(0, 3);

        // Title
        _titleText = NewText("Title", panel.transform, "HACK :: LOCK ONTO TARGET IP", 28, C_CYAN);
        _titleText.fontStyle = FontStyles.Bold;
        _titleText.characterSpacing = 6;
        var ttrt = _titleText.rectTransform;
        ttrt.anchorMin = new Vector2(0, 1); ttrt.anchorMax = new Vector2(1, 1);
        ttrt.pivot = new Vector2(0.5f, 1);
        ttrt.anchoredPosition = new Vector2(0, -30);
        ttrt.sizeDelta = new Vector2(0, 40);
        _titleText.alignment = TextAlignmentOptions.Center;

        // Target IP label (big, center)
        _targetText = NewText("TargetIP", panel.transform, "TARGET IP: -", 36, Color.white);
        _targetText.alignment = TextAlignmentOptions.Center;
        var trt2 = _targetText.rectTransform;
        trt2.anchorMin = new Vector2(0, 1); trt2.anchorMax = new Vector2(1, 1);
        trt2.pivot = new Vector2(0.5f, 1);
        trt2.anchoredPosition = new Vector2(0, -90);
        trt2.sizeDelta = new Vector2(0, 60);

        // Hint
        _hintText = NewText("Hint", panel.transform, "", 20, C_GRAY);
        _hintText.alignment = TextAlignmentOptions.Center;
        var hrt = _hintText.rectTransform;
        hrt.anchorMin = new Vector2(0, 1); hrt.anchorMax = new Vector2(1, 1);
        hrt.pivot = new Vector2(0.5f, 1);
        hrt.anchoredPosition = new Vector2(0, -160);
        hrt.sizeDelta = new Vector2(0, 30);

        // Timer bar background
        var barBg = NewImage("TimerBarBG", panel.transform, new Color(1, 1, 1, 0.1f));
        var bbrt = barBg.rectTransform;
        bbrt.anchorMin = new Vector2(0, 1); bbrt.anchorMax = new Vector2(1, 1);
        bbrt.pivot = new Vector2(0.5f, 1);
        bbrt.anchoredPosition = new Vector2(0, -210);
        bbrt.sizeDelta = new Vector2(-100, 10);

        // Timer fill
        _timerBar = NewImage("TimerBar", panel.transform, C_CYAN);
        var tbrt = _timerBar.rectTransform;
        tbrt.anchorMin = new Vector2(0, 1); tbrt.anchorMax = new Vector2(1, 1);
        tbrt.pivot = new Vector2(0, 1);
        tbrt.anchoredPosition = new Vector2(50, -210);
        tbrt.sizeDelta = new Vector2(-100, 10);
        _timerBar.type = Image.Type.Filled;
        _timerBar.fillMethod = Image.FillMethod.Horizontal;
        _timerBar.fillOrigin = (int)Image.OriginHorizontal.Left;
        _timerBar.fillAmount = 1f;

        // Timer text (right side)
        _timerText = NewText("TimerText", panel.transform, "0.0s", 18, C_CYAN);
        _timerText.alignment = TextAlignmentOptions.Right;
        var trtt = _timerText.rectTransform;
        trtt.anchorMin = new Vector2(1, 1); trtt.anchorMax = new Vector2(1, 1);
        trtt.pivot = new Vector2(1, 1);
        trtt.anchoredPosition = new Vector2(-50, -190);
        trtt.sizeDelta = new Vector2(120, 28);

        // Grid
        var gridGO = new GameObject("Grid", typeof(RectTransform));
        gridGO.transform.SetParent(panel.transform, false);
        _gridRT = gridGO.GetComponent<RectTransform>();
        _gridRT.anchorMin = new Vector2(0.5f, 0.5f);
        _gridRT.anchorMax = new Vector2(0.5f, 0.5f);
        _gridRT.pivot = new Vector2(0.5f, 0.5f);
        _gridRT.anchoredPosition = new Vector2(0, -70);
        _gridRT.sizeDelta = new Vector2(960, 360);

        _grid = gridGO.AddComponent<GridLayoutGroup>();
        _grid.cellSize        = new Vector2(220, 80);
        _grid.spacing         = new Vector2(10, 10);
        _grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        _grid.constraintCount = 4;
        _grid.childAlignment  = TextAnchor.MiddleCenter;

        // Footer
        var foot = NewText("Footer", panel.transform, "FACILITY BREACH  ·  HACK PROTOCOL", 14, C_GRAY);
        foot.alignment = TextAlignmentOptions.Center;
        foot.characterSpacing = 6;
        var frt = foot.rectTransform;
        frt.anchorMin = new Vector2(0, 0); frt.anchorMax = new Vector2(1, 0);
        frt.pivot = new Vector2(0.5f, 0);
        frt.anchoredPosition = new Vector2(0, 24);
        frt.sizeDelta = new Vector2(0, 24);
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = color;
        t.richText = true;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
