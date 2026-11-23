using LuluInBeat.MouseMode.ChartSystem;
using TMPro;
using UnityEngine;

/// <summary>
/// Mouse/Tutorial gameplay HUD: score and combo are independent TMP objects,
/// not text baked into a frame PNG. The chart player remains the only source
/// of gameplay data. Decorative text never intercepts pointer input.
/// </summary>
[DisallowMultipleComponent]
public sealed class MouseGameplayScoreComboHUD : MonoBehaviour
{
    [SerializeField] private MouseModeChartPlayer chartPlayer;
    [SerializeField] private TMP_FontAsset preferredFont;

    private TextMeshProUGUI comboValue;
    private TextMeshProUGUI scoreValue;
    private int displayedScore = -1;
    private int displayedCombo = -1;
    private float comboPop = 1f;

    private void Awake()
    {
        if (gameObject.scene.name != "Mouse" &&
            gameObject.scene.name != "Tutorial")
            return;

        ResolveChartPlayer();
        TMP_FontAsset font = ResolveFont();

        CreateText("Combo_Label", font, "COMBO", 19f,
            new Vector2(.5f, 1f), new Vector2(-104f, -26f),
            new Vector2(105f, 32f), TextAlignmentOptions.MidlineRight, false);
        comboValue = CreateText("Combo_Value", font, "000", 38f,
            new Vector2(.5f, 1f), new Vector2(40f, -15f),
            new Vector2(195f, 54f), TextAlignmentOptions.MidlineLeft, true);
        CreateText("Score_Label", font, "SCORE", 19f,
            new Vector2(1f, 1f), new Vector2(-450f, -26f),
            new Vector2(100f, 32f), TextAlignmentOptions.MidlineRight, false);
        scoreValue = CreateText("Score_Value", font, "0000000", 35f,
            new Vector2(1f, 1f), new Vector2(-112f, -15f),
            new Vector2(310f, 54f), TextAlignmentOptions.MidlineRight, true);

        RefreshValues(true);
    }

    private void LateUpdate()
    {
        if (comboValue == null || scoreValue == null)
            return;

        if (chartPlayer == null)
            ResolveChartPlayer();

        RefreshValues(false);
        if (Mathf.Abs(comboPop - 1f) > .001f)
        {
            comboPop = Mathf.MoveTowards(comboPop, 1f,
                Time.unscaledDeltaTime * .75f);
            comboValue.rectTransform.localScale =
                Vector3.one * comboPop;
        }
    }

    private void ResolveChartPlayer()
    {
        if (chartPlayer != null)
            return;

        foreach (MouseModeChartPlayer candidate in
            FindObjectsOfType<MouseModeChartPlayer>(true))
        {
            if (candidate != null &&
                candidate.gameObject.scene == gameObject.scene)
            {
                chartPlayer = candidate;
                break;
            }
        }
    }

    private TMP_FontAsset ResolveFont()
    {
        if (preferredFont != null)
            return preferredFont;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            foreach (TMP_Text other in
                canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                if (other != null && other.font != null &&
                    other.font.name.IndexOf("Paperlogy",
                        System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return other.font;
            }
        }

        return TMP_Settings.defaultFontAsset;
    }

    private TextMeshProUGUI CreateText(string name, TMP_FontAsset font,
        string initialText, float size, Vector2 anchor,
        Vector2 position, Vector2 dimensions,
        TextAlignmentOptions alignment, bool emphasize)
    {
        GameObject go = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(
            anchor.x > .75f ? 1f : (anchor.x < .25f ? 0f : .5f), 1f);
        rt.anchoredPosition = position;
        rt.sizeDelta = dimensions;

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.text = initialText;
        text.fontSize = size;
        text.fontStyle = emphasize ? FontStyles.Bold : FontStyles.Normal;
        text.alignment = alignment;
        text.color = emphasize
            ? new Color32(250, 244, 255, 255)
            : new Color32(203, 187, 223, 255);
        text.enableAutoSizing = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.outlineColor = new Color32(34, 23, 47, 255);
        text.outlineWidth = emphasize ? .12f : .06f;
        return text;
    }

    private void RefreshValues(bool force)
    {
        if (comboValue == null || scoreValue == null)
            return;

        int combo = chartPlayer != null ? chartPlayer.Combo : 0;
        int score = chartPlayer != null ? chartPlayer.Score : 0;

        if (force || score != displayedScore)
        {
            displayedScore = score;
            scoreValue.text = Mathf.Max(0, score).ToString("D7");
        }

        if (force || combo != displayedCombo)
        {
            if (displayedCombo >= 0 && combo > displayedCombo)
            {
                comboPop = 1.055f;
                comboValue.rectTransform.localScale =
                    Vector3.one * comboPop;
            }

            displayedCombo = combo;
            comboValue.text = Mathf.Max(0, combo).ToString("D3");
        }
    }
}
