using System.Collections.Generic;
using UnityEngine;

public enum KeycardColor { Blue, Red, Green, Yellow }

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Keycard Inventory
//  Tracks which colored keycards the player has picked up.
//  Drop on the Player GameObject.
// ─────────────────────────────────────────────────────────────────────────────
public class KeycardInventory : MonoBehaviour
{
    readonly HashSet<KeycardColor> _cards = new HashSet<KeycardColor>();

    public bool HasKeycard(KeycardColor color) => _cards.Contains(color);

    public void AddKeycard(KeycardColor color)
    {
        if (_cards.Add(color))
            Debug.Log("[KeycardInventory] Picked up " + color + " keycard.");
    }

    public IEnumerable<KeycardColor> AllCards => _cards;

    public void Clear() => _cards.Clear();
}
