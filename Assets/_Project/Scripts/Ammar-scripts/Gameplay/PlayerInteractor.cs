using UnityEngine;
using UnityEngine.InputSystem;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Player Interactor
//  Casts a forward ray from the camera; finds any IInteractable.
//  Player holds E to interact, prompt UI shows progress bar.
// ─────────────────────────────────────────────────────────────────────────────
public class PlayerInteractor : MonoBehaviour
{
    [Header("Refs")]
    public Transform rayOrigin;     // auto: child Camera
    public InteractionPromptUI promptUI;

    [Header("Settings")]
    public float maxDistance = 3.5f;
    public LayerMask interactionMask = ~0;
    [Tooltip("F by default — E conflicts with FPS Microgame's weapon swap.")]
    public Key interactKey = Key.F;

    IInteractable _current;
    float _holdTimer;

    void Start()
    {
        if (rayOrigin == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null) rayOrigin = cam.transform;
        }
        if (promptUI == null) promptUI = Object.FindFirstObjectByType<InteractionPromptUI>();
    }

    void Update()
    {
        // Lazy lookup so the prompt UI is found even if it spawns after Start()
        if (promptUI == null) promptUI = Object.FindFirstObjectByType<InteractionPromptUI>();

        IInteractable found = null;
        if (rayOrigin != null &&
            Physics.Raycast(rayOrigin.position, rayOrigin.forward, out RaycastHit hit, maxDistance, interactionMask, QueryTriggerInteraction.Collide))
        {
            found = hit.collider.GetComponent<IInteractable>();
            if (found == null) found = hit.collider.GetComponentInParent<IInteractable>();
        }

        // Target changed
        if (found != _current)
        {
            _current = found;
            _holdTimer = 0f;
        }

        if (_current == null)
        {
            if (promptUI != null) promptUI.Hide();
            return;
        }

        bool canInteract = _current.CanInteract(gameObject, out string reason);
        if (promptUI != null) promptUI.Show(canInteract ? _current.PromptText : reason, canInteract);

        if (!canInteract)
        {
            _holdTimer = 0f;
            if (promptUI != null) promptUI.SetProgress(0f);
            return;
        }

        // HoldDuration <= 0 → press-once mode (single key press triggers).
        // HoldDuration > 0 → hold mode (must hold for that many seconds).
        if (_current.HoldDuration <= 0.01f)
        {
            if (promptUI != null) promptUI.SetProgress(0f);
            bool pressed = Keyboard.current != null && Keyboard.current[interactKey].wasPressedThisFrame;
            if (pressed) _current.OnInteract(gameObject);
        }
        else
        {
            bool keyHeld = Keyboard.current != null && Keyboard.current[interactKey].isPressed;
            if (keyHeld)
            {
                _holdTimer += Time.deltaTime;
                float dur = _current.HoldDuration;

                // Notify interactable of hold progress (used by ServerHack alarm logic)
                if (_current is IInteractableProgress prog)
                    prog.OnHoldProgress(gameObject, _holdTimer);

                if (promptUI != null) promptUI.SetProgress(_holdTimer / dur);
                if (_holdTimer >= dur)
                {
                    _current.OnInteract(gameObject);
                    _holdTimer = 0f;
                }
            }
            else
            {
                _holdTimer = 0f;
                if (_current is IInteractableProgress prog)
                    prog.OnHoldProgress(gameObject, 0f);
                if (promptUI != null) promptUI.SetProgress(0f);
            }
        }
    }
}
