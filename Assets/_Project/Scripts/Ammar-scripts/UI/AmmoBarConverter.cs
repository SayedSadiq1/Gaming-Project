using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.FPS.UI;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Ammo Bar Converter
//
//  The FPS Microgame's AmmoCounter uses a circular radial "reload ring"
//  fill image around the weapon icon. This runtime patcher converts that
//  ring into a horizontal bar matching the Facility Breach HUD style
//  (cyan accent, dark track, thin profile, sitting below the weapon icon).
//
//  Why a runtime patch instead of editing the FPS prefab?
//    • Keeps the "no FPS edits" rule intact
//    • Self-installs on every scene load → also works in friends' levels
//    • One-stop place to retune the look if the design changes
//
//  Pattern mirrors PlayerHealthRegen — RuntimeInitializeOnLoadMethod hook,
//  re-runs every scene load, idempotent.
// ─────────────────────────────────────────────────────────────────────────────
public static class AmmoBarConverter
{
    // Style constants
    static readonly Color C_BAR_FILL  = new Color32(0x00, 0xC8, 0xFF, 0xFF);
    static readonly Color C_BAR_TRACK = new Color(1, 1, 1, 0.14f);

    const float BAR_HEIGHT = 8f;
    const float BAR_MARGIN_X = 6f;
    const float BAR_MARGIN_Y = -6f;   // sits just below the icon

    // Solid white sprite, lazily created. The FPS Microgame's AmmoFillImage
    // ships with a circular ring sprite — even after we switch fillMethod to
    // Horizontal, that ring sprite is still drawn (just filled left-to-right).
    // Replacing the sprite with a plain rectangle is what makes it a real bar.
    static Sprite s_SolidSprite;
    static Sprite GetSolidSprite()
    {
        if (s_SolidSprite != null) return s_SolidSprite;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var pixels = new Color[4];
        for (int i = 0; i < 4; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        s_SolidSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
        s_SolidSprite.hideFlags = HideFlags.HideAndDontSave;
        s_SolidSprite.name = "AmmoBar_SolidWhite";
        return s_SolidSprite;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Persistent runner — re-checks every second for new AmmoCounters that
        // get spawned when the player picks up a new weapon mid-level.
        if (Object.FindFirstObjectByType<PollingRunner>() != null) return;
        var host = new GameObject("AmmoBarConverter_Runner");
        Object.DontDestroyOnLoad(host);
        host.AddComponent<PollingRunner>();
    }

    class PollingRunner : MonoBehaviour
    {
        readonly System.Collections.Generic.HashSet<int> _seen = new System.Collections.Generic.HashSet<int>();

        System.Collections.IEnumerator Start()
        {
            // Initial pass: wait a couple frames so weapon-swap can spawn its counters
            yield return null;
            yield return null;
            Tick();

            // Re-poll every second for newly added weapons (e.g. AK pickup)
            while (true)
            {
                yield return new WaitForSeconds(1f);
                Tick();
            }
        }

        void Tick()
        {
            var counters = Object.FindObjectsByType<AmmoCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int newCount = 0;
            foreach (var ac in counters)
            {
                if (ac == null) continue;
                int id = ac.GetInstanceID();
                if (_seen.Contains(id)) continue;
                _seen.Add(id);
                newCount++;
            }
            if (newCount > 0) Convert();
        }
    }

    public static void Convert()
    {
        int converted = 0;
        var counters = Object.FindObjectsByType<AmmoCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var ac in counters)
        {
            if (ac == null || ac.AmmoFillImage == null) continue;

            var fill   = ac.AmmoFillImage;
            var fillRT = fill.rectTransform;
            var bgImg  = ac.AmmoBackgroundImage;

            // 1. Switch the fill image to a horizontal bar AND replace the
            //    circular ring sprite with a plain solid rectangle so it
            //    actually looks like a bar instead of a horizontally-filled ring.
            fill.sprite          = GetSolidSprite();
            fill.type            = Image.Type.Filled;
            fill.fillMethod      = Image.FillMethod.Horizontal;
            fill.fillOrigin      = (int)Image.OriginHorizontal.Left;
            fill.preserveAspect  = false;
            fill.material        = null;            // drop any masked/circular material
            fill.color           = C_BAR_FILL;

            // 2. Re-anchor it as a thin bar at the bottom of its parent
            //    (parent is typically the weapon icon panel)
            fillRT.anchorMin        = new Vector2(0f, 0f);
            fillRT.anchorMax        = new Vector2(1f, 0f);
            fillRT.pivot            = new Vector2(0.5f, 0f);
            fillRT.anchoredPosition = new Vector2(0f, BAR_MARGIN_Y);
            fillRT.offsetMin        = new Vector2(BAR_MARGIN_X, BAR_MARGIN_Y);
            fillRT.offsetMax        = new Vector2(-BAR_MARGIN_X, BAR_MARGIN_Y + BAR_HEIGHT);

            // 3. Add a track behind the fill so the empty portion is visible
            //    Skip if we already added one (idempotent).
            if (fillRT.parent.Find("_AmmoBarTrack") == null)
            {
                var trackGO = new GameObject("_AmmoBarTrack", typeof(RectTransform));
                trackGO.transform.SetParent(fillRT.parent, false);
                trackGO.transform.SetSiblingIndex(fillRT.GetSiblingIndex()); // behind the fill
                var trackImg = trackGO.AddComponent<Image>();
                trackImg.sprite = GetSolidSprite();
                trackImg.color  = C_BAR_TRACK;
                trackImg.raycastTarget = false;
                var trt = trackGO.GetComponent<RectTransform>();
                trt.anchorMin        = fillRT.anchorMin;
                trt.anchorMax        = fillRT.anchorMax;
                trt.pivot            = fillRT.pivot;
                trt.anchoredPosition = fillRT.anchoredPosition;
                trt.offsetMin        = fillRT.offsetMin;
                trt.offsetMax        = fillRT.offsetMax;
            }

            // 4. The background image (the original ring backdrop) is now
            //    redundant. Hide it so the ring doesn't bleed through.
            if (bgImg != null) bgImg.enabled = false;

            converted++;
        }
        if (converted > 0)
            Debug.Log($"[AmmoBarConverter] Converted {converted} radial reload ring(s) to horizontal bars.");
    }
}
