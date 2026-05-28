using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Main Menu Controller
// ─────────────────────────────────────────────────────────────────────────────
public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainPanel;
    public GameObject settingsPanel;
    public GameObject creditsPanel;
    public GameObject controlsPanel;

    [Header("Buttons")]
    public Button     continueButton;          // greys out if no save
    [Tooltip("Toggles co-op on/off. Label updates to 'CO-OP: ON' / 'CO-OP: OFF'.")]
    public Button     coopToggleButton;
    public TMPro.TextMeshProUGUI coopToggleLabel;

    [Header("Scene Names")]
    public string level1SceneName = "Level1";

    public const string PREF_COOP_ENABLED = "FB_CoopEnabled";

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        Time.timeScale   = 1f;
        ShowMain();
        UpdateContinueButtonState();
        RefreshCoopLabel();
    }

    public void OnToggleCoop()
    {
        bool now = PlayerPrefs.GetInt(PREF_COOP_ENABLED, 0) == 1;
        PlayerPrefs.SetInt(PREF_COOP_ENABLED, now ? 0 : 1);
        PlayerPrefs.Save();
        RefreshCoopLabel();
        Debug.Log("[MainMenu] Co-op toggled → " + (!now ? "ON" : "OFF"));
    }

    void RefreshCoopLabel()
    {
        if (coopToggleLabel == null) return;
        bool on = PlayerPrefs.GetInt(PREF_COOP_ENABLED, 0) == 1;
        coopToggleLabel.text = on ? "CO-OP: ON" : "CO-OP: OFF";
        coopToggleLabel.color = on
            ? new UnityEngine.Color32(0x4D, 0xFF, 0x4D, 0xFF)
            : new UnityEngine.Color32(0xFF, 0x4D, 0x4D, 0xFF);
    }

    public void UpdateContinueButtonState()
    {
        if (continueButton == null) return;
        bool has = SaveSystem.HasSave();
        continueButton.interactable = has;
    }

    public void OnPlay()         // NEW GAME — start fresh from the beginning
    {
        if (string.IsNullOrEmpty(level1SceneName)) { Debug.LogError("[MainMenu] level1SceneName empty."); return; }
        SaveSystem.DeleteSave();
        SaveSystem.Save(level1SceneName, 100f, 30);     // placeholder so HasSave() returns true next session
        SaveSystem.ContinueRequested = false;            // start at default spawn, not restore
        LoadingScreen.Load(level1SceneName);
    }

    public void OnContinue()     // CONTINUE — load saved scene + restore player state
    {
        if (!SaveSystem.HasSave()) { Debug.LogWarning("[MainMenu] No save to continue."); return; }
        string scene = SaveSystem.GetSavedScene();
        if (string.IsNullOrEmpty(scene)) return;
        SaveSystem.ContinueRequested = true;             // AutoSaveManager will restore position/HP on load
        LoadingScreen.Load(scene);
    }

    public void OnSettings() { Toggle(false, true,  false, false); }
    public void OnCredits()  { Toggle(false, false, true,  false); }
    public void OnControls() { Toggle(false, false, false, true);  }

    public void OnBackToMain()     { ShowMain(); }
    public void OnBackToSettings() { Toggle(false, true, false, false); }

    public void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void ShowMain()
    {
        Toggle(true, false, false, false);
        UpdateContinueButtonState();
    }

    void Toggle(bool main, bool settings, bool credits, bool controls)
    {
        if (mainPanel)     mainPanel.SetActive(main);
        if (settingsPanel) settingsPanel.SetActive(settings);
        if (creditsPanel)  creditsPanel.SetActive(credits);
        if (controlsPanel) controlsPanel.SetActive(controls);
    }
}
