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
        BuildPromptUI();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[Level3InteractionsBuilder] ✓ Setup complete. Play, look at a target, hold F.");
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
        gate.requireAllEnemiesDead = true;
        gate.requiresKeycard       = true;
        gate.requiredColor         = KeycardColor.Blue;

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
            else if (i == 1)                 // Server 2 — needs Server 1 hacked
            {
                hack.hackDuration = 10f;
                hack.prerequisiteServerIds = new[] { 1 };
                hack.useMultiPhase = false;
            }
            else                             // Server 3 — multi-phase (5s fail → 60s lock → 10s hack)
            {
                hack.prerequisiteServerIds  = new[] { 1, 2 };
                hack.useMultiPhase           = true;
                hack.firstAttemptDuration    = 5f;
                hack.lockoutDuration         = 60f;
                hack.finalHackDuration       = 10f;
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

    static void SetupBlueKeycardPrefab()
    {
        // 1) Update the asset prefab so any future instances are correct
        string path = "Assets/_Project/Prefabs/BlueKeycard.prefab";
        var bk = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (bk != null)
        {
            var contents = PrefabUtility.LoadPrefabContents(path);

            var pickup = contents.GetComponent<KeycardPickup>();
            if (pickup == null) pickup = contents.AddComponent<KeycardPickup>();
            // ── Force-reset to press-once defaults ──
            pickup.color         = KeycardColor.Blue;
            pickup.holdDuration  = 0f;

            var col = contents.GetComponent<Collider>();
            if (col == null)
            {
                var box = contents.AddComponent<BoxCollider>();
                box.size = new Vector3(0.6f, 0.4f, 0.6f);
                col = box;
            }

            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);
            Debug.Log("[Level3InteractionsBuilder] BlueKeycard.prefab → KeycardPickup (press-once) + collider.");
        }
        else
        {
            Debug.LogWarning("[Level3InteractionsBuilder] No BlueKeycard.prefab at " + path);
        }

        // 2) Patch any in-scene BlueKeycard instances — FORCE-RESET fields so
        //    the old "HOLD F" / 0.15s hold gets wiped.
        var sceneObjs = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int patched = 0;
        foreach (var t in sceneObjs)
        {
            if (t == null) continue;
            string n = t.gameObject.name;
            if (n.IndexOf("BlueKeycard", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (PrefabUtility.IsPartOfPrefabAsset(t.gameObject)) continue;

            var go = t.gameObject;

            var p = go.GetComponent<KeycardPickup>();
            if (p == null) p = Undo.AddComponent<KeycardPickup>(go);
            // FORCE-RESET — overrides any old serialized values
            p.color        = KeycardColor.Blue;
            p.holdDuration = 0f;
            EditorUtility.SetDirty(p);

            if (go.GetComponent<Collider>() == null)
            {
                var box = Undo.AddComponent<BoxCollider>(go);
                box.size = new Vector3(0.6f, 0.4f, 0.6f);
            }
            patched++;
            Debug.Log($"[Level3InteractionsBuilder] In-scene BlueKeycard '{n}' → reset to press-once.");
        }
        if (patched == 0)
            Debug.LogWarning("[Level3InteractionsBuilder] No in-scene BlueKeycard found. Drag the prefab into the scene first.");
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
}
