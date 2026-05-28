using Unity.FPS.Gameplay;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Level2ObjectiveTrigger : MonoBehaviour
{
    public Level2ObjectiveManager manager;
    public Level2ObjectiveManager.ObjectiveStep step;
    public bool activateOnce = true;

    bool _used;

    void Awake()
    {
        var trigger = GetComponent<Collider>();
        trigger.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (activateOnce && _used) return;
        if (!IsPlayer(other)) return;

        _used = true;
        if (manager != null)
            manager.CompleteObjective(step);
    }

    static bool IsPlayer(Collider other)
    {
        if (other.CompareTag("Player")) return true;
        return other.GetComponentInParent<PlayerWeaponsManager>() != null;
    }
}
