using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LuluInBeat.MouseMode.ChartSystem;

/// <summary>
/// Bridges chart completion to the existing monitor power-off transition.
/// The scene's authored frames, note positions and pause panel are untouched.
/// The result console is assembled from independent nine-sliced UI sprites,
/// live chart statistics and the original Pino illustrations.
/// </summary>
[DisallowMultipleComponent]
public sealed class MouseModeResultFlow : MonoBehaviour
{
    private MouseModeChartPlayer chartPlayer;
    private MouseModeBulletDirector bulletDirector;
    private MouseBootSequenceUI bootSequence;
    private MouseRhythmPauseUI pauseUi;
    private GameObject resultPanel;
    private TMP_Text songLabel;
    private TMP_Text resultLabel;
    private TMP_Text scoreLabel;
    private TMP_Text rankLabel;
    private TMP_Text[] judgmentValues;
    private TMP_Text[] detailValues;
    private RectTransform scoreFill;
    private TMP_Text reactionLabel;
    private Image pinoResultPortrait;
    private Sprite[] pinoResultFrames;
    private float pinoAnimationStart;
    private bool pinoAnimationStarted;
    private bool finished;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneBootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapDirectPlay()
    {
        Bootstrap(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Bootstrap(scene);
    }

    private static void Bootstrap(Scene scene)
    {
        if (!scene.IsValid() || scene.name != "Mouse")
            return;

        foreach (Canvas candidate in FindObjectsOfType<Canvas>(true))
        {
            if (candidate.gameObject.scene != scene ||
                candidate.name != "GameplayCanvas")
                continue;

            if (candidate.GetComponent<MouseModeResultFlow>() == null)
                candidate.gameObject.AddComponent<MouseModeResultFlow>();
            return;
        }

        Debug.LogWarning("[MouseResult] GameplayCanvas not found in Mouse scene.");
    }

    private void Awake()
    {
        chartPlayer = FindObjectOfType<MouseModeChartPlayer>();
        bulletDirector = FindObjectOfType<MouseModeBulletDirector>();
        bootSequence = FindObjectOfType<MouseBootSequenceUI>();
        pauseUi = FindObjectOfType<MouseRhythmPauseUI>();

        if (chartPlayer == null)
        {
            Debug.LogWarning("[MouseResult] Missing MouseModeChartPlayer.");
            enabled = false;
            return;
        }

        chartPlayer.SongCompleted += OnSongCompleted;
        if (bulletDirector != null)
            bulletDirector.StabilityDepleted += OnStabilityDepleted;

        BuildFallbackPanel();
        if (bootSequence != null)
            bootSequence.BindResultPanelIfMissing(resultPanel);
    }

    private void OnDestroy()
    {
        if (chartPlayer != null)
            chartPlayer.SongCompleted -= OnSongCompleted;
        if (bulletDirector != null)
            bulletDirector.StabilityDepleted -= OnStabilityDepleted;
    }

    // Animate the *existing* user-authored Pino frames, starting only when
    // the result panel becomes visible after the power-off transition.
    private void Update()
    {
        if (!finished || resultPanel == null || !resultPanel.activeInHierarchy ||
            pinoResultPortrait == null || !pinoResultPortrait.gameObject.activeInHierarchy ||
            pinoResultFrames == null || pinoResultFrames.Length == 0)
            return;

        if (!pinoAnimationStarted)
        {
            pinoAnimationStarted = true;
            pinoAnimationStart = Time.unscaledTime;
        }

        int index = Mathf.Min(pinoResultFrames.Length - 1,
            Mathf.FloorToInt((Time.unscaledTime - pinoAnimationStart) / 0.18f));
        Sprite next = pinoResultFrames[index];
        if (next != null && pinoResultPortrait.sprite != next)
            pinoResultPortrait.sprite = next;
    }

    private void OnSongCompleted()
    {
        // If the final judgement depleted stability in this frame,
        // never display CLEAR while the director is waiting to stop.
        Finish(bulletDirector != null && bulletDirector.ShouldFailAtSongEnd);
    }

    private void OnStabilityDepleted()
    {
        Finish(true);
    }

    private void Finish(bool stabilityFailed)
    {
        if (finished || chartPlayer == null)
            return;
        finished = true;

        // Capture *before* StopChart clears runtime note visuals.
        int score = bulletDirector != null ? bulletDirector.FinalScore : chartPlayer.Score;
        score = Mathf.Clamp(score, 0, 1000000);

        // Completing the song without losing stability is not an automatic
        // clear. A C rank (600,000) is the minimum passing score; anything
        // below it fails and must not be recorded as a cleared song.
        bool failed = stabilityFailed || score < 600000;
        float elapsed = chartPlayer.SongElapsedSeconds;
        int hitCount = bulletDirector != null ? bulletDirector.HitCount : 0;
        int perfect = chartPlayer.PerfectCount;
        int great = chartPlayer.GreatCount;
        int good = chartPlayer.GoodCount;
        int miss = chartPlayer.MissCount;
        MouseModeChartAsset selection = MouseModeSongSelection.CurrentChart != null
            ? MouseModeSongSelection.CurrentChart : chartPlayer.ActiveChartAsset;

        string songId = selection != null && selection.data != null
            ? selection.data.songId : null;
        SaveManager save = SaveManager.Instance;
        bool newBest = !failed && save != null &&
            save.RecordClearedSong(songId, score, chartPlayer.MaxCombo);
        int bestScore = save != null ? save.GetBestScore(songId) : score;

        if (songLabel != null)
            songLabel.text = selection != null &&
                !string.IsNullOrWhiteSpace(selection.displayTitle)
                ? selection.displayTitle : "SONG RESULT";
        if (resultLabel != null)
        {
            resultLabel.text = failed ? "FAILED" : newBest ? "NEW RECORD!" : "CLEAR";
            resultLabel.color = failed ? new Color32(148,48,78,255) : newBest ? new Color32(38,105,84,255) : RuruConsoleUI.Ink;
        }
        if (reactionLabel != null)
            reactionLabel.text = failed
                ? (stabilityFailed
                    ? "피노  ·  앗, 안정화가 떨어졌어! 빨간 탄막을 조금 더 조심하자."
                    : "루루  ·  …괜찮아. 다음에는 노트가 겹치는 순간에 집중해 보자.")
                : (newBest
                    ? "피노  ·  신기록이야! 방금 플레이, 정말 좋았어!"
                    : "루루  ·  …좋아. 끝까지 버텼네. 다음 곡도 해 볼까?");
        if (pinoResultPortrait != null)
        {
            string resultPrefix = failed ? "Pino_Failed_" : "Pino_Clear_";
            Sprite[] extracted = new Sprite[4];
            bool extractedComplete = true;
            for (int i = 0; i < extracted.Length; i++)
            {
                extracted[i] = Resources.Load<Sprite>(
                    "RuruArt/OriginalParts/Characters/" + resultPrefix + i);
                if (extracted[i] == null)
                    extractedComplete = false;
            }

            // Original multi-sprite atlas remains a safe fallback.
            Sprite[] original = Resources.LoadAll<Sprite>(
                failed ? "RuruArt/Characters/피노_False"
                       : "RuruArt/Characters/피노_Clear");
            pinoResultFrames = extractedComplete ? extracted : null;
            Sprite initial = extractedComplete ? extracted[0] :
                original.Length > 0 ? original[0] : null;
            pinoResultPortrait.sprite = initial;
            pinoResultPortrait.gameObject.SetActive(initial != null);
            pinoAnimationStarted = false;
        }
        if (scoreLabel != null)
            scoreLabel.text = score.ToString("N0");
        if (rankLabel != null)
        {
            rankLabel.text = failed ? "F" : RankForScore(score);
            rankLabel.color = failed ? RuruConsoleUI.Rose : RuruConsoleUI.Lilac;
        }
        if (judgmentValues != null)
        {
            int[] values={perfect,great,good,miss};
            for(int i=0;i<values.Length;i++) judgmentValues[i].text=values[i].ToString("N0");
        }
        if(detailValues!=null)
        {
            string[] values={chartPlayer.MaxCombo.ToString("N0"),hitCount.ToString("N0"),
                (bulletDirector!=null ? bulletDirector.GrazeCount : 0).ToString("N0"),
                chartPlayer.EmptyClickCount.ToString("N0"),chartPlayer.AccuracyPercent.ToString("0.0")+"%",
                bestScore.ToString("N0"),FormatTime(elapsed)};
            for(int i=0;i<values.Length;i++) detailValues[i].text=values[i];
        }
        if(scoreFill!=null)
        {
            scoreFill.anchorMax=new Vector2(score/1000000f,1f);
            scoreFill.GetComponent<Image>().color=failed ? RuruConsoleUI.Rose : RuruConsoleUI.Mint;
        }

        chartPlayer.StopChart();
        if (bulletDirector != null)
            bulletDirector.enabled = false;
        if (pauseUi != null)
            pauseUi.enabled = false;

        if (bootSequence != null)
            bootSequence.ShowResultsAfterPowerOff();
        else if (resultPanel != null)
        {
            resultPanel.SetActive(true);
            resultPanel.transform.SetAsLastSibling();
        }
    }

    public static string RankForScore(int score)
    {
        score = Mathf.Clamp(score, 0, 1000000);
        if (score == 1000000) return "SS";
        if (score >= 900000) return "S";
        if (score >= 800000) return "A";
        if (score >= 700000) return "B";
        if (score >= 600000) return "C";
        return "FAILED";
    }

    public static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
    }

    private void BuildFallbackPanel()
    {
        resultPanel = new GameObject("ResultPanel_Runtime",typeof(RectTransform),typeof(Image));
        resultPanel.layer=gameObject.layer;
        RectTransform screen=resultPanel.GetComponent<RectTransform>();
        screen.SetParent(transform,false);
        screen.anchorMin=Vector2.zero; screen.anchorMax=Vector2.one;
        screen.offsetMin=screen.offsetMax=Vector2.zero;
        Image scrim=resultPanel.GetComponent<Image>();
        var resultPaper=Resources.Load<Sprite>("RuruArt/UI/StudioPage");
        scrim.sprite=resultPaper;scrim.color=resultPaper!=null?Color.white:new Color32(230,220,239,255);
        scrim.raycastTarget=true;

        RectTransform stage=RuruConsoleUI.Stage(screen,"ResultConsoleStage");
        var paper=RuruConsoleUI.Paper; var muted=RuruConsoleUI.Muted;
        var lilac=RuruConsoleUI.Lilac; var center=TextAlignmentOptions.Center;
        var frame=RuruConsoleUI.Region(stage,"DeviceOuterFrame",0,0,1,1).gameObject.AddComponent<Image>();
        frame.sprite=Resources.Load<Sprite>("RuruArt/UI/DeviceLightFrame");frame.raycastTarget=false;frame.enabled=false;
        var logo=RuruConsoleUI.Region(stage,"PinoWingMark",.075f,.885f,.125f,.958f).gameObject.AddComponent<Image>();
        logo.sprite=Resources.Load<Sprite>("RuruArt/UI/PinoWingMark");logo.preserveAspect=true;logo.raycastTarget=false;logo.enabled=logo.sprite!=null;
        RuruConsoleUI.Text(stage,"DeviceId","플레이 결과",.135f,.885f,.52f,.958f,43,RuruConsoleUI.Ink,display:true);
        resultLabel=RuruConsoleUI.Text(stage,"Outcome","RESULT",.56f,.885f,.925f,.95f,48,RuruConsoleUI.Ink,TextAlignmentOptions.MidlineRight,true);
        RuruConsoleUI.Strip(stage,"HeaderRule",.075f,.878f,.925f,.881f,lilac);

        var visual=RuruConsoleUI.Region(stage,"PinoMonitor",.055f,.19f,.355f,.85f);
        RuruConsoleUI.Text(visual,"MonitorLabel","피노",.08f,.87f,.92f,.96f,26,RuruConsoleUI.Ink,display:true);
        // Keep the character open on the pale device surface, not inside another window.
        var ring=RuruConsoleUI.Region(visual,"PinoBeatRing",.10f,.26f,.90f,.83f).gameObject.AddComponent<Image>();
        ring.sprite=Resources.Load<Sprite>("RuruArt/UI/BeatTarget");ring.preserveAspect=true;ring.raycastTarget=false;ring.color=new Color(.48f,.37f,.62f,.24f);ring.enabled=ring.sprite!=null;
        var portrait=RuruConsoleUI.Region(visual,"PortraitWell",.055f,.205f,.945f,.87f);
        pinoResultPortrait=portrait.gameObject.AddComponent<Image>();
        pinoResultPortrait.preserveAspect=true; pinoResultPortrait.raycastTarget=false;
        pinoResultPortrait.gameObject.SetActive(false);
        RuruConsoleUI.Frame(visual,"ReactionStrip",.035f,.01f,.965f,.205f,RuruConsoleUI.MainKey);
        reactionLabel=RuruConsoleUI.Text(visual,"PinoReaction","",.09f,.035f,.91f,.185f,24,RuruConsoleUI.Ink);
        reactionLabel.enableWordWrapping=true;

        var stats=RuruConsoleUI.Frame(stage,"ResultStatisticsCard",.37f,.205f,.935f,.85f,RuruConsoleUI.Shell).rectTransform;
        songLabel=RuruConsoleUI.Text(stats,"SongTitle","SONG RESULT",.045f,.86f,.955f,.96f,42,paper);
        var score=RuruConsoleUI.Frame(stats,"ScoreDisplay",.04f,.56f,.96f,.85f,RuruConsoleUI.Inset).rectTransform;
        RuruConsoleUI.Text(score,"ScoreCaption","SCORE",.05f,.69f,.60f,.94f,23,muted,display:true);
        scoreLabel=RuruConsoleUI.Text(score,"ScoreValue","0000000",.045f,.17f,.70f,.76f,91,paper,display:true);
        var rankArt=RuruConsoleUI.Region(score,"RankBadge",.735f,.09f,.965f,.92f).gameObject.AddComponent<Image>();
        rankArt.sprite=Resources.Load<Sprite>("RuruArt/UI/StudioRank");
        rankArt.raycastTarget=false;rankArt.preserveAspect=true;
        if(rankArt.sprite==null){rankArt.sprite=RuruConsoleUI.Shell;rankArt.type=Image.Type.Sliced;rankArt.preserveAspect=false;}
        RuruConsoleUI.Text(score,"RankCaption","RANK",.75f,.71f,.96f,.90f,22,muted,center,true);
        rankLabel=RuruConsoleUI.Text(score,"RankValue","-",.765f,.19f,.945f,.70f,92,lilac,center,true);
        var scoreTrack=RuruConsoleUI.Strip(score,"ScoreTrack",.05f,.075f,.68f,.115f,new Color32(103,84,127,255));
        scoreFill=RuruConsoleUI.Strip(scoreTrack.transform,"ScoreFill",0,0,0,1,lilac).rectTransform;

        string[] j={"PERFECT","GREAT","GOOD","MISS"};
        Color[] jc={RuruConsoleUI.Mint,lilac,paper,RuruConsoleUI.Rose};
        judgmentValues=new TMP_Text[4];
        for(int i=0;i<4;i++)
        {
            float left=.04f+i*.232f;
            var cell=RuruConsoleUI.Region(stats,"Judgment_"+j[i],left,.355f,left+.222f,.535f);
            RuruConsoleUI.Strip(cell,"JudgmentAccent",.06f,.94f,.94f,.96f,jc[i]);
            if(i>0)RuruConsoleUI.Strip(cell,"JudgmentSeparator",0,.10f,.008f,.88f,new Color32(103,84,127,255));
            RuruConsoleUI.Text(cell,"Caption",j[i],.07f,.60f,.93f,.95f,22,jc[i],center,true);
            judgmentValues[i]=RuruConsoleUI.Text(cell,"Value","0",.07f,.06f,.93f,.64f,44,paper,center,true);
        }
        string[] details={"MAX COMBO","피격","그레이즈","헛클릭","정확도","최고 기록"};
        detailValues=new TMP_Text[7];
        for(int i=0;i<6;i++)
        {
            int col=i%3,row=i/3;
            float left=.05f+col*.306f, bottom=.18f-row*.125f;
            RuruConsoleUI.Text(stats,"DetailCaption"+i,details[i],left,bottom+.062f,left+.285f,bottom+.12f,19,muted);
            detailValues[i]=RuruConsoleUI.Text(stats,"DetailValue"+i,"-",left,bottom,left+.285f,bottom+.068f,29,paper,display:true);
        }
        detailValues[6]=RuruConsoleUI.Text(visual,"Elapsed","",.08f,.785f,.92f,.85f,20,RuruConsoleUI.Ink,TextAlignmentOptions.MidlineRight,true);
        RuruConsoleUI.Text(stage,"FooterHint","다음 곡을 선택하거나 다시 도전할 수 있어요.",.075f,.07f,.47f,.165f,21,RuruConsoleUI.Ink,display:true);
        RuruConsoleUI.Key(stage,"ResultRetry","다시 도전",.49f,.07f,.70f,.16f,true,ReloadSong);
        RuruConsoleUI.Key(stage,"ResultSongSelect","곡 선택",.715f,.07f,.925f,.16f,false,ReturnToSongs);
        resultPanel.SetActive(false);
    }

    private void ReloadSong()
    {
        if (SceneLoadingTransition.IsLoading)
            return;
        SceneLoadingTransition.Load("Mouse");
    }

    private void ReturnToSongs()
    {
        if (SceneLoadingTransition.IsLoading)
            return;
        if (SceneLoadingTransition.Load("SongSelectScene"))
            MouseModeSongSelection.Choose(null);
    }
}
