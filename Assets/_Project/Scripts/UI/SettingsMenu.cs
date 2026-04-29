using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Settings Menu
//  Master / Music / SFX volume + Mouse Sensitivity.
//  Persists via PlayerPrefs and applies on every load.
// ─────────────────────────────────────────────────────────────────────────────
public class SettingsMenu : MonoBehaviour
{
    [Header("Sliders")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;
    public Slider sensitivitySlider;

    [Header("Value Labels (optional)")]
    public TextMeshProUGUI masterValue;
    public TextMeshProUGUI musicValue;
    public TextMeshProUGUI sfxValue;
    public TextMeshProUGUI sensValue;

    [Header("Refs")]
    public AudioSource musicSource;

    public const string K_MASTER = "FB_MasterVol";
    public const string K_MUSIC  = "FB_MusicVol";
    public const string K_SFX    = "FB_SfxVol";
    public const string K_SENS   = "FB_MouseSens";

    void OnEnable()
    {
        Bind(masterSlider, K_MASTER, 1f,  OnMasterChanged);
        Bind(musicSlider,  K_MUSIC,  0.7f, OnMusicChanged);
        Bind(sfxSlider,    K_SFX,    1f,  OnSfxChanged);
        Bind(sensitivitySlider, K_SENS, 1f, OnSensChanged);
    }

    void Bind(Slider s, string key, float def, System.Action<float> cb)
    {
        if (s == null) return;
        s.value = PlayerPrefs.GetFloat(key, def);
        s.onValueChanged.RemoveAllListeners();
        s.onValueChanged.AddListener(v => cb(v));
        cb(s.value);
    }

    void OnMasterChanged(float v)
    {
        AudioListener.volume = v;
        PlayerPrefs.SetFloat(K_MASTER, v);
        if (masterValue) masterValue.text = $"{Mathf.RoundToInt(v * 100)}%";
    }

    void OnMusicChanged(float v)
    {
        if (musicSource) musicSource.volume = v;
        PlayerPrefs.SetFloat(K_MUSIC, v);
        if (musicValue) musicValue.text = $"{Mathf.RoundToInt(v * 100)}%";
    }

    void OnSfxChanged(float v)
    {
        PlayerPrefs.SetFloat(K_SFX, v);
        if (sfxValue) sfxValue.text = $"{Mathf.RoundToInt(v * 100)}%";
    }

    void OnSensChanged(float v)
    {
        PlayerPrefs.SetFloat(K_SENS, v);
        if (sensValue) sensValue.text = v.ToString("0.00");
    }

    // ── Static helper any other script can call to apply saved volume on game start ──
    public static void ApplySavedVolumes()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(K_MASTER, 1f);
    }

    // ── Reset all settings (volume, sensitivity, key bindings) to defaults ──
    public void ResetAll()
    {
        // Volumes & sensitivity
        PlayerPrefs.DeleteKey(K_MASTER);
        PlayerPrefs.DeleteKey(K_MUSIC);
        PlayerPrefs.DeleteKey(K_SFX);
        PlayerPrefs.DeleteKey(K_SENS);

        // Control bindings
        KeyBindings.ResetAll();

        // Re-bind sliders to apply default visuals & audio
        Bind(masterSlider,      K_MASTER, 1f,   OnMasterChanged);
        Bind(musicSlider,       K_MUSIC,  0.7f, OnMusicChanged);
        Bind(sfxSlider,         K_SFX,    1f,   OnSfxChanged);
        Bind(sensitivitySlider, K_SENS,   1f,   OnSensChanged);

        // Refresh controls UI if it's in the same scene
        var ctrls = Object.FindFirstObjectByType<ControlsBindingMenu>(FindObjectsInactive.Include);
        if (ctrls != null) ctrls.RefreshAll();

        Debug.Log("[SettingsMenu] All settings reset to defaults.");
    }
}
