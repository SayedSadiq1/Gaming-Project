using System.Collections.Generic;
using UnityEngine;

// Add this to the Player GameObject.
// Tracks which keycard IDs have been collected.
public class ChainDoorKeycardHolder : MonoBehaviour
{
    readonly HashSet<string> _collected = new HashSet<string>();

    public bool HasKeycard(string id) => _collected.Contains(id);

    public void Collect(string id)
    {
        if (_collected.Add(id))
            Debug.Log($"[KeycardHolder] Collected keycard: {id}");
    }
}
