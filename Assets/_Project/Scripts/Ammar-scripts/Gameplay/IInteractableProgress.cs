using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Optional companion interface for IInteractable. If an interactable also
//  implements this, PlayerInteractor calls OnHoldProgress every frame while
//  the player is holding the interact key. Useful for state changes during
//  the hold (e.g. ServerHack's alarm phase).
// ─────────────────────────────────────────────────────────────────────────────
public interface IInteractableProgress
{
    void OnHoldProgress(GameObject interactor, float currentHoldTime);
}
