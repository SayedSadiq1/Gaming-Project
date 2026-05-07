using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.FPS.Game;

namespace Aegis.GrenadeSystem.HiEx {
public class DamageHandler : MonoBehaviour
{
    // Bridge to project Health component
    Health m_Health;

    void Awake()
    {
        m_Health = GetComponent<Health>();
        if (m_Health == null) m_Health = GetComponentInParent<Health>();
    }

    public void ApplyDamage(float dam)
    {
        if (m_Health != null)
        {
            m_Health.TakeDamage(dam, gameObject);
            Debug.Log(gameObject.name + " took " + dam + " Damage from the Grenade (via Health bridge)");
        }
        else
        {
            // Fallback for objects without the project's Health component
            Debug.LogWarning(gameObject.name + " took " + dam + " Damage but has no Health component.");
        }
    }
}
}
