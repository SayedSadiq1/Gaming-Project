using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class ThrowableManager : MonoBehaviour
{
    [Header("Settings")]
    public Transform ThrowPoint;
    public Key ThrowKey = Key.G;
    public Key CycleKey = Key.T;

    [Header("Prefabs")]
    public List<GameObject> ThrowablePrefabs;

    [Header("HUD Hook (optional)")]
    [Tooltip("Starting frag count synced with the HUD. Set 0 to disable HUD hook.")]
    public int startingFrags = 3;

    private int m_CurrentIndex = 0;
    private int m_FrameCount = 0;
    private CombatHUD m_HUD;

    void Start()
    {
        Debug.Log("ThrowableManager Started on " + gameObject.name + ". Press " + ThrowKey + " to throw, " + CycleKey + " to cycle.");
        m_HUD = Object.FindFirstObjectByType<CombatHUD>();
        if (m_HUD != null && startingFrags > 0)
        {
            m_HUD.SetFragCount(startingFrags);
            Debug.Log("[ThrowableManager] Frag counter initialised to " + startingFrags);
        }
    }

    void Update()
    {
        m_FrameCount++;
        if (m_FrameCount % 300 == 0)
        {
            // Debug.Log("ThrowableManager Heartbeat - Update is running.");
        }

        if (Keyboard.current == null) return;

        if (Keyboard.current[ThrowKey].wasPressedThisFrame)
        {
            Debug.Log("Throw Key Pressed: " + ThrowKey);
            ThrowCurrent();
        }

        if (Keyboard.current[CycleKey].wasPressedThisFrame)
        {
            Debug.Log("Cycle Key Pressed: " + CycleKey);
            CycleThrowable();
        }
    }

    void ThrowCurrent()
    {
        if (ThrowablePrefabs == null || ThrowablePrefabs.Count == 0)
        {
            Debug.LogWarning("No throwables assigned in ThrowablePrefabs list!");
            return;
        }

        if (ThrowablePrefabs[m_CurrentIndex] == null)
        {
            Debug.LogWarning("Throwable at index " + m_CurrentIndex + " is null!");
            return;
        }

        // Check if this throwable is a frag (HUD only tracks frag counter)
        var prefab = ThrowablePrefabs[m_CurrentIndex];
        bool isFrag = prefab.GetComponent<FragGrenade>() != null
                   || prefab.name.IndexOf("frag", System.StringComparison.OrdinalIgnoreCase) >= 0;

        // Lazy HUD lookup in case it spawned after Start
        if (m_HUD == null) m_HUD = Object.FindFirstObjectByType<CombatHUD>();

        // Block frag throw if HUD says we're out
        if (isFrag && m_HUD != null && m_HUD.GetFragCount() <= 0)
        {
            Debug.Log("[ThrowableManager] Out of frag grenades.");
            return;
        }

        if (ThrowPoint == null)
        {
            Debug.LogWarning("No ThrowPoint assigned! Attempting to find Camera.");
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) ThrowPoint = cam.transform;
            else ThrowPoint = transform;
        }

        GameObject throwableObj = Instantiate(prefab, ThrowPoint.position, ThrowPoint.rotation);
        ThrowableBase throwable = throwableObj.GetComponent<ThrowableBase>();
        if (throwable != null)
        {
            throwable.Throw(ThrowPoint.forward);
            Debug.Log("Success! Thrown: " + throwableObj.name);

            // Decrement HUD counter ONLY for frags
            if (isFrag && m_HUD != null)
            {
                m_HUD.AddFrag(-1);
                Debug.Log("[ThrowableManager] Frag count now: " + m_HUD.GetFragCount());
            }
        }
        else
        {
            Debug.LogError("Spawned object " + throwableObj.name + " does not have a ThrowableBase component!");
        }
    }

    void CycleThrowable()
    {
        if (ThrowablePrefabs == null || ThrowablePrefabs.Count == 0) return;
        m_CurrentIndex = (m_CurrentIndex + 1) % ThrowablePrefabs.Count;
        Debug.Log("Selected Throwable: " + ThrowablePrefabs[m_CurrentIndex].name);
    }
}
