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

    [Header("Audio")]
    public AudioClip hackSuccessSound;
    public AudioClip hackFailSound;
    public AudioClip alarmSound;
    [Range(0f, 1f)] public float audioVolume = 1f;

    [Header("Visual Feedback")]
    public Renderer indicatorRenderer;
    public Color    hackedColor = new Color(1f, 0.2f, 0.2f, 1f);
    public Light    indicatorLight;
    public GameObject hackVFX;

    [Header("Optional Keycard Requirement")]
    public bool requiresKeycard = false;
    public KeycardColor requiredColor = KeycardColor.Blue;

    enum Phase { Initial, FirstAttempt, Lockout, Unlocked, FinalAttempt, Hacked }
    Phase  _phase = Phase.Initial;
    float  _lockoutRemaining;
    float  _currentHoldTime;
    float  _maxHoldThisAttempt;
    bool   _wasHoldingSignificantly;

    bool _hacked;
    static readonly HashSet<int> _hackedIds = new HashSet<int>();

    public static int  HackedCount => _hackedIds.Count;
    public static bool IsHacked(int id) => _hackedIds.Contains(id);
    public static void ResetAll() => _hackedIds.Clear();

    void OnEnable()
    {
        if (serverId == 1) _hackedIds.Clear();
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
                Debug.Log($"[ServerHack] Server {serverId} FIRST ATTEMPT FAILED. Lockout {lockoutDuration}s started.");
                return;
            }
            // Phase == Unlocked / FinalAttempt → SUCCESS
        }

        ActuallyHack();
    }

    void ActuallyHack()
    {
        _hacked = true;
        _phase  = Phase.Hacked;
        _hackedIds.Add(serverId);

        if (indicatorRenderer != null)
        {
            var mat = indicatorRenderer.material;
            if (mat.HasProperty("_BaseColor"))     mat.SetColor("_BaseColor", hackedColor);
            if (mat.HasProperty("_Color"))         mat.SetColor("_Color",     hackedColor);
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", hackedColor * 1.5f);
        }
        if (indicatorLight != null) indicatorLight.color = hackedColor;
        if (hackVFX != null) Instantiate(hackVFX, transform.position, transform.rotation);
        PlayClip(hackSuccessSound);

        var hud = Object.FindFirstObjectByType<CombatHUD>();
        if (hud != null) hud.IncrementObjective();

        Debug.Log($"[ServerHack] Server {serverId} HACKED. Total: {_hackedIds.Count}/3");
    }

    void PlayClip(AudioClip clip)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, transform.position, audioVolume);
    }
}
