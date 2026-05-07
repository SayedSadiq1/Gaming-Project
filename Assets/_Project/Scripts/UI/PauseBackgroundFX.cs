using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Pause Menu Background FX
//  ─────────────────────────────────────────────────────────────────────────────
//  A lighter version of MenuBackgroundFX for the pause overlay:
//    • Fewer, slower drifting cyan particles
//    • Subtle scanlines
//    • Pulsing dark overlay
//
//  Uses Time.unscaledDeltaTime so animations play while Time.timeScale == 0.
// ─────────────────────────────────────────────────────────────────────────────
public class PauseBackgroundFX : MonoBehaviour
{
    [Header("Particles")]
    public int   particleCount = 25;       // fewer than main menu
    public float minSpeed      = 4f;       // slower drift
    public float maxSpeed      = 14f;
    public float minSize       = 2f;
    public float maxSize       = 5f;
    public Color particleColor = new Color(0f, 0.78f, 1f, 0.35f);

    [Header("Scanlines")]
    public int   scanlineCount = 3;
    public float scanlineSpeed = 8f;

    [Header("Haze pulse")]
    public Image hazeImage;
    public float pulseSpeed = 0.3f;
    public float minAlpha   = 0.75f;
    public float maxAlpha   = 0.90f;

    RectTransform _canvasRT;
    readonly List<RectTransform> _particles = new();
    readonly List<float>         _speeds    = new();
    readonly List<RectTransform> _scanlines = new();

    void OnEnable()
    {
        // Re-resolve canvas each time the pause overlay opens
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) { enabled = false; return; }
        _canvasRT = canvas.GetComponent<RectTransform>();

        // Only spawn once; subsequent activations reuse them
        if (_particles.Count == 0) SpawnParticles();
        if (_scanlines.Count == 0) SpawnScanlines();
    }

    void SpawnParticles()
    {
        var holder = new GameObject("PauseParticles", typeof(RectTransform));
        holder.transform.SetParent(transform, false);
        Stretch(holder.GetComponent<RectTransform>());

        float w = _canvasRT.rect.width;
        float h = _canvasRT.rect.height;

        for (int i = 0; i < particleCount; i++)
        {
            var go = new GameObject("P_" + i, typeof(RectTransform));
            go.transform.SetParent(holder.transform, false);
            var rt = go.GetComponent<RectTransform>();
            float s = Random.Range(minSize, maxSize);
            rt.sizeDelta = new Vector2(s, s);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(Random.Range(0, w), Random.Range(0, h));

            var img = go.AddComponent<Image>();
            var c = particleColor;
            c.a *= Random.Range(0.2f, 1f);
            img.color = c;
            img.raycastTarget = false;

            _particles.Add(rt);
            _speeds.Add(Random.Range(minSpeed, maxSpeed));
        }
    }

    void SpawnScanlines()
    {
        var holder = new GameObject("PauseScanlines", typeof(RectTransform));
        holder.transform.SetParent(transform, false);
        Stretch(holder.GetComponent<RectTransform>());

        float h = _canvasRT.rect.height;
        for (int i = 0; i < scanlineCount; i++)
        {
            var go = new GameObject("SL_" + i, typeof(RectTransform));
            go.transform.SetParent(holder.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(0, 1f);
            rt.anchoredPosition = new Vector2(0, h * (i + 0.5f) / scanlineCount);

            var img = go.AddComponent<Image>();
            img.color = new Color(0f, 0.78f, 1f, 0.06f);
            img.raycastTarget = false;

            _scanlines.Add(rt);
        }
    }

    void Update()
    {
        if (_canvasRT == null) return;
        float w = _canvasRT.rect.width;
        float h = _canvasRT.rect.height;
        float dt = Time.unscaledDeltaTime;   // works while paused

        // Particles drift up
        for (int i = 0; i < _particles.Count; i++)
        {
            var rt  = _particles[i];
            var pos = rt.anchoredPosition;
            pos.y += _speeds[i] * dt;
            pos.x += Mathf.Sin(Time.unscaledTime * 0.4f + i) * 0.3f;
            if (pos.y > h + 15) { pos.y = -15; pos.x = Random.Range(0, w); }
            rt.anchoredPosition = pos;
        }

        // Scanlines pan upward
        for (int i = 0; i < _scanlines.Count; i++)
        {
            var rt  = _scanlines[i];
            var pos = rt.anchoredPosition;
            pos.y += scanlineSpeed * dt;
            if (pos.y > h + 5) pos.y = -5;
            rt.anchoredPosition = pos;
        }

        // Haze pulse
        if (hazeImage != null)
        {
            var c = hazeImage.color;
            c.a = Mathf.Lerp(minAlpha, maxAlpha,
                             (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f);
            hazeImage.color = c;
        }
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
