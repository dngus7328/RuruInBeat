#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Exact, non-destructive extraction of the APPROVED original mouse TopHUD.
/// Each output is a real, transparent PNG usable as an independent Unity Image.
/// No new decoration, colors, typography, or AI-generated art is introduced.
/// The original source asset and its GUID are never changed.
/// </summary>
[InitializeOnLoad]
public static class LuluTopHUDPartGenerator
{
    public const string SourcePath = "Assets/Image2/HUD.3.png";
    public const string OutputFolder = "Assets/Resources/RuruArt/UI/HUDParts";
    public const int SourceWidth = 1672;
    public const int SourceHeight = 941;
    public const int Top = 40;
    public const int Bottom = 168;

    // Each piece occupies a NON-OVERLAPPING region of the approved source.
    // Splits are made along the icon/left rule/center decoration/right rule.
    // Matching normalized anchors reconstruct the source pixel-for-pixel.
    public static readonly string[] PartNames =
    {
        "HUD_IconSide",
        "HUD_LeftRule",
        "HUD_CenterRule",
        "HUD_RightRule"
    };
    public static readonly int[] XEdges = { 0, 135, 560, 1200, 1672 };

    static LuluTopHUDPartGenerator()
    {
        EditorApplication.delayCall += GenerateMissing;
    }

    [MenuItem("Tools/Lulu in Beat/Extract existing mouse TopHUD into independent PNGs")]
    public static void GenerateMissing()
    {
        Generate(false);
    }

    [MenuItem("Tools/Lulu in Beat/Regenerate mouse TopHUD parts from original")]
    public static void RegenerateFromOriginal()
    {
        if (!EditorUtility.DisplayDialog(
            "TopHUD 원본 기준 재생성",
            "원본 HUD.3.png에서 4개 부품을 다시 잘라냅니다. " +
            "기존 분리 PNG 4개만 덮어쓰며 HUD.3.png는 변경하지 않습니다.",
            "재생성", "취소"))
            return;

        Generate(true);
    }

    private static void Generate(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (!File.Exists(SourcePath))
        {
            Debug.LogWarning("[LuluTopHUD] Original PNG unavailable; " +
                             "keeping the active HUD exactly as it is.");
            return;
        }

        bool missing = false;
        for (int i = 0; i < PartNames.Length; i++)
        {
            if (!File.Exists(OutputFolder + "/" + PartNames[i] + ".png"))
            {
                missing = true;
                break;
            }
        }
        if (!missing && !force)
        {
            EnsureImportSettings();
            return;
        }

        Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Texture2D part = null;
        try
        {
            if (!source.LoadImage(File.ReadAllBytes(SourcePath), false))
                throw new InvalidOperationException("Failed to decode HUD.3.png.");
            if (source.width != SourceWidth || source.height != SourceHeight)
                throw new InvalidOperationException(
                    "TopHUD original size has changed. Expected " +
                    SourceWidth + "x" + SourceHeight + ", got " +
                    source.width + "x" + source.height +
                    ". Recheck crop positions instead of silently stretching.");

            Directory.CreateDirectory(OutputFolder);
            Color32[] sourcePixels = source.GetPixels32();
            int croppedHeight = Bottom - Top;
            int sourceBottom = SourceHeight - Bottom;

            for (int i = 0; i < PartNames.Length; i++)
            {
                string path = OutputFolder + "/" + PartNames[i] + ".png";
                if (!force && File.Exists(path))
                    continue;

                int left = XEdges[i];
                int width = XEdges[i + 1] - left;
                var cropped = new Color32[width * croppedHeight];
                for (int y = 0; y < croppedHeight; y++)
                {
                    Array.Copy(sourcePixels,
                        (sourceBottom + y) * SourceWidth + left,
                        cropped, y * width, width);
                }

                part = new Texture2D(width, croppedHeight,
                    TextureFormat.RGBA32, false);
                part.SetPixels32(cropped);
                part.Apply(false, false);

                // Check the extracted pixels against the ORIGINAL prior to save.
                Color32[] check = part.GetPixels32();
                for (int j = 0; j < check.Length; j++)
                {
                    Color32 x = check[j], y = cropped[j];
                    if (x.r != y.r || x.g != y.g ||
                        x.b != y.b || x.a != y.a)
                        throw new InvalidOperationException(
                            "TopHUD part pixel mismatch: " + PartNames[i]);
                }

                File.WriteAllBytes(path, part.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(part);
                part = null;
            }

            AssetDatabase.Refresh();
            EnsureImportSettings();

            Debug.Log("[LuluTopHUD] Four EXACT cropped transparent PNGs " +
                      "are available under " + OutputFolder +
                      ". Original HUD.3.png is untouched.");
        }
        catch (Exception error)
        {
            Debug.LogError("[LuluTopHUD] Extraction failed. " +
                           "Original HUD remains usable. " + error);
        }
        finally
        {
            if (part != null)
                UnityEngine.Object.DestroyImmediate(part);
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    private static void EnsureImportSettings()
    {
        for (int i = 0; i < PartNames.Length; i++)
        {
            string path = OutputFolder + "/" + PartNames[i] + ".png";
            if (!File.Exists(path))
                continue;

            TextureImporter importer =
                AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            bool dirty =
                importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                importer.mipmapEnabled ||
                importer.textureCompression !=
                    TextureImporterCompression.Uncompressed ||
                !importer.alphaIsTransparency;

            if (!dirty)
                continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression =
                TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 100;
            importer.SaveAndReimport();
        }
    }
}
#endif
