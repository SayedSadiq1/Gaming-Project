using UnityEngine;
using UnityEditor;

// Custom inspector for EnemyGunAI.
// Adds an "Attach Weapon to Hand" button that finds mixamorig:RightHand
// and parents the chosen weapon prefab there with the correct grip offset.
[CustomEditor(typeof(EnemyGunAI))]
public class EnemyWeaponAttacher : Editor
{
    // Weapon prefab to attach — shown in the inspector button panel.
    static GameObject _weaponPrefab;

    public override void OnInspectorGUI()
    {
        // Draw the default EnemyGunAI inspector fields first.
        DrawDefaultInspector();

        EnemyGunAI ai = (EnemyGunAI)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("── Weapon Attachment Tool ──", EditorStyles.boldLabel);

        _weaponPrefab = (GameObject)EditorGUILayout.ObjectField(
            "Weapon Prefab", _weaponPrefab, typeof(GameObject), false);

        GUI.enabled = _weaponPrefab != null;
        if (GUILayout.Button("Attach Weapon to Right Hand", GUILayout.Height(30)))
        {
            AttachWeapon(ai);
        }
        GUI.enabled = true;

        EditorGUILayout.HelpBox(
            "1. Drag a weapon prefab into the field above.\n" +
            "2. Click the button — it finds mixamorig:RightHand and parents the weapon there.\n" +
            "3. The Weapon field on EnemyGunAI is wired automatically.",
            MessageType.Info);
    }

    void AttachWeapon(EnemyGunAI ai)
    {
        // Find the right hand bone anywhere in the enemy hierarchy.
        Transform rightHand = FindBone(ai.transform, "mixamorig:RightHand");
        if (rightHand == null)
        {
            EditorUtility.DisplayDialog("Bone Not Found",
                "Could not find 'mixamorig:RightHand' in the enemy hierarchy.\n" +
                "Make sure the Swat FBX is imported and expanded in the Hierarchy.", "OK");
            return;
        }

        // Remove any previously attached weapon child to avoid duplicates.
        EnemyWeaponMount existing = ai.GetComponentInChildren<EnemyWeaponMount>();
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        // Instantiate the weapon prefab and parent it to the right hand.
        GameObject weaponGO = (GameObject)PrefabUtility.InstantiatePrefab(_weaponPrefab, rightHand);
        Undo.RegisterCreatedObjectUndo(weaponGO, "Attach Enemy Weapon");

        // ── Grip offset for AK74-style weapon on Mixamo RightHand ──
        // Barrel of the weapon (local -Z) should face the character's forward (+Z).
        // Mixamo RightHand in T-pose has fingers pointing in +X world.
        weaponGO.transform.localPosition = new Vector3(0.04f,  0.02f, 0.07f);
        weaponGO.transform.localRotation = Quaternion.Euler(-5f, 90f, 95f);
        weaponGO.transform.localScale    = Vector3.one;

        weaponGO.name = _weaponPrefab.name + "_equipped";

        // Auto-wire: find the FirePoint (MuzzlePoint) on the weapon
        // and set it on the EnemyWeaponMount.
        EnemyWeaponMount mount = weaponGO.GetComponent<EnemyWeaponMount>();
        if (mount == null)
        {
            EditorUtility.DisplayDialog("No EnemyWeaponMount",
                "The weapon prefab doesn't have an EnemyWeaponMount script.\n" +
                "Add EnemyWeaponMount to the prefab first, then try again.", "OK");
            Undo.DestroyObjectImmediate(weaponGO);
            return;
        }

        // Try to find the muzzle child (MuzzlePoint or WeaponMuzzle).
        if (mount.firePoint == null)
        {
            Transform muzzle = FindBone(weaponGO.transform, "MuzzlePoint")
                            ?? FindBone(weaponGO.transform, "WeaponMuzzle")
                            ?? FindBone(weaponGO.transform, "Muzzle");
            if (muzzle != null)
            {
                mount.firePoint = muzzle;
                EditorUtility.SetDirty(mount);
            }
        }

        // Wire the mount into EnemyGunAI.weapon.
        Undo.RecordObject(ai, "Wire EnemyWeaponMount");
        ai.weapon = mount;
        EditorUtility.SetDirty(ai);

        // Select the weapon so the user can see/tweak it.
        Selection.activeGameObject = weaponGO;
        SceneView.lastActiveSceneView?.FrameSelected();

        Debug.Log($"[EnemyWeaponAttacher] Attached {weaponGO.name} to {rightHand.name}. " +
                  $"Adjust localPosition/Rotation on {weaponGO.name} if needed.");
    }

    static Transform FindBone(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = FindBone(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
