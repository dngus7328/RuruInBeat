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
        RectTransform root = GetOrCreateReadoutRoot(transform);
        RuruConsoleUI.Text(root,"Combo_Label","COMBO",.637f,.66f,.758f,.92f,19,RuruConsoleUI.Muted,TextAlignmentOptions.Center);
        comboValue = (TextMeshProUGUI)RuruConsoleUI.Text(root,"Combo_Value","000",.634f,.12f,.765f,.69f,52,RuruConsoleUI.Paper,TextAlignmentOptions.Center);
        RuruConsoleUI.Text(root,"Score_Label","SCORE",.800f,.68f,.966f,.94f,19,RuruConsoleUI.Muted,TextAlignmentOptions.MidlineLeft);
        scoreValue = (TextMeshProUGUI)RuruConsoleUI.Text(root,"Score_Value","0000000",.795f,.09f,.972f,.72f,62,RuruConsoleUI.Paper,TextAlignmentOptions.MidlineRight);
        BuildSongReadout(root);
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

    // One coordinate space shared by the two runtime HUD producers.
    // Preserve the original exterior frame and the source HUD asset.
    public static RectTransform GetOrCreateReadoutRoot(Transform hud)
    {
        var existing = hud.Find("HUD_Readouts") as RectTransform;
        if (existing != null) return existing;
        var root = RuruConsoleUI.Region(hud,"HUD_Readouts",.035f,1f,.945f,1f);
        root.pivot = new Vector2(.5f,1f);
        root.offsetMin = new Vector2(0f,-211f);
        root.offsetMax = new Vector2(0f,-100f);
        var face = root.gameObject.AddComponent<Image>();
        face.sprite = Resources.Load<Sprite>("RuruArt/UI/HUD_InstrumentPanel");
        face.type = Image.Type.Simple;
        face.color = face.sprite != null ? Color.white : new Color32(48,38,60,255);
        face.raycastTarget = false;
        var old = hud.Find("TopHUD");
        if (old != null) old.gameObject.SetActive(false);
        return root;
    }

    private void BuildSongReadout(RectTransform root)
    {
        var art=RuruConsoleUI.Region(root,"SongJacketReadout",.012f,.19f,.059f,.81f);
        songCover=art.gameObject.AddComponent<Image>();
        songCover.preserveAspect=true; songCover.raycastTarget=false; songCover.enabled=false;
        songTitle=RuruConsoleUI.Text(root,"SongTitle","",.074f,.48f,.310f,.89f,32,RuruConsoleUI.Paper);
        songTime=RuruConsoleUI.Text(root,"SongTime","00:00 / 00:00",.074f,.24f,.310f,.48f,19,RuruConsoleUI.Muted,TextAlignmentOptions.MidlineLeft);
        var progressTrack=RuruConsoleUI.Strip(root,"SongProgressTrack",.074f,.13f,.310f,.18f,new Color32(83,65,101,255));
        songProgress=RuruConsoleUI.Strip(progressTrack.transform,"Progress",0,0,0,1,RuruConsoleUI.Lilac).rectTransform;
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
