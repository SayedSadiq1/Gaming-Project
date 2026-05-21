using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    public Transform respawnPoint;
    public bool activateOnce = false;

    private bool used;

    void OnTriggerEnter(Collider other)
    {
        PlayerCheckpointRespawn playerRespawn = other.GetComponentInParent<PlayerCheckpointRespawn>();

        if (playerRespawn == null) return;
        if (activateOnce && used) return;

        used = true;

        if (respawnPoint != null)
        {
            playerRespawn.SetCheckpoint(respawnPoint.position, respawnPoint.rotation);
        }
        else
        {
            playerRespawn.SetCheckpoint(transform.position, transform.rotation);
        }

        Debug.Log("Checkpoint saved");
    }
}