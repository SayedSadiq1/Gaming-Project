using UnityEngine;

namespace FacilityBreach
{
    /// <summary>
    /// Optional per-weapon reload speed override.
    /// Add this component to a weapon prefab to give it its own reload time.
    /// If absent, WeaponReloader falls back to its DefaultReloadTime.
    /// </summary>
    public class WeaponReloadConfig : MonoBehaviour
    {
        [Tooltip("How long this weapon takes to reload (seconds)")]
        public float ReloadTime = 2.5f;
    }
}
