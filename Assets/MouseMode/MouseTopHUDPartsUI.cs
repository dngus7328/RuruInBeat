using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Places independently imported TopHUD PNG parts on the SAME source-image
/// rectangle. Pixel coordinates match the approved HUD.3.png exactly.
/// Original Image remains a fallback if split sprites have not been imported.
/// Score/combo remain independent live TMP elements (not painted into PNGs).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class MouseTopHUDPartsUI : MonoBehaviour
{
    private const string ResourceRoot = "RuruArt/UI/HUDParts/";
    private const float OriginalWidth = 1672f;
    private const float OriginalHeight = 941f;
    private const float Top = 40f;
    private const float Bottom = 168f;

    private static readonly string[] Names =
    {
        "HUD_IconSide",
        "HUD_LeftRule",
        "HUD_CenterRule",
        "HUD_RightRule"
    };

    private static readonly int[] Edges = { 0, 135, 560, 1200, 1672 };
    private Image sourceImage;
    private bool assembled;

    private void Awake()
    {
        if (gameObject.scene.name != "Mouse" &&
            gameObject.scene.name != "Tutorial")
            return;

        sourceImage = GetComponent<Image>();
        TryAssemble();
    }

    private void TryAssemble()
    {
        if (assembled || sourceImage == null)
            return;

        var sprites = new Sprite[Names.Length];
        for (int i = 0; i < Names.Length; i++)
        {
            sprites[i] = Resources.Load<Sprite>(ResourceRoot + Names[i]);
            if (sprites[i] == null)
            {
                // A missing imported PNG is not an excuse to lose the HUD.
                // Keep the original, unchanged full HUD image visible.
                sourceImage.enabled = true;
                return;
            }

            Rect r = sprites[i].rect;
            if (Mathf.Abs(r.width - (Edges[i + 1] - Edges[i])) > .5f ||
                Mathf.Abs(r.height - (Bottom - Top)) > .5f)
            {
                Debug.LogWarning("[LuluTopHUD] Part dimensions differ from " +
                    "the source PNG; using the original HUD instead.", this);
                sourceImage.enabled = true;
                return;
            }
        }

        RectTransform parent = transform as RectTransform;
        if (parent == null)
            return;

        // Build ALL pieces before disabling the original. This makes the
        // upgrade non-destructive and avoids flashing an empty HUD.
        for (int i = 0; i < Names.Length; i++)
        {
            GameObject child = new GameObject(Names[i],
                typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image));

            RectTransform rt = child.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(
                Edges[i] / OriginalWidth,
                1f - Bottom / OriginalHeight);
            rt.anchorMax = new Vector2(
                Edges[i + 1] / OriginalWidth,
                1f - Top / OriginalHeight);
            rt.pivot = new Vector2(.5f, .5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image img = child.GetComponent<Image>();
            img.sprite = sprites[i];
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.color = sourceImage.color;
            img.raycastTarget = false;
        }

        assembled = true;
        sourceImage.enabled = false;
    }
}
