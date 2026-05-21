using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Minimap (Call of Duty style)
//  Camera follows the player and rotates with their heading; the on-screen
//  arrow stays fixed pointing up. The map appears to spin underneath it.
//  Pure UI-side script — no FPS Microgame references.
// ─────────────────────────────────────────────────────────────────────────────
public class Minimap : MonoBehaviour
{
    [Header("Refs")]
    public Camera        minimapCamera;
    public RectTransform playerArrowUI;
    public Transform     target;          // assigned by builder/bridge

    [Header("Settings")]
    public float height    = 30f;
    public float orthoSize = 18f;
    public bool  rotateWithPlayer = true;

    void Start()
    {
        if (target == null)
        {
            var taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null) target = taggedPlayer.transform;
            else Debug.LogWarning("[Minimap] No GameObject tagged 'Player' — minimap can't follow.");
        }

        if (minimapCamera != null)
        {
            minimapCamera.orthographic     = true;
            minimapCamera.orthographicSize = orthoSize;
        }
        if (playerArrowUI != null) playerArrowUI.localRotation = Quaternion.identity;
    }

    void LateUpdate()
    {
        if (target == null || minimapCamera == null) return;

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
