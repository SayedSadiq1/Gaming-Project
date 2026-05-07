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
    /// Press R → a golden ring fills over the reload time at screen centre → clip refills.
    /// Each weapon can have a WeaponReloadConfig component to set its own reload speed.
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
        Image m_Ring;
        TextMeshProUGUI m_Label;

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

            // Show UI, reset ring
            if (m_Ring != null) m_Ring.fillAmount = 0f;
            if (m_Canvas != null) m_Canvas.SetActive(true);

            // Animate the ring over the reload duration
            float elapsed = 0f;
            while (elapsed < duration)
            {
                // Safety: check if weapon was switched during reload
                if (m_WeaponsManager != null && m_WeaponsManager.GetActiveWeapon() != weapon)
                {
                    break;
                }

                elapsed += Time.deltaTime;
                if (m_Ring != null) m_Ring.fillAmount = Mathf.Clamp01(elapsed / duration);
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
        void BuildReloadUI()
        {
            if (m_Canvas != null) return;

            // Canvas
            m_Canvas = new GameObject("ReloadOverlay");
            m_Canvas.transform.SetParent(transform, false);

            Canvas canvas = m_Canvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = m_Canvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            m_Canvas.AddComponent<GraphicRaycaster>();

            // ── White ring (radial fill) ──────────────
            Sprite ringSprite = CreateRingSprite(128, 44, 60);

            GameObject ring = new GameObject("Ring");
            ring.transform.SetParent(m_Canvas.transform, false);
            m_Ring             = ring.AddComponent<Image>();
            m_Ring.sprite      = ringSprite;
            m_Ring.color       = new Color(1f, 0.84f, 0f); // Golden yellow
            m_Ring.type        = Image.Type.Filled;
            m_Ring.fillMethod  = Image.FillMethod.Radial360;
            m_Ring.fillClockwise = true;
            m_Ring.fillOrigin  = (int)Image.Origin360.Top;
            m_Ring.fillAmount  = 0f;
            m_Ring.preserveAspect = true;
            SetAnchored(ring, Vector2.zero, 120f, 120f);

            // ── "RELOADING" text label ────────────────────────────────────────
            GameObject labelGO = new GameObject("Label");
            labelGO.transform.SetParent(m_Canvas.transform, false);
            m_Label             = labelGO.AddComponent<TextMeshProUGUI>();
            m_Label.text        = "RELOADING";
            m_Label.fontSize    = 18f;
            m_Label.fontStyle   = FontStyles.Bold;
            m_Label.alignment   = TextAlignmentOptions.Center;
            m_Label.color       = Color.white;
            SetAnchored(labelGO, new Vector2(0f, -80f), 200f, 30f);
        }

        static Sprite CreateRingSprite(int size, int innerRadius, int outerRadius)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            float cx = size * 0.5f;
            float cy = size * 0.5f;
            float outerSq = outerRadius * outerRadius;
            float innerSq = innerRadius * innerRadius;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx + 0.5f;
                    float dy = y - cy + 0.5f;
                    float distSq = dx * dx + dy * dy;

                    if (distSq >= innerSq && distSq <= outerSq)
                        pixels[y * size + x] = Color.white;
                    else
                        pixels[y * size + x] = Color.clear;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
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
