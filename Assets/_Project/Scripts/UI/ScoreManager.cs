using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Score Manager
//  ─────────────────────────────────────────────────────────────────────────────
//  Singleton that tracks kills and score, and displays them on a HUD.
//
//  Features:
//    • Kill counter + total score (top-right)
//    • "+Points" pop-up that fades out on each kill
//    • Kill feed showing recent kills (fades after a few seconds)
//    • Score persists via PlayerPrefs (FB_ prefix)
//    • Builds its own UI on Awake — no manual setup needed
//
//  Usage:
//    ScoreManager.Instance.AddKill("Enemy Name", pointValue);
// ─────────────────────────────────────────────────────────────────────────────
public class ScoreManager : MonoBehaviour
{
    // ── Singleton ────────────────────────────────────────────────────────────
    public static ScoreManager Instance { get; private set; }

    // ── Settings ─────────────────────────────────────────────────────────────
    [Header("Points")]
    public int pointsPerKill = 100;

    [Header("Kill Feed")]
    public float feedDisplayTime = 3f;
    public int   maxFeedEntries  = 4;

    // ── State ────────────────────────────────────────────────────────────────
    int _kills;
    int _score;

    // ── UI refs (built at runtime) ──────────────────────────────────────────
    Canvas         _canvas;
    TextMeshProUGUI _scoreText;
    TextMeshProUGUI _killsText;
    TextMeshProUGUI _popupText;
    readonly List<TextMeshProUGUI> _feedEntries = new();
    readonly List<float>           _feedTimers  = new();

    // ── Colors (matching Facility Breach theme) ─────────────────────────────
    static readonly Color C_CYAN  = new Color32(0x00, 0xC8, 0xFF, 0xFF);
    static readonly Color C_WHITE = Color.white;
    static readonly Color C_GRAY  = new Color32(0xAA, 0xAA, 0xBB, 0xFF);
    static readonly Color C_BG    = new Color(0f, 0f, 0f, 0.35f);

    // ── PlayerPrefs keys ────────────────────────────────────────────────────
    const string K_SCORE = "FB_Score";
    const string K_KILLS = "FB_Kills";

    // ─────────────────────────────────────────────────────────────────────────
    //  LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Always start fresh — score resets on scene load / restart
        _kills = 0;
        _score = 0;
        PlayerPrefs.SetInt(K_KILLS, 0);
        PlayerPrefs.SetInt(K_SCORE, 0);

        BuildHUD();
        RefreshDisplay();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // Fade out kill feed entries over time
        for (int i = _feedEntries.Count - 1; i >= 0; i--)
        {
            _feedTimers[i] -= Time.deltaTime;
            if (_feedTimers[i] <= 0)
            {
                Destroy(_feedEntries[i].gameObject);
                _feedEntries.RemoveAt(i);
                _feedTimers.RemoveAt(i);
                RepositionFeed();
            }
            else if (_feedTimers[i] < 1f)
            {
                // Fade out in the last second
                var c = _feedEntries[i].color;
                c.a = _feedTimers[i];
                _feedEntries[i].color = c;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PUBLIC API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Call this when an enemy dies. Increments kill count, adds points,
    /// shows popup and kill feed entry.
    /// </summary>
    /// <param name="enemyName">Display name (e.g. "Insurgent"). Falls back to "Enemy".</param>
    /// <param name="points">Points awarded. Uses pointsPerKill if 0.</param>
    public void AddKill(string enemyName = "Enemy", int points = 0)
    {
        if (points <= 0) points = pointsPerKill;

        _kills++;
        _score += points;

        // Save
        PlayerPrefs.SetInt(K_KILLS, _kills);
        PlayerPrefs.SetInt(K_SCORE, _score);

        RefreshDisplay();
        ShowPopup($"+{points}");
        AddFeedEntry(enemyName);
    }

    /// <summary>Resets score and kills to zero (e.g. on New Game).</summary>
    public void ResetScore()
    {
        _kills = 0;
        _score = 0;
        PlayerPrefs.SetInt(K_KILLS, 0);
        PlayerPrefs.SetInt(K_SCORE, 0);
        RefreshDisplay();
    }

    /// <summary>Current total score.</summary>
    public int Score => _score;

    /// <summary>Current kill count.</summary>
    public int Kills => _kills;

    // ─────────────────────────────────────────────────────────────────────────
    //  HUD BUILDER (creates UI at runtime — no prefab needed)
    // ─────────────────────────────────────────────────────────────────────────

    void BuildHUD()
    {
        // Canvas
        var canvasGO = new GameObject("ScoreCanvas");
        canvasGO.transform.SetParent(transform, false);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 50;   // above gameplay, below pause menu (100)

        var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode         = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        // ── Score display (top-right) ───────────────────────────────────────

        // Background panel for score area
        var scorePanelGO = new GameObject("ScorePanel", typeof(RectTransform));
        scorePanelGO.transform.SetParent(canvasGO.transform, false);
        var panelImg = scorePanelGO.AddComponent<UnityEngine.UI.Image>();
        panelImg.color = C_BG;
        panelImg.raycastTarget = false;
        var panelRT = scorePanelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(1, 1);
        panelRT.anchorMax = new Vector2(1, 1);
        panelRT.pivot     = new Vector2(1, 1);
        panelRT.anchoredPosition = new Vector2(-20, -20);
        panelRT.sizeDelta = new Vector2(280, 90);

        // "SCORE" label + value
        _scoreText = CreateTMPText("ScoreText", scorePanelGO.transform,
            "SCORE: 0", 32, C_CYAN, TextAlignmentOptions.Right);
        var srt = _scoreText.rectTransform;
        srt.anchorMin = new Vector2(0, 0.5f);
        srt.anchorMax = new Vector2(1, 1);
        srt.offsetMin = new Vector2(12, 0);
        srt.offsetMax = new Vector2(-12, -8);
        _scoreText.fontStyle = FontStyles.Bold;
        _scoreText.characterSpacing = 3;

        // "KILLS" label + value
        _killsText = CreateTMPText("KillsText", scorePanelGO.transform,
            "KILLS: 0", 22, C_GRAY, TextAlignmentOptions.Right);
        var krt = _killsText.rectTransform;
        krt.anchorMin = new Vector2(0, 0);
        krt.anchorMax = new Vector2(1, 0.5f);
        krt.offsetMin = new Vector2(12, 6);
        krt.offsetMax = new Vector2(-12, 0);
        _killsText.characterSpacing = 2;

        // ── Points popup (center-right, fades out) ──────────────────────────
        _popupText = CreateTMPText("PopupText", canvasGO.transform,
            "", 40, C_CYAN, TextAlignmentOptions.Center);
        _popupText.fontStyle = FontStyles.Bold;
        var prt = _popupText.rectTransform;
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot     = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = new Vector2(0, 80);
        prt.sizeDelta = new Vector2(300, 50);
        _popupText.gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  DISPLAY HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    void RefreshDisplay()
    {
        if (_scoreText != null) _scoreText.text = $"SCORE: {_score}";
        if (_killsText != null) _killsText.text = $"KILLS: {_kills}";
    }

    void ShowPopup(string text)
    {
        if (_popupText == null) return;
        StopCoroutine(nameof(PopupRoutine));
        _popupText.text = text;
        _popupText.gameObject.SetActive(true);
        StartCoroutine(nameof(PopupRoutine));
    }

    IEnumerator PopupRoutine()
    {
        float duration = 1.2f;
        float elapsed  = 0f;
        var   rt       = _popupText.rectTransform;
        var   startPos = new Vector2(0, 80);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Float upward and fade out
            rt.anchoredPosition = startPos + new Vector2(0, t * 40f);
            var c = C_CYAN;
            c.a = 1f - t;
            _popupText.color = c;

            yield return null;
        }

        _popupText.gameObject.SetActive(false);
    }

    void AddFeedEntry(string enemyName)
    {
        // Trim old entries if at max
        while (_feedEntries.Count >= maxFeedEntries)
        {
            Destroy(_feedEntries[0].gameObject);
            _feedEntries.RemoveAt(0);
            _feedTimers.RemoveAt(0);
        }

        var entry = CreateTMPText("FeedEntry", _canvas.transform,
            $"► {enemyName} eliminated", 20, C_WHITE, TextAlignmentOptions.Right);
        entry.characterSpacing = 2;

        _feedEntries.Add(entry);
        _feedTimers.Add(feedDisplayTime);

        RepositionFeed();
    }

    void RepositionFeed()
    {
        // Stack feed entries below the score panel (top-right)
        for (int i = 0; i < _feedEntries.Count; i++)
        {
            var rt = _feedEntries[i].rectTransform;
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot     = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-24, -120 - (i * 30));
            rt.sizeDelta = new Vector2(400, 28);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  TEXT FACTORY
    // ─────────────────────────────────────────────────────────────────────────

    static TextMeshProUGUI CreateTMPText(string name, Transform parent,
        string text, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;
        return tmp;
    }
}
