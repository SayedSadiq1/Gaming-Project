using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Button Hover FX
//  - Slight scale on hover
//  - Cyan accent bar pulses to full opacity
//  - Plays hover/click SFX
// ─────────────────────────────────────────────────────────────────────────────
[RequireComponent(typeof(RectTransform))]
public class ButtonHoverFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Visuals")]
    public RectTransform target;
    public Image accentBar;
    public Image background;
    public float hoverScale = 1.04f;
    public float lerpSpeed  = 12f;

    [Header("Colors")]
    public Color accentNormal     = new Color(0f, 0.78f, 1f, 0.35f);
    public Color accentHover      = new Color(0f, 0.78f, 1f, 1f);
    public Color backgroundNormal = new Color(0f, 0f, 0f, 0.45f);
    public Color backgroundHover  = new Color(0f, 0.10f, 0.18f, 0.85f);

    [Header("Audio")]
    public AudioSource sfxSource;
    public AudioClip hoverClip;
    public AudioClip clickClip;

    Vector3 _baseScale;
    bool _hovered;

    void Awake()
    {
        if (target == null) target = GetComponent<RectTransform>();
        _baseScale = target.localScale;
        if (accentBar)  accentBar.color  = accentNormal;
        if (background) background.color = backgroundNormal;
    }

    void Update()
    {
        // Disabled state — flat grey, no hover
        var btn = GetComponent<Button>();
        if (btn != null && !btn.interactable)
        {
            target.localScale = _baseScale;
            if (accentBar)  accentBar.color  = new Color(0.25f, 0.25f, 0.25f, 0.25f);
            if (background) background.color = new Color(0f, 0f, 0f, 0.25f);
            return;
        }

        Vector3 wantScale = _hovered ? _baseScale * hoverScale : _baseScale;
        target.localScale = Vector3.Lerp(target.localScale, wantScale, Time.unscaledDeltaTime * lerpSpeed);

        if (accentBar)
            accentBar.color = Color.Lerp(accentBar.color, _hovered ? accentHover : accentNormal,
                                         Time.unscaledDeltaTime * lerpSpeed);
        if (background)
            background.color = Color.Lerp(background.color, _hovered ? backgroundHover : backgroundNormal,
                                          Time.unscaledDeltaTime * lerpSpeed);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        _hovered = true;
        if (sfxSource && hoverClip) sfxSource.PlayOneShot(hoverClip, 0.6f);
    }

    public void OnPointerExit(PointerEventData e)
    {
        _hovered = false;
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (sfxSource && clickClip) sfxSource.PlayOneShot(clickClip, 0.9f);
    }
}
