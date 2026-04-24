using UnityEngine;
using Unity.FPS.Game;

public class EnemyDeath : MonoBehaviour
{
    Health health;

    void Awake()
    {
        health = GetComponent<Health>();
        health.OnDie += OnDie;
    }

    void OnDie()
    {
        Destroy(gameObject);
    }
}