using UnityEngine;
using System.Reflection;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Player Health Regen
//
//  Wait N seconds after the last damage, then regen the player's HP back to
//  full at a configurable rate. Auto-installs onto the Player at game start
//  so we don't have to touch the Player prefab.
//
//  Uses reflection to talk to Unity.FPS.Game.Health (the FPS Microgame's
//  health component) since FPS scripts live in a separate asmdef and the
//  no-FPS-edits rule blocks direct references from this project.
// ─────────────────────────────────────────────────────────────────────────────
public class PlayerHealthRegen : MonoBehaviour
{
    [Tooltip("Seconds of no damage required before regen kicks in.")]
    public float regenDelay = 5f;

    [Tooltip("HP healed per second once regen starts. 100 HP / 2s = 50/sec.")]
    public float regenPerSecond = 50f;

    // Cached reflection handles for Unity.FPS.Game.Health
    Component        _health;
    PropertyInfo     _currentHpProp;
    FieldInfo        _maxHpField;
    MethodInfo       _healMethod;     // Heal(float)

    float _lastSeenHp = -1f;
    float _timeSinceDamage = 0f;

    // ── Auto-install on Player at game start ──────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),
                      UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                              UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        if (player.GetComponent<PlayerHealthRegen>() != null) return; // already added
        player.AddComponent<PlayerHealthRegen>();
    }

    void Start()
    {
        TryBindHealth();
    }

    void TryBindHealth()
    {
        // Find a component named "Health" (whichever assembly it lives in).
        foreach (var c in GetComponentsInChildren<Component>(true))
        {
            if (c == null) continue;
            var t = c.GetType();
            if (t.Name != "Health") continue;

            _currentHpProp = t.GetProperty("CurrentHealth");
            _maxHpField    = t.GetField("MaxHealth");
            _healMethod    = t.GetMethod("Heal", new[] { typeof(float) });
            if (_currentHpProp != null && _maxHpField != null && _healMethod != null)
            {
                _health = c;
                _lastSeenHp = ReadHp();
                Debug.Log($"[HealthRegen] Bound to {t.FullName} on {c.gameObject.name}. Start HP = {_lastSeenHp}");
                return;
            }
        }
        Debug.LogWarning("[HealthRegen] No FPS Microgame Health component found on Player.");
    }

    void Update()
    {
        if (_health == null) { TryBindHealth(); return; }

        float hp  = ReadHp();
        float max = ReadMax();

        // Damage detected → reset the regen timer
        if (hp < _lastSeenHp - 0.01f)
        {
            _timeSinceDamage = 0f;
        }
        _lastSeenHp = hp;

        // Skip regen if dead or already full
        if (hp <= 0f || hp >= max) { return; }

        // Tick the no-damage timer; once past the delay, regen
        _timeSinceDamage += Time.deltaTime;
        if (_timeSinceDamage >= regenDelay)
        {
            float healThisFrame = regenPerSecond * Time.deltaTime;
            _healMethod.Invoke(_health, new object[] { healThisFrame });
            // After Heal(), CurrentHealth is updated by Health.cs; re-read so
            // _lastSeenHp stays in sync and we don't mistake the heal for damage.
            _lastSeenHp = ReadHp();
        }
    }

    float ReadHp()
    {
        try { return (float)_currentHpProp.GetValue(_health); }
        catch { return 0f; }
    }

    float ReadMax()
    {
        try { return (float)_maxHpField.GetValue(_health); }
        catch { return 100f; }
    }
}
