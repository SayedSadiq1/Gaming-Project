using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Interaction interface
//  Implement this on anything the player can press-E on.
// ─────────────────────────────────────────────────────────────────────────────
public interface IInteractable
{
    string PromptText { get; }
    float  HoldDuration { get; }                // 0 = instant
    bool   CanInteract(GameObject interactor, out string reason);
    void   OnInteract(GameObject interactor);
}
