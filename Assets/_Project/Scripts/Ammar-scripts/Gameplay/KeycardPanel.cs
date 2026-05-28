using System.Collections;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Keycard Panel (external door unlock)
//  Drop this on a panel GameObject outside a locked room. Player aims +
//  holds the interact key. If they have the matching keycard, two doors
//  slide apart in opposite directions.
//
//  Each door's CLOSED position is captured automatically at Start.
//  The OPEN positions are computed as closed + offset (set in inspector).
// ─────────────────────────────────────────────────────────────────────────────
public class KeycardPanel : MonoBehaviour, IInteractable
{
    [Header("Keycard Required")]
    public KeycardColor requiredColor = KeycardColor.Blue;
    [Tooltip("If set, also unlocks when the player has a team-system keycard with this ID " +
             "(ChainDoorKeycardHolder). Leave empty to only use the legacy color system.")]
    public string keycardID = "blue_keycard";

    [Header("Doors")]
    public Transform leftDoor;
    [Tooltip("Local-space offset added to the left door's start position when opening.")]
    public Vector3   leftOpenOffset  = new Vector3(-2.75f, 0f, 0f);
    public Transform rightDoor;
    [Tooltip("Local-space offset added to the right door's start position when opening.")]
    public Vector3   rightOpenOffset = new Vector3( 2.75f, 0f, 0f);

    [Header("Settings")]
    public float  slideDuration = 1.2f;
    [Tooltip("0 = press once, >0 = hold for that many seconds")]
    public float  holdDuration  = 0f;
    public string unlockedText  = "PRESS F TO UNLOCK";
    public string lockedText    = "REQUIRES BLUE KEYCARD";
    public AudioClip openSound;
    public AudioClip lockedSound;

    public string PromptText  { get; private set; } = "LOCKED";
    public float  HoldDuration => holdDuration;

    Vector3 _leftClosedLocal, _rightClosedLocal;
    bool _captured, _opening, _opened;

    void Awake()  { CaptureClosed(); }
    void Start()  { CaptureClosed(); }

    void CaptureClosed()
    {
        if (_captured) return;
        if (leftDoor != null)  _leftClosedLocal  = leftDoor.localPosition;
        if (rightDoor != null) _rightClosedLocal = rightDoor.localPosition;
        _captured = true;
    }

    public bool CanInteract(GameObject interactor, out string reason)
    {
        if (_opened || _opening) { reason = "Already open"; return false; }

        // 1) Team's keycard system — ChainDoorKeycardHolder with string IDs.
        if (!string.IsNullOrEmpty(keycardID))
        {
            var holder = interactor.GetComponent<ChainDoorKeycardHolder>()
                      ?? interactor.GetComponentInParent<ChainDoorKeycardHolder>();
            if (holder != null && holder.HasKeycard(keycardID))
            {
                PromptText = unlockedText;
                reason = "";
                return true;
            }
        }

        // 2) Legacy colour-based system (KeycardInventory).
        var inv = interactor.GetComponent<KeycardInventory>();
        if (inv == null) inv = interactor.GetComponentInParent<KeycardInventory>();
        if (inv != null && inv.HasKeycard(requiredColor))
        {
            PromptText = unlockedText;
            reason = "";
            return true;
        }

        PromptText = lockedText;
        reason = lockedText;
        return false;
    }

    public void OnInteract(GameObject interactor)
    {
        if (_opened || _opening) return;
        _opening = true;
        if (openSound != null) AudioSource.PlayClipAtPoint(openSound, transform.position);
        StartCoroutine(SlideDoorsRoutine());
    }

    IEnumerator SlideDoorsRoutine()
    {
        CaptureClosed();
        Vector3 lTarget = _leftClosedLocal  + leftOpenOffset;
        Vector3 rTarget = _rightClosedLocal + rightOpenOffset;

        float t = 0f;
        while (t < slideDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / slideDuration);
            if (leftDoor  != null) leftDoor.localPosition  = Vector3.Lerp(_leftClosedLocal,  lTarget, k);
            if (rightDoor != null) rightDoor.localPosition = Vector3.Lerp(_rightClosedLocal, rTarget, k);
            yield return null;
        }
        if (leftDoor  != null) leftDoor.localPosition  = lTarget;
        if (rightDoor != null) rightDoor.localPosition = rTarget;
        _opened = true;
        _opening = false;
    }
}
