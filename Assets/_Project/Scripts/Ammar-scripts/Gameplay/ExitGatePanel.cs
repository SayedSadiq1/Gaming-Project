using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Exit Gate Panel (Level 3 → Level 4)
//
//  Acts like KeycardPanel but enforces three conditions before unlocking:
//    1. All 3 servers hacked  (ServerHack.HackedCount >= requiredServers)
//    2. All enemies dead      (no live Health component on a non-Player object)
//    3. Player holds the matching keycard (blue)
//
//  Dynamic prompt shows which requirement is still missing.
//  On success: slides the two doors apart, fades to black, prints
//  "MISSION COMPLETE" then loads `nextSceneName` (or just holds the
//  black screen if the name is empty / scene doesn't exist).
// ─────────────────────────────────────────────────────────────────────────────
public class ExitGatePanel : MonoBehaviour, IInteractable
{
    [Header("Conditions")]
    public int           requiredServers  = 3;
    [Tooltip("Minimum spawner-spawned enemies the player must kill before this gate unlocks. Read from EnemySpawner.TotalKills.")]
    public int           requiredKills    = 30;
    public bool          requiresKeycard  = true;
    public KeycardColor  requiredColor    = KeycardColor.Blue;
    [Tooltip("ID for the team's keycard system (ChainDoorKeycardHolder). Either this OR the legacy colour matches to unlock.")]
    public string        keycardID        = "blue_keycard";

    [Header("Doors")]
    public Transform leftDoor;
    public Vector3   leftOpenOffset  = new Vector3(-2.75f, 0f, 0f);
    public Transform rightDoor;
    public Vector3   rightOpenOffset = new Vector3( 2.75f, 0f, 0f);
    public float     slideDuration   = 1.5f;

    [Header("Prompts")]
    public string promptReady          = "PRESS F TO EXIT FACILITY";
    public string promptMissingServers = "HACK ALL SERVERS FIRST ({0}/{1})";
    public string promptNotEnoughKills = "ELIMINATE HOSTILES ({0}/{1} KILLED)";
    public string promptNoKeycard      = "REQUIRES BLUE KEYCARD";

    [Header("Cutscene")]
    public string nextSceneName        = "Level4";
    public float  fadeInDelay          = 0.6f;
    public float  fadeDuration         = 1.8f;
    public float  holdBlackBeforeLoad  = 1.5f;
    public string completeText         = "MISSION COMPLETE";
    public string completeSubText      = "To be continued...";

    [Header("Audio")]
    public AudioClip openSound;
    public AudioClip lockedSound;

    public string PromptText  { get; private set; } = "LOCKED";
    public float  HoldDuration => 0f;   // press-once

    Vector3 _leftClosedLocal, _rightClosedLocal;
    bool _captured, _opening, _opened, _cutsceneStarted;

    void Awake() { CaptureClosed(); }
    void Start() { CaptureClosed(); }

    void CaptureClosed()
    {
        if (_captured) return;
        if (leftDoor  != null) _leftClosedLocal  = leftDoor.localPosition;
        if (rightDoor != null) _rightClosedLocal = rightDoor.localPosition;
        _captured = true;
    }

    public bool CanInteract(GameObject interactor, out string reason)
    {
        if (_opened || _opening) { reason = "Already open"; return false; }

        // Condition 1 — servers
        int hacked = ServerHack.HackedCount;
        if (hacked < requiredServers)
        {
            PromptText = string.Format(promptMissingServers, hacked, requiredServers);
            reason = PromptText;
            return false;
        }

        // Condition 2 — kills (cumulative, from EnemySpawner.TotalKills)
        if (requiredKills > 0)
        {
            int kills = EnemySpawner.TotalKills;
            if (kills < requiredKills)
            {
                PromptText = string.Format(promptNotEnoughKills, kills, requiredKills);
                reason = PromptText;
                return false;
            }
        }

        // Condition 3 — keycard (accept EITHER team's string-ID system OR legacy colour)
        if (requiresKeycard)
        {
            bool hasKey = false;

            // Team system first
            if (!string.IsNullOrEmpty(keycardID))
            {
                var holder = interactor.GetComponent<ChainDoorKeycardHolder>()
                          ?? interactor.GetComponentInParent<ChainDoorKeycardHolder>();
                if (holder != null && holder.HasKeycard(keycardID)) hasKey = true;
            }

            // Legacy fallback
            if (!hasKey)
            {
                var inv = interactor.GetComponent<KeycardInventory>();
                if (inv == null) inv = interactor.GetComponentInParent<KeycardInventory>();
                if (inv != null && inv.HasKeycard(requiredColor)) hasKey = true;
            }

            if (!hasKey)
            {
                PromptText = promptNoKeycard;
                reason = PromptText;
                return false;
            }
        }

        PromptText = promptReady;
        reason = "";
        return true;
    }

    public void OnInteract(GameObject interactor)
    {
        if (_opened || _opening) return;
        _opening = true;
        if (openSound != null) AudioSource.PlayClipAtPoint(openSound, transform.position);

        // Player has escaped successfully — silence the Server 3 alarm.
        ServerHack.StopAllAlarmsForExit();

        // Note: the extract checkpoint save was intentionally removed.
        // Saving now only happens once — on level entry (SaveEntryCheckpoint).

        StartCoroutine(SlideRoutine());
    }

    IEnumerator SlideRoutine()
    {
        CaptureClosed();
        Vector3 lTarget = _leftClosedLocal  + leftOpenOffset;
        Vector3 rTarget = _rightClosedLocal + rightOpenOffset;

        float t = 0f;
        while (t < slideDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / slideDuration);
            if (leftDoor  != null) leftDoor.localPosition  = Vector3.Lerp(_leftClosedLocal,  lTarget, k);
            if (rightDoor != null) rightDoor.localPosition = Vector3.Lerp(_rightClosedLocal, rTarget, k);
            yield return null;
        }
        if (leftDoor  != null) leftDoor.localPosition  = lTarget;
        if (rightDoor != null) rightDoor.localPosition = rTarget;
        _opened = true;
        _opening = false;

        if (!_cutsceneStarted) StartCoroutine(CutsceneRoutine());
    }

    IEnumerator CutsceneRoutine()
    {
        _cutsceneStarted = true;
        if (fadeInDelay > 0f) yield return new WaitForSeconds(fadeInDelay);

        // If a LabExplosionCutscene exists in the scene, run it. It now drives
        // the Mission Complete screen (with NEXT LEVEL / MAIN MENU buttons) on
        // its own, so we don't auto-load the next scene anymore — the buttons do.
        var lab = Object.FindFirstObjectByType<LabExplosionCutscene>();
        if (lab != null)
        {
            // Pass our next scene name through so the buttons know where to go.
            lab.nextSceneName = nextSceneName;
            yield return lab.Play();
        }
        else
        {
            // Fallback if someone trips the gate without the cinematic wired —
            // skip straight to the Mission Complete screen.
            MissionCompleteScreen.Show(completeText, completeSubText, nextSceneName, fadeDuration);
        }
        // Done. The Mission Complete screen owns the transition from here.
    }
}
