using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Save System (lightweight)
//  Stores last scene + player state via PlayerPrefs.
//  Call Save(...) from a checkpoint, HasSave()/GetSavedScene() from the menu.
// ─────────────────────────────────────────────────────────────────────────────
public static class SaveSystem
{
    const string K_SCENE  = "FB_Save_LastScene";
    const string K_HEALTH = "FB_Save_Health";
    const string K_AMMO   = "FB_Save_Ammo";
    const string K_TIME   = "FB_Save_Timestamp";

    public static bool HasSave()
    {
        return PlayerPrefs.HasKey(K_SCENE) && !string.IsNullOrEmpty(PlayerPrefs.GetString(K_SCENE, ""));
    }

    public static string GetSavedScene()
    {
        return PlayerPrefs.GetString(K_SCENE, "");
    }

    public static float GetSavedHealth() { return PlayerPrefs.GetFloat(K_HEALTH, 100f); }
    public static int   GetSavedAmmo()   { return PlayerPrefs.GetInt(K_AMMO, 30); }
    public static string GetSaveTimestamp() { return PlayerPrefs.GetString(K_TIME, ""); }

    public static void Save(string sceneName, float health = 100f, int ammo = 30)
    {
        PlayerPrefs.SetString(K_SCENE, sceneName);
        PlayerPrefs.SetFloat (K_HEALTH, health);
        PlayerPrefs.SetInt   (K_AMMO,   ammo);
        PlayerPrefs.SetString(K_TIME,   System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        PlayerPrefs.Save();
        Debug.Log($"[SaveSystem] Saved at scene='{sceneName}' health={health} ammo={ammo}");
    }

    public static void DeleteSave()
    {
        PlayerPrefs.DeleteKey(K_SCENE);
        PlayerPrefs.DeleteKey(K_HEALTH);
        PlayerPrefs.DeleteKey(K_AMMO);
        PlayerPrefs.DeleteKey(K_TIME);
        PlayerPrefs.Save();
    }
}
