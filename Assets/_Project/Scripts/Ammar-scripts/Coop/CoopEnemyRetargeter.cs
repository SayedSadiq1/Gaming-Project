using System.Reflection;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Co-op Enemy Retargeter
//
//  Every ~0.3s, walks every EnemyGunAI / EnemyMovement in the scene and sets
//  their `target` field to whichever live player (P1 or P2) is closest.
//
//  Reflection-based so we don't need to recompile the FPS Microgame enemy
//  scripts. Lightweight — only runs 3x per second.
// ─────────────────────────────────────────────────────────────────────────────
public class CoopEnemyRetargeter : MonoBehaviour
{
    public float retargetInterval = 0.3f;
    float _next;

    void Update()
    {
        if (Time.time < _next) return;
        _next = Time.time + retargetInterval;
        Retarget();
    }

    void Retarget()
    {
        var live = CoopBootstrap.LivePlayerTransforms();
        if (live.Count == 0) return;

        // Find every MonoBehaviour with a public Transform field named "target"
        // — this covers EnemyGunAI and EnemyMovement, and any future enemy
        // script that follows the same convention.
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (mb == null) continue;
            var t = mb.GetType();
            if (t.Name != "EnemyGunAI" && t.Name != "EnemyMovement") continue;

            var field = t.GetField("target", BindingFlags.Public | BindingFlags.Instance);
            if (field == null || field.FieldType != typeof(Transform)) continue;

            // Pick the closest live player to this enemy
            Transform nearest = null;
            float bestSqr = float.MaxValue;
            Vector3 here = mb.transform.position;
            foreach (var lp in live)
            {
                float sqr = (lp.position - here).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; nearest = lp; }
            }
            if (nearest != null) field.SetValue(mb, nearest);
        }
    }
}
