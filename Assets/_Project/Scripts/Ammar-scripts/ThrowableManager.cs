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
    
    private int m_CurrentIndex = 0;
    private int m_FrameCount = 0;

    void Start()
    {
        Debug.Log("ThrowableManager Started on " + gameObject.name + ". Press " + ThrowKey + " to throw, " + CycleKey + " to cycle.");
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

        if (ThrowPoint == null)
        {
            Debug.LogWarning("No ThrowPoint assigned! Attempting to find Camera.");
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) ThrowPoint = cam.transform;
            else ThrowPoint = transform;
        }

        GameObject throwableObj = Instantiate(ThrowablePrefabs[m_CurrentIndex], ThrowPoint.position, ThrowPoint.rotation);
        ThrowableBase throwable = throwableObj.GetComponent<ThrowableBase>();
        if (throwable != null)
        {
            throwable.Throw(ThrowPoint.forward);
            Debug.Log("Success! Thrown: " + throwableObj.name);
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
