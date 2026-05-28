using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Loading Screen
//
//  Drop-in replacement for SceneManager.LoadScene(...). Shows a full-screen
//  branded loading image for a minimum duration before activating the new
//  scene. Used for every scene transition: New Game, Continue, Quit to
//  Main Menu, Next Level, Play Again, etc.
//
//  USAGE:
//      LoadingScreen.Load("Level1");
//      LoadingScreen.Load("MainMenu", 3f);  // override min duration
//
//  Image is loaded from Resources/UI/Loading_Background.png. Put your image
//  at: Assets/_Project/Resources/UI/Loading_Background.png (the file MUST
//  live inside a folder named "Resources" — any number of nested folders is
//  fine — and its Texture Type set to Sprite (2D and UI)).
// ─────────────────────────────────────────────────────────────────────────────
public class LoadingScreen : MonoBehaviour
{
    static LoadingScreen _instance;

    public const float DEFAULT_MIN_DURATION = 5f;
    public const string RESOURCES_PATH      = "UI/Loading_Background";

    // ── UI refs (built at runtime) ────────────────────────────────────────
    CanvasGroup       _group;
    Image             _background;
    RectTransform     _barFillRT;
    TextMeshProUGUI   _percentText;

    static readonly Color C_BG_DARK  = new Color(0.02f, 0.03f, 0.06f, 1f);
    static readonly Color C_CYAN     = new Color32(0x00, 0xC8, 0xFF, 0xFF);
    static readonly Color C_CYAN_DIM = new Color(0f, 0.78f, 1f, 0.35f);
    static readonly Color C_GRAY     = new Color32(0x88, 0x88, 0x95, 0xFF);

    bool _isLoading;

    /// <summary>Drop-in replacement for SceneManager.LoadScene with a branded loading screen.</summary>
    public static void Load(string sceneName, float minDuration = DEFAULT_MIN_DURATION)
    {
        EnsureInstance();
        if (_instance._isLoading)
        {
            Debug.Log($"[LoadingScreen] Already loading — ignoring request for '{sceneName}'.");
            return;
        }
        _instance.StartCoroutine(_instance.LoadRoutine(sceneName, minDuration));
    }

    /// <summary>Same as Load() but accepts a scene build index.</summary>
    public static void Load(int sceneBuildIndex, float minDuration = DEFAULT_MIN_DURATION)
    {
        EnsureInstance();
        if (_instance._isLoading)
        {
            Debug.Log($"[LoadingScreen] Already loading — ignoring request for build index {sceneBuildIndex}.");
            return;
        }
        _instance.StartCoroutine(_instance.LoadRoutineByIndex(sceneBuildIndex, minDuration));
    }

    static void EnsureInstance()
    {
        if (_instance != null) return;

        var go = new GameObject("LoadingScreen");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<LoadingScreen>();
        _instance.Build();
    }

    void Build()
    {
        // ── Canvas ──
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10000;   // above everything (including FadeToBlack)
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable   = false;

        // ── Background image (full-screen) ──
        var bgGO = new GameObject("Background", typeof(RectTransform));
        bgGO.transform.SetParent(transform, false);
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;
        _background = bgGO.AddComponent<Image>();
        _background.color = Color.white;
        _background.preserveAspect = false;   // fill viewport
        _background.raycastTarget  = true;     // block clicks
        var loadedSprite = Resources.Load<Sprite>(RESOURCES_PATH);
        if (loadedSprite != null) _background.sprite = loadedSprite;
        else
        {
            Debug.LogWarning($"[LoadingScreen] No sprite found at Resources/{RESOURCES_PATH}.png — " +
                             "falling back to dark background. Move Loading_Background.png into a " +
                             "Resources folder and set its Texture Type to Sprite (2D and UI).");
            _background.color = C_BG_DARK;
        }

        // Dim vignette on top so text reads clearly
        var dim = NewImage("Dim", transform, new Color(0, 0, 0, 0.35f));
        dim.rectTransform.anchorMin = Vector2.zero;
        dim.rectTransform.anchorMax = Vector2.one;
        dim.rectTransform.offsetMin = Vector2.zero;
        dim.rectTransform.offsetMax = Vector2.zero;

        // ── Top accent line ──
        var topLine = NewImage("TopAccent", transform, C_CYAN);
        var tlRT = topLine.rectTransform;
        tlRT.anchorMin = new Vector2(0, 1); tlRT.anchorMax = new Vector2(1, 1);
        tlRT.pivot = new Vector2(0.5f, 1);
        tlRT.anchoredPosition = new Vector2(0, -8);
        tlRT.sizeDelta = new Vector2(0, 2);

        // ── Bottom accent line ──
        var botLine = NewImage("BottomAccent", transform, C_CYAN_DIM);
        var blRT = botLine.rectTransform;
        blRT.anchorMin = new Vector2(0, 0); blRT.anchorMax = new Vector2(1, 0);
        blRT.pivot = new Vector2(0.5f, 0);
        blRT.anchoredPosition = new Vector2(0, 8);
        blRT.sizeDelta = new Vector2(0, 2);

        // ── "LOADING" label (top-left) ──
        var loadingLabel = NewText("LoadingLabel", transform, "LOADING", 28, C_CYAN);
        loadingLabel.fontStyle = FontStyles.Bold;
        loadingLabel.characterSpacing = 8;
        var llRT = loadingLabel.rectTransform;
        llRT.anchorMin = new Vector2(0, 0); llRT.anchorMax = new Vector2(0, 0);
        llRT.pivot = new Vector2(0, 0);
        llRT.anchoredPosition = new Vector2(80, 160);
        llRT.sizeDelta = new Vector2(400, 40);

        // ── Percent text (right of loading label) ──
        _percentText = NewText("Percent", transform, "0%", 28, Color.white);
        _percentText.fontStyle = FontStyles.Bold;
        var pRT = _percentText.rectTransform;
        pRT.anchorMin = new Vector2(1, 0); pRT.anchorMax = new Vector2(1, 0);
        pRT.pivot = new Vector2(1, 0);
        pRT.anchoredPosition = new Vector2(-80, 160);
        pRT.sizeDelta = new Vector2(120, 40);
        _percentText.alignment = TextAlignmentOptions.MidlineRight;

        // ── Progress bar background ──
        var barBg = NewImage("BarBG", transform, new Color(1, 1, 1, 0.15f));
        var bbgRT = barBg.rectTransform;
        bbgRT.anchorMin = new Vector2(0, 0); bbgRT.anchorMax = new Vector2(1, 0);
        bbgRT.pivot = new Vector2(0.5f, 0);
        bbgRT.anchoredPosition = new Vector2(0, 130);
        bbgRT.sizeDelta = new Vector2(-160, 8);

        // ── Progress fill (grows left-to-right via anchorMax.x) ──
        var barFill = NewImage("BarFill", barBg.transform, C_CYAN);
        _barFillRT = barFill.rectTransform;
        _barFillRT.anchorMin = new Vector2(0, 0);
        _barFillRT.anchorMax = new Vector2(0, 1);   // starts at 0 width
        _barFillRT.pivot     = new Vector2(0, 0.5f);
        _barFillRT.offsetMin = Vector2.zero;
        _barFillRT.offsetMax = Vector2.zero;

        // ── Footer ──
        var foot = NewText("Footer", transform, "FACILITY BREACH  ·  TACTICAL OPS", 16, C_GRAY);
        foot.characterSpacing = 8;
        var fRT = foot.rectTransform;
        fRT.anchorMin = new Vector2(0, 0); fRT.anchorMax = new Vector2(1, 0);
        fRT.pivot = new Vector2(0.5f, 0);
        fRT.anchoredPosition = new Vector2(0, 30);
        fRT.sizeDelta = new Vector2(0, 24);
        foot.alignment = TextAlignmentOptions.Center;
    }

    IEnumerator LoadRoutine(string sceneName, float minDuration)
    {
        yield return ShowAndLoad(SceneManager.LoadSceneAsync(sceneName), minDuration);
    }

    IEnumerator LoadRoutineByIndex(int sceneBuildIndex, float minDuration)
    {
        yield return ShowAndLoad(SceneManager.LoadSceneAsync(sceneBuildIndex), minDuration);
    }

    IEnumerator ShowAndLoad(AsyncOperation op, float minDuration)
    {
        if (op == null)
        {
            Debug.LogError("[LoadingScreen] LoadSceneAsync returned null. Is the scene in Build Settings?");
            yield break;
        }
        _isLoading = true;
        op.allowSceneActivation = false;

        // Free the cursor + ensure time isn't paused (we just left a possibly-paused scene)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        Time.timeScale   = 1f;

        _group.blocksRaycasts = true;
        _group.alpha = 1f;
        SetProgress(0f);

        float elapsed = 0f;
        while (true)
        {
            elapsed += Time.unscaledDeltaTime;

            // Unity reports progress 0→0.9 while loading; the last 0.1 is activation.
            float loadProg = Mathf.Clamp01(op.progress / 0.9f);
            float timeProg = Mathf.Clamp01(elapsed / minDuration);
            // Visible progress is the slower of the two so the bar always finishes
            // when both conditions are met.
            SetProgress(Mathf.Min(loadProg, timeProg));

            bool minTimeMet  = elapsed   >= minDuration;
            bool sceneReady  = op.progress >= 0.9f;
            if (minTimeMet && sceneReady) break;

            yield return null;
        }

        SetProgress(1f);
        op.allowSceneActivation = true;

        // Wait one more frame for the new scene to fully take over, then fade out
        yield return null;
        yield return new WaitForSecondsRealtime(0.15f);

        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _isLoading = false;
    }

    void SetProgress(float t)
    {
        t = Mathf.Clamp01(t);
        if (_barFillRT != null)
        {
            var a = _barFillRT.anchorMax;
            a.x = t;
            _barFillRT.anchorMax = a;
        }
        if (_percentText != null) _percentText.text = Mathf.RoundToInt(t * 100f) + "%";
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
        t.enableWordWrapping = true;
        t.raycastTarget = false;
        return t;
    }
}
