using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Enemy Spawner
//
//  Drop this on any GameObject. It instantiates enemies in waves based on the
//  selected trigger mode (manual, on Start, on player trigger, or on a named
//  string signal). Used for ambushes, server lockouts, reinforcement waves.
//
//  Trigger modes
//  ─────────────
//    • Manual          — only fires when another script calls Spawn().
//    • OnStart         — fires on first frame.
//    • OnTriggerEnter  — fires when the Player walks into the attached
//                        Collider (set it as a Trigger).
//    • OnSignal        — fires when EnemySpawner.FireSignal("name") is called
//                        anywhere. Multiple spawners can listen for the same
//                        signal — handy for "alarm goes off → all spawners
//                        in the level open up".
//
//  Where it spawns
//  ───────────────
//    If `spawnPoints` is empty, spawns at this GameObject's transform.
//    Otherwise picks a random point from the list each spawn.
//    `scatterRadius` adds a random XZ offset so enemies don't pile on the
//    exact same point.
//    `snapToNavMesh` calls NavMesh.SamplePosition so the spawned enemy lands
//    on walkable geometry instead of floating / falling through floors.
//
//  Throttling
//  ──────────
//    `maxAliveAtOnce > 0` waits until enough spawned enemies are dead before
//    queueing the next one. Counts live enemies via the FPS Microgame's
//    `Unity.FPS.Game.Health` component (reflected, no asmdef dependency).
//
//  Editor preview
//  ──────────────
//    Scene-view gizmos draw a red wire sphere at each spawn point with the
//    scatter radius shown. Easy to eyeball coverage without playing.
// ─────────────────────────────────────────────────────────────────────────────
[DisallowMultipleComponent]
public class EnemySpawner : MonoBehaviour
{
    public enum TriggerMode { Manual, OnStart, OnTriggerEnter, OnSignal }

    [Header("What to spawn")]
    [Tooltip("Pool of enemy prefabs. One is picked at random per spawn. Leave a single entry for a single-type spawner.")]
    public GameObject[] enemyPrefabs;

    [Header("How many")]
    [Tooltip("Total enemies this spawner will produce in its lifetime.")]
    public int   totalToSpawn   = 3;
    [Tooltip("How many enemies per wave (spawned with a small inter-spawn delay).")]
    public int   perWave        = 1;
    [Tooltip("Delay between individual enemies within a wave (seconds).")]
    public float spawnInterval  = 0.3f;
    [Tooltip("Delay between successive waves (seconds).")]
    public float waveInterval   = 2.0f;
    [Tooltip("0 = no cap. Otherwise pause spawning while this many enemies (from this spawner) are still alive.")]
    public int   maxAliveAtOnce = 0;

    [Header("Where")]
    [Tooltip("Optional spawn-point children. If empty, spawns at this GameObject's transform.")]
    public Transform[] spawnPoints;
    [Tooltip("Random XZ offset added to the chosen spawn point (radius in metres).")]
    public float       scatterRadius = 1.5f;
    [Tooltip("Snap the spawn position to the nearest NavMesh point so enemies don't fall through floors.")]
    public bool        snapToNavMesh = true;
    public float       navMeshSnapDistance = 2.5f;

    [Header("Trigger")]
    public TriggerMode triggerMode  = TriggerMode.Manual;
    [Tooltip("Signal name to listen for (when triggerMode = OnSignal).")]
    public string      signalName   = "";
    [Tooltip("Only fire once total. Untick for spawners that re-arm.")]
    public bool        oneShot      = true;
    [Tooltip("Delay between trigger firing and the first spawn (seconds).")]
    public float       triggerDelay = 0f;

    [Header("Player detection (OnTriggerEnter mode)")]
    public string playerTag = "Player";

    [Header("Events")]
    public UnityEvent OnSpawnStarted;
    public UnityEvent OnAllEnemiesSpawned;
    public UnityEvent OnAllEnemiesDead;

    [Header("Debug / Gizmos")]
    public Color gizmoColor = new Color(1f, 0.3f, 0.3f, 0.85f);
    public bool  debugLog   = false;

    // ── Runtime state ──────────────────────────────────────────────────────
    int   _spawnedSoFar;
    bool  _hasFired;
    bool  _allSpawnedNotified;
    bool  _allDeadNotified;
    readonly List<GameObject> _alive = new List<GameObject>();

    // ── Static kill counter (sum across all EnemySpawners in scene) ────────
    //  ExitGatePanel reads this to enforce the "kill at least N before escape"
    //  requirement. Counts any spawner-spawned enemy that has been destroyed
    //  OR whose FPS-Microgame Health has dropped to <= 0.
    public static int TotalKills { get; private set; }
    public static void ResetTotalKills() => TotalKills = 0;

    static System.Reflection.FieldInfo _healthCurrentField;
    static bool _healthFieldResolved;
    static System.Reflection.FieldInfo HealthCurrentField
    {
        get
        {
            if (_healthFieldResolved) return _healthCurrentField;
            _healthFieldResolved = true;
            var healthType = System.Type.GetType("Unity.FPS.Game.Health, Unity.FPS.Game");
            if (healthType != null) _healthCurrentField = healthType.GetField("CurrentHealth");
            return _healthCurrentField;
        }
    }

    // ── Static signal bus ──────────────────────────────────────────────────
    static readonly Dictionary<string, List<EnemySpawner>> _listeners =
        new Dictionary<string, List<EnemySpawner>>();

    public static void FireSignal(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        if (!_listeners.TryGetValue(name, out var list)) return;
        // Iterate over a copy in case spawners self-unregister mid-fire.
        var snapshot = list.ToArray();
        foreach (var sp in snapshot)
        {
            if (sp == null) continue;
            sp.Trigger();
        }
        Debug.Log($"[EnemySpawner] Signal '{name}' fired → {snapshot.Length} spawner(s) triggered.");
    }

    void OnEnable()
    {
        if (triggerMode == TriggerMode.OnSignal && !string.IsNullOrEmpty(signalName))
        {
            if (!_listeners.TryGetValue(signalName, out var list))
            {
                list = new List<EnemySpawner>();
                _listeners[signalName] = list;
            }
            if (!list.Contains(this)) list.Add(this);
        }
    }

    void OnDisable()
    {
        if (triggerMode == TriggerMode.OnSignal && !string.IsNullOrEmpty(signalName))
        {
            if (_listeners.TryGetValue(signalName, out var list))
                list.Remove(this);
        }
    }

    void Start()
    {
        if (triggerMode == TriggerMode.OnStart) Trigger();
    }

    void Update()
    {
        // Detect deaths every frame. Any tracked enemy that is now null
        // (destroyed) OR whose Health.CurrentHealth has hit 0 counts as a kill.
        // We snapshot-and-remove so each kill is counted exactly once.
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            var g = _alive[i];
            if (g == null) { TotalKills++; _alive.RemoveAt(i); continue; }
            if (IsDeadByHealth(g)) { TotalKills++; _alive.RemoveAt(i); continue; }
        }
    }

    static bool IsDeadByHealth(GameObject g)
    {
        var field = HealthCurrentField;
        if (field == null) return false;   // no FPS Microgame Health → can't tell; rely on Destroy
        var healthType = field.DeclaringType;
        var comp = g.GetComponent(healthType) ?? g.GetComponentInChildren(healthType);
        if (comp == null) return false;
        try
        {
            float hp = (float)field.GetValue(comp);
            return hp <= 0.01f;
        }
        catch { return false; }
    }

    void OnTriggerEnter(Collider other)
    {
        if (triggerMode != TriggerMode.OnTriggerEnter) return;
        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag)) return;
        Trigger();
    }

    // ── Public API ─────────────────────────────────────────────────────────
    /// <summary>Fires the spawner respecting oneShot + triggerDelay.</summary>
    public void Trigger()
    {
        if (oneShot && _hasFired) return;
        _hasFired = true;
        StartCoroutine(TriggerRoutine());
    }

    /// <summary>Bypasses oneShot / delay — spawn immediately.</summary>
    public void Spawn()
    {
        StartCoroutine(SpawnLoop());
    }

    IEnumerator TriggerRoutine()
    {
        if (triggerDelay > 0f) yield return new WaitForSeconds(triggerDelay);
        OnSpawnStarted?.Invoke();
        if (debugLog) Debug.Log($"[EnemySpawner:{name}] Triggered. Will spawn {totalToSpawn} total.");
        yield return SpawnLoop();
    }

    IEnumerator SpawnLoop()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
        {
            Debug.LogWarning($"[EnemySpawner:{name}] No enemy prefabs assigned — nothing to spawn.");
            yield break;
        }

        while (_spawnedSoFar < totalToSpawn)
        {
            int waveCount = Mathf.Min(perWave, totalToSpawn - _spawnedSoFar);
            for (int i = 0; i < waveCount; i++)
            {
                // Honour the live-cap before spawning each enemy.
                if (maxAliveAtOnce > 0)
                {
                    while (CountAlive() >= maxAliveAtOnce)
                        yield return new WaitForSeconds(0.5f);
                }

                SpawnOne();

                if (spawnInterval > 0f && i < waveCount - 1)
                    yield return new WaitForSeconds(spawnInterval);
            }

            if (_spawnedSoFar >= totalToSpawn) break;
            if (waveInterval > 0f) yield return new WaitForSeconds(waveInterval);
        }

        _allSpawnedNotified = true;
        OnAllEnemiesSpawned?.Invoke();
        if (debugLog) Debug.Log($"[EnemySpawner:{name}] All {totalToSpawn} enemies spawned.");

        // Watch for all-dead and fire the event once.
        while (!_allDeadNotified && CountAlive() > 0) yield return null;
        if (!_allDeadNotified)
        {
            _allDeadNotified = true;
            OnAllEnemiesDead?.Invoke();
            if (debugLog) Debug.Log($"[EnemySpawner:{name}] All spawned enemies are dead.");
        }
    }

    void SpawnOne()
    {
        var prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        if (prefab == null) { Debug.LogWarning($"[EnemySpawner:{name}] Null entry in enemyPrefabs."); return; }

        Vector3 origin = PickSpawnOrigin();
        Vector3 jitter = RandomXZ(scatterRadius);
        Vector3 pos    = origin + jitter;

        if (snapToNavMesh && NavMesh.SamplePosition(pos, out var hit, navMeshSnapDistance, NavMesh.AllAreas))
            pos = hit.position;

        var go = Instantiate(prefab, pos, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
        _alive.Add(go);
        _spawnedSoFar++;
        if (debugLog) Debug.Log($"[EnemySpawner:{name}] Spawned {prefab.name} #{_spawnedSoFar} at {pos}.");
    }

    Vector3 PickSpawnOrigin()
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            var t = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (t != null) return t.position;
        }
        return transform.position;
    }

    static Vector3 RandomXZ(float radius)
    {
        if (radius <= 0f) return Vector3.zero;
        var c = Random.insideUnitCircle * radius;
        return new Vector3(c.x, 0f, c.y);
    }

    // ── Live count — Update() keeps _alive trimmed, so .Count is accurate ──
    int CountAlive() => _alive.Count;

    // ── Editor gizmos ──────────────────────────────────────────────────────
    void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(scatterRadius, 0.3f));
        }
        else
        {
            foreach (var t in spawnPoints)
            {
                if (t == null) continue;
                Gizmos.DrawWireSphere(t.position, Mathf.Max(scatterRadius, 0.3f));
                Gizmos.DrawLine(transform.position, t.position);
            }
        }

        // Up-tick to signal "spawner" visually
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
        Vector3 top = transform.position + Vector3.up * 1.5f;
        Gizmos.DrawLine(transform.position, top);
        Gizmos.DrawWireCube(top, new Vector3(0.3f, 0.3f, 0.3f));
    }
}
