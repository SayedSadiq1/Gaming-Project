using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Interaction Prompt UI
//  Box-style progress bar (no sprite needed). Auto-tints red if the prompt
//  text contains "INTRUDER" or "ACCESS DENIED".
// ─────────────────────────────────────────────────────────────────────────────
public class InteractionPromptUI : MonoBehaviour
{
    public CanvasGroup     group;
    public TextMeshProUGUI text;
    public Image           progressBar;               // the visible fill (color image)
    public RectTransform   progressFillRT;            // (preferred) anchor-based fill rect
    public Color           okColor      = new Color(0f, 0.78f, 1f, 1f);
    public Color           lockedColor  = new Color(1f, 0.3f, 0.3f, 1f);
    public Color           alarmColor   = new Color(1f, 0.25f, 0.25f, 1f);

    void Start()
    {
        if (group != null) group.alpha = 0f;
    }

    public void Show(string promptText, bool canInteract)
    {
        if (group != null) group.alpha = 1f;

        bool urgent = !string.IsNullOrEmpty(promptText) && (
            promptText.IndexOf("INTRUDER", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            promptText.IndexOf("DETECTED", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            promptText.IndexOf("ALARM",    System.StringComparison.OrdinalIgnoreCase) >= 0 );

        Color c = canInteract ? okColor : lockedColor;
        if (urgent) c = alarmColor;

        if (text != null) { text.text = promptText; text.color = c; }
        if (progressBar != null) progressBar.color = c;
    }

    public void SetProgress(float t)
    {
        t = Mathf.Clamp01(t);

        // Preferred: animate via anchor (works without any sprite, gives flat boxy edges)
        if (progressFillRT != null)
        {
            var aMax = progressFillRT.anchorMax;
            aMax.x = t;
            progressFillRT.anchorMax = aMax;
        }
        // Fallback for old setups
        else if (progressBar != null)
        {
            progressBar.fillAmount = t;
        }
    }

    public void Hide()
    {
        if (group != null) group.alpha = 0f;
        SetProgress(0f);
    }
}
