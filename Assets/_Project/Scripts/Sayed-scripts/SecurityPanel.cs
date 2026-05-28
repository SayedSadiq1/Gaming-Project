using System;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

// Attach to each security panel button object in the scene.
// Player walks up and presses E to deactivate it.
// Both panels must be deactivated to lift the SecurityLockdown.
public class SecurityPanel : MonoBehaviour
{
    [Header("Interaction")]
    public float interactDistance = 2.5f;
    public string panelLabel = "PANEL A";

    [Tooltip("The SecurityLockdown manager — panel can only be deactivated while lockdown is active.")]
    public SecurityLockdown lockdownManager;

    [Header("Labels")]
    public string labelActive      = "Press [E] to deactivate panel";
    public string labelDeactivated = "Panel offline";

    [Header("Visual (optional)")]
    [Tooltip("A light or indicator object to disable when deactivated.")]
    public GameObject activeIndicator;

    [Header("Audio")]
    public AudioClip deactivateSound;

    public bool IsDeactivated { get; private set; }

    // Fired once when this panel is deactivated.
    public event Action OnDeactivated;

    Transform _player;
    bool      _playerInRange;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        var go = GameObject.FindWithTag("Player");
        if (go != null) _player = go.transform;
    }

    void Update()
    {
        if (_player == null || IsDeactivated) return;

        Vector2 myXZ     = new Vector2(transform.position.x, transform.position.z);
        Vector2 playerXZ = new Vector2(_player.position.x,   _player.position.z);
        _playerInRange   = Vector2.Distance(myXZ, playerXZ) <= interactDistance;

        bool lockdownActive = lockdownManager != null && lockdownManager.IsLockedDown;
        if (_playerInRange && lockdownActive && Keyboard.current != null && Keyboard.current[Key.E].wasPressedThisFrame)
            Deactivate();
    }

    void Deactivate()
    {
        IsDeactivated = true;
        _playerInRange = false;

        if (activeIndicator != null)
            activeIndicator.SetActive(false);

        if (deactivateSound != null)
            AudioSource.PlayClipAtPoint(deactivateSound, transform.position);

        OnDeactivated?.Invoke();
    }

    void OnGUI()
    {
        if (!_playerInRange) return;

        bool lockdownActive = lockdownManager != null && lockdownManager.IsLockedDown;
        if (!lockdownActive && !IsDeactivated) return;

        string msg   = IsDeactivated ? labelDeactivated : labelActive;
        Color  color = IsDeactivated
            ? new Color(0f, 1f, 0.55f, 1f)   // green — deactivated
            : new Color(1f, 0.35f, 0.1f, 1f); // orange — active / dangerous

        GUIStyle style = new GUIStyle(GUI.skin.box)
        {
            fontSize  = 17,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            wordWrap  = false,
        };
        style.normal.textColor = color;

        float w = 460f, h = 44f;
        GUI.Box(new Rect((Screen.width - w) * 0.5f, Screen.height * 0.72f, w, h),
            $"[{panelLabel}]  {msg}", style);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}
