using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Attach to a generator or any power object in the scene.
// The player walks up and presses E to turn it on.
// Assign this to a ChainDoorController's "Required Power Source" field to gate that door.
public class ElectricitySource : MonoBehaviour
{
    [Header("Interaction")]
    public float interactDistance = 3f;

    [Header("Audio")]
    [Tooltip("Played first when the player starts the generator (cranking/startup noise).")]
    public AudioClip starterSound;
    [Tooltip("Played after the starter sound finishes — the generator running / power-on confirmation.")]
    public AudioClip powerOnSound;

    [Header("Labels")]
    public string labelOff = "Press [E] to start generator";
    public string labelOn  = "Generator online";

    [Header("Audio Volume")]
    [Range(0f, 1f)] public float audioVolume = 1f;

    public bool IsPowered { get; private set; }

    // Fires once when this source is powered on.
    public event Action OnPowered;

    Transform   _player;
    bool        _playerInRange;
    AudioSource _loopSource;

    void Start()
    {
        var go = GameObject.FindWithTag("Player");
        if (go != null) _player = go.transform;
    }

    void Update()
    {
        if (_player == null) return;

        Vector2 myXZ     = new Vector2(transform.position.x, transform.position.z);
        Vector2 playerXZ = new Vector2(_player.position.x,   _player.position.z);
        _playerInRange   = Vector2.Distance(myXZ, playerXZ) <= interactDistance;

        if (!IsPowered && _playerInRange && Keyboard.current != null && Keyboard.current[Key.E].wasPressedThisFrame)
            PowerOn();
    }

    public void PowerOn()
    {
        IsPowered = true;
        StartCoroutine(PlayStartupSounds());
        OnPowered?.Invoke();
    }

    IEnumerator PlayStartupSounds()
    {
        if (starterSound != null)
        {
            AudioSource.PlayClipAtPoint(starterSound, transform.position, audioVolume);
            yield return new WaitForSeconds(starterSound.length);
        }
        if (powerOnSound != null)
        {
            _loopSource              = gameObject.AddComponent<AudioSource>();
            _loopSource.clip         = powerOnSound;
            _loopSource.loop         = true;
            _loopSource.spatialBlend = 1f;   // 3D — sound comes from the generator
            _loopSource.volume       = audioVolume;
            _loopSource.Play();
        }
    }

    void OnGUI()
    {
        if (!_playerInRange && !IsPowered) return;
        if (IsPowered && !_playerInRange) return;

        string msg   = IsPowered
            ? labelOn
            : labelOff;
        Color  color = IsPowered
            ? new Color(0f, 1f, 0.55f, 1f)   // green — matches CombatHUD health color
            : new Color(0f, 0.85f, 1f, 1f);   // cyan

        GUIStyle style = new GUIStyle(GUI.skin.box)
        {
            fontSize  = 20,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
        };
        style.normal.textColor = color;

        float w = 320f, h = 44f;
        GUI.Box(new Rect((Screen.width - w) * 0.5f, Screen.height * 0.72f, w, h), msg, style);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}
