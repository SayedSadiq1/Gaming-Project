using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.FPS.Gameplay;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Co-op Bootstrap (horizontal split-screen, 2 players)
//
//  Drop this component on an empty GameObject in any gameplay scene. On Start:
//    1. Finds the existing FPS Player (P1) and the optional `Player2Spawn`
//       empty in the scene (defines where P2 lands).
//    2. Instantiates a second Player by cloning the existing one.
//    3. Sets camera Rects so P1 renders top half, P2 bottom half.
//    4. Disables P2's AudioListener (Unity needs exactly one).
//    5. Clones the global InputActions asset twice, restricts P1 to
//       Keyboard+Mouse and P2 to Gamepad, hands each to its PlayerInputHandler.
//    6. Pauses AutoSave (co-op runs aren't saved).
//    7. Spawns a CoopEnemyRetargeter so enemies aim at nearest live player.
//    8. Hooks death/extract for either player.
//
//  Requirements:
//    • An empty GameObject named "Player2Spawn" placed somewhere walkable
//      (if missing, P2 spawns 3m to P1's right)
//    • A connected gamepad for P2 (USB / Bluetooth Xbox / PS controller)
//
//  P1 = keyboard + mouse
//  P2 = gamepad
// ─────────────────────────────────────────────────────────────────────────────
public class CoopBootstrap : MonoBehaviour
{
    [Header("Spawn")]
    [Tooltip("Optional spawn point for Player 2. If empty, P2 spawns 3m to P1's right.")]
    public Transform player2Spawn;
    [Tooltip("Fallback offset from P1 if no Player2Spawn is set.")]
    public Vector3   p2FallbackOffset = new Vector3(2.5f, 0, 0);

    [Header("Camera Split")]
    [Tooltip("Top half for P1, bottom half for P2. Untick to flip.")]
    public bool      player1OnTop = true;

    [Header("Debug")]
    public bool debugLog = true;

    // ── Static accessors so other systems can find both players ──────────
    public static GameObject Player1 { get; private set; }
    public static GameObject Player2 { get; private set; }
    public static bool       IsActive { get; private set; }
    public static List<Transform> LivePlayerTransforms()
    {
        var list = new List<Transform>(2);
        if (Player1 != null && IsAlive(Player1)) list.Add(Player1.transform);
        if (Player2 != null && IsAlive(Player2)) list.Add(Player2.transform);
        return list;
    }

    static bool IsAlive(GameObject p)
    {
        if (p == null) return false;
        var healthType = System.Type.GetType("Unity.FPS.Game.Health, Unity.FPS.Game");
        if (healthType == null) return true;
        var c = p.GetComponentInChildren(healthType);
        if (c == null) return true;
        try {
            var f = healthType.GetField("CurrentHealth");
            if (f == null) return true;
            return (float)f.GetValue(c) > 0.01f;
        } catch { return true; }
    }

    /// <summary>PlayerPrefs key set by the main menu's co-op toggle.</summary>
    public const string PREF_COOP_ENABLED = "FB_CoopEnabled";
    public static bool IsCoopEnabled => PlayerPrefs.GetInt(PREF_COOP_ENABLED, 0) == 1;

    void Awake()
    {
        // Main-menu controlled gate. If co-op is OFF, disable ourselves so the
        // scene plays as single-player even though the bootstrap exists.
        if (!IsCoopEnabled)
        {
            Debug.Log("[CoopBootstrap] FB_CoopEnabled=0 → co-op disabled, single-player mode.");
            gameObject.SetActive(false);
        }
    }

    void Start()
    {
        StartCoroutine(BootstrapRoutine());
    }

    IEnumerator BootstrapRoutine()
    {
        Player1 = GameObject.FindGameObjectWithTag("Player");
        if (Player1 == null)
        {
            Debug.LogError("[CoopBootstrap] No Player-tagged GameObject in scene. Aborting co-op setup.");
            yield break;
        }

        // ── P1 INPUT: restrict global InputActions to Keyboard+Mouse so the
        //    existing Player can't be moved by P2's gamepad. P1's PIH.Start
        //    already ran with global; the devices filter applies retroactively.
        RestrictGlobalToKbm();

        // ── P2 SPAWN: instantiate immediately, configure overrideActions
        //    SAME FRAME so P2.PIH.Start (queued for end-of-frame) sees them.
        Vector3 p2Pos = player2Spawn != null
            ? player2Spawn.position
            : Player1.transform.position + p2FallbackOffset;
        Quaternion p2Rot = player2Spawn != null
            ? player2Spawn.rotation
            : Player1.transform.rotation;

        Player2 = Instantiate(Player1, p2Pos, p2Rot);
        Player2.name = "Player_2";
        if (debugLog) Debug.Log($"[CoopBootstrap] Spawned Player 2 at {p2Pos}.");

        // CRITICAL: set overrideActions BEFORE yielding so P2.PIH.Start picks it up
        ConfigureP2InputBeforeStart(Player2);

        // Now let one frame pass so all the spawned Start() callbacks fire
        yield return null;

        // Split-screen cameras (safe after Start has run)
        ConfigureCameraSplit(Player1, player1OnTop);
        ConfigureCameraSplit(Player2, !player1OnTop);

        // One AudioListener only — kill P2's
        DisableAllAudioListenersOn(Player2);

        // HUD: keep the SINGLE original CombatHUDCanvas as Screen Space - Overlay
        // (same as single-player). Both halves see the same HUD layout. P2's
        // stats aren't reflected — known limitation, but the HUD looks normal.
        // (Per-player HUD cloning was creating too many side-effects.)

        // Disable the FPS Microgame's GameHUD overlay (the reload ring + crosshair)
        // because it conflicts with our CombatHUD.
        DisableFpsGameHud();

        // Spawn a small P2 status widget at the bottom of the screen so the
        // gamepad player has live readout of their HP / ammo / weapon (the
        // main CombatHUD shows P1's data).
        CoopPlayer2Widget.SpawnFor(Player2);

        // Spawn the enemy retargeter (one per scene)
        if (FindFirstObjectByType<CoopEnemyRetargeter>() == null)
        {
            var rt = new GameObject("CoopEnemyRetargeter");
            rt.AddComponent<CoopEnemyRetargeter>();
        }

        // Pause AutoSave for the co-op run
        PlayerPrefs.SetInt("FB_AutoSave_Paused", 1);
        PlayerPrefs.Save();
        Debug.Log("[CoopBootstrap] AutoSave paused for co-op session.");

        // Hook either-player-died → DeathScreen routing (FPS's GameFlowManager
        // already fires PlayerDeathEvent for P1; we manually watch P2 here).
        StartCoroutine(WatchPlayerDeath());

        IsActive = true;
        if (debugLog) Debug.Log("[CoopBootstrap] ✓ Co-op setup complete. P1=KBM, P2=Gamepad.");
    }

    // ── Per-player HUD ─────────────────────────────────────────────────────
    //  Clones CombatHUDCanvas and binds each clone (P1's + P2's) to that
    //  player's camera + that player's transform/health/weapons.
    //
    //  Render mode goes from Screen Space - Overlay to Screen Space - Camera
    //  so the HUD only renders on its camera's viewport rect (i.e. half the
    //  screen).
    void SetupPerPlayerHud()
    {
        var hudCanvas = GameObject.Find("CombatHUDCanvas");
        if (hudCanvas == null)
        {
            Debug.LogWarning("[CoopBootstrap] No CombatHUDCanvas in scene — skipping per-player HUD.");
            return;
        }

        var p1Cam = Player1.GetComponentInChildren<Camera>(true);
        var p2Cam = Player2.GetComponentInChildren<Camera>(true);

        // Convert the original to render on P1's camera viewport
        var p1Canvas = BindCanvasToCamera(hudCanvas, p1Cam, "_P1");
        BindCanvasScriptsToPlayer(p1Canvas, Player1.transform);

        // Clone it for P2
        var p2Clone = Instantiate(hudCanvas);
        p2Clone.name = "CombatHUDCanvas_P2";
        var p2Canvas = BindCanvasToCamera(p2Clone, p2Cam, "_P2");
        BindCanvasScriptsToPlayer(p2Clone, Player2.transform);
        DuplicateMinimapForP2(p2Clone);

        if (debugLog) Debug.Log("[CoopBootstrap] ✓ Per-player HUDs configured.");
    }

    static GameObject BindCanvasToCamera(GameObject canvasGO, Camera playerCam, string suffix)
    {
        var canvas = canvasGO.GetComponent<Canvas>();
        if (canvas != null && playerCam != null)
        {
            // Make sure UI layer is excluded from the player camera AND the
            // canvas hierarchy lives on UI layer — that way only the dedicated
            // UI camera renders the HUD, with no post-processing.
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer < 0) uiLayer = 5;                      // Unity's default UI layer index
            SetLayerRecursive(canvasGO, uiLayer);
            playerCam.cullingMask &= ~(1 << uiLayer);

            var uiCam = MakeUICamera(playerCam, suffix, uiLayer);

            canvas.renderMode    = UnityEngine.RenderMode.ScreenSpaceCamera;
            canvas.worldCamera   = uiCam;
            canvas.planeDistance = 1f;
            canvas.sortingOrder  = 50;
        }
        canvasGO.name = canvasGO.name.Replace("(Clone)", "").TrimEnd() + suffix;
        return canvasGO;
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
    }

    // Spawn a UI-only camera that renders ONLY the UI layer.
    // For URP, this MUST be an Overlay camera added to the base camera's
    // stack — otherwise an independent camera clears its viewport and the
    // world disappears.
    static Camera MakeUICamera(Camera playerCam, string suffix, int uiLayer)
    {
        var go = new GameObject("UICamera" + suffix);
        go.transform.SetParent(playerCam.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        var cam = go.AddComponent<Camera>();
        cam.cullingMask   = 1 << uiLayer;               // render ONLY the UI layer
        cam.useOcclusionCulling = false;
        cam.allowMSAA     = false;
        cam.allowHDR      = false;
        cam.orthographic  = false;
        // Rect is inherited from the base camera in the stack; we don't set
        // it here. (Overlay cameras use the base camera's viewport.)

        // URP: mark this as an Overlay camera and add to the player cam's stack
        var camData = cam.GetUniversalAdditionalCameraData();
        if (camData != null)
        {
            camData.renderType = CameraRenderType.Overlay;
            camData.renderPostProcessing = false;       // keep HUD crisp
        }
        var baseCamData = playerCam.GetUniversalAdditionalCameraData();
        if (baseCamData != null && !baseCamData.cameraStack.Contains(cam))
        {
            baseCamData.cameraStack.Add(cam);
        }

        return cam;
    }

    static void BindCanvasScriptsToPlayer(GameObject canvasGO, Transform playerTransform)
    {
        // HUDBridge — wires Health / Ammo / Weapon name. Set overridePlayer
        // and call Rebind() so it picks up the new player immediately (not
        // just on the next late lookup).
        foreach (var br in canvasGO.GetComponentsInChildren<HUDBridge>(true))
        {
            br.overridePlayer = playerTransform;
            br.Rebind();
        }
        // Minimap — has a public `target` field. Just point it.
        foreach (var mm in canvasGO.GetComponentsInChildren<Minimap>(true))
        {
            mm.target = playerTransform;
        }
    }

    // ── Disable FPS Microgame's GameHUD ────────────────────────────────────
    //  Kills the screen-center reload ring + crosshair from the FPS HUD.
    //  Searches by canvas name AND by AmmoCounter / PlayerHealthBar / Crosshair
    //  type names (whatever scripts live on the GameHUD prefab) — much more
    //  resilient than just looking for a canvas named "GameHUD".
    static void DisableFpsGameHud()
    {
        var doomed = new HashSet<GameObject>();

        // By canvas name
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c == null) continue;
            string n = c.gameObject.name;
            if (n.IndexOf("GameHUD", System.StringComparison.OrdinalIgnoreCase) >= 0)
                doomed.Add(c.gameObject);
        }

        // By component type names (catch dynamically-spawned HUDs)
        string[] fpsHudTypes = { "AmmoCounter", "PlayerHealthBar", "JetpackCounter", "Crosshair", "StanceHUD", "FeedbackFlashHUD" };
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null) continue;
            string tn = mb.GetType().Name;
            if (System.Array.IndexOf(fpsHudTypes, tn) < 0) continue;
            // Walk up to find its root canvas, disable that
            var canvas = mb.GetComponentInParent<Canvas>(true);
            if (canvas != null) doomed.Add(canvas.gameObject);
        }

        foreach (var go in doomed) go.SetActive(false);
        if (doomed.Count > 0) Debug.Log($"[CoopBootstrap] Disabled {doomed.Count} FPS HUD canvas(es).");
    }

    // ── Per-player minimap (clone camera + RenderTexture for P2) ─────────
    //  Without this, both HUDs' minimap RawImages show the same RT (which is
    //  written by P1's MinimapCamera — so P2 sees P1's view). Clone the
    //  camera GameObject and its RT so P2 gets its own.
    void DuplicateMinimapForP2(GameObject p2Canvas)
    {
        var p2Minimap = p2Canvas.GetComponentInChildren<Minimap>(true);
        if (p2Minimap == null || p2Minimap.minimapCamera == null) return;

        var sharedCam   = p2Minimap.minimapCamera;     // currently the scene's MinimapCamera
        var sharedRT    = sharedCam.targetTexture;     // its RenderTexture

        // Clone the camera GameObject
        var cloneCamGO = Instantiate(sharedCam.gameObject,
                                     sharedCam.transform.position,
                                     sharedCam.transform.rotation);
        cloneCamGO.name = "MinimapCamera_P2";
        var cloneCam = cloneCamGO.GetComponent<Camera>();

        // Clone the RenderTexture (new instance, same descriptor)
        if (sharedRT != null)
        {
            var cloneRT = new RenderTexture(sharedRT.descriptor);
            cloneRT.name = "MinimapRT_P2";
            cloneRT.Create();
            cloneCam.targetTexture = cloneRT;

            // Find the minimap RawImage on this canvas and re-route it to the
            // new RT (it currently points at sharedRT — same as P1's).
            foreach (var ri in p2Canvas.GetComponentsInChildren<UnityEngine.UI.RawImage>(true))
            {
                if (ri.texture == sharedRT) ri.texture = cloneRT;
            }
        }

        // Point P2's Minimap script at the cloned camera
        p2Minimap.minimapCamera = cloneCam;
        if (debugLog) Debug.Log("[CoopBootstrap] Duplicated minimap camera + RT for P2.");
    }

    // ── Camera split ──────────────────────────────────────────────────────
    static void ConfigureCameraSplit(GameObject player, bool topHalf)
    {
        var cam = player.GetComponentInChildren<Camera>(true);
        if (cam == null)
        {
            Debug.LogWarning($"[CoopBootstrap] No camera under {player.name}");
            return;
        }
        // Horizontal split: full width, half height
        // top half:    y=0.5, h=0.5
        // bottom half: y=0,   h=0.5
        cam.rect = new Rect(0, topHalf ? 0.5f : 0f, 1f, 0.5f);
    }

    static void DisableAllAudioListenersOn(GameObject root)
    {
        foreach (var al in root.GetComponentsInChildren<AudioListener>(true))
            al.enabled = false;
    }

    // ── P1 input: restrict global actions to keyboard+mouse ──────────────
    static void RestrictGlobalToKbm()
    {
        var global = InputSystem.actions;
        if (global == null) return;
        var devs = new List<InputDevice>();
        if (Keyboard.current != null) devs.Add(Keyboard.current);
        if (Mouse.current != null)    devs.Add(Mouse.current);
        global.devices = devs.ToArray();
        Debug.Log($"[CoopBootstrap] Global actions devices = [{string.Join(", ", devs.ConvertAll(d => d.displayName))}]");
    }

    // ── P2 input: clone the actions asset, pair to gamepad, hand to its PIH ─
    static void ConfigureP2InputBeforeStart(GameObject player2)
    {
        var pih = player2.GetComponent<PlayerInputHandler>();
        if (pih == null)
        {
            Debug.LogWarning($"[CoopBootstrap] No PlayerInputHandler on {player2.name}");
            return;
        }

        var global = InputSystem.actions;
        var local  = Instantiate(global);
        local.name = "P2_Actions_Gamepad";

        // Restrict to the gamepad device only. Don't use bindingMask —
        // that was filtering too aggressively and breaking Reload/NextWeapon.
        // Devices-only filter means: keyboard bindings exist on the actions
        // but receive no input because keyboard isn't in the device list,
        // gamepad bindings get gamepad input.
        if (Gamepad.current != null)
            local.devices = new InputDevice[] { Gamepad.current };
        else
            Debug.LogWarning("[CoopBootstrap] No Gamepad detected for P2. " +
                             "Plug in a controller and re-enter Play mode.");

        // Force-enable every action map on the cloned asset (Instantiate
        // doesn't auto-enable them).
        foreach (var map in local.actionMaps) map.Enable();

        pih.overrideActions = local;
    }

    // ── Death watch (either-dies → DeathScreen) ───────────────────────────
    IEnumerator WatchPlayerDeath()
    {
        var healthType = System.Type.GetType("Unity.FPS.Game.Health, Unity.FPS.Game");
        if (healthType == null) yield break;
        var hpField = healthType.GetField("CurrentHealth");

        while (Player1 != null && Player2 != null)
        {
            bool p1Dead = !IsAlive(Player1);
            bool p2Dead = !IsAlive(Player2);
            if (p1Dead || p2Dead)
            {
                Debug.Log($"[CoopBootstrap] Player down (P1={p1Dead}, P2={p2Dead}) → loading DeathScreen.");
                // Unpause AutoSave before scene change (the death screen flow
                // already handles its own cursor/save state)
                PlayerPrefs.SetInt("FB_AutoSave_Paused", 0);
                PlayerPrefs.Save();
                LoadingScreen.Load("DeathScreen");
                yield break;
            }
            yield return new WaitForSeconds(0.25f);
        }
    }

    void OnDestroy()
    {
        IsActive = false;
        // Restore AutoSave when leaving co-op
        PlayerPrefs.SetInt("FB_AutoSave_Paused", 0);
        PlayerPrefs.Save();
    }
}
