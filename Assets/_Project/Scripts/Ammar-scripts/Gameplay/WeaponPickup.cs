using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Weapon Pickup / Spawner
//
//  Drop this on an empty GameObject ("Weapon Spawner") in the scene, drag any
//  weapon prefab (M107, AK, M1911, etc.) into the WeaponPrefab field, and:
//   • the weapon's mesh floats + rotates above the spawner like the keycard
//   • when the player comes near, a "[E] Pick up <weapon name>" prompt shows
//   • pressing E adds that weapon to the player's PlayerWeaponsManager
//     inventory and removes the pickup from the scene
//
//  Inspector fields are minimal on purpose — set the WeaponPrefab and you're
//  done. The script auto-spawns the visual, auto-detects the player, and
//  auto-creates a prompt label if there isn't one.
// ─────────────────────────────────────────────────────────────────────────────
public class WeaponPickup : MonoBehaviour
{
    [Header("What to give the player")]
    [Tooltip("Drag the weapon prefab here (e.g. AK, M107, M1911). Must have a WeaponController on its root.")]
    public WeaponController WeaponPrefab;

    [Header("Pickup feel")]
    [Tooltip("Distance from the player at which 'press E' works.")]
    public float PickupRange = 2.5f;
    [Tooltip("Vertical bob amplitude (metres).")]
    public float BobHeight = 0.15f;
    [Tooltip("Bob cycles per second.")]
    public float BobSpeed = 1.5f;
    [Tooltip("Degrees per second the weapon visual rotates around Y.")]
    public float RotateSpeed = 60f;
    [Tooltip("Scale multiplier applied to the floating visual.")]
    public float VisualScale = 1f;

    [Header("Optional override (otherwise auto-generated)")]
    [Tooltip("If left empty, the script instantiates the weapon prefab's mesh as the floating visual.")]
    public GameObject VisualOverride;

    GameObject _visual;
    Transform  _player;
    Vector3    _visualBasePos;
    bool       _pickedUp;

    // tiny on-screen prompt
    static GUIStyle s_PromptStyle;
    bool _showPrompt;

    void Start()
    {
        if (WeaponPrefab == null)
        {
            Debug.LogWarning($"[WeaponPickup] '{name}' has no WeaponPrefab assigned — nothing to give.");
            enabled = false;
            return;
        }

        // ── Build the floating visual ──────────────────────────────────────
        if (VisualOverride != null)
        {
            _visual = Instantiate(VisualOverride, transform);
        }
        else
        {
            // Instantiate the weapon prefab but strip everything that would make
            // it try to "be a weapon" — we only want the mesh.
            var visualSource = WeaponPrefab.gameObject;
            _visual = Instantiate(visualSource, transform);

            // Disable anything functional
            foreach (var wc in _visual.GetComponentsInChildren<WeaponController>(true)) wc.enabled = false;
            foreach (var b  in _visual.GetComponentsInChildren<Behaviour>(true))
            {
                // keep MeshRenderer / SkinnedMeshRenderer (they're Renderers, not Behaviours, so safe)
                // disable AudioSources so they don't play idle
                if (b is AudioSource) b.enabled = false;
            }
            foreach (var col in _visual.GetComponentsInChildren<Collider>(true)) col.enabled = false;
            foreach (var rb  in _visual.GetComponentsInChildren<Rigidbody>(true))
            {
                rb.isKinematic = true;
                rb.useGravity  = false;
            }
        }

        _visual.transform.localPosition = Vector3.zero;
        _visual.transform.localRotation = Quaternion.identity;
        _visual.transform.localScale    = Vector3.one * VisualScale;
        _visual.SetActive(true);
        _visualBasePos = _visual.transform.localPosition;

        // ── Find player ────────────────────────────────────────────────────
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) _player = p.transform;
    }

    void Update()
    {
        if (_pickedUp || _visual == null) return;

        // Bob + rotate
        float bob = Mathf.Sin(Time.time * BobSpeed * Mathf.PI * 2f) * BobHeight;
        _visual.transform.localPosition = _visualBasePos + Vector3.up * bob;
        _visual.transform.Rotate(Vector3.up, RotateSpeed * Time.deltaTime, Space.Self);

        // Re-find player if needed (in case it spawned later)
        if (_player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return;
            _player = p.transform;
        }

        // Proximity check
        float dist = Vector3.Distance(_player.position, transform.position);
        _showPrompt = dist <= PickupRange;

        var kb = Keyboard.current;
        if (_showPrompt && kb != null && kb.eKey.wasPressedThisFrame)
        {
            TryPickup();
        }
    }

    void TryPickup()
    {
        if (_player == null) return;
        var pwm = _player.GetComponent<PlayerWeaponsManager>();
        if (pwm == null) pwm = _player.GetComponentInChildren<PlayerWeaponsManager>(true);
        if (pwm == null)
        {
            Debug.LogWarning("[WeaponPickup] Player has no PlayerWeaponsManager — can't add weapon.");
            return;
        }

        bool added = pwm.AddWeapon(WeaponPrefab);
        if (added)
        {
            Debug.Log($"[WeaponPickup] ✓ Player picked up {WeaponPrefab.WeaponName}.");
            _pickedUp = true;
            Destroy(gameObject);
        }
        else
        {
            Debug.Log($"[WeaponPickup] Player already has {WeaponPrefab.WeaponName} (or inventory full).");
            // Stay in the world but flash the prompt so the player understands
        }
    }

    void OnGUI()
    {
        if (!_showPrompt || _pickedUp || WeaponPrefab == null) return;

        if (s_PromptStyle == null)
        {
            s_PromptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            s_PromptStyle.normal.textColor = new Color(0f, 0.78f, 1f, 1f); // cyan to match theme
        }

        string label = $"[E] Pick up {WeaponPrefab.WeaponName}";
        var size = s_PromptStyle.CalcSize(new GUIContent(label));
        float x = (Screen.width  - size.x) * 0.5f;
        float y = Screen.height * 0.62f;

        // shadow
        var prev = GUI.color;
        GUI.color = new Color(0, 0, 0, 0.7f);
        GUI.Label(new Rect(x + 2, y + 2, size.x, size.y), label, s_PromptStyle);
        GUI.color = prev;
        GUI.Label(new Rect(x, y, size.x, size.y), label, s_PromptStyle);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.78f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, PickupRange);
    }
}
