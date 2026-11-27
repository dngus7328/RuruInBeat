#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures every packed asset is imported as a 2D sprite automatically.
/// No Inspector work, sprite slicing, or alpha cleanup is required.
/// </summary>
public sealed class RuruSceneSpriteImport : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        const string root = "Assets/Resources/RuruArt/Scenes/";
        if (!assetPath.StartsWith(root, System.StringComparison.Ordinal))
            return;

        TextureImporter texture = assetImporter as TextureImporter;
        if (texture == null) return;
        texture.textureType = TextureImporterType.Sprite;
        texture.spriteImportMode = SpriteImportMode.Single;
        texture.alphaIsTransparency = true;
        texture.mipmapEnabled = false;
        texture.npotScale = TextureImporterNPOTScale.None;
        texture.filterMode = FilterMode.Bilinear;
        texture.textureCompression = TextureImporterCompression.Uncompressed;
        texture.spritePixelsPerUnit = 100f;

        // Sliced panels remain crisp at different monitor aspect ratios.
        if (assetPath.EndsWith("ShelfPanel.png") ||
            assetPath.EndsWith("InfoPanel.png"))
            texture.spriteBorder = new Vector4(28,28,28,28);
        else if (assetPath.EndsWith("TrackSlot_Normal.png") ||
                 assetPath.EndsWith("TrackSlot_Active.png"))
            texture.spriteBorder = new Vector4(25,24,25,24);
        else if (assetPath.EndsWith("DialoguePanel.png"))
            texture.spriteBorder = new Vector4(40,37,40,37);
        else if (assetPath.EndsWith("GuidePanel.png"))
            texture.spriteBorder = new Vector4(30,27,30,27);
        else if (assetPath.EndsWith("ActionButton.png") ||
                 assetPath.EndsWith("DialogueButton.png"))
            texture.spriteBorder = new Vector4(20,20,20,20);
        else if (assetPath.EndsWith("SpeakerPlate.png"))
            texture.spriteBorder = new Vector4(22,16,22,16);
        else
            texture.spriteBorder = Vector4.zero;
    }
}
#endif