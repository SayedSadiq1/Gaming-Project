using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Server Hack
//
//  Two modes:
//    • Normal (Servers 1 & 2): hold F for hackDuration seconds, done.
//    • Multi-Phase (Server 3): FAIL ATTEMPT → LOCKOUT → REAL HACK
//        1. Player holds F for `firstAttemptDuration` (5s).
//        2. "HACK FAILED" — fail sound. System locks for `lockoutDuration` (60s).
//        3. During lockout, prompt shows red countdown, hack blocked.
//        4. After lockout, player holds F for `finalHackDuration` (10s).
//        5. Success sound, server hacked, HUD ticks up.
//
//  Side effects of releasing mid-hack: fail sound plays.
// ─────────────────────────────────────────────────────────────────────────────
public class ServerHack : MonoBehaviour, IInteractable, IInteractableProgress
{
    [Header("Identity")]
    public int    serverId     = 1;
    public float  hackDuration = 10f;
    public string promptText   = "HOLD F TO HACK SERVER";

    [Header("Prerequisites")]
    [Tooltip("Server IDs that must already be hacked before this one is accessible.")]
    public int[] prerequisiteServerIds = new int[0];

    [Header("Multi-Phase Flow (Server 3 style)")]
    public bool   useMultiPhase         = false;
    public float  firstAttemptDuration  = 5f;
    public float  lockoutDuration       = 60f;
    public float  finalHackDuration     = 10f;

    [Header("Password Phase (Server 2 style)")]
    [Tooltip("If true: after the normal hold-to-hack finishes, a password panel opens. Hack only completes when the correct password is entered.")]
    public bool   usePasswordPhase      = false;
    [Tooltip("The password the player must enter — read from the sign behind the server in the prerequisite room.")]
    public string requiredPassword      = "FB-Db-Rm2!78821";
    [Tooltip("Text shown above the input field while the panel is open.")]
    public string passwordTitle         = "PLEASE INSERT DATABASE PASSWORD";
    [Tooltip("Hint shown above the title (small text). Tells the player where to find the password.")]
    public string passwordHint          = "Hint: password on the back of the servers in room 1";

    [Header("Audio")]
    public AudioClip hackSuccessSound;
    public AudioClip hackFailSound;
    public AudioClip alarmSound;
    [Tooltip("Looping alarm that parents to the Player so the player can hear it anywhere.")]
    public AudioClip alarmLoopSound;
    [Range(0f, 1f)] public float audioVolume = 1f;
    [Range(0f, 1f)] public float alarmLoopVolume = 0.7f;
    [Tooltip("If true, the alarm KEEPS PLAYING after this server is hacked. Only the ExitGatePanel (or a manual ServerHack.StopAllAlarmsForExit call) can silence it. Used for Server 3.")]
    public bool      keepAlarmUntilExit = false;

    [Header("Hacked Background Music (Server 3)")]
    [Tooltip("Music clip that fades in when this server is successfully hacked. Designed to play under the alarm as background tension music. Leave empty to disable.")]
    public AudioClip hackedMusic;
    [Range(0f, 1f)] public float hackedMusicVolume = 0.2f;
    [Tooltip("Seconds to fade the music from 0 → target volume.")]
    public float    hackedMusicFadeIn = 2f;
    [Tooltip("If true, the music keeps playing even after the alarm is silenced (until the scene ends). Recommended ON for the escape sequence.")]
    public bool     hackedMusicPersists = true;

    [Header("Enemy Spawner Signal")]
    [Tooltip("When the alarm fires (first failed attempt), broadcast this signal so any EnemySpawner with matching signalName starts spawning waves.")]
    public string spawnerSignalOnAlarm = "";

    [Header("Visual Feedback")]
    public Renderer indicatorRenderer;
    public Color    hackedColor = new Color(1f, 0.2f, 0.2f, 1f);
    public Light    indicatorLight;
    public GameObject hackVFX;
    [Tooltip("Parent of all the server rack meshes for this room. Every renderer under it gets tinted red when hacked.")]
    public Transform hackedServersGroup;
    [Tooltip("Emission intensity multiplier when tinting the server group.")]
    public float     hackedEmissionBoost = 2.0f;

    [Header("Optional Keycard Requirement")]
    public bool requiresKeycard = false;
    public KeycardColor requiredColor = KeycardColor.Blue;

    enum Phase { Initial, FirstAttempt, Lockout, Unlocked, FinalAttempt, AwaitingPassword, Hacked }
    Phase  _phase = Phase.Initial;
    float  _lockoutRemaining;
    float  _currentHoldTime;
    float  _maxHoldThisAttempt;
    bool   _wasHoldingSignificantly;
    bool   _passwordPanelOpen;

    bool _hacked;
    AudioSource _alarmLoopSource;   // child of the Player while alarm is active
    static AudioSource _hackedMusicSource;  // 2D background music after Server 3 hack
    static readonly HashSet<int> _hackedIds = new HashSet<int>();

    public static int  HackedCount => _hackedIds.Count;
    public static bool IsHacked(int id) => _hackedIds.Contains(id);
    public static void ResetAll() => _hackedIds.Clear();

    void OnEnable()
    {
        if (serverId == 1)
        {
            _hackedIds.Clear();
            EnemySpawner.ResetTotalKills();   // fresh scene → fresh kill count
        }
    }

    void Update()
    {
        // Tick lockout countdown
        if (_phase == Phase.Lockout)
        {
            _lockoutRemaining -= Time.deltaTime;
            if (_lockoutRemaining <= 0f)
            {
                _lockoutRemaining = 0f;
                _phase = Phase.Unlocked;
                _maxHoldThisAttempt = 0f;
                _wasHoldingSignificantly = false;
                Debug.Log("[ServerHack] Lockout finished. System available again.");
            }
        }
    }

    // ── Dynamic prompt text ─────────────────────────────────────────────────
    public string PromptText
    {
        get
        {
            if (_hacked) return "HACKED";
            switch (_phase)
            {
                case Phase.Lockout:
                    return $"INTRUDER DETECTED — SYSTEM LOCKED ({Mathf.CeilToInt(_lockoutRemaining)}s)";
                case Phase.AwaitingPassword:
                    return "PRESS F TO ENTER DATABASE PASSWORD";
                default:
                    return promptText;
            }
        }
    }

    // ── Dynamic hold duration per phase ─────────────────────────────────────
    public float HoldDuration
    {
        get
        {
            // Password panel re-open is a press-once interaction, not a hold.
            if (_phase == Phase.AwaitingPassword) return 0f;
            if (!useMultiPhase) return hackDuration;
            switch (_phase)
            {
                case Phase.Initial:
                case Phase.FirstAttempt: return firstAttemptDuration;
                case Phase.Unlocked:
                case Phase.FinalAttempt: return finalHackDuration;
                default:                 return finalHackDuration;
            }
        }
    }

    // ── Can interact gate ───────────────────────────────────────────────────
    public bool CanInteract(GameObject interactor, out string reason)
    {
        reason = "";
        if (_hacked) { reason = "Already hacked"; return false; }

        // While the password panel is open, block the interactor so F-presses
        // don't keep re-opening it / fire the underlying hold.
        if (_passwordPanelOpen) { reason = "ENTERING PASSWORD..."; return false; }

        foreach (int prereq in prerequisiteServerIds)
        {
            if (!_hackedIds.Contains(prereq))
            {
                reason = $"ACCESS DENIED — HACK SERVER {prereq} FIRST";
                return false;
            }
        }

        if (useMultiPhase && _phase == Phase.Lockout)
        {
            reason = $"INTRUDER DETECTED — SYSTEM LOCKED ({Mathf.CeilToInt(_lockoutRemaining)}s)";
            return false;
        }

        if (requiresKeycard)
        {
            var inv = interactor.GetComponent<KeycardInventory>();
            if (inv == null) inv = interactor.GetComponentInParent<KeycardInventory>();
            if (inv == null || !inv.HasKeycard(requiredColor))
            {
                reason = "Requires " + requiredColor + " Keycard";
                return false;
            }
        }
        return true;
    }

    // ── Hold progress: track phase transitions + mid-release fail sound ────
    public void OnHoldProgress(GameObject interactor, float currentTime)
    {
        _currentHoldTime = currentTime;

        bool isHolding = currentTime > 0.5f;
        if (isHolding && currentTime > _maxHoldThisAttempt) _maxHoldThisAttempt = currentTime;

        // Detect mid-release: held > 1s then dropped to 0, AND hack didn't complete
        if (_wasHoldingSignificantly && !isHolding && _phase != Phase.Lockout && _phase != Phase.Hacked)
        {
            if (_maxHoldThisAttempt > 1.0f && _maxHoldThisAttempt < HoldDuration - 0.2f)
            {
                PlayClip(hackFailSound);
                Debug.Log($"[ServerHack] Hack on server {serverId} interrupted (released at {_maxHoldThisAttempt:F1}s).");
            }
            _maxHoldThisAttempt = 0f;
        }
        _wasHoldingSignificantly = isHolding;

        // Phase transitions when starting / stopping a hold
        if (useMultiPhase)
        {
            if (_phase == Phase.Initial  && currentTime > 0.01f) _phase = Phase.FirstAttempt;
            else if (_phase == Phase.Unlocked && currentTime > 0.01f) _phase = Phase.FinalAttempt;
            else if (_phase == Phase.FirstAttempt && currentTime <= 0.01f) _phase = Phase.Initial;
            else if (_phase == Phase.FinalAttempt && currentTime <= 0.01f) _phase = Phase.Unlocked;
        }
    }

    // ── Hold complete ──────────────────────────────────────────────────────
    public void OnInteract(GameObject interactor)
    {
        if (_hacked) return;

        // Password-phase reopen: player pressed F while we were already awaiting
        // password input. Just re-open the panel.
        if (_phase == Phase.AwaitingPassword)
        {
            OpenPasswordPanel();
            return;
        }

        if (useMultiPhase)
        {
            if (_phase == Phase.FirstAttempt || _phase == Phase.Initial)
            {
                // First attempt → FAIL, trigger lockout
                _phase = Phase.Lockout;
                _lockoutRemaining = lockoutDuration;
                _maxHoldThisAttempt = 0f;
                _wasHoldingSignificantly = false;
                PlayClip(hackFailSound);
                PlayClip(alarmSound);
                StartAlarmLoop();
                // Background music starts WITH the alarm (so the player hears both together).
                if (hackedMusic != null) StartHackedMusicFadeIn();
                // Broadcast to any EnemySpawner listening — kicks off the reinforcement waves.
                if (!string.IsNullOrEmpty(spawnerSignalOnAlarm))
                {
                    EnemySpawner.FireSignal(spawnerSignalOnAlarm);
                    Debug.Log($"[ServerHack] Fired spawner signal '{spawnerSignalOnAlarm}'.");
                }
                Debug.Log($"[ServerHack] Server {serverId} FIRST ATTEMPT FAILED. Lockout {lockoutDuration}s started.");
                return;
            }
            // Phase == Unlocked / FinalAttempt → SUCCESS
        }

        // Password-phase Server 2 — first hold finished, gate on password entry.
        if (usePasswordPhase)
        {
            _phase = Phase.AwaitingPassword;
            Debug.Log($"[ServerHack] Server {serverId} stage 1 complete — awaiting password.");
            OpenPasswordPanel();
            return;
        }

        ActuallyHack();
    }

    // ── Password panel ─────────────────────────────────────────────────────
    void OpenPasswordPanel()
    {
        var panel = Object.FindFirstObjectByType<PasswordEntryUI>();
        if (panel == null)
        {
            Debug.LogWarning("[ServerHack] No PasswordEntryUI in scene — cannot prompt for password. " +
                             "Run 'Facility Breach → Setup Level 3 Interactions' to build it.");
            // Fail-safe: skip the password gate so the level remains beatable.
            ActuallyHack();
            return;
        }

        _passwordPanelOpen = true;
        panel.Show(passwordTitle, passwordHint, requiredPassword, OnPasswordResult);
    }

    void OnPasswordResult(bool success)
    {
        _passwordPanelOpen = false;
        if (success)
        {
            Debug.Log($"[ServerHack] Server {serverId} password accepted.");
            ActuallyHack();
        }
        else
        {
            Debug.Log($"[ServerHack] Server {serverId} password panel cancelled — staying in AwaitingPassword phase.");
            PlayClip(hackFailSound);
        }
    }

    void ActuallyHack()
    {
        _hacked = true;
        _phase  = Phase.Hacked;
        _hackedIds.Add(serverId);

        if (indicatorRenderer != null)
        {
            TintRenderer(indicatorRenderer, hackedColor, 1.5f);
        }
        if (hackedServersGroup != null)
        {
            var rends = hackedServersGroup.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends) TintRenderer(r, hackedColor, hackedEmissionBoost);
            Debug.Log($"[ServerHack] Tinted {rends.Length} renderers under '{hackedServersGroup.name}' red.");
        }
        if (indicatorLight != null) indicatorLight.color = hackedColor;
        if (hackVFX != null) Instantiate(hackVFX, transform.position, transform.rotation);
        PlayClip(hackSuccessSound);
        // Server 3 keeps the alarm screaming until the player actually escapes.
        if (!keepAlarmUntilExit) StopAlarmLoop();
        else Debug.Log($"[ServerHack] Server {serverId} hacked — alarm KEPT ALIVE until exit (keepAlarmUntilExit=true).");

        // Server 3 → fade in background music under the alarm
        if (hackedMusic != null)
        {
            StartHackedMusicFadeIn();
        }
        else
        {
            Debug.Log($"[ServerHack] Server {serverId} hacked but no Hacked Music assigned — assign the MP3 in the inspector on this ServerHack component if you want background music.");
        }

        var hud = Object.FindFirstObjectByType<CombatHUD>();
        if (hud != null) hud.IncrementObjective();

        Debug.Log($"[ServerHack] Server {serverId} HACKED. Total: {_hackedIds.Count}/3");
    }

    void PlayClip(AudioClip clip)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, transform.position, audioVolume);
    }

    static void TintRenderer(Renderer r, Color color, float emissionBoost)
    {
        if (r == null) return;
        // Use .materials so each renderer gets its own instance (no shared-mat leak).
        var mats = r.materials;
        for (int i = 0; i < mats.Length; i++)
        {
            var mat = mats[i];
            if (mat == null) continue;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color); // URP
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     color); // Standard
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * emissionBoost);
            }
        }
        r.materials = mats;
    }

    // ── Looping alarm parented to the Player so it follows them anywhere ───
    void StartAlarmLoop()
    {
        if (alarmLoopSound == null) return;
        if (_alarmLoopSource != null) return;   // already playing

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[ServerHack] No Player tagged GameObject — alarm loop won't follow.");
            // Fall back to a world-positioned loop at the server.
            var hostGO = new GameObject("ServerAlarmLoop");
            hostGO.transform.SetParent(transform, false);
            _alarmLoopSource = hostGO.AddComponent<AudioSource>();
        }
        else
        {
            var hostGO = new GameObject("ServerAlarmLoop");
            hostGO.transform.SetParent(player.transform, false);
            hostGO.transform.localPosition = Vector3.zero;
            _alarmLoopSource = hostGO.AddComponent<AudioSource>();
            _alarmLoopSource.spatialBlend = 0f;   // 2D — heard anywhere in the level
        }

        _alarmLoopSource.clip   = alarmLoopSound;
        _alarmLoopSource.loop   = true;
        _alarmLoopSource.volume = alarmLoopVolume;
        _alarmLoopSource.playOnAwake = false;
        _alarmLoopSource.Play();
        Debug.Log("[ServerHack] Alarm loop started on player.");
    }

    // Auto-stop the background music whenever we leave Level-3 (main menu,
    // next level, restart, death screen, etc.). Wired up at game start so it
    // always runs — independent of any specific ServerHack instance lifetime.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void HookSceneCleanup()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnAnySceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnAnySceneLoaded;
    }

    static void OnAnySceneLoaded(UnityEngine.SceneManagement.Scene scene,
                                  UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // If we just loaded a scene that isn't Level-3, kill the hacked music.
        if (scene.name != "Level-3" && scene.name != "Level3")
        {
            if (_hackedMusicSource != null)
            {
                StopHackedMusic();
                Debug.Log($"[ServerHack] Left Level-3 (now in '{scene.name}') — background music stopped.");
            }
            // Also clear the hacked-server tracking so a fresh run resets state.
            _hackedIds.Clear();
        }
    }

    // ── Background music (faded in when Server 3 is hacked) ────────────────
    void StartHackedMusicFadeIn()
    {
        // Clean up any stale reference (e.g., from a previous Play session in the editor)
        if (_hackedMusicSource != null)
        {
            try { if (_hackedMusicSource.isPlaying) return; } catch { _hackedMusicSource = null; }
        }

        var host = new GameObject("ServerHack_BackgroundMusic");
        if (hackedMusicPersists) Object.DontDestroyOnLoad(host);

        _hackedMusicSource = host.AddComponent<AudioSource>();
        _hackedMusicSource.clip         = hackedMusic;
        _hackedMusicSource.loop         = true;
        _hackedMusicSource.spatialBlend = 0f;       // 2D
        _hackedMusicSource.volume       = 0f;
        _hackedMusicSource.playOnAwake  = false;
        _hackedMusicSource.outputAudioMixerGroup = null; // ensure no muted mixer routing
        _hackedMusicSource.bypassEffects  = true;
        _hackedMusicSource.bypassListenerEffects = true;
        _hackedMusicSource.ignoreListenerPause   = true;
        _hackedMusicSource.Play();

        StartCoroutine(FadeMusicIn(_hackedMusicSource, hackedMusicVolume, hackedMusicFadeIn));
        Debug.Log($"[ServerHack] ✓ Background music STARTED — clip='{hackedMusic.name}', target {hackedMusicVolume:0.00}, fade {hackedMusicFadeIn:0.0}s, isPlaying={_hackedMusicSource.isPlaying}");
    }

    System.Collections.IEnumerator FadeMusicIn(AudioSource src, float target, float duration)
    {
        if (duration <= 0f) { src.volume = target; yield break; }
        float t = 0f;
        while (t < duration && src != null)
        {
            t += Time.unscaledDeltaTime;
            src.volume = Mathf.Lerp(0f, target, t / duration);
            yield return null;
        }
        if (src != null) src.volume = target;
    }

    public static void StopHackedMusic()
    {
        if (_hackedMusicSource == null) return;
        _hackedMusicSource.Stop();
        Destroy(_hackedMusicSource.gameObject);
        _hackedMusicSource = null;
    }

    void StopAlarmLoop()
    {
        if (_alarmLoopSource == null) return;
        _alarmLoopSource.Stop();
        Destroy(_alarmLoopSource.gameObject);
        _alarmLoopSource = null;
        Debug.Log("[ServerHack] Alarm loop stopped.");
    }

    void OnDestroy()
    {
        // If we want the alarm to persist (Server 3), DON'T stop it here. The
        // AudioSource is parented to the Player so it survives the server's
        // destruction. The scene unload will clean it up when the player escapes.
        if (!keepAlarmUntilExit) StopAlarmLoop();
    }

    void OnDisable()
    {
        if (!keepAlarmUntilExit) StopAlarmLoop();
    }

    // ── Stop every alarm in the level — called by ExitGatePanel on escape ──
    public static void StopAllAlarmsForExit()
    {
        // Find every ServerAlarmLoop GameObject (parented to the Player) and
        // destroy them. More reliable than chasing ServerHack refs since
        // keepAlarmUntilExit may have outlived the ServerHack itself.
        int count = 0;
        foreach (var obj in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
        {
            if (obj == null) continue;
            if (obj.gameObject.name != "ServerAlarmLoop") continue;
            obj.Stop();
            Object.Destroy(obj.gameObject);
            count++;
        }
        // Also ask any still-alive ServerHack to drop its reference so it
        // doesn't try to stop a destroyed source later.
        foreach (var sh in Object.FindObjectsByType<ServerHack>(FindObjectsSortMode.None))
            sh._alarmLoopSource = null;
        if (count > 0) Debug.Log($"[ServerHack] StopAllAlarmsForExit silenced {count} alarm loop(s).");
    }
}
