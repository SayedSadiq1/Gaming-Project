using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Keycard Door
//  Locked door. Player needs matching color keycard to open.
//  Drop on any door GameObject and assign requiredColor.
// ─────────────────────────────────────────────────────────────────────────────
public class KeycardDoor : MonoBehaviour, IInteractable
{
    public KeycardColor requiredColor = KeycardColor.Blue;
    public float holdDuration = 0.5f;
    public string unlockedText = "Hold E to Open";

    [Header("Open Behaviour")]
    public AudioClip openSound;
    [Tooltip("If true, disables this GameObject when opened. Otherwise slides up.")]
    public bool disableOnOpen = true;
    public float slideUpDistance = 3f;
    public float slideSpeed = 4f;

    public string PromptText  { get; private set; } = "REQUIRES BLUE KEYCARD";
    public float  HoldDuration => holdDuration;

    bool _opening;
    Vector3 _targetPos;
    Vector3 _startPos;

    void Start() { _startPos = transform.position; }

    public bool CanInteract(GameObject interactor, out string reason)
    {
        if (_opening) { reason = "Opening..."; return false; }

        var inv = interactor.GetComponent<KeycardInventory>();
        if (inv == null) inv = interactor.GetComponentInParent<KeycardInventory>();
        if (inv != null && inv.HasKeycard(requiredColor))
        {
            PromptText = unlockedText;
            reason = "";
            return true;
        }
        PromptText = $"REQUIRES {requiredColor.ToString().ToUpper()} KEYCARD";
        reason = PromptText;
        return false;
    }

    public void OnInteract(GameObject interactor)
    {
        if (_opening) return;
        _opening = true;
        if (openSound != null) AudioSource.PlayClipAtPoint(openSound, transform.position);

        if (disableOnOpen)
        {
            gameObject.SetActive(false);
        }
        else
        {
            _targetPos = _startPos + Vector3.up * slideUpDistance;
        }
    }

    void Update()
    {
        if (_opening && !disableOnOpen && transform.position != _targetPos)
        {
            transform.position = Vector3.MoveTowards(transform.position, _targetPos, slideSpeed * Time.deltaTime);
        }
    }
}
