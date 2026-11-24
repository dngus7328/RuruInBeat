#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Import the two user-approved character cutouts as crisp 2D sprites.
/// Their source images are never repainted or replaced.
/// This is editor-only; it is not part of the player build.
/// </summary>
[InitializeOnLoad]
internal static class RuruOriginalCharacterSpriteImport
{
    private static readonly string[] Paths =
    {
        "Assets/Resources/RuruArt/Characters/Lulu_Master_Cutout.png",
        "Assets/Resources/RuruArt/Characters/Pino_Master_Cutout.png"
    };

    static RuruOriginalCharacterSpriteImport()
    {
        EditorApplication.delayCall += Prepare;
    }

    private static void Prepare()
    {
        foreach (string path in Paths)
        {
            TextureImporter importer =
                AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            bool changed = importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                !importer.alphaIsTransparency ||
                importer.mipmapEnabled;

            if (!changed) continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            Debug.Log("[RuruArt] Sprite import prepared: " + path);
        }
    }
}
#endif
