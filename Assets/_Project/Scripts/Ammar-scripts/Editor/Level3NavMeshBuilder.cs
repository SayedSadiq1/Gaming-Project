// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Level 3 NavMesh Builder
//  Top menu → Facility Breach → Bake NavMesh on All Floors
//
//  What it does:
//    1) Finds every GameObject in the scene whose name contains "Floor"
//    2) Marks each as Navigation Static (so the bake will consider it walkable)
//    3) Creates a NavMeshSurface on a "Level3_NavMeshRoot" GameObject if one
//       doesn't exist already
//    4) Bakes the NavMesh
//
//  Works with the modern AI Navigation package (Unity 6). Uses reflection so
//  the script compiles even if the package namespace is named differently.
// ─────────────────────────────────────────────────────────────────────────────

using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class Level3NavMeshBuilder
{
    [MenuItem("Facility Breach/Bake NavMesh on All Floors")]
    public static void Bake()
    {
        // 1) Find every Floor* GameObject and mark it Navigation Static
        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int marked = 0;
        foreach (var t in all)
        {
            if (t == null) continue;
            string n = t.gameObject.name;
            if (n.IndexOf("floor", System.StringComparison.OrdinalIgnoreCase) < 0) continue;

            var flags = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
            flags |= StaticEditorFlags.NavigationStatic;
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
            marked++;
        }
        Debug.Log($"[Level3NavMesh] Marked {marked} Floor objects as Navigation Static.");

        if (marked == 0)
        {
            Debug.LogWarning("[Level3NavMesh] No GameObjects with 'Floor' in the name found in this scene.");
            return;
        }

        // 2) Resolve the NavMeshSurface type (modern AI Navigation package)
        System.Type surfaceType =
            System.Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation") ??
            FindTypeInLoadedAssemblies("NavMeshSurface");

        if (surfaceType == null)
        {
            Debug.LogError("[Level3NavMesh] NavMeshSurface type not found. " +
                "Install Window → Package Manager → AI Navigation, then re-run.");
            return;
        }

        // 3) Find or create the surface
        var existing = Object.FindObjectsByType(surfaceType, FindObjectsSortMode.None);
        Component surface = null;
        if (existing != null && existing.Length > 0)
        {
            surface = (Component)existing[0];
            Debug.Log("[Level3NavMesh] Using existing NavMeshSurface on " + surface.gameObject.name);
        }
        else
        {
            var rootGO = new GameObject("Level3_NavMeshRoot");
            Undo.RegisterCreatedObjectUndo(rootGO, "Create NavMesh Root");
            surface = (Component)rootGO.AddComponent(surfaceType);
            Debug.Log("[Level3NavMesh] Created NavMeshSurface on " + rootGO.name);
        }

        // 4) Configure surface to include the whole scene (it'll pick up our static-marked floors)
        SetField(surface, "m_CollectObjects", 0);   // 0 = All, 1 = Volume, 2 = Children
        SetField(surface, "m_DefaultArea", 0);      // Walkable
        SetField(surface, "m_AgentTypeID", 0);      // Default humanoid agent

        // 5) Build the NavMesh
        var buildMethod = surfaceType.GetMethod("BuildNavMesh",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        if (buildMethod == null)
            buildMethod = surfaceType.GetMethod("BuildNavMesh", BindingFlags.Public | BindingFlags.Instance);

        if (buildMethod != null)
        {
            buildMethod.Invoke(surface, null);
            Debug.Log("[Level3NavMesh] ✓ NavMesh baked. " + marked + " floors included.");
        }
        else
        {
            Debug.LogError("[Level3NavMesh] BuildNavMesh method not found on NavMeshSurface. " +
                "Open the AI Navigation window manually and click Bake.");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    static System.Type FindTypeInLoadedAssemblies(string typeName)
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var t in asm.GetTypes())
                {
                    if (t.Name == typeName) return t;
                }
            }
            catch { /* skip unreflectable assemblies */ }
        }
        return null;
    }

    static void SetField(object target, string fieldName, object value)
    {
        var f = target.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (f != null) f.SetValue(target, value);
    }
}
