using UnityEditor;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — AutoSave Pause Toggle (Editor)
//  Top menu → Facility Breach → AutoSave: Pause (toggle ✓)
//
//  Flips a PlayerPrefs flag that AutoSaveManager reads each save/restore tick.
//  Use this when you're testing or repositioning the player and don't want
//  AutoSave overwriting state every 5 seconds.
//
//  Also: Facility Breach → AutoSave: Clear Saved Data — wipes the save slot.
// ─────────────────────────────────────────────────────────────────────────────
public static class AutoSavePauseToggle
{
    const string KEY        = "FB_AutoSave_Paused";
    const string MENU_PAUSE = "Facility Breach/AutoSave: Pause";
    const string MENU_CLEAR = "Facility Breach/AutoSave: Clear Saved Data";

    [MenuItem(MENU_PAUSE)]
    static void Toggle()
    {
        bool now = PlayerPrefs.GetInt(KEY, 0) == 1;
        PlayerPrefs.SetInt(KEY, now ? 0 : 1);
        PlayerPrefs.Save();
        Debug.Log("[AutoSave] " + (now ? "RESUMED — saves will run on schedule." : "PAUSED — no saves until you toggle this off."));
    }

    [MenuItem(MENU_PAUSE, validate = true)]
    static bool ToggleValidate()
    {
        // Show a checkmark next to the menu item when paused
        Menu.SetChecked(MENU_PAUSE, PlayerPrefs.GetInt(KEY, 0) == 1);
        return true;
    }

    [MenuItem(MENU_CLEAR)]
    static void ClearSave()
    {
        if (!EditorUtility.DisplayDialog("Clear AutoSave?",
            "This wipes the save slot (position, HP, ammo, dead enemies). " +
            "AutoSave stays paused/resumed as-is. Proceed?",
            "Yes, clear it", "Cancel")) return;

        // Mirror SaveSystem.DeleteSave() — wipe every FB_ save key we know about
        string[] keys = {
            "FB_Save_LastScene", "FB_Save_PosX", "FB_Save_PosY", "FB_Save_PosZ",
            "FB_Save_RotY", "FB_Save_Health", "FB_Save_Ammo",
            "FB_Save_Objectives", "FB_Save_Frags", "FB_Save_Exists"
        };
        foreach (var k in keys) PlayerPrefs.DeleteKey(k);

        // Also wipe per-scene dead-enemy lists
        foreach (var sceneKey in new[] { "Level1", "Level3", "Level-3", "Ammar-test", "Test-Scene", "Ali", "sayedhussainscene" })
            PlayerPrefs.DeleteKey("FB_DeadEnemies_" + sceneKey);

        PlayerPrefs.Save();
        Debug.Log("[AutoSave] Save data cleared.");
    }
}
