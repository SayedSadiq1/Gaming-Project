using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Minimap (Call of Duty style)
//  Camera follows the player and rotates with their heading; the on-screen
//  arrow stays fixed pointing up.
//
//  Even if the Player tag isn't set yet (or the player spawns late), the
//  camera still gets a top-down rotation so the map never looks sideways.
// ─────────────────────────────────────────────────────────────────────────────
public class Minimap : MonoBehaviour
{
    [Header("Refs")]
    public Camera        minimapCamera;
    public RectTransform playerArrowUI;
    public Transform     target;

    [Header("Settings")]
    public float height    = 30f;
    public float orthoSize = 18f;
    public bool  rotateWithPlayer = true;

    void Start()
    {
        if (minimapCamera != null)
        {
            minimapCamera.orthographic     = true;
            minimapCamera.orthographicSize = orthoSize;
            // Force top-down rotation immediately so the camera never starts
            // facing sideways even if the player tag isn't found at Start.
            minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
        if (playerArrowUI != null) playerArrowUI.localRotation = Quaternion.identity;
        TryFindTarget();
    }

    void TryFindTarget()
    {
        if (target != null) return;

        // 1) Try the "Player" tag
        GameObject p = null;
        try { p = GameObject.FindGameObjectWithTag("Player"); } catch { }
        if (p != null) { target = p.transform; Debug.Log("[Minimap] Bound player by tag: " + p.name); return; }

        // 2) Fallback: any GameObject with PlayerWeaponsManager
        var all = Object.FindObjectsByType<Component>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in all)
        {
            if (c == null) continue;
            if (c.GetType().Name == "PlayerWeaponsManager")
            {
                target = c.transform;
                Debug.Log("[Minimap] Bound player by PlayerWeaponsManager: " + c.gameObject.name);
                return;
            }
        }
    }

    void LateUpdate()
    {
        if (target == null) TryFindTarget();   // lazy lookup every frame until found
        if (minimapCamera == null) return;

        // If still no player target, keep camera pointing down at world origin
        if (target == null)
        {
            minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            return;
        }

        Vector3 p = minimapCamera.transform.position;
        p.x = target.position.x;
        p.z = target.position.z;
        p.y = target.position.y + height;
        minimapCamera.transform.position = p;

        minimapCamera.transform.rotation = rotateWithPlayer
            ? Quaternion.Euler(90f, target.eulerAngles.y, 0f)
            : Quaternion.Euler(90f, 0f, 0f);

        if (playerArrowUI != null) playerArrowUI.localRotation = Quaternion.identity;
    }
}
