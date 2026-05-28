using System.IO;
using UnityEngine;
using UnityEditor;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Loading Screen Setup
//  Menu: Facility Breach → Set Up Loading Screen
//
//  Auto-locates Loading_Background.png anywhere in Assets/, copies it into
//  Assets/_Project/Resources/UI/Loading_Background.png (the magic path
//  LoadingScreen.cs uses at runtime), and sets its Texture Type to Sprite.
//
//  One-click fix for the "Resources folder" requirement.
// ─────────────────────────────────────────────────────────────────────────────
public static class LoadingScreenSetup
{
    const string TARGET_PATH = "Assets/_Project/Resources/UI/Loading_Background.png";
    const string FILE_NAME   = "Loading_Background.png";

    [MenuItem("Facility Breach/Set Up Loading Screen")]
    public static void SetUp()
    {
        // 1) Make sure the Resources/UI folders exist
        EnsureFolder("Assets/_Project", "Resources");
        EnsureFolder("Assets/_Project/Resources", "UI");

        // 2) Find Loading_Background.png anywhere in the project
        string[] guids = AssetDatabase.FindAssets("Loading_Background t:Texture2D");
        string sourcePath = null;
        foreach (var g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileName(p).Equals(FILE_NAME, System.StringComparison.OrdinalIgnoreCase))
            {
                sourcePath = p;
                break;
            }
        }

        if (sourcePath == null)
        {
            EditorUtility.DisplayDialog("Loading Background not found",
                "Couldn't find a file named 'Loading_Background.png' anywhere in the project.\n\n" +
                "Drop the image into Assets/ first, then run this menu again.", "OK");
            return;
        }

        // Already in the right place? Just fix the import settings.
        if (sourcePath == TARGET_PATH)
        {
            Debug.Log($"[LoadingScreenSetup] Already at {TARGET_PATH}.");
        }
        else
        {
            // 3) Move it (preserves references)
            string err = AssetDatabase.MoveAsset(sourcePath, TARGET_PATH);
            if (!string.IsNullOrEmpty(err))
            {
                Debug.LogError($"[LoadingScreenSetup] MoveAsset failed: {err}");
                EditorUtility.DisplayDialog("Move failed",
                    "Couldn't move the file. Error:\n" + err +
                    "\n\nYou can manually move it from " + sourcePath + " to " + TARGET_PATH + ".",
                    "OK");
                return;
            }
            Debug.Log($"[LoadingScreenSetup] Moved {sourcePath} → {TARGET_PATH}");
        }

        // 4) Force the import to Sprite (2D and UI)
        var importer = AssetImporter.GetAtPath(TARGET_PATH) as TextureImporter;
        if (importer != null)
        {
            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }
            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }
            if (changed)
            {
                importer.SaveAndReimport();
                Debug.Log($"[LoadingScreenSetup] Set Texture Type → Sprite (2D and UI).");
            }
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Loading Screen ready",
            $"Loading_Background.png is now at:\n{TARGET_PATH}\n\n" +
            "Press Play and try New Game — the loading screen will appear for 5s with your image.",
            "OK");
    }

    static void EnsureFolder(string parent, string folder)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + folder))
        {
            AssetDatabase.CreateFolder(parent, folder);
            Debug.Log($"[LoadingScreenSetup] Created folder {parent}/{folder}");
        }
    }
}
