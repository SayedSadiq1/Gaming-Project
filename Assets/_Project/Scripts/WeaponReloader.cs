using System.Collections;
using TMPro;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FacilityBreach
{
    /// <summary>
    /// Manual reload system – attach this to the Player GameObject.
    ///
    /// Press R → a cyan progress bar fills below the crosshair → clip refills.
    /// Each weapon can have a WeaponReloadConfig component to set its own reload speed.
    /// Styled to match the Facility Breach HUD (cyan accent + dark track).
    /// </summary>
    public class WeaponReloader : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Reload time used when the weapon has no WeaponReloadConfig component")]
        public float DefaultReloadTime = 2.5f;

        [Tooltip("Key the player presses to reload")]
        public Key ReloadKey = Key.R;

        // ── runtime ──────────────────────────────────────────────────────────
        PlayerWeaponsManager m_WeaponsManager;
        bool m_IsReloading;

        // UI elements built at runtime – no prefab needed
        GameObject m_Canvas;
        Image m_BarFill;
        TextMeshProUGUI m_Label;

        // Style — matches the rest of the HUD
        static readonly Color C_CYAN     = new Color32(0x00, 0xC8, 0xFF, 0xFF);
        static readonly Color C_CYAN_DIM = new Color(0f, 0.78f, 1f, 0.35f);
        static readonly Color C_TRACK    = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color C_WHITE    = Color.white;

        // ─────────────────────────────────────────────────────────────────────
        void Start()
        {
            m_WeaponsManager = GetComponent<PlayerWeaponsManager>();
            BuildReloadUI();
            if (m_Canvas != null) m_Canvas.SetActive(false);
        }

        void Update()
        {
            if (m_IsReloading) return;
            if (Keyboard.current == null) return;
            if (!Keyboard.current[ReloadKey].wasPressedThisFrame) return;

            WeaponController weapon = m_WeaponsManager?.GetActiveWeapon();
            if (weapon == null) return;

            // FIX: Prevent reloading if the magazine is already full
            int currentAmmo = weapon.GetCurrentAmmo();
            int maxAmmo = Mathf.RoundToInt(weapon.MaxAmmo);
            
            if (currentAmmo >= maxAmmo) 
            {
                return;
            }

            StartCoroutine(DoReload(weapon));
        }

        IEnumerator DoReload(WeaponController weapon)
        {
            m_IsReloading = true;

            // Choose reload time – per-weapon config takes priority
            float duration = DefaultReloadTime;
            AudioClip reloadSfx = null;

            WeaponReloadConfig cfg = weapon.GetComponentInChildren<WeaponReloadConfig>();
            if (cfg != null)
            {
                duration = cfg.ReloadTime;
                reloadSfx = cfg.ReloadSfx;
            }

            // FIX: Play sound only when a valid reload starts
            if (reloadSfx != null)
            {
                AudioSource audioSource = weapon.GetComponent<AudioSource>();
                if (audioSource != null)
                {
                    audioSource.PlayOneShot(reloadSfx);
                }
            }

            // Show UI, reset bar
            if (m_BarFill != null) m_BarFill.fillAmount = 0f;
            if (m_Canvas  != null) m_Canvas.SetActive(true);

            // Animate the bar over the reload duration
            float elapsed = 0f;
            while (elapsed < duration)
            {
                // Safety: check if weapon was switched during reload
                if (m_WeaponsManager != null && m_WeaponsManager.GetActiveWeapon() != weapon)
                {
                    break;
                }

                elapsed += Time.deltaTime;
                if (m_BarFill != null) m_BarFill.fillAmount = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }

            // Refill clip if we reached the end of the duration
            if (elapsed >= duration)
            {
                // Refill to max
                int ammoNeeded = Mathf.RoundToInt(weapon.MaxAmmo) - weapon.GetCurrentAmmo();
                if (ammoNeeded > 0)
                {
                    weapon.UseAmmo(-ammoNeeded);
                }
            }

            if (m_Canvas != null) m_Canvas.SetActive(false);
            m_IsReloading = false;
        }

        // ── Build a lightweight screen-centre overlay entirely in code ────────
        //  Cyan progress bar matching the Facility Breach HUD style. Sits just
        //  below the crosshair so it doesn't block the player's view.
        void BuildReloadUI()
        {
            if (m_Canvas != null) return;

            // Canvas
            m_Canvas = new GameObject("ReloadOverlay");
            m_Canvas.transform.SetParent(transform, false);

            Canvas canvas = m_Canvas.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = m_Canvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            m_Canvas.AddComponent<GraphicRaycaster>();

            // Shared solid sprite — Unity Image.fillAmount won't work without
            // a sprite assigned (it just renders nothing).
            Sprite solid = CreateSolidSprite();

            // ── Container so the bar + label move as one ──
            var container = new GameObject("BarContainer", typeof(RectTransform));
            container.transform.SetParent(m_Canvas.transform, false);
            SetAnchored(container, new Vector2(0f, -80f), 360f, 32f);

            // ── Track (dark background) ──────────────────────────────────────
            var trackGO = new GameObject("Track", typeof(RectTransform));
            trackGO.transform.SetParent(container.transform, false);
            var trackImg = trackGO.AddComponent<Image>();
            trackImg.sprite = solid;
            trackImg.color  = C_TRACK;
            trackImg.raycastTarget = false;
            var trackRT = trackGO.GetComponent<RectTransform>();
            trackRT.anchorMin = new Vector2(0f, 0.5f);
            trackRT.anchorMax = new Vector2(1f, 0.5f);
            trackRT.pivot     = new Vector2(0.5f, 0.5f);
            trackRT.sizeDelta = new Vector2(0f, 10f);

            // ── Top + bottom cyan accent lines (match HUD style) ─────────────
            AddAccent(container.transform, solid, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                      new Vector2(0f, 8f), 2f, C_CYAN);
            AddAccent(container.transform, solid, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                      new Vector2(0f, -8f), 2f, C_CYAN_DIM);

            // ── Fill (cyan) — horizontal left-to-right ───────────────────────
            var fillGO = new GameObject("Fill", typeof(RectTransform));
            fillGO.transform.SetParent(container.transform, false);
            m_BarFill = fillGO.AddComponent<Image>();
            m_BarFill.sprite        = solid;
            m_BarFill.color         = C_CYAN;
            m_BarFill.type          = Image.Type.Filled;
            m_BarFill.fillMethod    = Image.FillMethod.Horizontal;
            m_BarFill.fillOrigin    = (int)Image.OriginHorizontal.Left;
            m_BarFill.fillAmount    = 0f;
            m_BarFill.raycastTarget = false;
            var fillRT = fillGO.GetComponent<RectTransform>();
            fillRT.anchorMin = new Vector2(0f, 0.5f);
            fillRT.anchorMax = new Vector2(1f, 0.5f);
            fillRT.pivot     = new Vector2(0.5f, 0.5f);
            fillRT.sizeDelta = new Vector2(0f, 10f);

            // ── "RELOADING" text label ───────────────────────────────────────
            GameObject labelGO = new GameObject("Label");
            labelGO.transform.SetParent(m_Canvas.transform, false);
            m_Label = labelGO.AddComponent<TextMeshProUGUI>();
            m_Label.text             = "RELOADING";
            m_Label.fontSize         = 18f;
            m_Label.fontStyle        = FontStyles.Bold;
            m_Label.alignment        = TextAlignmentOptions.Center;
            m_Label.color            = C_CYAN;
            m_Label.characterSpacing = 6f;
            m_Label.raycastTarget    = false;
            SetAnchored(labelGO, new Vector2(0f, -116f), 360f, 24f);
        }

        static void AddAccent(Transform parent, Sprite solid,
                              Vector2 anchorMin, Vector2 anchorMax,
                              Vector2 pos, float height, Color color)
        {
            var go = new GameObject("Accent", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = solid;
            img.color  = color;
            img.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = anchorMin;
            rt.anchorMax        = anchorMax;
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta        = new Vector2(0f, height);
        }

        // 2x2 solid white sprite (cached on the texture so we don't leak).
        static Sprite s_Solid;
        static Sprite CreateSolidSprite()
        {
            if (s_Solid != null) return s_Solid;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var pixels = new Color[4];
            for (int i = 0; i < 4; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            s_Solid = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
            s_Solid.hideFlags = HideFlags.HideAndDontSave;
            return s_Solid;
        }

        static void SetAnchored(GameObject go, Vector2 offset, float w, float h)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = offset;
        }
    }
}
