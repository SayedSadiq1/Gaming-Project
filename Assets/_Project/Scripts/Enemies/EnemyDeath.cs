using UnityEngine;
using Unity.FPS.Game;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Enemy Death
//  Listens for Health.OnDie, awards score, then destroys the enemy.
// ─────────────────────────────────────────────────────────────────────────────
public class EnemyDeath : MonoBehaviour
{
    [Tooltip("Points awarded when this enemy is killed")]
    public int pointsOnKill = 100;

    [Tooltip("Display name shown in the kill feed")]
    public string enemyDisplayName = "Enemy";

    Health health;

    void Awake()
    {
        health = GetComponent<Health>();
        health.OnDie += OnDie;

        // Auto-set display name from GameObject name if left as default
        if (enemyDisplayName == "Enemy" && !string.IsNullOrEmpty(gameObject.name))
        {
            // Strip "(Clone)" and clean up prefab names
            enemyDisplayName = gameObject.name.Replace("(Clone)", "").Trim();
        }
    }

    void OnDie()
    {
        // Award score
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.AddKill(enemyDisplayName, pointsOnKill);
        }

        Destroy(gameObject);
    }
}