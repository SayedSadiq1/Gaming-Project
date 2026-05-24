using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Lab Explosion Cinematic (3-camera version)
//
//  Player-placed cameras drive the cinematic. After the exit panel unlocks:
//    1. Freezes the player (input + viewmodel + main cam disabled)
//    2. For each of the 3 pre-placed cameras (camera room 1/2/3):
//         • Switch to that camera
//         • Spawn the grenade explosion at the matching Servers group
//         • Hold for `secondsPerShot`, with camera shake on the boom
//    3. Triggers FadeToBlack with stats (SCORE / KILLS / TIME / OBJECTIVES)
//
//  All 3 cameras MUST be present in the scene before Play. They are found by
//  name OR wired via inspector. Auto-disabled at start so they don't override
//  the player camera during normal gameplay.
// ─────────────────────────────────────────────────────────────────────────────
public class LabExplosionCutscene : MonoBehaviour
{
    [System.Serializable]
    public class RoomShot
    {
        [Tooltip("Pre-placed marker GameObject in the scene (e.g. 'camera room 1'). " +
                 "Only its POSITION matters — the camera auto-aims at the serversGroup.")]
        public Transform cameraMarker;
        [Tooltip("The Servers 1/2/3 group whose centre the camera looks at + explosions spawn around.")]
        public Transform serversGroup;
        [Tooltip("How many extra secondary blasts after the primary in this room.")]
        public int       secondaryBlasts = 2;
        [Tooltip("Field of view for this shot (0 = use the default).")]
        public float     fieldOfView = 60f;
        [Tooltip("Look-at offset added to the serversGroup centre. " +
                 "Use this to nudge the camera's target up/down/sideways if the aim is off.")]
        public Vector3   lookAtOffset = Vector3.zero;
    }

    [Header("Room Shots (one per camera)")]
    public List<RoomShot> roomShots = new();

    [Header("Explosion VFX/Audio")]
    public GameObject explosionPrefab;
    public AudioClip  explosionSoundOverride;
    [Range(0f, 1f)] public float explosionVolume = 1f;

    [Header("Timing")]
    public float secondsPerShot       = 2.5f;
    public float secondaryBlastDelay  = 0.25f;
    public float holdAfterLastShot    = 0.6f;
    public float fadeDuration         = 1.8f;
    public float holdBlackBeforeLoad  = 4.0f;
    public string completeText        = "MISSION COMPLETE";
    public string completeSubText     = "Underground Labs cleared";

    [Header("Next Level")]
    [Tooltip("Scene loaded when the player clicks NEXT LEVEL on the Mission Complete screen. ExitGatePanel auto-wires this in the builder.")]
    public string nextSceneName       = "Level4";

    [Header("Camera Shake")]
    public float shakeAmplitude = 0.25f;
    public float shakeDuration  = 0.4f;

    Camera _playerCam;
    AudioListener _playerListener;
    readonly List<Behaviour> _disabledOnPlayer = new();
    Camera _cinematicCam;       // the one camera we spawn and reuse for each shot
    AudioListener _cinematicListener;

    public IEnumerator Play()
    {
        FreezePlayer();
        SpawnCinematicCamera();

        for (int i = 0; i < roomShots.Count; i++)
        {
            var shot = roomShots[i];
            if (shot == null || shot.cameraMarker == null) continue;
            yield return PlayOneRoomShot(shot, i);
        }

        if (holdAfterLastShot > 0f) yield return new WaitForSeconds(holdAfterLastShot);

        // Show the styled Mission Complete screen with buttons (NEXT LEVEL / MAIN MENU).
        // Replaces the old FadeToBlack text-only ending.
        string stats = BuildStatsSubtitle();
        MissionCompleteScreen.Show(completeText, stats, nextSceneName, fadeDuration);

        // Stay alive briefly while the screen fades in — then yield forever, the
        // buttons own the next transition.
        yield return new WaitForSeconds(fadeDuration + 0.4f);
    }

    IEnumerator PlayOneRoomShot(RoomShot shot, int idx)
    {
        // Snap the single cinematic camera to this room's marker and aim it
        // at the server group (so the marker's own rotation doesn't matter).
        SnapCameraToShot(shot);

        // Primary explosion at room centre
        Vector3 centre = shot.serversGroup != null ? shot.serversGroup.position : shot.cameraMarker.position;
        SpawnExplosion(centre);
        StartCoroutine(ShakeCamera(_cinematicCam));

        // Secondaries scattered around the group
        Bounds bounds = shot.serversGroup != null
            ? ComputeGroupBounds(shot.serversGroup)
            : new Bounds(centre, Vector3.one * 4f);

        for (int s = 0; s < shot.secondaryBlasts; s++)
        {
            yield return new WaitForSeconds(secondaryBlastDelay);
            Vector3 pos = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                bounds.center.y + Random.Range(-0.5f, 1f),
                Random.Range(bounds.min.z, bounds.max.z));
            SpawnExplosion(pos);
            StartCoroutine(ShakeCamera(_cinematicCam));
        }

        // Hold on this camera for the remainder of secondsPerShot
        float spent = shot.secondaryBlasts * secondaryBlastDelay;
        float remaining = Mathf.Max(0f, secondsPerShot - spent);
        yield return new WaitForSeconds(remaining);
    }

    void SpawnCinematicCamera()
    {
        if (_cinematicCam != null) return;
        var go = new GameObject("LabExplosionCam_Runtime");
        // Don't parent to `this` — markers are world-space and we want the
        // camera to inherit absolutely nothing from anywhere.
        _cinematicCam = go.AddComponent<Camera>();
        _cinematicCam.depth = 100;
        _cinematicCam.clearFlags = CameraClearFlags.Skybox;
        _cinematicCam.fieldOfView = 60f;
        _cinematicListener = go.AddComponent<AudioListener>();
    }

    void SnapCameraToShot(RoomShot shot)
    {
        if (_cinematicCam == null || shot == null || shot.cameraMarker == null) return;

        // Camera position: the marker.
        _cinematicCam.transform.position = shot.cameraMarker.position;

        // Aim: look at the server group centre (+ optional offset). Falls back
        // to marker.forward if no group is wired.
        if (shot.serversGroup != null)
        {
            Vector3 target = shot.serversGroup.position + shot.lookAtOffset;
            Vector3 dir    = target - shot.cameraMarker.position;
            if (dir.sqrMagnitude > 0.0001f)
                _cinematicCam.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            else
                _cinematicCam.transform.rotation = Quaternion.identity;
        }
        else
        {
            _cinematicCam.transform.rotation = shot.cameraMarker.rotation;
        }

        if (shot.fieldOfView > 1f) _cinematicCam.fieldOfView = shot.fieldOfView;
    }

    // Editor preview — draws a yellow line from each marker to its server group
    // so you can see exactly where each camera will aim. Visible in Scene view.
    void OnDrawGizmos()
    {
        if (roomShots == null) return;
        foreach (var shot in roomShots)
        {
            if (shot == null || shot.cameraMarker == null) continue;
            // Marker sphere
            Gizmos.color = new Color(0f, 0.78f, 1f, 0.9f);   // cyan
            Gizmos.DrawWireSphere(shot.cameraMarker.position, 0.4f);
            // Aim line
            if (shot.serversGroup != null)
            {
                Vector3 target = shot.serversGroup.position + shot.lookAtOffset;
                Gizmos.color = new Color(1f, 0.84f, 0f, 0.9f); // gold
                Gizmos.DrawLine(shot.cameraMarker.position, target);
                Gizmos.DrawWireSphere(target, 0.3f);
            }
        }
    }

    void SpawnExplosion(Vector3 pos)
    {
        if (explosionPrefab == null) return;
        var fx = Instantiate(explosionPrefab, pos, Quaternion.identity);
        Destroy(fx, 6f);
        if (explosionSoundOverride != null)
            AudioSource.PlayClipAtPoint(explosionSoundOverride, pos, explosionVolume);
    }

    Bounds ComputeGroupBounds(Transform group)
    {
        var rends = group.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) return new Bounds(group.position, Vector3.one * 3f);
        var b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }

    // ── Player freeze ───────────────────────────────────────────────────────
    void FreezePlayer()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) { Debug.LogWarning("[LabExplosion] No Player tag."); return; }

        foreach (var b in player.GetComponentsInChildren<Behaviour>(true))
        {
            if (b == null || !b.enabled) continue;
            if (b is MonoBehaviour)
            {
                _disabledOnPlayer.Add(b);
                b.enabled = false;
            }
        }

        _playerCam = player.GetComponentInChildren<Camera>(true);
        if (_playerCam != null) _playerCam.enabled = false;
        _playerListener = player.GetComponentInChildren<AudioListener>(true);
        if (_playerListener != null) _playerListener.enabled = false;

        // Hide viewmodel weapon
        foreach (var rend in player.GetComponentsInChildren<Renderer>(true))
        {
            var p = rend.transform.parent;
            while (p != null)
            {
                if (p.name.IndexOf("Weapon", System.StringComparison.OrdinalIgnoreCase) >= 0)
                { rend.enabled = false; break; }
                p = p.parent;
            }
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;
    }

    IEnumerator ShakeCamera(Camera cam)
    {
        if (cam == null) yield break;
        var basePos = cam.transform.localPosition;
        float t = 0f;
        while (t < shakeDuration)
        {
            t += Time.deltaTime;
            float fall = 1f - (t / shakeDuration);
            cam.transform.localPosition = basePos +
                (Vector3)Random.insideUnitCircle * (shakeAmplitude * fall);
            yield return null;
        }
        cam.transform.localPosition = basePos;
    }

    // ── Build a "SCORE / KILLS / TIME / OBJECTIVES" stats string ───────────
    string BuildStatsSubtitle()
    {
        int score = 0, kills = 0, objectives = 0;
        var smType = System.Type.GetType("ScoreManager, Assembly-CSharp");
        if (smType != null)
        {
            var instProp = smType.GetProperty("Instance");
            var inst     = instProp != null ? instProp.GetValue(null) : null;
            if (inst != null)
            {
                var sProp = smType.GetProperty("Score");
                var kProp = smType.GetProperty("Kills");
                if (sProp != null) score = (int)sProp.GetValue(inst);
                if (kProp != null) kills = (int)kProp.GetValue(inst);
            }
        }

        // Server objectives — read static HackedCount from ServerHack
        objectives = ServerHack.HackedCount;

        float t = Time.timeSinceLevelLoad;
        int mm = Mathf.FloorToInt(t / 60f);
        int ss = Mathf.FloorToInt(t % 60f);
        string time = $"{mm:00}:{ss:00}";

        // Rating: rough D-S based on score (matches GDD §8 idea)
        char rating =
            score >= 4000 ? 'S' :
            score >= 2500 ? 'A' :
            score >= 1500 ? 'B' :
            score >=  500 ? 'C' : 'D';

        return $"<size=42>Rating: <b>{rating}</b></size>\n\n" +
               $"SCORE  <b>{score}</b>\n" +
               $"KILLS  <b>{kills}</b>\n" +
               $"OBJECTIVES  <b>{objectives}/3</b>\n" +
               $"TIME  <b>{time}</b>\n\n" +
               $"<size=22>{completeSubText}</size>";
    }
}
