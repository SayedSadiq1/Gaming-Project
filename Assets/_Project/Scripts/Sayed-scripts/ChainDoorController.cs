using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Add this to the Chain door GameObject (the prefab root).
// The player must have ChainDoorKeycardHolder with a matching keycardID to open it.
public class ChainDoorController : MonoBehaviour
{
    [Header("Keycard")]
    [Tooltip("Must match the keycardID on the KeycardPickupTrigger that unlocks this door.")]
    public string keycardID = "door_1";

    [Header("Power Requirement (optional)")]
    [Tooltip("Assign an ElectricitySource here to require power before this door can open. Leave empty for no power requirement.")]
    public ElectricitySource requiredPowerSource;

    [Header("Proximity")]
    [Tooltip("How close the player must be (XZ plane) to see the prompt and press E.")]
    public float interactDistance = 5f;

    [Header("Hinge / Rotation")]
    [Tooltip("Drag the post-grabber child Transform here to set the hinge point. " +
             "Leave empty to rotate around this object's own pivot (the prefab root).")]
    public Transform hingePoint;

    [Tooltip("How many degrees the door swings open. Negative reverses direction.")]
    public float openAngle = 90f;

    [Tooltip("Degrees per second.")]
    public float openSpeed = 120f;

    [Header("Messages")]
    public string msgNoPower    = "No power — start the generator first";
    public string msgNoKeycard  = "Need keycard to open";
    public string msgLockdown   = "SECURITY LOCKDOWN ACTIVE";
    public string msgReady      = "[E]  Open Door";

    [Header("Audio")]
    public AudioClip openSound;

    // Fires once when the door opens — used by Level1ObjectiveManager.
    public event Action OnOpened;

    bool _lockedDown;
    bool _opened;
    bool _opening;
    bool _playerInRange;
    bool _hasCard;
    float _t;

    // Called by SecurityLockdown on this specific door instance only.
    public void SetLockdown(bool locked) => _lockedDown = locked;

    Vector3    _startPos;
    Quaternion _startRot;
    Vector3    _targetPos;
    Quaternion _targetRot;

    Transform _player;
    ChainDoorKeycardHolder _holder;

    void Start()
    {
        // Find by component — no "Player" tag required.
        _holder = FindAnyObjectByType<ChainDoorKeycardHolder>();
        if (_holder != null) _player = _holder.transform;
    }

    void Update()
    {
        if (_opening)
        {
            _t += Time.deltaTime * (openSpeed / Mathf.Abs(openAngle));
            _t  = Mathf.Clamp01(_t);

            transform.position = Vector3.Lerp(_startPos, _targetPos, _t);
            transform.rotation = Quaternion.Lerp(_startRot, _targetRot, _t);

            if (_t >= 1f) _opening = false;
            return;
        }

        if (_opened || _player == null) return;

        Vector2 doorXZ   = new Vector2(transform.position.x, transform.position.z);
        Vector2 playerXZ = new Vector2(_player.position.x,   _player.position.z);
        _playerInRange = Vector2.Distance(doorXZ, playerXZ) <= interactDistance;
        _hasCard = _holder != null && _holder.HasKeycard(keycardID);

        bool hasPower = requiredPowerSource == null || requiredPowerSource.IsPowered;

        if (_playerInRange && _hasCard && hasPower && !_lockedDown && Keyboard.current != null && Keyboard.current[Key.E].wasPressedThisFrame)
            OpenDoor();
    }

    void OpenDoor()
    {
        _opened  = true;
        _opening = true;
        _t       = 0f;

        OnOpened?.Invoke();
        if (openSound != null) AudioSource.PlayClipAtPoint(openSound, transform.position);

        // The hinge pivot: use the assigned post-grabber, or fall back to this transform's origin.
        Vector3 hinge = hingePoint != null ? hingePoint.position : transform.position;

        _startPos = transform.position;
        _startRot = transform.rotation;

        Quaternion swing = Quaternion.Euler(0f, openAngle, 0f);

        // Rotate the door's position around the hinge on the Y axis.
        _targetPos = hinge + swing * (transform.position - hinge);
        _targetRot = swing * transform.rotation;
    }

    void OnGUI()
    {
        if (_opened || !_playerInRange) return;

        bool hasPower = requiredPowerSource == null || requiredPowerSource.IsPowered;

        string msg;
        Color  textColor;

        if (_lockedDown)
        {
            msg       = msgLockdown;
            textColor = new Color(1f, 0.2f, 0.1f, 1f); // red
        }
        else if (!hasPower)
        {
            msg       = msgNoPower;
            textColor = new Color(1f, 0.6f, 0f, 1f);   // orange
        }
        else if (!_hasCard)
        {
            msg       = msgNoKeycard;
            textColor = new Color(1f, 0.35f, 0.35f);   // red
        }
        else
        {
            msg       = msgReady;
            textColor = new Color(0f, 0.85f, 1f);       // cyan
        }

        GUIStyle style = new GUIStyle(GUI.skin.box)
        {
            fontSize   = 17,
            alignment  = TextAnchor.MiddleCenter,
            fontStyle  = FontStyle.Bold,
            wordWrap   = false,
        };
        style.normal.textColor = textColor;

        float w = 460f, h = 44f;
        GUI.Box(new Rect((Screen.width - w) * 0.5f, Screen.height * 0.72f, w, h), msg, style);
    }
}
