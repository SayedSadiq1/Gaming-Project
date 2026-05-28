using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Level 3 Restore
//  Menu: Facility Breach → Restore Level 3 Scene Setup
//
//  One-click restore of every scene-level tweak we built since the last
//  Level-3 backup. Specifically:
//    1. Configure Weapon Spawner (named "Weapon Spawner") with WeaponPickup +
//       AK74 prefab.
//    2. Set Server 3's ServerHack.hackedMusic = Main Theme MP3 + 20% volume.
//    3. Make sure Server 3 has Keep Alarm Until Exit ✓.
//    4. Set ExitGatePanel.nextSceneName so the cutscene routes to the next
//       level after extract.
//    5. Spawn the team's blue keycard if missing.
//    6. Marks the scene dirty so the user just hits Ctrl+S to save.
//
//  Safe to re-run — every step is idempotent.
// ─────────────────────────────────────────────────────────────────────────────
public static class Level3Restore
{
    const string AK_PATH         = "Assets/_Project/Weapons/AK74.prefab";
    const string MAIN_THEME_PATH = "Assets/_Project/Audio/Black Ops 2 Soundtrack Main Theme - 128.MP3";
    const string KEYCARD_PATH    = "Assets/_Project/Prefabs/Level1/gate system/keycard.prefab";
    const string KEYCARD_ID      = "blue_keycard";
    const string NEXT_SCENE      = "sayedhussainscene";

    [MenuItem("Facility Breach/Restore Level 3 Scene Setup")]
    public static void Restore()
    {
        var log = new System.Text.StringBuilder();
        log.AppendLine("=== Level 3 Restore Report ===");

        // ── 1. Weapon Spawner ─────────────────────────────────────────────────
        var weaponSpawner = GameObject.Find("Weapon Spawner");
        if (weaponSpawner == null)
        {
            log.AppendLine("✗ 'Weapon Spawner' GameObject not found in scene — create one and re-run.");
        }
        else
        {
            var pickup = weaponSpawner.GetComponent<WeaponPickup>();
            if (pickup == null) pickup = weaponSpawner.AddComponent<WeaponPickup>();

            var ak = AssetDatabase.LoadAssetAtPath<GameObject>(AK_PATH);
            if (ak == null)
            {
                log.AppendLine("✗ AK74 prefab not found at " + AK_PATH);
            }
            else
            {
                var wc = ak.GetComponent<Unity.FPS.Game.WeaponController>();
                if (wc == null)
                {
                    log.AppendLine("✗ AK74 prefab has no WeaponController.");
                }
                else
                {
                    pickup.WeaponPrefab = wc;
                    EditorUtility.SetDirty(weaponSpawner);
                    log.AppendLine("✓ Weapon Spawner configured with AK74.");
                }
            }
        }

        // ── 2. Server 3 music + keep alarm ────────────────────────────────────
        ServerHack server3 = null;
        foreach (var sh in Object.FindObjectsByType<ServerHack>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (sh.serverId == 3) { server3 = sh; break; }
        }
        if (server3 == null)
        {
            log.AppendLine("✗ No ServerHack with serverId=3 found in scene.");
        }
        else
        {
            var music = AssetDatabase.LoadAssetAtPath<AudioClip>(MAIN_THEME_PATH);
            if (music == null)
            {
                log.AppendLine("✗ Music clip not found at " + MAIN_THEME_PATH);
            }
            else
            {
                server3.hackedMusic         = music;
                server3.hackedMusicVolume   = 0.2f;
                server3.hackedMusicFadeIn   = 2f;
                server3.hackedMusicPersists = true;
                server3.keepAlarmUntilExit  = true;
                EditorUtility.SetDirty(server3);
                log.AppendLine("✓ Server 3 music wired (vol=0.20, fade=2s, persists, alarm kept).");
            }
        }

        // ── 3. Exit Gate Panel — next scene ───────────────────────────────────
        var gates = Object.FindObjectsByType<ExitGatePanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (gates.Length == 0)
        {
            log.AppendLine("✗ No ExitGatePanel in scene — run 'Setup Level 3 Interactions' first.");
        }
        else
        {
            foreach (var gate in gates)
            {
                gate.nextSceneName = NEXT_SCENE;
                EditorUtility.SetDirty(gate);
            }
            log.AppendLine($"✓ ExitGatePanel.nextSceneName = '{NEXT_SCENE}' on {gates.Length} gate(s).");
        }

        // ── 4. LabExplosionCutscene — next scene ──────────────────────────────
        var cutscene = Object.FindFirstObjectByType<LabExplosionCutscene>(FindObjectsInactive.Include);
        if (cutscene == null)
        {
            log.AppendLine("ℹ No LabExplosionCutscene in scene — the gate will still load nextSceneName " +
                           "directly without a cutscene. Build one manually if you want the cutscene.");
        }
        else
        {
            cutscene.nextSceneName = NEXT_SCENE;
            EditorUtility.SetDirty(cutscene);
            log.AppendLine($"✓ LabExplosionCutscene.nextSceneName = '{NEXT_SCENE}'.");
        }

        // ── 4.5 ChainDoorKeycardHolder on Player (required for team keycard pickup) ─
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            log.AppendLine("✗ No GameObject tagged 'Player' — team keycard pickup will not work.");
        }
        else
        {
            var holder = player.GetComponent<ChainDoorKeycardHolder>();
            if (holder == null)
            {
                player.AddComponent<ChainDoorKeycardHolder>();
                EditorUtility.SetDirty(player);
                log.AppendLine("✓ Added ChainDoorKeycardHolder to Player (needed for team keycard pickup).");
            }
            else
            {
                log.AppendLine("✓ Player already has ChainDoorKeycardHolder.");
            }
        }

        // ── 5. Blue Keycard — REPLACE any old user keycard with the team prefab ─
        var teamPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KEYCARD_PATH);
        if (teamPrefab == null)
        {
            log.AppendLine("✗ Team keycard prefab not found at " + KEYCARD_PATH);
        }
        else
        {
            // 1) Find any OLD user keycard (named like "BlueKeycard" but no team script)
            //    so we can salvage its position and then delete it.
            Vector3    spawnPos   = Vector3.zero;
            Quaternion spawnRot   = Quaternion.identity;
            Vector3    spawnScale = Vector3.one;
            bool       foundOld   = false;
            int        oldRemoved = 0;

            var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in allTransforms)
            {
                if (t == null) continue;
                string n = t.gameObject.name;
                if (n.IndexOf("BlueKeycard", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (PrefabUtility.IsPartOfPrefabAsset(t.gameObject)) continue;
                // Skip if it already has the team's KeycardPickupTrigger
                if (t.gameObject.GetComponent<KeycardPickupTrigger>() != null) continue;

                if (!foundOld)
                {
                    spawnPos   = t.position;
                    spawnRot   = t.rotation;
                    spawnScale = t.localScale;
                    foundOld   = true;
                }
                Undo.DestroyObjectImmediate(t.gameObject);
                oldRemoved++;
            }

            // 2) Check if a team keycard ALREADY exists in the scene
            var existingTeam = Object.FindFirstObjectByType<KeycardPickupTrigger>(FindObjectsInactive.Include);
            if (existingTeam != null)
            {
                existingTeam.keycardID = KEYCARD_ID;
                existingTeam.gameObject.SetActive(true);
                // If we found an old one, move the team keycard to that position
                if (foundOld)
                {
                    existingTeam.transform.position   = spawnPos;
                    existingTeam.transform.rotation   = spawnRot;
                    existingTeam.transform.localScale = spawnScale;
                }
                EditorUtility.SetDirty(existingTeam);
                log.AppendLine($"✓ Team keycard already in scene — set ID, repositioned, removed {oldRemoved} old BlueKeycard(s).");
            }
            else
            {
                // 3) No team keycard yet — spawn one. Position: at old keycard if we found one,
                //    otherwise 2m in front of Player.
                if (!foundOld)
                {
                    var p = GameObject.FindGameObjectWithTag("Player");
                    spawnPos = p != null
                        ? p.transform.position + p.transform.forward * 2f + Vector3.up
                        : Vector3.zero;
                }

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(teamPrefab);
                Undo.RegisterCreatedObjectUndo(inst, "Spawn Team Keycard");
                inst.transform.position   = spawnPos;
                inst.transform.rotation   = spawnRot;
                if (foundOld) inst.transform.localScale = spawnScale;
                var trig = inst.GetComponent<KeycardPickupTrigger>();
                if (trig != null) trig.keycardID = KEYCARD_ID;
                EditorUtility.SetDirty(inst);
                log.AppendLine($"✓ Spawned team keycard at {spawnPos}, removed {oldRemoved} old BlueKeycard(s).");
            }
        }

        // ── 6. Mark scene dirty ───────────────────────────────────────────────
        var activeScene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(activeScene);

        Debug.Log(log.ToString());
        EditorUtility.DisplayDialog("Level 3 Restore",
            log.ToString() + "\nSave the scene (Ctrl+S) to keep these changes.", "OK");
    }
}
