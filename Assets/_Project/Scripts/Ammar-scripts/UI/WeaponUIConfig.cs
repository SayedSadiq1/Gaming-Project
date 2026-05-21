using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Weapon UI Config
//  One asset per weapon defines its HUD look (color, icon, label, layout type).
//  Match the `weaponName` to the WeaponController.WeaponName on the prefab.
// ─────────────────────────────────────────────────────────────────────────────
[CreateAssetMenu(menuName = "Facility Breach/Weapon UI Config", fileName = "WeaponUI_New")]
public class WeaponUIConfig : ScriptableObject
{
    [Tooltip("Must match WeaponController.WeaponName (case-insensitive)")]
    public string weaponName = "Weapon";

    [Tooltip("Big label shown on HUD")]
    public string displayName = "WEAPON";

    [Tooltip("Type label: ASSAULT RIFLE / SNIPER / etc.")]
    public string typeLabel  = "PRIMARY";

    [Tooltip("Tint color for HUD accents when this weapon is equipped")]
    public Color accentColor = new Color(0f, 0.78f, 1f, 1f);

    [Tooltip("Optional weapon silhouette icon (used in the big weapon nameplate)")]
    public Sprite icon;

    // Layout flags (drives WeaponUIController to show/hide extra elements)
    [Header("Per-Weapon Layout Hints")]
    public bool showCooldownRing = false;   // Knife
    public bool showSpreadIndicator = false;// Shotgun
    public bool showBoltStatus = false;     // Sniper
    public bool showFullAutoLED = false;    // AR
}
