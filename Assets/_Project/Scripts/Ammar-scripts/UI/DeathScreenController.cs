using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Death Screen Controller
//
//  Wired up in DeathScreen.unity by the editor builder. Receives clicks from
//  the PLAY AGAIN / MAIN MENU buttons:
//
//    • PLAY AGAIN — reads SaveSystem.GetSavedScene() (the last gameplay
//                   scene the player was in), wipes player state + dead
//                   enemy list for ONLY that scene, then loads it. Fresh
//                   start in the same level — global save untouched.
//    • MAIN MENU  — loads "MainMenu".
//
//  Also fills in a "Last level: X" label so the player knows which level
//  pressing PLAY AGAIN will restart.
// ─────────────────────────────────────────────────────────────────────────────
public class DeathScreenController : MonoBehaviour
{
    [Header("Wired by DeathScreenBuilder")]
    public Button          playAgainButton;
    public Button          mainMenuButton;
    public TextMeshProUGUI lastLevelLabel;

    [Header("Fallback")]
    [Tooltip("Scene to load if SaveSystem has no record of the last level.")]
    public string fallbackScene = "Test-Scene";

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        Time.timeScale   = 1f;

        // Refresh the "Last level" label so the player sees what Play Again will load
        string scene = SaveSystem.GetSavedScene();
        if (string.IsNullOrEmpty(scene)) scene = fallbackScene;
        if (lastLevelLabel != null)
            lastLevelLabel.text = $"Last level: <color=#00C8FF>{scene}</color>";

        // Grey out Play Again if the target scene isn't actually buildable
        if (playAgainButton != null)
            playAgainButton.interactable = Application.CanStreamedLevelBeLoaded(scene);
    }

    public void OnPlayAgain()
    {
        string scene = SaveSystem.GetSavedScene();
        if (string.IsNullOrEmpty(scene)) scene = fallbackScene;

        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogWarning($"[DeathScreen] Scene '{scene}' not in Build Settings.");
            return;
        }

        // Wipe THIS level only — leaves other levels' progress alone
        SaveSystem.ResetForLevel(scene);
        SceneManager.LoadScene(scene);
    }

    public void OnMainMenu()
    {
        SaveSystem.ContinueRequested = false;
        SceneManager.LoadScene("MainMenu");
    }
}
