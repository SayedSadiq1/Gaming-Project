using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Save System
//  Stores scene + player transform + health via PlayerPrefs.
//  AutoSaveManager calls Save() periodically and on quit.
//  Set ContinueRequested = true from MainMenuController.OnContinue() so the
//  gameplay scene knows to restore the saved state on load.
// ─────────────────────────────────────────────────────────────────────────────
public static class SaveSystem
{
    const string K_SCENE   = "FB_Save_LastScene";
    const string K_POS_X   = "FB_Save_PosX";
    const string K_POS_Y   = "FB_Save_PosY";
    const string K_POS_Z   = "FB_Save_PosZ";
    const string K_ROT_Y   = "FB_Save_RotY";
    const string K_HEALTH  = "FB_Save_Health";
    const string K_AMMO    = "FB_Save_Ammo";
    const string K_OBJ_DONE= "FB_Save_ObjDone";
    const string K_FRAGS   = "FB_Save_Frags";
    const string K_TIME    = "FB_Save_Timestamp";

    /// <summary>True if user clicked CONTINUE in the menu. Read once in the level scene, then reset.</summary>
    public static bool ContinueRequested = false;

    public static bool HasSave()
        => PlayerPrefs.HasKey(K_SCENE) && !string.IsNullOrEmpty(PlayerPrefs.GetString(K_SCENE, ""));

    public static string GetSavedScene()  => PlayerPrefs.GetString(K_SCENE, "");
    public static Vector3 GetSavedPos()   => new Vector3(
        PlayerPrefs.GetFloat(K_POS_X, 0f),
        PlayerPrefs.GetFloat(K_POS_Y, 1f),
        PlayerPrefs.GetFloat(K_POS_Z, 0f));
    public static float GetSavedRotY()    => PlayerPrefs.GetFloat(K_ROT_Y, 0f);
    public static float GetSavedHealth()  => PlayerPrefs.GetFloat(K_HEALTH, 100f);
    public static int   GetSavedAmmo()    => PlayerPrefs.GetInt(K_AMMO, 30);
    public static int   GetSavedObjDone() => PlayerPrefs.GetInt(K_OBJ_DONE, 0);
    public static int   GetSavedFrags()   => PlayerPrefs.GetInt(K_FRAGS, 3);
    public static string GetSaveTimestamp() => PlayerPrefs.GetString(K_TIME, "");

    /// <summary>Full save — called by AutoSaveManager during gameplay.</summary>
    public static void Save(string sceneName, Vector3 pos, float rotY,
                            float health, int ammo, int objDone, int frags)
    {
        PlayerPrefs.SetString(K_SCENE, sceneName);
        PlayerPrefs.SetFloat (K_POS_X, pos.x);
        PlayerPrefs.SetFloat (K_POS_Y, pos.y);
        PlayerPrefs.SetFloat (K_POS_Z, pos.z);
        PlayerPrefs.SetFloat (K_ROT_Y, rotY);
        PlayerPrefs.SetFloat (K_HEALTH, health);
        PlayerPrefs.SetInt   (K_AMMO,   ammo);
        PlayerPrefs.SetInt   (K_OBJ_DONE, objDone);
        PlayerPrefs.SetInt   (K_FRAGS,  frags);
        PlayerPrefs.SetString(K_TIME, System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        PlayerPrefs.Save();
    }

    /// <summary>Light save — just scene name and defaults. Called by New Game.</summary>
    public static void Save(string sceneName, float health = 100f, int ammo = 30)
    {
        PlayerPrefs.SetString(K_SCENE, sceneName);
        PlayerPrefs.SetFloat (K_HEALTH, health);
        PlayerPrefs.SetInt   (K_AMMO,   ammo);
        PlayerPrefs.SetString(K_TIME, System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        // Don't clear position keys — they'll be filled by first AutoSave tick
        PlayerPrefs.Save();
        Debug.Log($"[SaveSystem] New-game placeholder saved (scene='{sceneName}').");
    }

    public static void DeleteSave()
    {
        PlayerPrefs.DeleteKey(K_SCENE);
        PlayerPrefs.DeleteKey(K_POS_X);
        PlayerPrefs.DeleteKey(K_POS_Y);
        PlayerPrefs.DeleteKey(K_POS_Z);
        PlayerPrefs.DeleteKey(K_ROT_Y);
        PlayerPrefs.DeleteKey(K_HEALTH);
        PlayerPrefs.DeleteKey(K_AMMO);
        PlayerPrefs.DeleteKey(K_OBJ_DONE);
        PlayerPrefs.DeleteKey(K_FRAGS);
        PlayerPrefs.DeleteKey(K_TIME);
        PlayerPrefs.Save();
    }
}
