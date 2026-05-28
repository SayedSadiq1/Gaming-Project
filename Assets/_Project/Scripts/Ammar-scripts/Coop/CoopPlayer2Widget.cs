using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Co-op Player 2 HUD Widget
//
//  Spawns a small Screen Space - Overlay canvas anchored to the bottom of the
//  screen so it appears in P2's half (the bottom viewport). Shows P2's:
//    • Health number
//    • Current weapon ammo
//    • Weapon name
//
//  Reads from Player2 via reflection (no asmdef dependency on Unity.FPS.Game).
//  The main CombatHUD still shows P1's stats up top — this just gives P2 a
//  matching numeric readout in their own viewport half.
// ─────────────────────────────────────────────────────────────────────────────
public class CoopPlayer2Widget : MonoBehaviour
{
    Transform       _player;
    Component       _health;
    PropertyInfo    _healthCurrentProp;
    FieldInfo       _healthMaxField;

    PropertyInfo    _weaponIsActiveProp;
    MethodInfo      _weaponGetCurrentAmmo;
    FieldInfo       _weaponMaxAmmoField;
    FieldInfo       _weaponNameField;

    TextMeshProUGUI _hpText;
    TextMeshProUGUI _ammoText;
    TextMeshProUGUI _weaponText;
    Image           _hpBarFill;

    public static void SpawnFor(GameObject player2)
    {
        var go = new GameObject("CoopPlayer2Widget");
        DontDestroyOnLoad(go);
        var w = go.AddComponent<CoopPlayer2Widget>();
        w._player = player2.transform;
        w.Build();
    }

    void Build()
    {
        // Canvas — Screen Space Overlay, sorting order above CombatHUD
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        // Container panel — bottom-left of bottom half
        var panelGO = new GameObject("P2_Panel", typeof(RectTransform));
        panelGO.transform.SetParent(transform, false);
        var prt = panelGO.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0, 0);
        prt.anchorMax = new Vector2(0, 0);
        prt.pivot     = new Vector2(0, 0);
        prt.anchoredPosition = new Vector2(40, 60);
        prt.sizeDelta = new Vector2(360, 90);

        var bg = panelGO.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.04f, 0.08f, 0.85f);

        // Cyan accent bar (left edge)
        var accent = NewImage("Accent", panelGO.transform, new Color32(0xFF, 0x8A, 0x00, 0xFF)); // orange = P2
        var aRT = accent.rectTransform;
        aRT.anchorMin = new Vector2(0, 0); aRT.anchorMax = new Vector2(0, 1);
        aRT.pivot = new Vector2(0, 0.5f);
        aRT.sizeDelta = new Vector2(6, 0);

        // Label "P2"
        var labelGO = NewText("Label", panelGO.transform, "P2", 22, new Color32(0xFF, 0x8A, 0x00, 0xFF));
        labelGO.fontStyle = FontStyles.Bold;
        var lrt = labelGO.rectTransform;
        lrt.anchorMin = new Vector2(0, 1); lrt.anchorMax = new Vector2(0, 1);
        lrt.pivot = new Vector2(0, 1);
        lrt.anchoredPosition = new Vector2(18, -6);
        lrt.sizeDelta = new Vector2(40, 26);

        // HP text
        _hpText = NewText("HP", panelGO.transform, "100", 26, Color.white);
        _hpText.fontStyle = FontStyles.Bold;
        var hRT = _hpText.rectTransform;
        hRT.anchorMin = new Vector2(0, 1); hRT.anchorMax = new Vector2(0, 1);
        hRT.pivot = new Vector2(0, 1);
        hRT.anchoredPosition = new Vector2(70, -6);
        hRT.sizeDelta = new Vector2(80, 26);

        // HP bar
        var barBg = NewImage("HPBarBG", panelGO.transform, new Color(1, 1, 1, 0.15f));
        var bbgRT = barBg.rectTransform;
        bbgRT.anchorMin = new Vector2(0, 0); bbgRT.anchorMax = new Vector2(1, 0);
        bbgRT.pivot = new Vector2(0.5f, 0);
        bbgRT.anchoredPosition = new Vector2(-10, 8);
        bbgRT.sizeDelta = new Vector2(-30, 6);

        var barFill = NewImage("HPBarFill", barBg.transform, new Color(0.2f, 1f, 0.4f, 1f));
        var bfRT = barFill.rectTransform;
        bfRT.anchorMin = new Vector2(0, 0); bfRT.anchorMax = new Vector2(1, 1);
        bfRT.offsetMin = Vector2.zero; bfRT.offsetMax = Vector2.zero;
        _hpBarFill = barFill;

        // Weapon name
        _weaponText = NewText("Weapon", panelGO.transform, "—", 18, new Color(0.7f, 0.9f, 1f, 1f));
        var wRT = _weaponText.rectTransform;
        wRT.anchorMin = new Vector2(1, 1); wRT.anchorMax = new Vector2(1, 1);
        wRT.pivot = new Vector2(1, 1);
        wRT.anchoredPosition = new Vector2(-12, -6);
        wRT.sizeDelta = new Vector2(170, 22);
        _weaponText.alignment = TextAlignmentOptions.TopRight;

        // Ammo text
        _ammoText = NewText("Ammo", panelGO.transform, "0/0", 26, Color.white);
        _ammoText.fontStyle = FontStyles.Bold;
        var amRT = _ammoText.rectTransform;
        amRT.anchorMin = new Vector2(1, 1); amRT.anchorMax = new Vector2(1, 1);
        amRT.pivot = new Vector2(1, 1);
        amRT.anchoredPosition = new Vector2(-12, -30);
        amRT.sizeDelta = new Vector2(170, 30);
        _ammoText.alignment = TextAlignmentOptions.TopRight;
    }

    void Update()
    {
        if (_player == null) return;

        // Re-bind Health each frame until found (in case player spawns late)
        if (_health == null) BindHealth();
        if (_health != null && _healthCurrentProp != null)
        {
            float cur = (float)_healthCurrentProp.GetValue(_health);
            float max = _healthMaxField != null ? (float)_healthMaxField.GetValue(_health) : 100f;
            if (_hpText != null) _hpText.text = ((int)cur).ToString();
            if (_hpBarFill != null)
            {
                var sd = _hpBarFill.rectTransform.anchorMax;
                sd.x = Mathf.Clamp01(cur / max);
                _hpBarFill.rectTransform.anchorMax = sd;
                _hpBarFill.color = cur < max * 0.3f
                    ? new Color(1f, 0.3f, 0.3f, 1f)
                    : cur < max * 0.6f
                        ? new Color(1f, 0.85f, 0.2f, 1f)
                        : new Color(0.2f, 1f, 0.4f, 1f);
            }
        }

        // Current weapon
        Component active = null;
        foreach (var c in _player.GetComponentsInChildren<Component>(true))
        {
            if (c == null || c.GetType().Name != "WeaponController") continue;
            if (_weaponIsActiveProp == null)
            {
                var t = c.GetType();
                _weaponIsActiveProp = t.GetProperty("IsWeaponActive");
                _weaponMaxAmmoField = t.GetField("MaxAmmo");
                _weaponGetCurrentAmmo = t.GetMethod("GetCurrentAmmo");
                _weaponNameField = t.GetField("WeaponName");
            }
            bool isActive = false;
            if (_weaponIsActiveProp != null)
            {
                var v = _weaponIsActiveProp.GetValue(c);
                if (v is bool b) isActive = b;
            }
            else isActive = ((MonoBehaviour)c).gameObject.activeSelf;
            if (isActive) { active = c; break; }
        }

        if (active != null)
        {
            int cur = _weaponGetCurrentAmmo != null ? (int)_weaponGetCurrentAmmo.Invoke(active, null) : 0;
            int max = _weaponMaxAmmoField != null ? (int)_weaponMaxAmmoField.GetValue(active) : 0;
            if (_ammoText != null) _ammoText.text = $"{cur}/{max}";
            if (_weaponText != null) _weaponText.text = GetWeaponName(active);
        }
        else
        {
            if (_ammoText != null) _ammoText.text = "—";
            if (_weaponText != null) _weaponText.text = "—";
        }
    }

    void BindHealth()
    {
        if (_player == null) return;
        foreach (var c in _player.GetComponentsInChildren<Component>(true))
        {
            if (c == null || c.GetType().Name != "Health") continue;
            _health = c;
            _healthCurrentProp = c.GetType().GetProperty("CurrentHealth");
            _healthMaxField    = c.GetType().GetField("MaxHealth");
            return;
        }
    }

    static string GetWeaponName(Component c)
    {
        var n = c.gameObject.name;
        int idx = n.IndexOf("(Clone)", System.StringComparison.OrdinalIgnoreCase);
        if (idx >= 0) n = n.Substring(0, idx).Trim();
        return n.Replace("PFB_", "").Trim();
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = color;
        t.enableWordWrapping = false;
        return t;
    }
}
