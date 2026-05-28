using UnityEngine;
using UnityEngine.SceneManagement;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Level Exit
//  Trigger zone at the end of the level. When the player walks in AND has
//  hacked the required number of servers, loads the next scene.
// ─────────────────────────────────────────────────────────────────────────────
[RequireComponent(typeof(Collider))]
public class LevelExit : MonoBehaviour
{
    [Tooltip("Scene name to load on exit. Leave blank to just log a victory.")]
    public string nextSceneName = "MainMenu";

    public int requiredServersHacked = 3;
    public AudioClip exitSound;

    bool _triggered;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag("Player")) return;

        if (ServerHack.HackedCount < requiredServersHacked)
        {
            Debug.Log($"[LevelExit] Need {requiredServersHacked} servers hacked. Currently: {ServerHack.HackedCount}");
            return;
        }

        _triggered = true;
        if (exitSound != null) AudioSource.PlayClipAtPoint(exitSound, transform.position);
        Debug.Log("[LevelExit] All objectives complete. Loading: " + nextSceneName);

        if (!string.IsNullOrEmpty(nextSceneName))
            LoadingScreen.Load(nextSceneName);
    }
}
