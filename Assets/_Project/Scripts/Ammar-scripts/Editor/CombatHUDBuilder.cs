// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Combat HUD Builder (Warzone style)
//  Top menu → Facility Breach → Build Combat HUD
//
//  Builds the entire in-game HUD canvas in one click, in the currently open
//  scene. Everything lives under Assets/_Project — no FPS Microgame UI used.
//
//  Creates:
//    • CombatHUDCanvas (overlay canvas)
//      ├── Top-left:   Objective tracker ("0 / 3" with cyan accent bar)
//      ├── Top-center: Compass strip (rotates with player heading)
//      ├── Top-right:  Minimap (circular, CoD-style rotating)
//      ├── Below mini: Weapon Nameplate (icon + name + type + accent)
//      ├── Center:     Crosshair (4 cyan lines + dot, dynamic spread)
//      ├── Center:     Hit Marker (X flash)
//      ├── Edges:      Red damage vignette (fullscreen flash on hit)
//      ├── Bot-left:   Health bar (320x80, green→red fill)
//      ├── Bot-left:   FRAG grenade counter (single slot — no flash/smoke)
//      └── Bot-right:  Ammo panel (big number + weapon name + reloading)
//    • MinimapCamera (in scene, follows player, renders to MinimapRT)
//    • HUDBridge component on the Player (reads FPS data, feeds the HUD)
//    • HideFPSHUD component on the canvas (disables old FPS Microgame HUD)
//
//  Re-running is safe — old builds are cleared first.
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public static class CombatHUDBuilder
{
    static readonly Color C_CYAN     = new Color32(0x00, 0xC8, 0xFF, 0xFF);
    static readonly Color C_CYAN_DIM = new Color(0f, 0.78f, 1f, 0.40f);
    static readonly Color C_PANEL    = new Color(0.02f, 0.04f, 0.08f, 0.78f);
    static readonly Color C_PANEL_BG = new Color(0.02f, 0.04f, 0.08f, 0.92f);
    static readonly Color C_WHITE    = Color.white;
    static readonly Color C_GREEN    = new Color(0f, 1f, 0.55f, 1f);
    static readonly Color C_RED      = new Color(1f, 0.25f, 0.25f, 1f);
    static readonly Color C_GRAY     = new Color32(0x88, 0x88, 0x95, 0xFF);

    [MenuItem("Facility Breach/Build Combat HUD", priority = -10)]
    public static void Build()
    {
        EnsureFolder("Assets/_Project", "ScriptableObjects");
        EnsureFolder("Assets/_Project", "RenderTextures");

        // 1) Cleanup any previous builds in the scene
        foreach (var name in new[] { "CombatHUDCanvas", "MinimapCamera", "PlayerHUDOverlay" })
        {
            var existing = GameObject.Find(name);
            if (existing != null) Undo.DestroyObjectImmediate(existing);
        }

        // 2) Canvas
        var canvasGO = new GameObject("CombatHUDCanvas");
        Undo.RegisterCreatedObjectUndo(canvasGO, "Build Combat HUD");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // 3) CombatHUD component (we'll fill its references as we build)
        var hud = canvasGO.AddComponent<CombatHUD>();

        // 4) Hide-FPS-HUD utility
        canvasGO.AddComponent<HideFPSHUD>();

        // 5) Build sections — no separate WeaponNameplate; ammo panel handles
        //    the cool swap animation + weapon name display.
        BuildObjectives(canvasGO.transform, hud);
        BuildCompass(canvasGO.transform, hud);
        BuildMinimap(canvasGO.transform, out RenderTexture rt, out RectTransform arrowRT);
        BuildCrosshair(canvasGO.transform, hud);
        BuildHitMarker(canvasGO.transform, hud);
        BuildDamageVignette(canvasGO.transform, hud);
        BuildHealthPanel(canvasGO.transform, hud);
        BuildFragCounter(canvasGO.transform, hud);
        BuildAmmoPanel(canvasGO.transform, hud);

        // 6) Minimap camera (separate scene GameObject)
        var camGO = new GameObject("MinimapCamera");
        Undo.RegisterCreatedObjectUndo(camGO, "Build Combat HUD");
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic        = true;
        cam.orthographicSize    = 18;
        cam.clearFlags          = CameraClearFlags.SolidColor;
        cam.backgroundColor     = new Color(0.02f, 0.04f, 0.08f, 1f);
        cam.targetTexture       = rt;
        cam.cullingMask         = ~(1 << 5);  // skip UI layer
        cam.depth               = -1;

        var minimap = canvasGO.AddComponent<Minimap>();
        minimap.minimapCamera     = cam;
        minimap.playerArrowUI     = arrowRT;
        minimap.rotateWithPlayer  = true;

        // 7) Weapon configs (create or load)
        hud.weaponConfigs.Clear();
        hud.weaponConfigs.AddRange(EnsureWeaponConfigs());

        // 8) Add HUDBridge to the Player (or warn)
        AttachHUDBridge(hud);

        // 9) Save scene dirty
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = canvasGO;
        Debug.Log("[CombatHUDBuilder] ✓ Combat HUD built. " +
                  "Press Play. The FPS Microgame HUD will auto-hide at runtime.");
    }

    // ════════════════════════════════════════════════════════════════════════
    //  SECTION BUILDERS
    // ════════════════════════════════════════════════════════════════════════
    static void BuildObjectives(Transform parent, CombatHUD hud)
    {
        var holder = NewRect("Objectives", parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        var rt = holder.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(30, -100);
        rt.sizeDelta        = new Vector2(280, 80);

        var bg = NewImage("BG", holder.transform, C_PANEL); Stretch(bg.rectTransform);

        var accent = NewImage("Accent", holder.transform, C_CYAN);
        var aRT = accent.rectTransform;
        aRT.anchorMin = new Vector2(0, 0); aRT.anchorMax = new Vector2(0, 1);
        aRT.pivot = new Vector2(0, 0.5f); aRT.sizeDelta = new Vector2(4, 0);

        var lbl = NewText("Label", holder.transform, "OBJECTIVE", 14, C_CYAN);
        lbl.characterSpacing = 6;
        var lRT = lbl.rectTransform;
        lRT.anchorMin = new Vector2(0, 1); lRT.anchorMax = new Vector2(1, 1);
        lRT.pivot = new Vector2(0, 1); lRT.anchoredPosition = new Vector2(16, -8);
        lRT.sizeDelta = new Vector2(0, 20);
        lbl.alignment = TextAlignmentOptions.Left;

        var prog = NewText("Progress", holder.transform, "0 / 3", 30, C_WHITE);
        prog.fontStyle = FontStyles.Bold;
        var pRT = prog.rectTransform;
        pRT.anchorMin = new Vector2(0, 0); pRT.anchorMax = new Vector2(1, 0);
        pRT.pivot = new Vector2(0, 0); pRT.anchoredPosition = new Vector2(16, 8);
        pRT.sizeDelta = new Vector2(0, 40);
        prog.alignment = TextAlignmentOptions.Left;
        hud.objectiveProgressText = prog;
    }

    static void BuildCompass(Transform parent, CombatHUD hud)
    {
        var holder = NewRect("Compass", parent, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
        var rt = holder.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(0, -40);
        rt.sizeDelta        = new Vector2(640, 40);

        var bg = NewImage("BG", holder.transform, C_PANEL); Stretch(bg.rectTransform);

        // Mask the inner area so tick container can be wider than visible
        var mask = NewRect("Mask", holder.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        var mRT = mask.GetComponent<RectTransform>();
        Stretch(mRT);
        var maskImg = mask.AddComponent<Image>(); maskImg.color = new Color(0,0,0,0.001f);
        mask.AddComponent<RectMask2D>();

        var ticks = NewRect("Ticks", mask.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        var tRT = ticks.GetComponent<RectTransform>();
        tRT.sizeDelta = new Vector2(0, 40);
        hud.compassTickContainer = tRT;

        float pixPerDeg = 4f;
        hud.compassPixelsPerDegree = pixPerDeg;

        // Two cycles of ticks (0..720) so panning wraps cleanly
        for (int deg = 0; deg < 720; deg += 15)
        {
            int d = deg % 360;
            string label = d switch { 0 => "N", 90 => "E", 180 => "S", 270 => "W", _ => "" };
            float x = deg * pixPerDeg;

            // Tick line
            var tick = NewImage("Tick_" + deg, ticks.transform, C_CYAN_DIM);
            var tickRT = tick.rectTransform;
            tickRT.anchorMin = new Vector2(0.5f, 0); tickRT.anchorMax = new Vector2(0.5f, 1);
            tickRT.pivot = new Vector2(0.5f, 0.5f);
            tickRT.anchoredPosition = new Vector2(x - 360 * pixPerDeg, 0);
            tickRT.sizeDelta = new Vector2(2, d % 90 == 0 ? 24 : 12);

            if (!string.IsNullOrEmpty(label))
            {
                var lbl = NewText("L_" + deg, ticks.transform, label, 18, C_CYAN);
                lbl.fontStyle = FontStyles.Bold;
                var lRT = lbl.rectTransform;
                lRT.anchorMin = new Vector2(0.5f, 1); lRT.anchorMax = new Vector2(0.5f, 1);
                lRT.pivot = new Vector2(0.5f, 1);
                lRT.anchoredPosition = new Vector2(x - 360 * pixPerDeg, -2);
                lRT.sizeDelta = new Vector2(40, 22);
                lbl.alignment = TextAlignmentOptions.Center;
            }
        }

        // Center tick highlight
        var center = NewImage("CenterTick", holder.transform, C_CYAN);
        var cRT = center.rectTransform;
        cRT.anchorMin = new Vector2(0.5f, 0); cRT.anchorMax = new Vector2(0.5f, 1);
        cRT.pivot = new Vector2(0.5f, 0.5f);
        cRT.sizeDelta = new Vector2(2, 0);
    }

    static void BuildMinimap(Transform parent, out RenderTexture rt, out RectTransform arrowRT)
    {
        rt = AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/_Project/RenderTextures/MinimapRT.renderTexture");
        if (rt == null)
        {
            rt = new RenderTexture(512, 512, 16);
            rt.name = "MinimapRT";
            AssetDatabase.CreateAsset(rt, "Assets/_Project/RenderTextures/MinimapRT.renderTexture");
            rt = AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/_Project/RenderTextures/MinimapRT.renderTexture");
        }

        var holder = NewRect("Minimap", parent, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
        var hRT = holder.GetComponent<RectTransform>();
        hRT.anchoredPosition = new Vector2(-30, -30);
        hRT.sizeDelta        = new Vector2(200, 200);

        var border = NewImage("Border", holder.transform, C_CYAN);
        Stretch(border.rectTransform);
        var bg = NewImage("BG", holder.transform, C_PANEL_BG);
        var bgRT = bg.rectTransform; Stretch(bgRT);
        bgRT.offsetMin = new Vector2(2, 2); bgRT.offsetMax = new Vector2(-2, -2);

        var imgGO = new GameObject("Render", typeof(RectTransform));
        imgGO.transform.SetParent(holder.transform, false);
        var iRT = imgGO.GetComponent<RectTransform>();
        Stretch(iRT);
        iRT.offsetMin = new Vector2(4, 4); iRT.offsetMax = new Vector2(-4, -4);
        var raw = imgGO.AddComponent<RawImage>(); raw.texture = rt;

        // Arrow ▲ at center, fixed pointing up
        var arrowGO = new GameObject("PlayerArrow", typeof(RectTransform));
        arrowGO.transform.SetParent(holder.transform, false);
        arrowRT = arrowGO.GetComponent<RectTransform>();
        arrowRT.anchorMin = new Vector2(0.5f, 0.5f); arrowRT.anchorMax = new Vector2(0.5f, 0.5f);
        arrowRT.pivot = new Vector2(0.5f, 0.5f);
        arrowRT.sizeDelta = new Vector2(40, 40);
        var at = arrowGO.AddComponent<TextMeshProUGUI>();
        at.text = "▲"; at.fontSize = 36; at.color = C_CYAN;
        at.alignment = TextAlignmentOptions.Center; at.fontStyle = FontStyles.Bold;
        at.raycastTarget = false;

        var title = NewText("Title", holder.transform, "TACTICAL MAP", 12, C_CYAN);
        title.characterSpacing = 8;
        var tRT = title.rectTransform;
        tRT.anchorMin = new Vector2(0, 1); tRT.anchorMax = new Vector2(1, 1);
        tRT.pivot = new Vector2(0.5f, 0); tRT.anchoredPosition = new Vector2(0, 4);
        tRT.sizeDelta = new Vector2(0, 16);
        title.alignment = TextAlignmentOptions.Center;
    }

    // (Top-right weapon nameplate was removed — the bottom-right ammo panel
    //  now handles weapon swap visuals via scale+slide animation.)

    static void BuildCrosshair(Transform parent, CombatHUD hud)
    {
        var holder = NewRect("Crosshair", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        var rt = holder.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(80, 80);

        hud.crosshairLines = new RectTransform[4];
        hud.crosshairLines[0] = MakeLine(holder.transform, "Top",    new Vector2(2, 10));
        hud.crosshairLines[1] = MakeLine(holder.transform, "Bottom", new Vector2(2, 10));
        hud.crosshairLines[2] = MakeLine(holder.transform, "Left",   new Vector2(10, 2));
        hud.crosshairLines[3] = MakeLine(holder.transform, "Right",  new Vector2(10, 2));

        // Center dot
        var dot = NewImage("Dot", holder.transform, C_CYAN);
        var dRT = dot.rectTransform;
        dRT.anchorMin = new Vector2(0.5f, 0.5f); dRT.anchorMax = new Vector2(0.5f, 0.5f);
        dRT.pivot = new Vector2(0.5f, 0.5f);
        dRT.sizeDelta = new Vector2(3, 3);
    }
    static RectTransform MakeLine(Transform parent, string name, Vector2 size)
    {
        var img = NewImage(name, parent, C_CYAN);
        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        return rt;
    }

    static void BuildHitMarker(Transform parent, CombatHUD hud)
    {
        var holder = NewRect("HitMarker", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        var rt = holder.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(40, 40);
        hud.hitMarkerGroup = holder.AddComponent<CanvasGroup>();
        hud.hitMarkerGroup.alpha = 0f;

        AddX(holder.transform,  45f);
        AddX(holder.transform, -45f);
    }
    static void AddX(Transform parent, float angle)
    {
        var img = NewImage("X", parent, C_WHITE);
        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(2, 24);
        rt.localRotation = Quaternion.Euler(0, 0, angle);
    }

    static void BuildDamageVignette(Transform parent, CombatHUD hud)
    {
        var img = NewImage("DamageVignette", parent, new Color(1, 0, 0, 0));
        Stretch(img.rectTransform);
        img.raycastTarget = false;
        hud.damageVignette = img;
    }

    static void BuildHealthPanel(Transform parent, CombatHUD hud)
    {
        var holder = NewRect("HealthPanel", parent, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
        var rt = holder.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(30, 30);
        rt.sizeDelta        = new Vector2(320, 80);

        var bg = NewImage("BG", holder.transform, C_PANEL); Stretch(bg.rectTransform);

        var lbl = NewText("Label", holder.transform, "HEALTH", 14, C_CYAN);
        lbl.characterSpacing = 6;
        var lRT = lbl.rectTransform;
        lRT.anchorMin = new Vector2(0, 1); lRT.anchorMax = new Vector2(0, 1);
        lRT.pivot = new Vector2(0, 1); lRT.anchoredPosition = new Vector2(14, -8);
        lRT.sizeDelta = new Vector2(120, 18);
        lbl.alignment = TextAlignmentOptions.Left;

        var val = NewText("Value", holder.transform, "100", 32, C_WHITE);
        val.fontStyle = FontStyles.Bold;
        var vRT = val.rectTransform;
        vRT.anchorMin = new Vector2(1, 1); vRT.anchorMax = new Vector2(1, 1);
        vRT.pivot = new Vector2(1, 1); vRT.anchoredPosition = new Vector2(-14, -2);
        vRT.sizeDelta = new Vector2(80, 36);
        val.alignment = TextAlignmentOptions.Right;
        hud.healthText = val;

        var barBG = NewImage("BarBG", holder.transform, new Color(1, 1, 1, 0.15f));
        var bRT = barBG.rectTransform;
        bRT.anchorMin = new Vector2(0, 0); bRT.anchorMax = new Vector2(1, 0);
        bRT.pivot = new Vector2(0, 0); bRT.anchoredPosition = new Vector2(14, 14);
        bRT.sizeDelta = new Vector2(-28, 14);

        var fill = NewImage("BarFill", holder.transform, C_GREEN);
        var fRT = fill.rectTransform;
        fRT.anchorMin = new Vector2(0, 0); fRT.anchorMax = new Vector2(1, 0);
        fRT.pivot = new Vector2(0, 0); fRT.anchoredPosition = new Vector2(14, 14);
        fRT.sizeDelta = new Vector2(-28, 14);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 1f;
        hud.healthFill = fill;

        var hf = NewImage("HitFlash", holder.transform, new Color(1, 0.2f, 0.2f, 0f));
        Stretch(hf.rectTransform);
        hud.healthHitFlashGroup = hf.gameObject.AddComponent<CanvasGroup>();
        hud.healthHitFlashGroup.alpha = 0f;
    }

    static void BuildFragCounter(Transform parent, CombatHUD hud)
    {
        var holder = NewRect("FragCounter", parent, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
        var rt = holder.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(370, 30);
        rt.sizeDelta        = new Vector2(100, 80);
        hud.fragCounterPanel = holder;   // so CombatHUD can hide it when no grenade system

        var bg = NewImage("BG", holder.transform, C_PANEL); Stretch(bg.rectTransform);
        var accent = NewImage("Accent", holder.transform, new Color(1f, 0.5f, 0.2f, 1f));
        var aRT = accent.rectTransform;
        aRT.anchorMin = new Vector2(0, 0); aRT.anchorMax = new Vector2(0, 1);
        aRT.pivot = new Vector2(0, 0.5f); aRT.sizeDelta = new Vector2(4, 0);

        var lbl = NewText("Label", holder.transform, "FRAG", 14, new Color(1f, 0.55f, 0.20f));
        lbl.characterSpacing = 6;
        var lRT = lbl.rectTransform;
        lRT.anchorMin = new Vector2(0, 1); lRT.anchorMax = new Vector2(1, 1);
        lRT.pivot = new Vector2(0.5f, 1); lRT.anchoredPosition = new Vector2(0, -8);
        lRT.sizeDelta = new Vector2(0, 18);
        lbl.alignment = TextAlignmentOptions.Center;

        var count = NewText("Count", holder.transform, "x3", 30, C_WHITE);
        count.fontStyle = FontStyles.Bold;
        var cRT = count.rectTransform;
        cRT.anchorMin = new Vector2(0, 0); cRT.anchorMax = new Vector2(1, 0);
        cRT.pivot = new Vector2(0.5f, 0); cRT.anchoredPosition = new Vector2(0, 8);
        cRT.sizeDelta = new Vector2(0, 40);
        count.alignment = TextAlignmentOptions.Center;
        hud.fragCountText = count;
    }

    static void BuildAmmoPanel(Transform parent, CombatHUD hud)
    {
        var holder = NewRect("AmmoPanel", parent, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0));
        var rt = holder.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(-30, 30);
        rt.sizeDelta        = new Vector2(340, 130);

        var bg = NewImage("BG", holder.transform, C_PANEL); Stretch(bg.rectTransform);

        var accent = NewImage("Accent", holder.transform, C_CYAN);
        var aRT = accent.rectTransform;
        aRT.anchorMin = new Vector2(1, 0); aRT.anchorMax = new Vector2(1, 1);
        aRT.pivot = new Vector2(1, 0.5f); aRT.sizeDelta = new Vector2(4, 0);
        hud.ammoAccentBar = accent;

        var wn = NewText("WeaponName", holder.transform, "WEAPON", 16, C_CYAN);
        wn.characterSpacing = 6;
        var wnRT = wn.rectTransform;
        wnRT.anchorMin = new Vector2(0, 1); wnRT.anchorMax = new Vector2(1, 1);
        wnRT.pivot = new Vector2(0.5f, 1); wnRT.anchoredPosition = new Vector2(-8, -10);
        wnRT.sizeDelta = new Vector2(-20, 22);
        wn.alignment = TextAlignmentOptions.Right;
        hud.ammoWeaponNameText = wn;

        var cur = NewText("AmmoCurrent", holder.transform, "30", 64, C_WHITE);
        cur.fontStyle = FontStyles.Bold;
        var curRT = cur.rectTransform;
        curRT.anchorMin = new Vector2(0.5f, 0); curRT.anchorMax = new Vector2(0.5f, 0);
        curRT.pivot = new Vector2(0.5f, 0); curRT.anchoredPosition = new Vector2(-40, 6);
        curRT.sizeDelta = new Vector2(160, 80);
        cur.alignment = TextAlignmentOptions.Right;
        hud.ammoCurrentText = cur;

        var res = NewText("AmmoMax", holder.transform, "/ 90", 24, C_GRAY);
        var resRT = res.rectTransform;
        resRT.anchorMin = new Vector2(0.5f, 0); resRT.anchorMax = new Vector2(0.5f, 0);
        resRT.pivot = new Vector2(0, 0); resRT.anchoredPosition = new Vector2(30, 24);
        resRT.sizeDelta = new Vector2(80, 40);
        res.alignment = TextAlignmentOptions.Left;
        hud.ammoMaxText = res;

        var low = NewImage("LowFlash", holder.transform, new Color(1, 0.3f, 0.3f, 0));
        Stretch(low.rectTransform);
        low.raycastTarget = false;
        hud.ammoLowFlash = low;

        var reload = NewText("Reloading", holder.transform, "RELOADING", 18, C_CYAN);
        reload.fontStyle = FontStyles.Bold;
        reload.characterSpacing = 6;
        var rlRT = reload.rectTransform;
        rlRT.anchorMin = new Vector2(0, 0); rlRT.anchorMax = new Vector2(1, 0);
        rlRT.pivot = new Vector2(0.5f, 0); rlRT.anchoredPosition = new Vector2(-8, -22);
        rlRT.sizeDelta = new Vector2(-20, 22);
        reload.alignment = TextAlignmentOptions.Center;
        hud.reloadingGroup = reload.gameObject.AddComponent<CanvasGroup>();
        hud.reloadingGroup.alpha = 0f;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  WEAPON CONFIGS & BRIDGE
    // ════════════════════════════════════════════════════════════════════════
    static WeaponUIConfig[] EnsureWeaponConfigs()
    {
        var arr = new[]
        {
            EnsureCfg("WeaponUI_AK74",     "AK74",        "AK-74",          "ASSAULT RIFLE", new Color(1f, 0.55f, 0.10f)),
            EnsureCfg("WeaponUI_M107",     "M107",        "M107",           "SNIPER RIFLE",  new Color(1f, 0.25f, 0.25f)),
            EnsureCfg("WeaponUI_M1911",    "M1911",       "M1911",          "PISTOL",        new Color(0.95f, 0.95f, 1f)),
            EnsureCfg("WeaponUI_Bennelli", "Bennelli M4", "BENELLI M4",     "SHOTGUN",       new Color(1f, 0.85f, 0.15f)),
            EnsureCfg("WeaponUI_Knife",    "Knife",       "TACTICAL KNIFE", "MELEE",         new Color(0f, 0.78f, 1f)),
        };
        AssetDatabase.SaveAssets();
        return arr;
    }
    static WeaponUIConfig EnsureCfg(string assetName, string wpn, string disp, string type, Color color)
    {
        string path = $"Assets/_Project/ScriptableObjects/{assetName}.asset";
        var cfg = AssetDatabase.LoadAssetAtPath<WeaponUIConfig>(path);
        if (cfg == null)
        {
            cfg = ScriptableObject.CreateInstance<WeaponUIConfig>();
            AssetDatabase.CreateAsset(cfg, path);
        }
        cfg.weaponName = wpn; cfg.displayName = disp; cfg.typeLabel = type; cfg.accentColor = color;
        EditorUtility.SetDirty(cfg);
        return cfg;
    }

    static void AttachHUDBridge(CombatHUD hud)
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[CombatHUDBuilder] No GameObject tagged 'Player' — HUDBridge not attached. " +
                             "Add the HUDBridge component to your Player manually.");
            return;
        }
        var existing = player.GetComponent<HUDBridge>();
        if (existing == null) existing = player.AddComponent<HUDBridge>();
        existing.hud = hud;
        EditorUtility.SetDirty(existing);
        Debug.Log("[CombatHUDBuilder] HUDBridge attached to " + player.name);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  HELPERS
    // ════════════════════════════════════════════════════════════════════════
    static GameObject NewRect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 piv)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = piv;
        return go;
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
        t.raycastTarget = false;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void EnsureFolder(string parent, string folder)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + folder))
            AssetDatabase.CreateFolder(parent, folder);
    }
}
