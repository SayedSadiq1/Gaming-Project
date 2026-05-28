using UnityEngine;
using UnityEngine.InputSystem;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Level 5 Proximity Auto Door
//
//  Attach to the sci-fi_doors root. The two panels (Low_L / Low_R) slide apart
//  automatically when something gets close, and slide back when it leaves —
//  no keycard, no key press.
//
//  Detection modes:
//    • PlayerOnly      — opens for the GameObject tagged "Player" (reliable, default).
//    • AnyObjectInLayers — opens for ANY collider on the chosen layers (use this
//                          if you want enemies / props to open it too). Set the
//                          Detection Mask to the layers that should count, and
//                          keep static walls/floor OUT of that mask or the door
//                          will never close.
// ─────────────────────────────────────────────────────────────────────────────
public class ProximityAutoDoor : MonoBehaviour
{
    public enum DetectMode { PlayerOnly, AnyObjectInLayers }
    public enum Axis { X, Y, Z }

    [Header("Detection")]
    public DetectMode mode = DetectMode.PlayerOnly;
    [Tooltip("How close (metres, measured from this door) something must be to open it.")]
    public float openRadius = 4f;
    [Tooltip("Only used in AnyObjectInLayers mode. Layers that should trigger the door.")]
    public LayerMask detectionMask = ~0;

    [Header("Panels (auto-found by name if left empty)")]
    public Transform leftPanel;   // Low_L
    public Transform rightPanel;  // Low_R

    [Header("Slide")]
    [Tooltip("Local axis the panels slide along. Try the others if it opens the wrong way.")]
    public Axis slideAxis = Axis.X;
    [Tooltip("How far each panel slides open, in metres.")]
    public float slideDistance = 1.8f;
    [Tooltip("Slide speed (higher = snappier).")]
    public float slideSpeed = 4f;
    [Tooltip("Flip if the panels slide together instead of apart.")]
    public bool invertDirection = false;

    [Header("Close")]
    [Tooltip("Auto-close when nothing is near.")]
    public bool autoClose = true;
    [Tooltip("Seconds to wait after the area is clear before closing.")]
    public float closeDelay = 1f;

    [Header("Audio (optional)")]
    public AudioClip openSound;
    public AudioClip closeSound;

    [Header("Debug (temporary — turn off when working)")]
    [Tooltip("Logs detection distance to the Console once per second.")]
    public bool debugLogging = true;
    [Tooltip("Hold this key to force the door open, regardless of proximity. " +
             "Use it to confirm the slide direction/distance are right.")]
    public Key forceOpenKey = Key.O;

    Vector3 _leftClosed,  _leftOpen;
    Vector3 _rightClosed, _rightOpen;
    bool    _isOpen;
    float   _clearTimer;
    Transform _player;
    AudioSource _audio;

    void Start()
    {
        if (leftPanel  == null) leftPanel  = FindDeep(transform, "Low_L");
        if (rightPanel == null) rightPanel = FindDeep(transform, "Low_R");

        if (leftPanel == null || rightPanel == null)
        {
            Debug.LogWarning("[ProximityAutoDoor] Could not find Low_L / Low_R panels. " +
                             "Assign them manually in the inspector.", this);
            enabled = false;
            return;
        }

        Vector3 axis = AxisVector(slideAxis) * (invertDirection ? -1f : 1f);

        _leftClosed  = leftPanel.localPosition;
        _rightClosed = rightPanel.localPosition;
        // Panels slide in opposite directions along the chosen local axis.
        _leftOpen    = _leftClosed  - axis * slideDistance;
        _rightOpen   = _rightClosed + axis * slideDistance;

        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO != null) _player = playerGO.transform;

        if (debugLogging)
            Debug.Log($"[ProximityAutoDoor] Ready. leftPanel={leftPanel.name}, rightPanel={rightPanel.name}, " +
                      $"player={(_player != null ? _player.name : "NOT FOUND")}, " +
                      $"leftClosed={_leftClosed} → leftOpen={_leftOpen}", this);

        // Persistent 2D AudioSource so the SFX slider controls it.
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake  = false;
        _audio.spatialBlend = 1f;   // 3D — the door sound comes from the door
    }

    float _logTimer;

    void Update()
    {
        bool forced = Keyboard.current != null && Keyboard.current[forceOpenKey].isPressed;
        bool wantOpen = forced || SomethingNearby();

        if (debugLogging)
        {
            _logTimer += Time.deltaTime;
            if (_logTimer >= 1f)
            {
                _logTimer = 0f;
                float d = _player != null ? Vector3.Distance(DoorCenter(), _player.position) : -1f;
                Debug.Log($"[ProximityAutoDoor] dist={d:F2}  openRadius={openRadius}  " +
                          $"wantOpen={wantOpen}  isOpen={_isOpen}  forced={forced}", this);
            }
        }

        if (wantOpen)
        {
            _clearTimer = 0f;
            if (!_isOpen) SetOpen(true);
        }
        else if (_isOpen && autoClose)
        {
            _clearTimer += Time.deltaTime;
            if (_clearTimer >= closeDelay) SetOpen(false);
        }

        // Smoothly drive both panels toward their target each frame.
        float t = Time.deltaTime * slideSpeed;
        if (leftPanel != null)
            leftPanel.localPosition  = Vector3.Lerp(leftPanel.localPosition,  _isOpen ? _leftOpen  : _leftClosed,  t);
        if (rightPanel != null)
            rightPanel.localPosition = Vector3.Lerp(rightPanel.localPosition, _isOpen ? _rightOpen : _rightClosed, t);
    }

    void SetOpen(bool open)
    {
        _isOpen = open;
        var clip = open ? openSound : closeSound;
        if (clip != null && _audio != null) _audio.PlayOneShot(clip);
    }

    // The real-world centre of the doorway — midpoint of the two panels.
    // Using this (not the root pivot) makes detection work even when the
    // imported model's pivot sits far from where the door actually appears.
    Vector3 DoorCenter()
    {
        if (leftPanel != null && rightPanel != null)
            return (leftPanel.position + rightPanel.position) * 0.5f;
        return transform.position;
    }

    bool SomethingNearby()
    {
        Vector3 center = DoorCenter();

        if (mode == DetectMode.PlayerOnly)
        {
            if (_player == null)
            {
                var go = GameObject.FindWithTag("Player");
                if (go != null) _player = go.transform;
            }
            if (_player == null) return false;
            return Vector3.Distance(center, _player.position) <= openRadius;
        }

        // AnyObjectInLayers
        var hits = Physics.OverlapSphere(center, openRadius, detectionMask, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h == null) continue;
            if (h.transform.IsChildOf(transform)) continue; // ignore the door's own colliders
            return true;
        }
        return false;
    }

    static Vector3 AxisVector(Axis a)
    {
        switch (a)
        {
            case Axis.Y: return Vector3.up;
            case Axis.Z: return Vector3.forward;
            default:     return Vector3.right;
        }
    }

    // Depth-first search for a child whose name contains `namePart`.
    static Transform FindDeep(Transform root, string namePart)
    {
        foreach (Transform child in root)
        {
            if (child.name.IndexOf(namePart, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return child;
            var found = FindDeep(child, namePart);
            if (found != null) return found;
        }
        return null;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 center = (leftPanel != null && rightPanel != null)
            ? (leftPanel.position + rightPanel.position) * 0.5f
            : transform.position;
        Gizmos.color = new Color(0f, 0.78f, 1f, 0.25f);
        Gizmos.DrawWireSphere(center, openRadius);
    }
}
