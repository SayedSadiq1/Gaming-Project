using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class CombatHUD : MonoBehaviour
{
    [Header("Per-Weapon Configs")]
    public List<WeaponUIConfig> weaponConfigs = new List<WeaponUIConfig>();

    // ── HEALTH ──────────────────────────────────────────────────────────────
    [Header("Health")]
    public Image           healthFill;
    public TextMeshProUGUI healthText;
    public CanvasGroup     healthHitFlashGroup;
    public Color           healthColorFull = new Color(0f, 1f, 0.55f, 1f);
    public Color           healthColorLow  = new Color(1f, 0.25f, 0.25f, 1f);
    public float           healthLerpSpeed = 5f;
    public float           healthLowThreshold = 0.35f;

    // ── AMMO ────────────────────────────────────────────────────────────────
    [Header("Ammo")]
    public TextMeshProUGUI ammoCurrentText;
    public TextMeshProUGUI ammoMaxText;
    public TextMeshProUGUI ammoWeaponNameText;
    public Image           ammoAccentBar;
    public Image           ammoLowFlash;
    public CanvasGroup     reloadingGroup;

    // ── WEAPON NAMEPLATE ────────────────────────────────────────────────────
    [Header("Weapon Nameplate")]
    public CanvasGroup     nameplateGroup;
    public TextMeshProUGUI nameplateNameText;
    public TextMeshProUGUI nameplateTypeText;
    public Image           nameplateIcon;
    public Image           nameplateAccent;
    public float           nameplateIdleAlpha = 0.35f;

    // ── CROSSHAIR ───────────────────────────────────────────────────────────
    [Header("Crosshair (Top, Bottom, Left, Right)")]
    public RectTransform[] crosshairLines;
    public float           crosshairBaseGap  = 10f;
    public float           crosshairMaxGap   = 40f;
    public float           crosshairKick     = 6f;
    public float           crosshairLerp     = 14f;

    // ── HIT MARKER ──────────────────────────────────────────────────────────
    [Header("Hit Marker")]
    public CanvasGroup hitMarkerGroup;
    public float       hitMarkerDuration = 0.2f;

    // ── DAMAGE VIGNETTE ─────────────────────────────────────────────────────
    [Header("Damage Vignette")]
    public Image damageVignette;
    public float damageVignetteFade = 1.2f;

    // ── COMPASS ─────────────────────────────────────────────────────────────
    [Header("Compass")]
    public RectTransform compassTickContainer;
    [Tooltip("Pixels per degree of player rotation")]
    public float         compassPixelsPerDegree = 4f;

    // ── OBJECTIVES ──────────────────────────────────────────────────────────
    [Header("Objective Tracker")]
    public TextMeshProUGUI objectiveProgressText;

    // ── GRENADES (frag only) ────────────────────────────────────────────────
    [Header("Frag Grenade Counter")]
    public TextMeshProUGUI fragCountText;
    [Tooltip("The panel GameObject — gets auto-hidden when no grenade system is in the scene.")]
    public GameObject fragCounterPanel;

    // ── INTERNAL STATE ──────────────────────────────────────────────────────
    float  _displayedHealth = 100f;
    float  _targetHealth    = 100f;
    float  _maxHealth       = 100f;
    int    _currentAmmo, _maxAmmo;
    float  _ammoDisplayed;
    float  _playerYaw;
    int    _fragCount = 3;
    int    _objectivesDone, _objectivesTotal = 3;
    float  _hitMarkerTimer;
    float  _vignetteAlpha;
    float  _crosshairGap;
    float  _crosshairExtraKick;
    float  _nameplateFadeTimer;
    string _activeWeaponName = "";
    bool   _reloading;

    void Start()
    {
        SetFragCount(_fragCount);
        SetObjectives(_objectivesDone, _objectivesTotal);
        if (reloadingGroup)     reloadingGroup.alpha = 0f;
        if (hitMarkerGroup)     hitMarkerGroup.alpha = 0f;
        if (healthHitFlashGroup)healthHitFlashGroup.alpha = 0f;
        if (damageVignette)     damageVignette.color = new Color(1, 0, 0, 0);
        if (nameplateGroup)     nameplateGroup.alpha = nameplateIdleAlpha;

        // Hide frag counter if no Aegis GrenadeSystem exists in this scene
        if (fragCounterPanel != null && !HasGrenadeSystemInScene())
            fragCounterPanel.SetActive(false);

        // Self-heal: make sure a HUDBridge exists somewhere in the scene
        if (Object.FindFirstObjectByType<HUDBridge>() == null)
        {
            var bridgeGO = new GameObject("HUDBridge_Auto");
            var bridge = bridgeGO.AddComponent<HUDBridge>();
            bridge.hud = this;
            Debug.Log("[CombatHUD] No HUDBridge found in scene — auto-created one.");
        }
    }

    static bool HasGrenadeSystemInScene()
    {
        var all = Object.FindObjectsByType<Component>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in all)
        {
            if (c != null && c.GetType().Name == "GrenadeSystem") return true;
        }
        return false;
    }

    /// <summary>Manually toggle the frag counter panel (e.g. when picking up first grenade).</summary>
    public void SetFragCounterVisible(bool visible)
    {
        if (fragCounterPanel != null) fragCounterPanel.SetActive(visible);
    }

    void Update()
    {
        TickHealth();
        TickAmmo();
        TickCrosshair();
        TickHitMarker();
        TickVignette();
        TickCompass();
        TickNameplate();
        TickReloading();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  PUBLIC API — called from HUDBridge or any gameplay script
    // ════════════════════════════════════════════════════════════════════════

    public void SetHealth(float current, float max)
    {
        _targetHealth = current;
        _maxHealth    = Mathf.Max(1f, max);
    }

    public void TriggerHealthFlash()
    {
        if (healthHitFlashGroup) healthHitFlashGroup.alpha = 1f;
        _vignetteAlpha = Mathf.Min(1f, _vignetteAlpha + 0.6f);
    }

    public void SetAmmo(int current, int max)
    {
        _currentAmmo = current;
        _maxAmmo     = max;
    }

    public void SetActiveWeapon(string weaponName)
    {
        if (weaponName == _activeWeaponName) return;
        _activeWeaponName = weaponName;
        ApplyWeaponConfig(FindConfig(weaponName), weaponName);
        _nameplateFadeTimer = 1.2f;
        if (nameplateGroup) nameplateGroup.alpha = 1f;
    }

    public void SetReloading(bool isReloading) => _reloading = isReloading;

    public void ShowHitMarker() => _hitMarkerTimer = hitMarkerDuration;

    public void TriggerDamageVignette(float strength = 0.6f)
        => _vignetteAlpha = Mathf.Min(1f, _vignetteAlpha + strength);

    public void SetPlayerYaw(float yaw) => _playerYaw = yaw;

    public void SetObjectives(int completed, int total)
    {
        _objectivesDone  = Mathf.Clamp(completed, 0, total);
        _objectivesTotal = Mathf.Max(0, total);
        if (objectiveProgressText)
            objectiveProgressText.text = $"{_objectivesDone} / {_objectivesTotal}";
    }
    public void IncrementObjective() => SetObjectives(_objectivesDone + 1, _objectivesTotal);

    public void SetFragCount(int count)
    {
        _fragCount = Mathf.Max(0, count);
        if (fragCountText) fragCountText.text = "x" + _fragCount;
    }
    public void AddFrag(int delta) => SetFragCount(_fragCount + delta);
    public int  GetFragCount() => _fragCount;

    public void KickCrosshair() => _crosshairExtraKick += crosshairKick;
    public void SetCrosshairMovement(float movementMagnitude)
        => _crosshairGap = Mathf.Lerp(_crosshairGap,
            crosshairBaseGap + Mathf.Clamp01(movementMagnitude / 6f) * (crosshairMaxGap - crosshairBaseGap)
            + _crosshairExtraKick,
            Time.deltaTime * crosshairLerp);

    // ════════════════════════════════════════════════════════════════════════
    //  TICKERS
    // ════════════════════════════════════════════════════════════════════════
    void TickHealth()
    {
        _displayedHealth = Mathf.Lerp(_displayedHealth, _targetHealth, Time.deltaTime * healthLerpSpeed);
        float pct = _maxHealth > 0 ? Mathf.Clamp01(_displayedHealth / _maxHealth) : 0f;
        if (healthFill)
        {
            healthFill.fillAmount = pct;
            healthFill.color = pct > healthLowThreshold
                ? healthColorFull
                : Color.Lerp(healthColorLow, healthColorFull, pct / Mathf.Max(0.01f, healthLowThreshold));
        }
        if (healthText) healthText.text = Mathf.CeilToInt(_displayedHealth).ToString();
        if (healthHitFlashGroup)
            healthHitFlashGroup.alpha = Mathf.MoveTowards(healthHitFlashGroup.alpha, 0f, Time.deltaTime * 2f);
    }

    void TickAmmo()
    {
        _ammoDisplayed = Mathf.Lerp(_ammoDisplayed, _currentAmmo, Time.deltaTime * 15f);
        if (ammoCurrentText) ammoCurrentText.text = Mathf.RoundToInt(_ammoDisplayed).ToString();
        if (ammoMaxText)     ammoMaxText.text     = "/ " + _maxAmmo;
        if (ammoWeaponNameText) ammoWeaponNameText.text = _activeWeaponName.ToUpper();

        if (ammoLowFlash)
        {
            float pct = _maxAmmo > 0 ? (float)_currentAmmo / _maxAmmo : 1f;
            var c = ammoLowFlash.color;
            c.a = pct < 0.25f ? Mathf.PingPong(Time.time * 4f, 1f) * 0.4f : 0f;
            ammoLowFlash.color = c;
        }
    }

    void TickReloading()
    {
        if (!reloadingGroup) return;
        reloadingGroup.alpha = Mathf.Lerp(reloadingGroup.alpha, _reloading ? 1f : 0f, Time.deltaTime * 8f);
    }

    void TickCrosshair()
    {
        // Decay kick over time
        _crosshairExtraKick = Mathf.MoveTowards(_crosshairExtraKick, 0f, Time.deltaTime * 25f);

        // If no one is calling SetCrosshairMovement, gently lerp to base gap
        if (Time.frameCount % 60 == 0) // periodic safety reset
            _crosshairGap = Mathf.Lerp(_crosshairGap, crosshairBaseGap + _crosshairExtraKick, Time.deltaTime * 5f);

        if (crosshairLines == null || crosshairLines.Length < 4) return;
        float g = _crosshairGap;
        crosshairLines[0].anchoredPosition = new Vector2(0,  g);
        crosshairLines[1].anchoredPosition = new Vector2(0, -g);
        crosshairLines[2].anchoredPosition = new Vector2(-g, 0);
        crosshairLines[3].anchoredPosition = new Vector2( g, 0);
    }

    void TickHitMarker()
    {
        if (!hitMarkerGroup) return;
        if (_hitMarkerTimer > 0f)
        {
            _hitMarkerTimer -= Time.deltaTime;
            hitMarkerGroup.alpha = Mathf.Clamp01(_hitMarkerTimer / hitMarkerDuration);
        }
        else hitMarkerGroup.alpha = 0f;
    }

    void TickVignette()
    {
        if (!damageVignette) return;
        _vignetteAlpha = Mathf.MoveTowards(_vignetteAlpha, 0f, Time.deltaTime / Mathf.Max(0.01f, damageVignetteFade));
        var c = damageVignette.color;
        c.a = _vignetteAlpha;
        damageVignette.color = c;
    }

    void TickCompass()
    {
        if (!compassTickContainer) return;
        // Move ticks horizontally based on player yaw
        float x = -_playerYaw * compassPixelsPerDegree;
        compassTickContainer.anchoredPosition = new Vector2(x, compassTickContainer.anchoredPosition.y);
    }

    void TickNameplate()
    {
        if (!nameplateGroup) return;
        if (_nameplateFadeTimer > 0f)
        {
            _nameplateFadeTimer -= Time.deltaTime;
            nameplateGroup.alpha = 1f;
        }
        else
            nameplateGroup.alpha = Mathf.Lerp(nameplateGroup.alpha, nameplateIdleAlpha, Time.deltaTime * 3f);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  HELPERS
    // ════════════════════════════════════════════════════════════════════════
    void ApplyWeaponConfig(WeaponUIConfig cfg, string fallbackName)
    {
        Color accent = cfg != null ? cfg.accentColor : new Color(0f, 0.78f, 1f);
        string name  = cfg != null ? cfg.displayName : fallbackName.ToUpper();
        string type  = cfg != null ? cfg.typeLabel   : "WEAPON";

        if (nameplateNameText) nameplateNameText.text = name;
        if (nameplateTypeText) { nameplateTypeText.text = type; nameplateTypeText.color = accent; }
        if (nameplateAccent)   nameplateAccent.color   = accent;
        if (nameplateIcon && cfg != null && cfg.icon != null) nameplateIcon.sprite = cfg.icon;
        if (ammoAccentBar)     ammoAccentBar.color     = accent;
    }

    WeaponUIConfig FindConfig(string weaponName)
    {
        if (string.IsNullOrEmpty(weaponName)) return null;
        foreach (var c in weaponConfigs)
            if (c != null && string.Equals(c.weaponName, weaponName, System.StringComparison.OrdinalIgnoreCase))
                return c;
        return null;
    }
}
