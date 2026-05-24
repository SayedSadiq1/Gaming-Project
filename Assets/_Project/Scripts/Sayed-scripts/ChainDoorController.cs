using UnityEngine;
using UnityEngine.InputSystem;

// Add this to the Chain door GameObject (the prefab root).
// The player must have ChainDoorKeycardHolder with a matching keycardID to open it.
public class ChainDoorController : MonoBehaviour
{
    [Header("Keycard")]
    [Tooltip("Must match the keycardID on the KeycardPickupTrigger that unlocks this door.")]
    public string keycardID = "door_1";

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

    [Header("Audio")]
    public AudioClip openSound;

    bool _opened;
    bool _opening;
    bool _playerInRange;
    bool _hasCard;
    float _t;

    Vector3    _startPos;
    Quaternion _startRot;
    Vector3    _targetPos;
    Quaternion _targetRot;

    Transform _player;
    ChainDoorKeycardHolder _holder;

    void Start()
    {
        // Find by component — no "Player" tag required.
        _holder = Object.FindFirstObjectByType<ChainDoorKeycardHolder>();
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

        if (_playerInRange && _hasCard && Keyboard.current != null && Keyboard.current[Key.E].wasPressedThisFrame)
            OpenDoor();
    }

    void OpenDoor()
    {
        _opened  = true;
        _opening = true;
        _t       = 0f;

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

        string msg       = _hasCard ? "[E]  Open Door" : "Need keycard to open";
        Color  textColor = _hasCard ? new Color(0f, 0.85f, 1f) : new Color(1f, 0.35f, 0.35f);

        GUIStyle style = new GUIStyle(GUI.skin.box)
        {
            fontSize   = 20,
            alignment  = TextAnchor.MiddleCenter,
            fontStyle  = FontStyle.Bold
        };
        style.normal.textColor = textColor;

        float w = 280f, h = 44f;
        GUI.Box(new Rect((Screen.width - w) * 0.5f, Screen.height * 0.72f, w, h), msg, style);
    }
}
