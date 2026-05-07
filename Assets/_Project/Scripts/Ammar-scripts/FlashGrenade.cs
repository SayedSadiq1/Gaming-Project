using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FlashGrenade : ThrowableBase
{
    [Header("Flash Settings")]
    public float FlashRadius = 15f; // Radius for blinding
    public float FlashHoldDuration = 2.5f; // Duration of pure white screen
    public float FlashFadeDuration = 2.0f; // Duration to fade back to normal
    public LayerMask ObstacleLayers = 1; // Default to layer 0/Default

    public override void Explode()
    {
        // 1. Detect Player
        CheckPlayerBlind();

        // 2. Detect Enemies
        CheckEnemiesBlind();

        base.Explode();
    }

    private void CheckPlayerBlind()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        Vector3 directionToGrenade = transform.position - mainCam.transform.position;
        float distance = directionToGrenade.magnitude;

        if (distance <= FlashRadius)
        {
            // Check Line of Sight
            if (!Physics.Linecast(mainCam.transform.position, transform.position, ObstacleLayers))
            {
                // Check if in front of player
                float dot = Vector3.Dot(mainCam.transform.forward, directionToGrenade.normalized);
                
                float intensity = 1f;
                float hold = 0.1f;
                
                if (dot > 0.4f) // Roughly 60 degree cone
                {
                    // Full blind if looking towards it
                    intensity = 1f;
                    hold = FlashHoldDuration;
                }
                else
                {
                    // Partial blind if looking away
                    intensity = 0.4f;
                    hold = 0.2f;
                }

                if (FlashEffectManager.Instance != null)
                {
                    FlashEffectManager.Instance.TriggerFlash(intensity, hold, FlashFadeDuration);
                }
            }
        }
    }

    private void CheckEnemiesBlind()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, FlashRadius);
        foreach (var col in colliders)
        {
            EnemyMovement enemy = col.GetComponentInParent<EnemyMovement>();
            if (enemy != null)
            {
                StartCoroutine(StunEnemy(enemy));
            }
        }
    }

    private IEnumerator StunEnemy(EnemyMovement enemy)
    {
        enemy.enabled = false;
        UnityEngine.AI.NavMeshAgent agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.isStopped = true;

        yield return new WaitForSeconds(FlashHoldDuration + FlashFadeDuration);

        if (enemy != null)
        {
            enemy.enabled = true;
            if (agent != null) agent.isStopped = false;
        }
    }
}
