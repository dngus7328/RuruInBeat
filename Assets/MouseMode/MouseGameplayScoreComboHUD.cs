using LuluInBeat.MouseMode.ChartSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    private TMP_Text songTitle, songTime;
    private Image songCover;
    private RectTransform songProgress;
    private MouseModeChartAsset displayedChart;
    private int displayedSecond=-1;
    private int displayedScore = -1;
    private int displayedCombo = -1;
    private float comboPop = 1f;

    private void Awake()
    {
        if (gameObject.scene.name != "Mouse" &&
            gameObject.scene.name != "Tutorial")
            return;

        ResolveChartPlayer();
        TMP_FontAsset font = RuruConsoleUI.Font(true) ?? ResolveFont();

        var comboLabel=CreateText("Combo_Label", font, "COMBO", 19f,
            new Vector2(.5f, 1f), new Vector2(124f, -98f),
            new Vector2(105f, 32f), TextAlignmentOptions.MidlineRight, false);
        comboValue = CreateText("Combo_Value", font, "000", 38f,
            new Vector2(.5f, 1f), new Vector2(250f, -90f),
            new Vector2(195f, 54f), TextAlignmentOptions.MidlineLeft, true);
        var scoreLabel=CreateText("Score_Label", font, "SCORE", 19f,
            new Vector2(1f, 1f), new Vector2(-427f, -98f),
            new Vector2(100f, 32f), TextAlignmentOptions.MidlineRight, false);
        scoreValue = CreateText("Score_Value", font, "0000000", 35f,
            new Vector2(1f, 1f), new Vector2(-88f, -90f),
            new Vector2(310f, 54f), TextAlignmentOptions.MidlineRight, true);

        // Align the three live readouts to the existing split HUD artwork.
        // Preserve the playable field and the source frame itself.
        PlaceTop(comboLabel,.385f,.475f,80f,36f);
        PlaceTop(comboValue,.480f,.665f,70f,58f);
        PlaceTop(scoreLabel,.805f,.862f,80f,36f);
        PlaceTop(scoreValue,.863f,.965f,70f,58f);
        BuildSongReadout();
        RefreshValues(true);
        RefreshSongReadout();
    }

    private void LateUpdate()
    {
        if (comboValue == null || scoreValue == null)
            return;

        if (chartPlayer == null)
            ResolveChartPlayer();

        RefreshValues(false);
        RefreshSongReadout();
        if (Mathf.Abs(comboPop - 1f) > .001f)
        {
            comboPop = Mathf.MoveTowards(comboPop, 1f,
                Time.unscaledDeltaTime * .75f);
            comboValue.rectTransform.localScale =
                Vector3.one * comboPop;
        }
    }

    private static void PlaceTop(TMP_Text label,float l,float r,float top,float height)
    {
        var rt=label.rectTransform;
        rt.anchorMin=new Vector2(l,1f); rt.anchorMax=new Vector2(r,1f);
        rt.pivot=new Vector2(.5f,1f);
        rt.offsetMin=new Vector2(0f,-top-height); rt.offsetMax=new Vector2(0f,-top);
        label.enableAutoSizing=true; label.fontSizeMin=label.fontSize*.65f; label.fontSizeMax=label.fontSize;
        label.overflowMode=TextOverflowModes.Ellipsis;
    }

    private void BuildSongReadout()
    {
        var art=RuruConsoleUI.Region(transform,"SongJacketReadout",.038f,1f,.077f,1f);
        art.pivot=new Vector2(.5f,1f);
        art.sizeDelta=new Vector2(0f,68f); art.anchoredPosition=new Vector2(0f,-86f);
        songCover=art.gameObject.AddComponent<Image>(); songCover.preserveAspect=true; songCover.raycastTarget=false; songCover.enabled=false;
        var root=RuruConsoleUI.Region(transform,"SongReadout",.095f,1,.325f,1);
        root.pivot=new Vector2(.5f,1);
        root.sizeDelta=new Vector2(0,40);
        root.anchoredPosition=new Vector2(0,-77);
        songTitle=RuruConsoleUI.Text(root,"SongTitle","",0,0,1,1,30,RuruConsoleUI.Paper);
        var timeRoot=RuruConsoleUI.Region(transform,"SongTimeReadout",.655f,1,.785f,1);
        timeRoot.pivot=new Vector2(.5f,1f);
        timeRoot.sizeDelta=new Vector2(0,30);timeRoot.anchoredPosition=new Vector2(0,-136f);
        songTime=RuruConsoleUI.Text(timeRoot,"SongTime","00:00",0,0,1,1,19,RuruConsoleUI.Muted,TextAlignmentOptions.MidlineRight,true);
        // Keep one progress strip, aligned to the center groove of the
        // authored top HUD rather than adding a second decorative meter.
        var progressRoot=RuruConsoleUI.Region(transform,"SongProgressReadout",.403f,1,.650f,1);
        progressRoot.pivot=new Vector2(.5f,1f);
        progressRoot.sizeDelta=new Vector2(0f,5f);
        progressRoot.anchoredPosition=new Vector2(0f,-151f);
        songProgress=RuruConsoleUI.Strip(progressRoot,"Progress",0,0,0,1,RuruConsoleUI.Lilac).rectTransform;
    }
    private void RefreshSongReadout()
    {
        if(songTitle==null || chartPlayer==null) return;
        var chart=chartPlayer.ActiveChartAsset;
        if(chart!=displayedChart)
        {
            displayedChart=chart;
            songTitle.text=chart!=null?chart.displayTitle:"";
            if(songCover!=null)
            {
                songCover.sprite=chart!=null?chart.songSelectJacket:null;
                songCover.enabled=songCover.sprite!=null;
            }
            displayedSecond=-1;
        }
        float elapsed=Mathf.Max(0,chartPlayer.SongElapsedSeconds);
        float duration=chart!=null && chart.song!=null?chart.song.length:0;
        songProgress.anchorMax=new Vector2(duration>0?Mathf.Clamp01(elapsed/duration):0,1);
        int seconds=Mathf.FloorToInt(elapsed);
        if(seconds!=displayedSecond)
        {
            displayedSecond=seconds;
            songTime.text=MouseModeResultFlow.FormatTime(elapsed)+" / "+MouseModeResultFlow.FormatTime(duration);
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
