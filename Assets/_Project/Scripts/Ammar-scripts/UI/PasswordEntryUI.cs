using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Password Entry UI
//
//  Modal panel used by Server 2's second-stage hack. Caller (ServerHack) passes
//  a title, hint, target password, and success/cancel callbacks via Show().
//
//  • Enter  submits the typed password
//  • Esc    cancels and closes the panel
//  • Wrong password → status flashes red, input clears, panel stays open
//  • Right password → success callback fires, panel closes
//
//  Built at runtime / from the Level3InteractionsBuilder editor script — no
//  prefab dependency. Mouse cursor is unlocked + made visible while the panel
//  is open, then restored to whatever the FPS Microgame had it set to.
// ─────────────────────────────────────────────────────────────────────────────
public class PasswordEntryUI : MonoBehaviour
{
    [Header("Wired by builder")]
    public CanvasGroup     group;
    public TextMeshProUGUI titleLabel;
    public TextMeshProUGUI hintLabel;
    public TextMeshProUGUI statusLabel;
    public TMP_InputField  inputField;

    Action<bool>   _onResult;   // true = success, false = cancel
    string         _targetPassword;
    bool           _open;
    CursorLockMode _savedLockState;
    bool           _savedCursorVisible;
    InteractionPromptUI _promptUI;
    float          _savedPromptAlpha;

    void Start()
    {
        if (group != null) { group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false; }
        _open = false;
    }

    public bool IsOpen => _open;

    public void Show(string title, string hint, string targetPassword, Action<bool> onResult)
    {
        if (titleLabel != null)  titleLabel.text = string.IsNullOrEmpty(title) ? "ENTER PASSWORD" : title;
        if (hintLabel  != null)  hintLabel.text  = string.IsNullOrEmpty(hint)  ? "" : hint;
        if (statusLabel != null) { statusLabel.text = ""; statusLabel.color = new Color(1, 0.3f, 0.3f, 1f); }
        if (inputField != null)
        {
            inputField.text = "";
            inputField.ActivateInputField();
        }

        _targetPassword = targetPassword ?? "";
        _onResult       = onResult;

        if (group != null) { group.alpha = 1f; group.interactable = true; group.blocksRaycasts = true; }
        _open = true;

        // Hide the InteractionPromptUI behind us — otherwise its "ENTERING
        // PASSWORD..." text overlaps the bottom of this panel.
        if (_promptUI == null) _promptUI = FindFirstObjectByType<InteractionPromptUI>();
        if (_promptUI != null && _promptUI.group != null)
        {
            _savedPromptAlpha = _promptUI.group.alpha;
            _promptUI.group.alpha = 0f;
        }

        // Unlock cursor so the player can type / see the field cursor
        _savedLockState     = Cursor.lockState;
        _savedCursorVisible = Cursor.visible;
        Cursor.lockState    = CursorLockMode.None;
        Cursor.visible      = true;
    }

    public void Hide(bool wasSuccess)
    {
        if (!_open) return;
        if (group != null) { group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false; }
        _open = false;

        // Restore the InteractionPromptUI (the PlayerInteractor will refresh
        // its content on the next frame as soon as the raycast hits something).
        if (_promptUI != null && _promptUI.group != null)
            _promptUI.group.alpha = _savedPromptAlpha;

        // Restore cursor state
        Cursor.lockState = _savedLockState != CursorLockMode.None ? _savedLockState : CursorLockMode.Locked;
        Cursor.visible   = _savedCursorVisible;

        var cb = _onResult; _onResult = null;
        cb?.Invoke(wasSuccess);
    }

    void Update()
    {
        if (!_open || Keyboard.current == null) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Hide(false);
            return;
        }

        if (Keyboard.current.enterKey.wasPressedThisFrame ||
            Keyboard.current.numpadEnterKey.wasPressedThisFrame)
        {
            Submit();
        }
    }

    public void Submit()
    {
        string typed = inputField != null ? inputField.text : "";
        if (string.Equals(typed, _targetPassword, StringComparison.Ordinal))
        {
            if (statusLabel != null) { statusLabel.text = "ACCESS GRANTED"; statusLabel.color = new Color(0.3f, 1f, 0.4f, 1f); }
            Hide(true);
        }
        else
        {
            if (statusLabel != null) { statusLabel.text = "ACCESS DENIED — WRONG PASSWORD"; statusLabel.color = new Color(1f, 0.3f, 0.3f, 1f); }
            if (inputField != null)
            {
                inputField.text = "";
                inputField.ActivateInputField();
            }
            Debug.Log("[PasswordEntryUI] Wrong password entered.");
        }
    }
}
