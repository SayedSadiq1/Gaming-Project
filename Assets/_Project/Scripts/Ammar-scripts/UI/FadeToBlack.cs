using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Fade-to-Black Cutscene Helper
//
//  Call FadeToBlack.Play(duration, "MISSION COMPLETE", "To be continued...")
//  from anywhere. Spawns its own DontDestroyOnLoad canvas so the fade
//  survives the next SceneManager.LoadScene call.
//
//  Used by ExitGatePanel to play the level-end cutscene.
// ─────────────────────────────────────────────────────────────────────────────
public class FadeToBlack : MonoBehaviour
{
    static FadeToBlack _instance;
    Image _blackImage;
    TextMeshProUGUI _titleText;
    TextMeshProUGUI _subText;
    CanvasGroup _group;
    Coroutine _routine;

    public static void Play(float duration, string title, string sub)
    {
        EnsureInstance();
        _instance.PlayInternal(duration, title, sub);
    }

    static void EnsureInstance()
    {
        if (_instance != null) return;
        var go = new GameObject("FadeToBlackCanvas");
        DontDestroyOnLoad(go);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        go.AddComponent<GraphicRaycaster>();

        var group = go.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        // Black background
        var bgGO = new GameObject("Black", typeof(RectTransform));
        bgGO.transform.SetParent(go.transform, false);
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = Color.black;
        bgImg.raycastTarget = false;

        // Title
        var tGO = new GameObject("Title", typeof(RectTransform));
        tGO.transform.SetParent(go.transform, false);
        var tRT = tGO.GetComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0, 0.5f);
        tRT.anchorMax = new Vector2(1, 0.5f);
        tRT.pivot     = new Vector2(0.5f, 0.5f);
        tRT.sizeDelta = new Vector2(0, 120);
        tRT.anchoredPosition = new Vector2(0, 40);
        var t = tGO.AddComponent<TextMeshProUGUI>();
        t.text = "MISSION COMPLETE";
        t.fontSize = 72;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0f, 0.78f, 1f, 1f); // cyan
        t.characterSpacing = 8;
        t.raycastTarget = false;

        // Subtitle
        var sGO = new GameObject("Sub", typeof(RectTransform));
        sGO.transform.SetParent(go.transform, false);
        var sRT = sGO.GetComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0, 0.5f);
        sRT.anchorMax = new Vector2(1, 0.5f);
        sRT.pivot     = new Vector2(0.5f, 0.5f);
        sRT.sizeDelta = new Vector2(0, 60);
        sRT.anchoredPosition = new Vector2(0, -40);
        var s = sGO.AddComponent<TextMeshProUGUI>();
        s.text = "To be continued...";
        s.fontSize = 28;
        s.alignment = TextAlignmentOptions.Center;
        s.color = new Color(1f, 1f, 1f, 0.7f);
        s.characterSpacing = 3;
        s.raycastTarget = false;

        var inst = go.AddComponent<FadeToBlack>();
        inst._blackImage = bgImg;
        inst._titleText  = t;
        inst._subText    = s;
        inst._group      = group;
        _instance = inst;
    }

    void PlayInternal(float duration, string title, string sub)
    {
        if (_titleText != null) _titleText.text = title ?? "";
        if (_subText   != null) _subText.text   = sub   ?? "";
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(FadeRoutine(duration));
    }

    IEnumerator FadeRoutine(float duration)
    {
        _group.blocksRaycasts = true;

        // Hide text initially, fade screen first.
        if (_titleText != null) _titleText.color = new Color(_titleText.color.r, _titleText.color.g, _titleText.color.b, 0f);
        if (_subText   != null) _subText.color   = new Color(_subText.color.r,   _subText.color.g,   _subText.color.b,   0f);

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            _group.alpha = k;
            yield return null;
        }
        _group.alpha = 1f;

        // Fade text in over 0.7s after screen is solid black
        float td = 0.7f, t2 = 0f;
        while (t2 < td)
        {
            t2 += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t2 / td);
            if (_titleText != null) _titleText.color = new Color(_titleText.color.r, _titleText.color.g, _titleText.color.b, k);
            if (_subText   != null) _subText.color   = new Color(_subText.color.r,   _subText.color.g,   _subText.color.b,   0.7f * k);
            yield return null;
        }
    }
}
