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

    [Header("Scene Names")]
    public string level1SceneName = "Test-Scene";

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        Time.timeScale   = 1f;
        ShowMain();
        UpdateContinueButtonState();
    }

    public void UpdateContinueButtonState()
    {
        if (continueButton == null) return;
        bool has = SaveSystem.HasSave();
        continueButton.interactable = has;
    }

    public void OnPlay()         // NEW GAME (clears save)
    {
        SaveSystem.DeleteSave();
        if (string.IsNullOrEmpty(level1SceneName)) { Debug.LogError("[MainMenu] level1SceneName empty."); return; }
        SceneManager.LoadScene(level1SceneName);
    }

    public void OnContinue()     // CONTINUE — load saved scene
    {
        if (!SaveSystem.HasSave()) { Debug.LogWarning("[MainMenu] No save to continue."); return; }
        string scene = SaveSystem.GetSavedScene();
        if (string.IsNullOrEmpty(scene)) return;
        SceneManager.LoadScene(scene);
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
