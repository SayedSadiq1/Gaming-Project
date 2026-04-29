using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Menu Background FX
//  - Drifting cyan particles (rising slowly)
//  - Slow horizontal scanlines panning vertically
//  - Pulsing haze layer
// ─────────────────────────────────────────────────────────────────────────────
public class MenuBackgroundFX : MonoBehaviour
{
    [Header("Particles")]
    public int   particleCount = 50;
    public float minSpeed      = 6f;
    public float maxSpeed      = 22f;
    public float minSize       = 2f;
    public float maxSize       = 6f;
    public Color particleColor = new Color(0f, 0.78f, 1f, 0.6f);

    [Header("Scanlines")]
    public int   scanlineCount = 4;
    public float scanlineSpeed = 12f;

    [Header("Haze pulse")]
    public Image hazeImage;
    public float pulseSpeed = 0.4f;
    public float minAlpha   = 0.55f;
    public float maxAlpha   = 0.85f;

    RectTransform _canvasRT;
    readonly List<RectTransform> _particles = new();
    readonly List<float>         _speeds    = new();
    readonly List<RectTransform> _scanlines = new();

    void Start()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) { enabled = false; return; }
        _canvasRT = canvas.GetComponent<RectTransform>();

        SpawnParticles();
        SpawnScanlines();
    }

    void SpawnParticles()
    {
        var holder = new GameObject("Particles", typeof(RectTransform));
        holder.transform.SetParent(transform, false);
        var hRT = holder.GetComponent<RectTransform>();
        Stretch(hRT);

        for (int i = 0; i < particleCount; i++)
        {
            var go = new GameObject("Particle_" + i, typeof(RectTransform));
            go.transform.SetParent(holder.transform, false);
            var rt = go.GetComponent<RectTransform>();
            float s = Random.Range(minSize, maxSize);
            rt.sizeDelta = new Vector2(s, s);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot     = new Vector2(0.5f, 0.5f);

            float w = _canvasRT.rect.width;
            float h = _canvasRT.rect.height;
            rt.anchoredPosition = new Vector2(Random.Range(0, w), Random.Range(0, h));

            var img = go.AddComponent<Image>();
            var c   = particleColor;
            c.a    *= Random.Range(0.3f, 1f);
            img.color = c;
            img.raycastTarget = false;

            _particles.Add(rt);
            _speeds.Add(Random.Range(minSpeed, maxSpeed));
        }
    }

    void SpawnScanlines()
    {
        var holder = new GameObject("Scanlines", typeof(RectTransform));
        holder.transform.SetParent(transform, false);
        var hRT = holder.GetComponent<RectTransform>();
        Stretch(hRT);

        float h = _canvasRT.rect.height;
        for (int i = 0; i < scanlineCount; i++)
        {
            var go = new GameObject("Scanline_" + i, typeof(RectTransform));
            go.transform.SetParent(holder.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(0, 1f);
            rt.anchoredPosition = new Vector2(0, h * (i + 0.5f) / scanlineCount);

            var img = go.AddComponent<Image>();
            img.color = new Color(0f, 0.78f, 1f, 0.10f);
            img.raycastTarget = false;

            _scanlines.Add(rt);
        }
    }

    void Update()
    {
        if (_canvasRT == null) return;
        float w = _canvasRT.rect.width;
        float h = _canvasRT.rect.height;

        // Particles drift up + horizontal jitter
        for (int i = 0; i < _particles.Count; i++)
        {
            var rt  = _particles[i];
            var pos = rt.anchoredPosition;
            pos.y += _speeds[i] * Time.unscaledDeltaTime;
            pos.x += Mathf.Sin(Time.unscaledTime * 0.5f + i) * 0.4f;
            if (pos.y > h + 20)
            {
                pos.y = -20;
                pos.x = Random.Range(0, w);
            }
            rt.anchoredPosition = pos;
        }

        // Scanlines pan upward, wrap
        for (int i = 0; i < _scanlines.Count; i++)
        {
            var rt  = _scanlines[i];
            var pos = rt.anchoredPosition;
            pos.y += scanlineSpeed * Time.unscaledDeltaTime;
            if (pos.y > h + 5) pos.y = -5;
            rt.anchoredPosition = pos;
        }

        // Haze pulse
        if (hazeImage != null)
        {
            var c = hazeImage.color;
            c.a   = Mathf.Lerp(minAlpha, maxAlpha,
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
