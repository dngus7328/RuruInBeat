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
    private MouseBootSequenceUI bootSequence;
    private CanvasGroup readoutVisibility;
    private float lastSongElapsed;

    private void Awake()
    {
        if (gameObject.scene.name != "Mouse" &&
            gameObject.scene.name != "Tutorial")
            return;

        ResolveChartPlayer();
        RectTransform root = GetOrCreateReadoutRoot(transform);
        bootSequence = FindObjectOfType<MouseBootSequenceUI>(true);
        readoutVisibility = root.GetComponent<CanvasGroup>();
        if (readoutVisibility == null) readoutVisibility = root.gameObject.AddComponent<CanvasGroup>();
        readoutVisibility.interactable = false;
        readoutVisibility.blocksRaycasts = false;
        readoutVisibility.alpha = bootSequence == null || bootSequence.IsDisplayPowered ? 1f : 0f;
                // Stable alignment and a clear hierarchy: only the number grows on hit.
        RuruConsoleUI.Text(root,"Combo_Label","COMBO",.635f,.69f,.755f,.97f,23,RuruConsoleUI.Muted,TextAlignmentOptions.Center);
        comboValue = (TextMeshProUGUI)RuruConsoleUI.Text(root,"Combo_Value","0",.630f,.12f,.773f,.72f,64,RuruConsoleUI.Paper,TextAlignmentOptions.Center);
        comboValue.color = new Color32(237,224,255,255);
        RuruConsoleUI.Text(root,"Score_Label","SCORE",.800f,.69f,.98f,.97f,23,RuruConsoleUI.Muted,TextAlignmentOptions.MidlineRight);
        scoreValue = (TextMeshProUGUI)RuruConsoleUI.Text(root,"Score_Value","0000000",.788f,.10f,.969f,.73f,68,Color.white,TextAlignmentOptions.MidlineRight);
        scoreValue.fontSizeMin = 38f;
        comboValue.fontSizeMin = 38f;
        foreach(float split in new[]{.323f,.619f,.779f})
        {
            var rule=RuruConsoleUI.Strip(root,"ReadoutSeparator",split,.16f,split+.0012f,.83f,new Color32(164,139,197,200));
            rule.rectTransform.localRotation=Quaternion.Euler(0,0,-12f);
        }
        RuruConsoleUI.Strip(root,"HUDLowerEdge",0,0,1,.012f,new Color32(169,143,207,255));
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

        if (readoutVisibility != null)
            readoutVisibility.alpha = bootSequence == null || bootSequence.IsDisplayPowered ? 1f : 0f;
        RefreshValues(false);
        RefreshSongReadout();
        if (Mathf.Abs(comboPop - 1f) > .001f)
        {
            comboPop = Mathf.MoveTowards(comboPop, 1f,
                Time.unscaledDeltaTime * 1.8f);
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
        root.SetAsFirstSibling(); // Power animation and outer frame must render above the HUD.
        root.pivot = new Vector2(.5f,1f);
        root.offsetMin = new Vector2(0f,-174f);
        root.offsetMax = new Vector2(0f,-73f);
        var face = root.gameObject.AddComponent<Image>();
        face.sprite = null;
        face.type = Image.Type.Simple;
        face.color = new Color32(30,23,45,220);
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
        songTitle=RuruConsoleUI.Text(root,"SongTitle","",.074f,.49f,.310f,.91f,36,RuruConsoleUI.Paper);
        songTitle.fontSizeMin=23f;
        songTime=RuruConsoleUI.Text(root,"SongTime","00:00 / 00:00",.074f,.23f,.310f,.49f,22,RuruConsoleUI.Muted,TextAlignmentOptions.MidlineLeft);
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
            lastSongElapsed=0f;
        }
        float duration=chart!=null && chart.song!=null?chart.song.length:0;
        if (!chartPlayer.IsChartPlaying) lastSongElapsed=0f;
        else if (chartPlayer.HasReachedSongStart && Time.timeScale > 0f && !AudioListener.pause)
            lastSongElapsed=Mathf.Max(0f,chartPlayer.SongElapsedSeconds);
        float elapsed=duration>0f?Mathf.Clamp(lastSongElapsed,0f,duration):lastSongElapsed;
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
            comboValue.text = Mathf.Max(0, combo).ToString();
        }
    }
}
