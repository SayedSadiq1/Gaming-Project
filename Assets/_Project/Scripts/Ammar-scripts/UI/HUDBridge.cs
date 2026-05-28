using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — HUD Bridge (reflection-based)
//  ZERO `using Unity.FPS.*` lines. Reads gameplay data (Health, weapons,
//  ammo) via reflection on whatever components live on the Player.
//
//  Re-scans for WeaponController components every frame so it picks up
//  weapons that are instantiated AFTER Start() (which is how the FPS
//  Microgame's PlayerWeaponsManager spawns them from prefab list).
// ─────────────────────────────────────────────────────────────────────────────
public class HUDBridge : MonoBehaviour
{
    public CombatHUD hud;
    public bool verboseLogs = false;

    [Tooltip("If set (by CoopBootstrap for per-player HUD canvases), this " +
             "Bridge binds to THIS Player instead of the first 'Player' tag.")]
    public Transform overridePlayer;

    Transform           _player;
    CharacterController _cc;

    // --- Health reflection ---
    Component       _health;
    PropertyInfo    _healthCurrent;
    FieldInfo       _healthMax;
    FieldInfo       _healthOnDamagedField;
    UnityAction<float, GameObject> _onDamagedHandler;
    bool _healthBound;

    // --- Weapon reflection (cached after first weapon found) ---
    PropertyInfo    _weaponIsActive;
    FieldInfo       _weaponMaxAmmoField;
    PropertyInfo    _weaponMaxAmmoProp;
    MethodInfo      _weaponGetCurrentAmmo;
    FieldInfo       _weaponNameField;
    FieldInfo       _weaponOnShootField;
    readonly HashSet<Component> _subscribedShoot = new HashSet<Component>();
    UnityAction     _onShootHandler;
    Component       _lastActiveWeapon;

    void Start()
    {
        if (hud == null) hud = Object.FindFirstObjectByType<CombatHUD>();
        TryBindPlayer();
    }

    /// <summary>
    /// Called by CoopBootstrap after setting overridePlayer. Wipes cached
    /// bindings so TryBindPlayer can re-run against the new player target.
    /// </summary>
    public void Rebind()
    {
        // Drop any previously-subscribed event handlers before resetting
        if (_health != null && _healthOnDamagedField != null && _onDamagedHandler != null)
        {
            var current = _healthOnDamagedField.GetValue(_health) as UnityAction<float, GameObject>;
            if (current != null) _healthOnDamagedField.SetValue(_health, current - _onDamagedHandler);
        }
        if (_weaponOnShootField != null && _onShootHandler != null)
        {
            foreach (var w in _subscribedShoot)
            {
                if (w == null) continue;
                var current = _weaponOnShootField.GetValue(w) as UnityAction;
                if (current != null) _weaponOnShootField.SetValue(w, current - _onShootHandler);
            }
        }
        _subscribedShoot.Clear();

        // Reset cached state so next Update / TryBindPlayer re-resolves
        _player = null;
        _cc = null;
        _health = null;
        _healthBound = false;
        _lastActiveWeapon = null;
        _weaponIsActive = null;
        _weaponMaxAmmoField = null;
        _weaponMaxAmmoProp = null;
        _weaponGetCurrentAmmo = null;
        _weaponNameField = null;
        _weaponOnShootField = null;

        if (hud == null) hud = Object.FindFirstObjectByType<CombatHUD>();
        TryBindPlayer();
    }

    void TryBindPlayer()
    {
        if (_player != null) return;

        GameObject p = null;

        // 0) Co-op override — CoopBootstrap assigns this for per-player HUDs.
        if (overridePlayer != null) p = overridePlayer.gameObject;

        // 1) Try the "Player" tag
        if (p == null) try { p = GameObject.FindGameObjectWithTag("Player"); } catch { }

        // 2) Fall back to any GameObject with PlayerWeaponsManager
        if (p == null)
        {
            var pwm = FindFirstByTypeName("PlayerWeaponsManager");
            if (pwm != null) p = pwm.gameObject;
        }

        // 3) Last resort: any GameObject with both Health and a CharacterController
        //    (matches the FPS Microgame's Player setup, even without the tag)
        if (p == null)
        {
            foreach (var c in FindAllByTypeName("Health"))
            {
                if (c.GetComponent<CharacterController>() != null) { p = c.gameObject; break; }
            }
        }

        if (p == null) return;

        _player = p.transform;
        _cc     = p.GetComponent<CharacterController>();
        TryBindHealth(p);
        Debug.Log("[HUDBridge] Bound Player: " + p.name);
    }

    static Component FindFirstByTypeName(string typeName)
    {
        var all = Object.FindObjectsByType<Component>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in all)
            if (c != null && c.GetType().Name == typeName) return c;
        return null;
    }

    static System.Collections.Generic.List<Component> FindAllByTypeName(string typeName)
    {
        var list = new System.Collections.Generic.List<Component>();
        var all = Object.FindObjectsByType<Component>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in all)
            if (c != null && c.GetType().Name == typeName) list.Add(c);
        return list;
    }

    void TryBindHealth(GameObject p)
    {
        if (_healthBound) return;
        // Search the Player and ALL children for a Health component
        foreach (var c in p.GetComponentsInChildren<Component>(true))
        {
            if (c == null) continue;
            var t = c.GetType();
            if (t.Name != "Health") continue;
            _health = c;
            _healthCurrent        = t.GetProperty("CurrentHealth");
            _healthMax            = t.GetField("MaxHealth");
            _healthOnDamagedField = t.GetField("OnDamaged");
            if (_healthOnDamagedField != null)
            {
                _onDamagedHandler = OnDamagedHandler;
                var current = _healthOnDamagedField.GetValue(_health) as UnityAction<float, GameObject>;
                _healthOnDamagedField.SetValue(_health, current + _onDamagedHandler);
            }
            _healthBound = true;
            Debug.Log("[HUDBridge] Bound Health on " + c.gameObject.name);
            return;
        }
        Debug.LogWarning("[HUDBridge] No Health component found on " + p.name + " or its children.");
    }

    void Update()
    {
        if (hud == null) return;
        if (_player == null) { TryBindPlayer(); if (_player == null) return; }

        TickHealth();
        TickWeapon();
        if (_cc != null)     hud.SetCrosshairMovement(_cc.velocity.magnitude);
        hud.SetPlayerYaw(_player.eulerAngles.y);
    }

    void TickHealth()
    {
        if (_health == null) return;
        if (_healthCurrent == null) return;
        float cur = (float)_healthCurrent.GetValue(_health);
        float max = _healthMax != null ? (float)_healthMax.GetValue(_health) : 100f;
        hud.SetHealth(cur, max);
    }

    // Re-scans every frame so runtime-instantiated weapons are picked up.
    void TickWeapon()
    {
        if (_player == null) return;

        Component active = null;
        var allComps = _player.GetComponentsInChildren<Component>(true);
        foreach (var c in allComps)
        {
            if (c == null) continue;
            if (c.GetType().Name != "WeaponController") continue;

            // First time we see a WeaponController: cache reflection metadata
            if (_weaponIsActive == null)
            {
                var t = c.GetType();
                _weaponIsActive       = t.GetProperty("IsWeaponActive");
                _weaponMaxAmmoField   = t.GetField("MaxAmmo");
                _weaponMaxAmmoProp    = t.GetProperty("MaxAmmo");
                _weaponGetCurrentAmmo = t.GetMethod("GetCurrentAmmo");
                _weaponNameField      = t.GetField("WeaponName");
                _weaponOnShootField   = t.GetField("OnShoot");
            }

            // Subscribe to OnShoot once per weapon
            if (_weaponOnShootField != null && !_subscribedShoot.Contains(c))
            {
                _onShootHandler = OnShootHandler;
                var current = _weaponOnShootField.GetValue(c) as UnityAction;
                _weaponOnShootField.SetValue(c, current + _onShootHandler);
                _subscribedShoot.Add(c);
            }

            // Pick the active one
            bool isActive = false;
            if (_weaponIsActive != null)
            {
                var v = _weaponIsActive.GetValue(c);
                if (v is bool b) isActive = b;
            }
            else
            {
                isActive = ((MonoBehaviour)c).gameObject.activeSelf;
            }
            if (isActive && active == null) active = c;
        }

        if (active == null) return;

        if (active != _lastActiveWeapon)
        {
            _lastActiveWeapon = active;
            hud.SetActiveWeapon(GetPrefabName(active));
            if (verboseLogs) Debug.Log("[HUDBridge] Active weapon swap → " + GetPrefabName(active));
        }

        int cur = _weaponGetCurrentAmmo != null ? (int)_weaponGetCurrentAmmo.Invoke(active, null) : 0;
        int max = _weaponMaxAmmoField != null ? (int)_weaponMaxAmmoField.GetValue(active)
                : _weaponMaxAmmoProp  != null ? (int)_weaponMaxAmmoProp.GetValue(active)
                : 0;
        hud.SetAmmo(cur, max);
    }

    static string GetPrefabName(Component c)
    {
        var mb = c as MonoBehaviour;
        if (mb == null) return "";
        string n = mb.gameObject.name;
        int idx = n.IndexOf("(Clone)", System.StringComparison.OrdinalIgnoreCase);
        if (idx >= 0) n = n.Substring(0, idx).Trim();
        return n.Replace("PFB_", "").Trim();
    }

    void OnDamagedHandler(float damage, GameObject source)
    {
        if (hud == null) return;
        hud.TriggerHealthFlash();
        hud.TriggerDamageVignette(Mathf.Clamp01(damage * 0.02f));
    }

    void OnShootHandler() { if (hud != null) hud.KickCrosshair(); }

    void OnDestroy()
    {
        if (_health != null && _healthOnDamagedField != null && _onDamagedHandler != null)
        {
            var current = _healthOnDamagedField.GetValue(_health) as UnityAction<float, GameObject>;
            if (current != null) _healthOnDamagedField.SetValue(_health, current - _onDamagedHandler);
        }
        if (_weaponOnShootField != null && _onShootHandler != null)
        {
            foreach (var w in _subscribedShoot)
            {
                if (w == null) continue;
                var current = _weaponOnShootField.GetValue(w) as UnityAction;
                if (current != null) _weaponOnShootField.SetValue(w, current - _onShootHandler);
            }
        }
    }
}
