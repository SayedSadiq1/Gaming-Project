using System.Reflection;
using UnityEngine;
using Unity.FPS.Game;

public class PlayerCheckpointRespawn : MonoBehaviour
{
    public Transform defaultSpawnPoint;

    private Vector3 checkpointPosition;
    private Quaternion checkpointRotation;

    private Health health;
    private CharacterController characterController;

    void Start()
    {
        health = GetComponent<Health>();
        characterController = GetComponent<CharacterController>();

        if (defaultSpawnPoint != null)
        {
            checkpointPosition = defaultSpawnPoint.position;
            checkpointRotation = defaultSpawnPoint.rotation;
        }
        else
        {
            checkpointPosition = transform.position;
            checkpointRotation = transform.rotation;
        }

        if (health != null)
        {
            health.OnDie += Respawn;
        }
    }

    public void SetCheckpoint(Vector3 position, Quaternion rotation)
    {
        checkpointPosition = position;
        checkpointRotation = rotation;
    }

    void Respawn()
    {
        Debug.Log("Player died. Respawning at checkpoint.");

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        transform.position = checkpointPosition;
        transform.rotation = checkpointRotation;

        if (characterController != null)
        {
            characterController.enabled = true;
        }

        ResetHealth();
    }

    void ResetHealth()
    {
        if (health == null) return;

        health.CurrentHealth = health.MaxHealth;

        FieldInfo deadField = typeof(Health).GetField(
            "m_IsDead",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        if (deadField != null)
        {
            deadField.SetValue(health, false);
        }

        health.OnHealed?.Invoke(health.MaxHealth);
    }
}