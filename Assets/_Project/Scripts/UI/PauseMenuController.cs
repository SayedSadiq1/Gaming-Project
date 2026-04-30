using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Pause Menu Controller
//  ─────────────────────────────────────────────────────────────────────────────
//  Handles:
//    • ESC to toggle pause on/off (singleton — no duplicates)
//    • Time.timeScale freeze and cursor management
//    • Panel switching (main pause → settings → back)
//    • Resume / Restart / Main Menu with full state cleanup
//    • Player input disable/enable while paused
//
//  Attach to the root PauseMenuCanvas (built by PauseMenuBuilder).
//  Works in any gameplay scene — safe to put on a prefab.
// ─────────────────────────────────────────────────────────────────────────────
public class PauseMenuController : MonoBehaviour
{
    // ── Inspector refs (wired by PauseMenuBuilder) ──────────────────────────
    [Header("Panels")]
    public GameObject pauseRoot;          // the full-screen overlay
    public GameObject mainPausePanel;     // Resume / Settings / Restart / Main Menu
    public GameObject settingsPanel;      // volume sliders sub-panel

    [Header("Buttons (auto-wired at runtime as fallback)")]
    public Button resumeButton;
    public Button settingsButton;
    public Button restartButton;
    public Button mainMenuButton;
    public Button backToPauseButton;

    [Header("Scene Names")]
    public string mainMenuSceneName = "MainMenu";

    // ── Singleton guard ─────────────────────────────────────────────────────
    static PauseMenuController s_instance;

    // ── State ───────────────────────────────────────────────────────────────
    bool _isPaused;
    bool _isTransitioning;   // true once Restart/MainMenu is clicked

    // Cached player refs so we can disable/enable input
    GameObject _playerRoot;
    MonoBehaviour[] _playerScripts;

    // ─────────────────────────────────────────────────────────────────────────
    //  LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        // Prevent duplicate pause menus (e.g. if prefab spawned twice)
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }
        s_instance = this;

        // Start hidden and unpaused
        if (pauseRoot != null) pauseRoot.SetActive(false);
        _isPaused = false;
        _isTransitioning = false;

        // Disable the old FPS Microgame pause menu (InGameMenuManager) so it
        // doesn't conflict with ours. We can't edit Assets/FPS/, so we disable
        // it at runtime instead.
        DisableOldPauseMenu();

        // Wire buttons at runtime — guarantees they work even if persistent
        // listeners didn't save properly
        WireButtons();
    }

    void OnDestroy()
    {
        // Clean up singleton reference when scene unloads
        if (s_instance == this) s_instance = null;
    }

    void Update()
    {
        // Block ESC once a transition (Restart / Main Menu) has been triggered.
        // This prevents the player from "resuming" into a stale game state.
        if (_isTransitioning) return;

        // Use new Input System (Unity 6 — old Input.GetKeyDown doesn't work)
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            if (_isPaused)
                Resume();
            else
                Pause();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PAUSE / RESUME
    // ─────────────────────────────────────────────────────────────────────────

    void Pause()
    {
        _isPaused = true;
        Time.timeScale = 0f;

        // Show cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        // Show the pause overlay, default to main panel
        ShowMainPausePanel();
        if (pauseRoot != null) pauseRoot.SetActive(true);

        // Disable player scripts so mouse-look / shooting stops
        DisablePlayerInput();
    }

    /// <summary>
    /// Resumes gameplay — called by the Resume button AND by pressing ESC
    /// while paused. Fully restores game state.
    /// </summary>
    public void Resume()
    {
        if (_isTransitioning) return;   // safety: can't resume after scene change

        _isPaused = false;
        Time.timeScale = 1f;

        // Hide UI
        if (pauseRoot != null) pauseRoot.SetActive(false);

        // Re-lock cursor for FPS gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

        // Re-enable player input
        EnablePlayerInput();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  BUTTON CALLBACKS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Opens the Settings sub-panel inside the pause menu.</summary>
    public void OnSettings()
    {
        if (mainPausePanel != null) mainPausePanel.SetActive(false);
        if (settingsPanel  != null) settingsPanel.SetActive(true);
    }

    /// <summary>Returns from Settings back to the main pause panel.</summary>
    public void OnBackToPause()
    {
        if (settingsPanel  != null) settingsPanel.SetActive(false);
        if (mainPausePanel != null) mainPausePanel.SetActive(true);
    }

    /// <summary>
    /// Restarts the current level. Fully clears the pause state so ESC
    /// cannot resume into the old session.
    /// </summary>
    public void OnRestart()
    {
        CleanUpBeforeTransition();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Loads the Main Menu scene. Fully clears the pause state so ESC
    /// cannot resume into the old session.
    /// </summary>
    public void OnMainMenu()
    {
        CleanUpBeforeTransition();
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  INTERNAL HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Wires button onClick listeners at runtime. This is the primary way
    /// buttons get connected — works regardless of persistent listeners.
    /// </summary>
    void WireButtons()
    {
        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveAllListeners();
            resumeButton.onClick.AddListener(Resume);
        }
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(OnSettings);
        }
        if (restartButton != null)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(OnRestart);
        }
        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveAllListeners();
            mainMenuButton.onClick.AddListener(OnMainMenu);
        }
        if (backToPauseButton != null)
        {
            backToPauseButton.onClick.RemoveAllListeners();
            backToPauseButton.onClick.AddListener(OnBackToPause);
        }
    }

    /// <summary>
    /// Finds and disables the old FPS Microgame InGameMenuManager so it
    /// doesn't open its own pause menu on Tab. We also hide its MenuRoot
    /// UI in case it was already visible.
    /// </summary>
    void DisableOldPauseMenu()
    {
        // Find by type name so we don't need a direct reference to the FPS assembly
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (mb.GetType().Name == "InGameMenuManager")
            {
                // Hide its UI root if it has one
                var menuRootField = mb.GetType().GetField("MenuRoot");
                if (menuRootField != null)
                {
                    var menuRoot = menuRootField.GetValue(mb) as GameObject;
                    if (menuRoot != null) menuRoot.SetActive(false);
                }

                mb.enabled = false;
                Debug.Log("[PauseMenu] Disabled old InGameMenuManager — replaced by new Pause Menu.");
                break;
            }
        }
    }

    /// <summary>
    /// Called before Restart or Main Menu to ensure the pause state is
    /// completely reset. The new scene starts fresh — ESC will NOT
    /// resume the old game.
    /// </summary>
    void CleanUpBeforeTransition()
    {
        _isTransitioning = true;
        _isPaused = false;
        Time.timeScale = 1f;

        // Destroy the singleton reference so the new scene can create
        // its own pause menu without conflict
        s_instance = null;

        // Show cursor (Main Menu expects it; Restart will re-lock via FPS scripts)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    /// <summary>Shows the main pause panel, hides settings.</summary>
    void ShowMainPausePanel()
    {
        if (mainPausePanel != null) mainPausePanel.SetActive(true);
        if (settingsPanel  != null) settingsPanel.SetActive(false);
    }

    // ── Player input disable / enable ───────────────────────────────────────
    //  Finds the FPS player by tag and toggles their MonoBehaviour scripts.
    //  This prevents mouse-look and shooting while the pause menu is open.

    void DisablePlayerInput()
    {
        CachePlayerRef();
        if (_playerScripts == null) return;
        foreach (var s in _playerScripts)
            if (s != null && s != this) s.enabled = false;
    }

    void EnablePlayerInput()
    {
        if (_playerScripts == null) return;
        foreach (var s in _playerScripts)
            if (s != null) s.enabled = true;
    }

    void CachePlayerRef()
    {
        if (_playerRoot != null) return;   // already cached

        _playerRoot = GameObject.FindGameObjectWithTag("Player");
        if (_playerRoot == null) return;

        // Cache all MonoBehaviour scripts on the player AND its children
        // (the FPS Microgame puts scripts on child objects too)
        _playerScripts = _playerRoot.GetComponentsInChildren<MonoBehaviour>();
    }

    // ── Public state query ──────────────────────────────────────────────────

    /// <summary>True while the game is paused.</summary>
    public bool IsPaused => _isPaused;
}
