// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Level 3 Interactions Builder
//  Top menu → Facility Breach → Setup Level 3 Interactions
//
//  Auto-wires:
//    • PlayerInteractor + KeycardInventory on the Player
//    • ServerHack on "Server Controller", "Server Controller 2/3"
//    • KeycardPickup on Assets/_Project/Prefabs/BlueKeycard.prefab
//    • Builds the interaction prompt UI canvas
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public static class Level3InteractionsBuilder
{
    static readonly Color C_CYAN  = new Color32(0x00, 0xC8, 0xFF, 0xFF);
    static readonly Color C_PANEL = new Color(0.02f, 0.04f, 0.08f, 0.85f);
    static readonly Color C_WHITE = Color.white;

    [MenuItem("Facility Breach/Setup Level 3 Interactions")]
    public static void Setup()
    {
        AttachToPlayer();
        AttachServerHacks();
        SetupBlueKeycardPrefab();
        SetupKeycardPanels();
        SetupExitGate();
        SetupLabExplosionCutscene();
        BuildPromptUI();
        BuildPasswordEntryCanvas();
        SetupPasswordSigns();
        SetupServer3Spawners();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[Level3InteractionsBuilder] ✓ Setup complete. Play, look at a target, hold F.");
    }

    static void SetupLabExplosionCutscene()
    {
        // Reuse the grenade Explosion prefab so the VFX/SFX matches frags.
        var explosionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Weapons/AegisGrenades/Prefabs/Explosion.prefab");
        if (explosionPrefab == null)
            Debug.LogWarning("[Level3InteractionsBuilder] Couldn't load Explosion.prefab — cutscene will have no VFX.");

        // Reuse or create the cutscene GameObject
        var existing = GameObject.Find("LabExplosionCutscene");
        if (existing != null) Undo.DestroyObjectImmediate(existing);

        var go = new GameObject("LabExplosionCutscene");
        Undo.RegisterCreatedObjectUndo(go, "Create Lab Explosion Cutscene");
        var cut = go.AddComponent<LabExplosionCutscene>();
        cut.explosionPrefab = explosionPrefab;

        // Find the 3 player-placed MARKERS (any GameObject — they don't need a
        // Camera component, just a position + rotation). Case-insensitive name
        // match so "camera room 1", "Camera Room 1", "Camera_Room_1" all work.
        var mark1 = FindMarkerByName("camera room 1");
        var mark2 = FindMarkerByName("camera room 2");
        var mark3 = FindMarkerByName("camera room 3");
        var grp1 = GameObject.Find("Servers 1");
        var grp2 = GameObject.Find("Servers 2");
        var grp3 = GameObject.Find("Servers 3");

        cut.roomShots = new System.Collections.Generic.List<LabExplosionCutscene.RoomShot>();
        cut.roomShots.Add(new LabExplosionCutscene.RoomShot
        {
            cameraMarker    = mark1,
            serversGroup    = grp1 != null ? grp1.transform : null,
            secondaryBlasts = 2,
            fieldOfView     = 60f,
            lookAtOffset    = new Vector3(13.7f, -1.98f, -3.27f),   // tuned manually
        });
        cut.roomShots.Add(new LabExplosionCutscene.RoomShot
        {
            cameraMarker    = mark2,
            serversGroup    = grp2 != null ? grp2.transform : null,
            secondaryBlasts = 2,
            fieldOfView     = 60f,
            lookAtOffset    = new Vector3(-1.24f, 0.83f, 3.57f),    // tuned manually
        });
        cut.roomShots.Add(new LabExplosionCutscene.RoomShot
        {
            cameraMarker    = mark3,
            serversGroup    = grp3 != null ? grp3.transform : null,
            secondaryBlasts = 3,
            fieldOfView     = 60f,
            lookAtOffset    = Vector3.zero,                          // not tuned yet — set when you do
        });

        EditorUtility.SetDirty(cut);

        Debug.Log("[Level3InteractionsBuilder] ✓ LabExplosionCutscene wired. " +
                  $"mark1={mark1!=null} mark2={mark2!=null} mark3={mark3!=null}, " +
                  $"grp1={grp1!=null} grp2={grp2!=null} grp3={grp3!=null}");
        if (mark1 == null || mark2 == null || mark3 == null)
            Debug.LogWarning("[Level3InteractionsBuilder] One or more camera markers missing. " +
                             "Place GameObjects named 'camera room 1/2/3' in the scene " +
                             "(any 3D object works — its position + rotation defines the shot).");
    }

    static Transform FindMarkerByName(string name)
    {
        // Match any GameObject (including inactive) by case-insensitive name
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null) continue;
            if (string.Equals(t.gameObject.name, name, System.StringComparison.OrdinalIgnoreCase))
                return t;
        }
        return null;
    }

    static void SetupExitGate()
    {
        var panel = GameObject.Find("Exit_panel");
        if (panel == null) { Debug.LogWarning("[Level3InteractionsBuilder] Couldn't find 'Exit_panel'."); return; }

        var leftDoor  = GameObject.Find("Exit_left");
        var rightDoor = GameObject.Find("Exit_Right");
        if (leftDoor == null || rightDoor == null)
        {
            Debug.LogWarning("[Level3InteractionsBuilder] Exit doors missing. " +
                             $"left={leftDoor != null}, right={rightDoor != null}");
        }

        var gate = panel.GetComponent<ExitGatePanel>();
        if (gate == null) gate = Undo.AddComponent<ExitGatePanel>(panel);

        gate.requiredServers       = 3;
        gate.requiredKills         = 30;     // must drop at least 30 spawned enemies first
        gate.requiresKeycard       = true;
        gate.requiredColor         = KeycardColor.Blue;
        gate.keycardID             = "blue_keycard";

        gate.leftDoor        = leftDoor  != null ? leftDoor.transform  : null;
        gate.rightDoor       = rightDoor != null ? rightDoor.transform : null;
        // Closed 35.73994 → Open 32.98994 (delta -2.75), and 41.77994 → 44.52994 (delta +2.75)
        gate.leftOpenOffset  = new Vector3(-2.75f, 0f, 0f);
        gate.rightOpenOffset = new Vector3( 2.75f, 0f, 0f);
        gate.slideDuration   = 1.5f;
        gate.nextSceneName   = "Level4";

        EditorUtility.SetDirty(gate);

        if (panel.GetComponent<Collider>() == null)
        {
            var col = Undo.AddComponent<BoxCollider>(panel);
            col.size = new Vector3(1.0f, 2.0f, 1.0f);
        }

        Debug.Log("[Level3InteractionsBuilder] ExitGatePanel wired " +
                  $"(left={(leftDoor != null ? leftDoor.name : "MISSING")}, " +
                  $"right={(rightDoor != null ? rightDoor.name : "MISSING")}).");
    }

    static void SetupKeycardPanels()
    {
        // Server Room 2 panel
        SetupOnePanel("panel (2)", "Server Room 2",
            leftOffset:  new Vector3(-2.71f, 0f, 0f),
            rightOffset: new Vector3( 2.72f, 0f, 0f));

        // Server Room 3 panel
        SetupOnePanel("panel (3)", "Server Room 3",
            leftOffset:  new Vector3(-2.75f, 0f, 0f),
            rightOffset: new Vector3( 2.75f, 0f, 0f));
    }

    static void SetupOnePanel(string panelName, string roomName, Vector3 leftOffset, Vector3 rightOffset)
    {
        var panel = GameObject.Find(panelName);
        if (panel == null) { Debug.LogWarning($"[Level3InteractionsBuilder] Couldn't find '{panelName}'."); return; }

        var room  = GameObject.Find(roomName);
        if (room == null) { Debug.LogWarning($"[Level3InteractionsBuilder] Couldn't find '{roomName}'."); return; }

        // Find left + right doors inside this specific room
        Transform leftDoor = null, rightDoor = null;
        foreach (var t in room.GetComponentsInChildren<Transform>(true))
        {
            if (t == null) continue;
            string n = t.gameObject.name;
            if (n == "Door_Left_01 (1)" && leftDoor == null)  leftDoor  = t;
            else if (n == "Door_Left_01 (2)" && rightDoor == null) rightDoor = t;
        }
        if (leftDoor == null || rightDoor == null)
            Debug.LogWarning($"[Level3InteractionsBuilder] Doors not found inside '{roomName}'. " +
                             $"Found left={leftDoor != null}, right={rightDoor != null}");

        var kp = panel.GetComponent<KeycardPanel>();
        if (kp == null) kp = Undo.AddComponent<KeycardPanel>(panel);
        kp.requiredColor   = KeycardColor.Blue;
        kp.keycardID       = "blue_keycard";   // team's keycard system uses this string ID
        kp.leftDoor        = leftDoor;
        kp.rightDoor       = rightDoor;
        kp.leftOpenOffset  = leftOffset;
        kp.rightOpenOffset = rightOffset;
        // Force-reset to press-once defaults
        kp.holdDuration    = 0f;
        kp.unlockedText    = "PRESS F TO UNLOCK";
        kp.lockedText      = "REQUIRES BLUE KEYCARD";
        EditorUtility.SetDirty(kp);

        if (panel.GetComponent<Collider>() == null)
        {
            var col = Undo.AddComponent<BoxCollider>(panel);
            col.size = new Vector3(1.0f, 2.0f, 1.0f);
        }

        Debug.Log($"[Level3InteractionsBuilder] {panelName} wired to {roomName} " +
                  $"(left={leftDoor?.name ?? "MISSING"}, right={rightDoor?.name ?? "MISSING"}).");
    }

    static void AttachToPlayer()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) { Debug.LogError("[Level3InteractionsBuilder] No Player tagged GameObject."); return; }

        if (player.GetComponent<KeycardInventory>() == null)
            Undo.AddComponent<KeycardInventory>(player);

        // Add team's ChainDoorKeycardHolder so the team's keycard.prefab works on our doors.
        if (player.GetComponent<ChainDoorKeycardHolder>() == null)
            Undo.AddComponent<ChainDoorKeycardHolder>(player);

        var pi = player.GetComponent<PlayerInteractor>();
        if (pi == null) pi = Undo.AddComponent<PlayerInteractor>(player);
        if (pi.rayOrigin == null)
        {
            var cam = player.GetComponentInChildren<Camera>();
            if (cam != null) pi.rayOrigin = cam.transform;
        }

        // ── FORCE-RESET — overrides any old serialized key (was E before) ──
        pi.interactKey = UnityEngine.InputSystem.Key.F;
        pi.maxDistance = 3.5f;
        EditorUtility.SetDirty(pi);

        Debug.Log("[Level3InteractionsBuilder] Player wired (interact key = F).");
    }

    static void AttachServerHacks()
    {
        // Load the audio clips we copied into _Project/Audio/SFX
        var successClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SFX/HackSuccess.mp3");
        var failClip    = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SFX/HackFail.mp3");
        var alarmClip   = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SFX/Server3Alarm.mp3");

        string[] names      = { "Server Controller", "Server Controller 2", "Server Controller 3" };
        string[] groupNames = { "Servers 1",          "Servers 2",          "Servers 3"          };
        for (int i = 0; i < names.Length; i++)
        {
            var server = GameObject.Find(names[i]);
            if (server == null) { Debug.LogWarning("[Level3InteractionsBuilder] Couldn't find " + names[i]); continue; }

            var hack = server.GetComponent<ServerHack>();
            if (hack == null) hack = Undo.AddComponent<ServerHack>(server);
            hack.serverId = i + 1;
            hack.promptText = "HOLD F TO HACK SERVER";

            // Audio (applied to all servers)
            if (successClip != null) hack.hackSuccessSound = successClip;
            if (failClip != null)    hack.hackFailSound    = failClip;

            // Wire the parent containing all the server racks for this room.
            var group = GameObject.Find(groupNames[i]);
            if (group != null)
            {
                hack.hackedServersGroup = group.transform;
                int rendCount = group.GetComponentsInChildren<Renderer>(true).Length;
                Debug.Log($"[Level3InteractionsBuilder] Linked '{groupNames[i]}' to {names[i]} ({rendCount} renderers will tint red on hack).");
            }
            else
            {
                Debug.LogWarning($"[Level3InteractionsBuilder] Couldn't find '{groupNames[i]}' — server racks won't turn red on hack.");
            }

            if (i == 0)                      // Server 1 — no prerequisites
            {
                hack.hackDuration = 10f;
                hack.prerequisiteServerIds = new int[0];
                hack.useMultiPhase = false;
            }
            else if (i == 1)                 // Server 2 — needs Server 1 hacked + password phase
            {
                hack.hackDuration = 10f;
                hack.prerequisiteServerIds = new[] { 1 };
                hack.useMultiPhase = false;
                hack.usePasswordPhase = true;
                hack.requiredPassword = "FB-Db-Rm2!78821";
                hack.passwordTitle    = "PLEASE INSERT DATABASE PASSWORD";
                hack.passwordHint     = "Hint: password on the back of the servers in room 1";
            }
            else                             // Server 3 — multi-phase (5s fail → 60s lock → 10s hack) + waves
            {
                hack.prerequisiteServerIds  = new[] { 1, 2 };
                hack.useMultiPhase           = true;
                hack.firstAttemptDuration    = 5f;
                hack.lockoutDuration         = 60f;   // matches 4 × 15s waves perfectly
                hack.finalHackDuration       = 10f;
                hack.keepAlarmUntilExit      = true;  // alarm persists through escape
                hack.spawnerSignalOnAlarm    = "server3_lockout";
                if (alarmClip != null)
                {
                    hack.alarmSound      = alarmClip;
                    hack.alarmLoopSound  = alarmClip;
                    hack.alarmLoopVolume = 0.6f;
                }
            }
            EditorUtility.SetDirty(hack);

            if (server.GetComponent<Collider>() == null)
            {
                var col = Undo.AddComponent<BoxCollider>(server);
                col.size = new Vector3(1.0f, 1.0f, 1.0f);
            }
            Debug.Log("[Level3InteractionsBuilder] Attached ServerHack id=" + (i+1) + " to " + names[i]);
        }
    }

    // Replace any old user `BlueKeycard` instances in the scene with the
    // team's `Assets/_Project/Prefabs/Level1/gate system/keycard.prefab`.
    // Preserves position/rotation/scale. Sets keycardID = "blue_keycard"
    // (which matches the IDs we wire onto KeycardPanel + ExitGatePanel).
    static void SetupBlueKeycardPrefab()
    {
        const string TEAM_PATH    = "Assets/_Project/Prefabs/Level1/gate system/keycard.prefab";
        const string KEYCARD_ID   = "blue_keycard";

        var teamPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TEAM_PATH);
        if (teamPrefab == null)
        {
            Debug.LogError($"[Level3InteractionsBuilder] Couldn't load team keycard at '{TEAM_PATH}'. " +
                           "Old BlueKeycard left in place.");
            return;
        }

        // Gather all OLD user BlueKeycard instances (anything in the scene named
        // BlueKeycard that isn't already a team-keycard with KeycardPickupTrigger).
        var oldKeycards = new System.Collections.Generic.List<Transform>();
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null) continue;
            string n = t.gameObject.name;
            if (n.IndexOf("BlueKeycard", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (PrefabUtility.IsPartOfPrefabAsset(t.gameObject)) continue;
            if (t.gameObject.GetComponent<KeycardPickupTrigger>() != null) continue;   // already team's
            oldKeycards.Add(t);
        }

        // If a team keycard already exists in the scene, just make sure its ID is right
        // and don't bother replacing anything.
        var existingTeam = Object.FindFirstObjectByType<KeycardPickupTrigger>();
        if (existingTeam != null)
        {
            existingTeam.keycardID = KEYCARD_ID;
            EditorUtility.SetDirty(existingTeam);
            Debug.Log($"[Level3InteractionsBuilder] Team keycard already in scene → set keycardID='{KEYCARD_ID}'.");
            // Also delete any leftover old BlueKeycard instances so we don't have two
            foreach (var t in oldKeycards) Undo.DestroyObjectImmediate(t.gameObject);
            return;
        }

        // No team keycard yet — spawn one at the position of the first old BlueKeycard.
        Vector3   spawnPos = oldKeycards.Count > 0 ? oldKeycards[0].position : Vector3.zero;
        Quaternion spawnRot = oldKeycards.Count > 0 ? oldKeycards[0].rotation : Quaternion.identity;
        Vector3   spawnScale = oldKeycards.Count > 0 ? oldKeycards[0].localScale : Vector3.one;

        if (oldKeycards.Count == 0)
        {
            Debug.LogWarning("[Level3InteractionsBuilder] No old BlueKeycard to replace. " +
                             "Spawning team keycard at world origin — please move it to where you want it.");
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(teamPrefab);
        Undo.RegisterCreatedObjectUndo(instance, "Spawn Team Keycard");
        instance.transform.position   = spawnPos;
        instance.transform.rotation   = spawnRot;
        instance.transform.localScale = spawnScale;

        var trigger = instance.GetComponent<KeycardPickupTrigger>();
        if (trigger != null)
        {
            trigger.keycardID = KEYCARD_ID;
            EditorUtility.SetDirty(trigger);
        }

        // Now delete the old BlueKeycard(s)
        foreach (var t in oldKeycards) Undo.DestroyObjectImmediate(t.gameObject);

        Debug.Log($"[Level3InteractionsBuilder] ✓ Replaced {oldKeycards.Count} old BlueKeycard(s) with team " +
                  $"keycard.prefab at {spawnPos} (keycardID='{KEYCARD_ID}').");
    }

    static void BuildPromptUI()
    {
        var existing = GameObject.Find("InteractionPromptCanvas");
        if (existing != null) Undo.DestroyObjectImmediate(existing);

        var canvasGO = new GameObject("InteractionPromptCanvas");
        Undo.RegisterCreatedObjectUndo(canvasGO, "Build Interaction Prompt");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        var ui = canvasGO.AddComponent<InteractionPromptUI>();
        ui.group = canvasGO.AddComponent<CanvasGroup>();

        // Panel near center-bottom
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(canvasGO.transform, false);
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.35f);
        rt.anchorMax = new Vector2(0.5f, 0.35f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(460, 90);

        var bg = panel.AddComponent<Image>();
        bg.color = C_PANEL;
        bg.raycastTarget = false;

        // Cyan left accent bar
        var accent = NewImage("Accent", panel.transform, C_CYAN);
        var aRT = accent.rectTransform;
        aRT.anchorMin = new Vector2(0, 0); aRT.anchorMax = new Vector2(0, 1);
        aRT.pivot = new Vector2(0, 0.5f);
        aRT.sizeDelta = new Vector2(4, 0);

        // Prompt text
        var textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(panel.transform, false);
        var trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0, 0.45f); trt.anchorMax = new Vector2(1, 1);
        trt.offsetMin = new Vector2(20, 0); trt.offsetMax = new Vector2(-10, -8);
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "HOLD E TO HACK";
        tmp.fontSize = 22;
        tmp.color = C_CYAN;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 4;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        ui.text = tmp;

        // Bar background (boxy, no sprite needed)
        var barBG = NewImage("BarBG", panel.transform, new Color(1, 1, 1, 0.15f));
        var bbgRT = barBG.rectTransform;
        bbgRT.anchorMin = new Vector2(0, 0); bbgRT.anchorMax = new Vector2(1, 0);
        bbgRT.pivot = new Vector2(0.5f, 0);
        bbgRT.anchoredPosition = new Vector2(0, 14);
        bbgRT.sizeDelta = new Vector2(-30, 8);

        // Fill (child of BarBG) — grows from left via anchorMax.x. No sprite,
        // so edges stay perfectly sharp/boxy.
        var bar = NewImage("Fill", barBG.transform, C_CYAN);
        var barRT = bar.rectTransform;
        barRT.anchorMin = new Vector2(0, 0);
        barRT.anchorMax = new Vector2(0, 1);   // starts at 0 width
        barRT.pivot     = new Vector2(0, 0.5f);
        barRT.offsetMin = Vector2.zero;
        barRT.offsetMax = Vector2.zero;
        ui.progressBar    = bar;
        ui.progressFillRT = barRT;

        ui.group.alpha = 0f;
        Debug.Log("[Level3InteractionsBuilder] InteractionPromptCanvas built.");
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // ── Password Entry Canvas ──────────────────────────────────────────────
    //  Modal panel that opens when Server 2's stage 1 hack completes.
    //  Built once at scene-setup time. Hidden by default (CanvasGroup α = 0).
    static void BuildPasswordEntryCanvas()
    {
        var existing = GameObject.Find("PasswordEntryCanvas");
        if (existing != null) Undo.DestroyObjectImmediate(existing);

        // Need an EventSystem for the TMP_InputField to receive focus / clicks
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        var canvasGO = new GameObject("PasswordEntryCanvas");
        Undo.RegisterCreatedObjectUndo(canvasGO, "Build Password Entry Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;                       // above the interaction prompt
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        var ui = canvasGO.AddComponent<PasswordEntryUI>();
        ui.group = canvasGO.AddComponent<CanvasGroup>();

        // Full-screen dim overlay
        var dim = NewImage("Dim", canvasGO.transform, new Color(0, 0, 0, 0.55f));
        var drt = dim.rectTransform;
        drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
        drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;

        // Central panel
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(canvasGO.transform, false);
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(720, 320);

        var bg = panel.AddComponent<Image>();
        bg.color = C_PANEL;
        bg.raycastTarget = true;

        // Cyan top accent bar
        var accent = NewImage("Accent", panel.transform, C_CYAN);
        var aRT = accent.rectTransform;
        aRT.anchorMin = new Vector2(0, 1); aRT.anchorMax = new Vector2(1, 1);
        aRT.pivot = new Vector2(0.5f, 1f);
        aRT.sizeDelta = new Vector2(0, 4);

        // Hint label (small, above title)
        var hintGO = new GameObject("Hint", typeof(RectTransform));
        hintGO.transform.SetParent(panel.transform, false);
        var hrt = hintGO.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0, 1); hrt.anchorMax = new Vector2(1, 1);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.anchoredPosition = new Vector2(0, -24);
        hrt.sizeDelta = new Vector2(-40, 30);
        var hintTMP = hintGO.AddComponent<TextMeshProUGUI>();
        hintTMP.text      = "Hint: password on the back of the servers in room 1";
        hintTMP.fontSize  = 16;
        hintTMP.color     = new Color(1f, 0.85f, 0.4f, 0.95f);   // amber hint colour
        hintTMP.fontStyle = FontStyles.Italic;
        hintTMP.alignment = TextAlignmentOptions.Center;
        hintTMP.raycastTarget = false;
        ui.hintLabel = hintTMP;

        // Title label (main message)
        var titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(panel.transform, false);
        var ttrt = titleGO.GetComponent<RectTransform>();
        ttrt.anchorMin = new Vector2(0, 1); ttrt.anchorMax = new Vector2(1, 1);
        ttrt.pivot = new Vector2(0.5f, 1f);
        ttrt.anchoredPosition = new Vector2(0, -64);
        ttrt.sizeDelta = new Vector2(-40, 40);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text      = "PLEASE INSERT DATABASE PASSWORD";
        titleTMP.fontSize  = 26;
        titleTMP.color     = C_CYAN;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.characterSpacing = 3;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;
        ui.titleLabel = titleTMP;

        // Input field (TMP_InputField)
        var inputGO = new GameObject("InputField", typeof(RectTransform));
        inputGO.transform.SetParent(panel.transform, false);
        var irt = inputGO.GetComponent<RectTransform>();
        irt.anchorMin = new Vector2(0.5f, 0.5f); irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.pivot = new Vector2(0.5f, 0.5f);
        irt.anchoredPosition = new Vector2(0, -10);
        irt.sizeDelta = new Vector2(560, 56);
        var inputBg = inputGO.AddComponent<Image>();
        inputBg.color = new Color(0, 0, 0, 0.6f);

        var input = inputGO.AddComponent<TMP_InputField>();

        // Text area (viewport)
        var viewport = new GameObject("Text Area", typeof(RectTransform));
        viewport.transform.SetParent(inputGO.transform, false);
        var vrt = viewport.GetComponent<RectTransform>();
        vrt.anchorMin = new Vector2(0, 0); vrt.anchorMax = new Vector2(1, 1);
        vrt.offsetMin = new Vector2(12, 8); vrt.offsetMax = new Vector2(-12, -8);
        viewport.AddComponent<RectMask2D>();

        // Inner text component
        var textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(viewport.transform, false);
        var tx = textGO.GetComponent<RectTransform>();
        tx.anchorMin = new Vector2(0, 0); tx.anchorMax = new Vector2(1, 1);
        tx.offsetMin = Vector2.zero; tx.offsetMax = Vector2.zero;
        var textTMP = textGO.AddComponent<TextMeshProUGUI>();
        textTMP.fontSize = 22;
        textTMP.color    = Color.white;
        textTMP.alignment = TextAlignmentOptions.MidlineLeft;
        textTMP.enableWordWrapping = false;

        // Placeholder
        var placeholderGO = new GameObject("Placeholder", typeof(RectTransform));
        placeholderGO.transform.SetParent(viewport.transform, false);
        var prt = placeholderGO.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0, 0); prt.anchorMax = new Vector2(1, 1);
        prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
        var phTMP = placeholderGO.AddComponent<TextMeshProUGUI>();
        phTMP.text      = "type password and press Enter…";
        phTMP.fontSize  = 20;
        phTMP.color     = new Color(1, 1, 1, 0.4f);
        phTMP.fontStyle = FontStyles.Italic;
        phTMP.alignment = TextAlignmentOptions.MidlineLeft;
        phTMP.enableWordWrapping = false;

        input.textViewport          = vrt;
        input.textComponent         = textTMP;
        input.placeholder           = phTMP;
        input.fontAsset             = textTMP.font;
        input.lineType              = TMP_InputField.LineType.SingleLine;
        input.contentType           = TMP_InputField.ContentType.Standard;
        input.caretWidth            = 2;
        input.customCaretColor      = true;
        input.caretColor            = C_CYAN;
        input.selectionColor        = new Color(C_CYAN.r, C_CYAN.g, C_CYAN.b, 0.4f);
        ui.inputField = input;

        // Status label (Wrong/Right feedback)
        var statusGO = new GameObject("Status", typeof(RectTransform));
        statusGO.transform.SetParent(panel.transform, false);
        var srt = statusGO.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 0);
        srt.pivot = new Vector2(0.5f, 0f);
        srt.anchoredPosition = new Vector2(0, 56);
        srt.sizeDelta = new Vector2(-40, 30);
        var statusTMP = statusGO.AddComponent<TextMeshProUGUI>();
        statusTMP.text      = "";
        statusTMP.fontSize  = 18;
        statusTMP.color     = new Color(1, 0.3f, 0.3f, 1f);
        statusTMP.fontStyle = FontStyles.Bold;
        statusTMP.alignment = TextAlignmentOptions.Center;
        statusTMP.raycastTarget = false;
        ui.statusLabel = statusTMP;

        // Footer hint (Enter / Esc keys)
        var footGO = new GameObject("FooterHint", typeof(RectTransform));
        footGO.transform.SetParent(panel.transform, false);
        var frt = footGO.GetComponent<RectTransform>();
        frt.anchorMin = new Vector2(0, 0); frt.anchorMax = new Vector2(1, 0);
        frt.pivot = new Vector2(0.5f, 0f);
        frt.anchoredPosition = new Vector2(0, 18);
        frt.sizeDelta = new Vector2(-40, 24);
        var footTMP = footGO.AddComponent<TextMeshProUGUI>();
        footTMP.text      = "[ ENTER ] submit     [ ESC ] cancel";
        footTMP.fontSize  = 14;
        footTMP.color     = new Color(1, 1, 1, 0.55f);
        footTMP.alignment = TextAlignmentOptions.Center;
        footTMP.raycastTarget = false;

        ui.group.alpha          = 0f;
        ui.group.interactable   = false;
        ui.group.blocksRaycasts = false;

        Debug.Log("[Level3InteractionsBuilder] PasswordEntryCanvas built.");
    }

    // ── World-space Password Sign ──────────────────────────────────────────
    //  Finds every GameObject named "Password" (case-insensitive) in the scene
    //  and attaches a 3D TextMeshPro child showing the database password +
    //  a small "DB PASSWORD" header so the player knows what they're looking at.
    static void SetupPasswordSigns()
    {
        const string SIGN_TEXT = "FB-Db-Rm2!78821";
        int count = 0;

        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null) continue;
            if (!string.Equals(t.gameObject.name, "Password", System.StringComparison.OrdinalIgnoreCase)) continue;

            // Remove any previous sign child so re-runs don't pile up
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var child = t.GetChild(i);
                if (child.name == "PasswordSign") Undo.DestroyObjectImmediate(child.gameObject);
            }

            // Header label (small, above the password)
            var headerGO = new GameObject("PasswordSign");
            Undo.RegisterCreatedObjectUndo(headerGO, "Create Password Sign");
            headerGO.transform.SetParent(t, false);
            headerGO.transform.localPosition = Vector3.zero;
            headerGO.transform.localRotation = Quaternion.identity;

            // Sub-child for header text
            var hdr = new GameObject("Header");
            hdr.transform.SetParent(headerGO.transform, false);
            hdr.transform.localPosition = new Vector3(0, 0.18f, 0);
            var hdrTMP = hdr.AddComponent<TextMeshPro>();
            hdrTMP.text      = "DB PASSWORD — RM 2";
            hdrTMP.fontSize  = 0.98f;
            hdrTMP.color     = new Color(1f, 0.85f, 0.4f, 1f);   // amber
            hdrTMP.fontStyle = FontStyles.Bold;
            hdrTMP.alignment = TextAlignmentOptions.Center;

            // Sub-child for the actual password
            var pw = new GameObject("Password");
            pw.transform.SetParent(headerGO.transform, false);
            pw.transform.localPosition = Vector3.zero;
            var pwTMP = pw.AddComponent<TextMeshPro>();
            pwTMP.text      = SIGN_TEXT;
            pwTMP.fontSize  = 1.35f;
            pwTMP.color     = C_CYAN;
            pwTMP.fontStyle = FontStyles.Bold;
            pwTMP.alignment = TextAlignmentOptions.Center;
            pwTMP.characterSpacing = 4;

            count++;
            Debug.Log($"[Level3InteractionsBuilder] Password sign attached to '{t.name}' at {t.position}.");
        }

        if (count == 0)
        {
            Debug.LogWarning("[Level3InteractionsBuilder] No GameObject named 'Password' found in the scene. " +
                             "Place an empty GameObject named 'Password' behind 'server_2 (9)' in Room 1 " +
                             "and re-run this menu — the sign will spawn as its child.");
        }
        else
        {
            Debug.Log($"[Level3InteractionsBuilder] ✓ {count} password sign(s) built (text = \"{SIGN_TEXT}\").");
        }
    }

    // ── Server 3 Spawners ──────────────────────────────────────────────────
    //  Finds every EnemySpawner in the scene and configures it for the
    //  Server 3 lockout: signal-triggered, 4 waves × 4 enemies (16 per
    //  spawner), 15s between waves. With 3 spawners that's 48 enemies total
    //  spread across the level. ExitGatePanel requires the player to kill 30.
    //
    //  Waves are synchronised because all spawners receive the same
    //  EnemySpawner.FireSignal("server3_lockout") broadcast from ServerHack
    //  at the same instant.
    static void SetupServer3Spawners()
    {
        const string SIGNAL = "server3_lockout";
        const string DEFAULT_ENEMY_PATH = "Assets/_Project/Prefabs/Enemies/Soldier_demo_gun.prefab";

        var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DEFAULT_ENEMY_PATH);
        if (enemyPrefab == null)
            Debug.LogWarning($"[Level3InteractionsBuilder] Default enemy prefab not found at '{DEFAULT_ENEMY_PATH}'. " +
                             "Spawners will be wired but you'll need to assign enemyPrefabs manually.");

        var all = Object.FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all.Length == 0)
        {
            Debug.LogWarning("[Level3InteractionsBuilder] No EnemySpawner components in the scene. " +
                             "Use 'Facility Breach → Create Enemy Spawner' to place at least 3, then re-run setup.");
            return;
        }

        foreach (var sp in all)
        {
            // Always set the signal/wave config — keep enemyPrefabs as-is if
            // the user already set them, otherwise default to Soldier_demo_gun.
            if ((sp.enemyPrefabs == null || sp.enemyPrefabs.Length == 0) && enemyPrefab != null)
                sp.enemyPrefabs = new[] { enemyPrefab };

            sp.triggerMode    = EnemySpawner.TriggerMode.OnSignal;
            sp.signalName     = SIGNAL;
            sp.oneShot        = true;

            // 4 waves × 4 enemies = 16 per spawner. With 3 spawners → 48 total,
            // 12 per wave (>= the user-requested "at least 10 per wave").
            sp.totalToSpawn   = 16;
            sp.perWave        = 4;
            sp.spawnInterval  = 0.3f;
            sp.waveInterval   = 15f;
            sp.maxAliveAtOnce = 0;

            sp.scatterRadius  = 2.0f;
            sp.snapToNavMesh  = true;
            sp.navMeshSnapDistance = 3.0f;

            EditorUtility.SetDirty(sp);
        }

        Debug.Log($"[Level3InteractionsBuilder] ✓ Configured {all.Length} EnemySpawner(s) for Server 3 lockout. " +
                  $"Signal='{SIGNAL}', 16 each (4 waves × 4), 15s between waves. " +
                  $"Total enemies on alarm: {all.Length * 16}. Exit gate requires 30 kills.");
    }
}
