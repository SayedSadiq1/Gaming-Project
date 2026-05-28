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
    public string fallbackScene = "Level1";

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        Time.timeScale   = 1f;

        // Resolve the scene Play Again will load, then update the label.
        string scene = ResolvePlayAgainScene();
        if (lastLevelLabel != null)
            lastLevelLabel.text = $"Last level: <color=#00C8FF>{scene}</color>";

        // Always interactable — OnPlayAgain handles the can't-load case itself.
        if (playAgainButton != null) playAgainButton.interactable = true;
    }

    public void OnPlayAgain()
    {
        string scene = ResolvePlayAgainScene();
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError($"[DeathScreen] Neither saved scene nor fallback '{scene}' is in Build Settings. " +
                           "Add Level1.unity (or your start scene) to File → Build Settings.");
            return;
        }

        // Wipe THIS level only — leaves other levels' progress alone
        SaveSystem.ResetForLevel(scene);
        LoadingScreen.Load(scene);
    }

    // Scene names that are NEVER a valid Play Again target — picking these
    // would either loop back to the death screen or kick to the main menu.
    static readonly System.Collections.Generic.HashSet<string> _nonGameplayScenes =
        new System.Collections.Generic.HashSet<string> { "MainMenu", "DeathScreen", "LoseScene" };

    /// <summary>
    /// Returns the scene Play Again should load: saved scene if present, NOT
    /// a non-gameplay scene, AND buildable. Otherwise the fallback.
    /// </summary>
    string ResolvePlayAgainScene()
    {
        string scene = SaveSystem.GetSavedScene();
        if (!string.IsNullOrEmpty(scene)
            && !_nonGameplayScenes.Contains(scene)
            && Application.CanStreamedLevelBeLoaded(scene))
            return scene;
        return fallbackScene;
    }

    public void OnMainMenu()
    {
        SaveSystem.ContinueRequested = false;
        LoadingScreen.Load("MainMenu");
    }
}
