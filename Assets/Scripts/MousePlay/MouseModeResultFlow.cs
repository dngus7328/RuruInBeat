using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LuluInBeat.MouseMode.ChartSystem;

/// <summary>
/// Bridges chart completion to the existing monitor power-off transition.
/// The scene's authored frames, note positions and pause panel are untouched.
/// Until an art-directed ResultPanel is assigned, a plain functional panel is
/// created only at runtime. No prefab/scene asset is written.
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
    private TMP_Text breakdownLabel;
    private TMP_Text hitLabel;
    private TMP_Text reactionLabel;
    private Image pinoResultPortrait;
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
            resultLabel.color = failed ? new Color(.83f,.34f,.45f,1f) :
                newBest ? new Color(.20f,.49f,.43f,1f) :
                new Color(.34f,.23f,.53f,1f);
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
            Sprite[] frames = Resources.LoadAll<Sprite>(
                failed ? "RuruArt/Characters/피노_False"
                       : "RuruArt/Characters/피노_Clear");
            if (frames.Length > 0) pinoResultPortrait.sprite = frames[0];
            pinoResultPortrait.gameObject.SetActive(frames.Length > 0);
        }
        if (scoreLabel != null)
            scoreLabel.text = score.ToString("N0");
        if (rankLabel != null)
        {
            rankLabel.text = failed ? "F" : RankForScore(score);
            rankLabel.color = failed ? new Color(.83f,.34f,.45f,1f) :
                new Color(.37f,.24f,.58f,1f);
        }
        if (breakdownLabel != null)
            breakdownLabel.text =
                "PERFECT  " + perfect +
                "      GREAT  " + great +
                "      GOOD  " + good +
                "      MISS  " + miss;
        if (hitLabel != null)
            hitLabel.text =
                "MAX COMBO  " + chartPlayer.MaxCombo +
                "       피격  " + hitCount +
                "       그레이즈  " + (bulletDirector != null ?
                    bulletDirector.GrazeCount : 0) + "\n" +
                "헛클릭  " + chartPlayer.EmptyClickCount +
                "       정확도  " + chartPlayer.AccuracyPercent.ToString("0.0") +
                "%       플레이  " + FormatTime(elapsed) +
                "       최고 기록  " + bestScore.ToString("N0");

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
        resultPanel = new GameObject("ResultPanel_Runtime",
            typeof(RectTransform), typeof(Image));
        RectTransform screen = resultPanel.GetComponent<RectTransform>();
        screen.SetParent(transform, false);
        Stretch(screen);
        Image scrim = resultPanel.GetComponent<Image>();
        scrim.color = new Color(.055f, .047f, .078f, .98f);
        scrim.raycastTarget = true;

        // Keep the Title, Song Select and Result screens visually related
        // by referencing the user's ORIGINAL TitleUI PNGs (not copies).
        RuruOriginalUITheme originalUI =
            Resources.Load<RuruOriginalUITheme>("RuruOriginalUITheme");
        Sprite panelArt = originalUI != null &&
            originalUI.confirmationPanel != null
            ? originalUI.confirmationPanel
            : Resources.Load<Sprite>("RuruArt/UI/UI_Panel");
        Sprite buttonArt = originalUI != null &&
            originalUI.titleButton != null
            ? originalUI.titleButton
            : Resources.Load<Sprite>("RuruArt/UI/UI_Button");
        Color primary = panelArt != null
            ? new Color(.15f,.10f,.22f,1f) : Color.white;
        Color subdued = panelArt != null
            ? new Color(.39f,.29f,.50f,1f) : new Color(.78f,.70f,.88f,1f);

        // Shared 9-slice art, not a stretched screenshot. All statistics stay
        // outside the cut corners and the bottom actions remain reachable.
        RectTransform card = Rect("ResultStatisticsCard", screen,
            new Vector2(.135f,.225f), new Vector2(.865f,.815f));
        Image cardImage = card.gameObject.AddComponent<Image>();
        cardImage.sprite = panelArt;
        cardImage.type = SupportsNineSlice(panelArt)
            ? Image.Type.Sliced : Image.Type.Simple;
        cardImage.color = panelArt != null ? Color.white :
            new Color(.22f,.16f,.30f,1f);
        cardImage.raycastTarget = false;

        resultLabel = Label(screen, "RESULT", .14f,.86f,.84f,.93f,55f,
            new Color(.92f,.88f,1f,1f),TextAlignmentOptions.Center);
        songLabel = Label(card, "SONG RESULT", .10f,.90f,.83f,.94f,26f,
            subdued,TextAlignmentOptions.Center);

        Label(card,"SCORE",.30f,.69f,.70f,.78f,22f,subdued,
            TextAlignmentOptions.MidlineLeft);
        scoreLabel = Label(card,"0",.30f,.71f,.49f,.72f,87f,primary,
            TextAlignmentOptions.MidlineLeft);
        Label(card,"RANK",.72f,.88f,.70f,.78f,22f,subdued,
            TextAlignmentOptions.Center);
        rankLabel = Label(card,"C",.72f,.88f,.49f,.72f,82f,primary,
            TextAlignmentOptions.Center);

        // Keep all four judgements on a single legible row, with secondary
        // session details on two separate lines underneath.
        breakdownLabel = Label(card,"",.25f,.92f,.39f,.49f,26f,primary,
            TextAlignmentOptions.Center);
        hitLabel = Label(card,"",.24f,.93f,.26f,.40f,20f,subdued,
            TextAlignmentOptions.Center);
        hitLabel.enableWordWrapping = true;

        reactionLabel = Label(card,"",.24f,.93f,.11f,.26f,23f,primary,
            TextAlignmentOptions.MidlineLeft);
        reactionLabel.enableWordWrapping = true;

        // The existing Pino clear/fail illustration is used as-is.
        // No new character design or unrelated character portrait is invented.
        RectTransform portrait = Rect("PinoResult_ExistingSprite",card,
            new Vector2(.075f,.31f),new Vector2(.27f,.75f));
        pinoResultPortrait = portrait.gameObject.AddComponent<Image>();
        pinoResultPortrait.preserveAspect = true;
        pinoResultPortrait.raycastTarget = false;
        pinoResultPortrait.gameObject.SetActive(false);

        AddButton(screen,"다시 도전",.285f,.495f,ReloadSong,buttonArt);
        AddButton(screen,"곡 선택",.505f,.715f,ReturnToSongs,buttonArt);
        resultPanel.SetActive(false);
    }

    private static bool SupportsNineSlice(Sprite sprite)
    {
        if (sprite == null) return false;
        Vector4 border = sprite.border;
        return border.x > 0f || border.y > 0f ||
               border.z > 0f || border.w > 0f;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static RectTransform Rect(string name, Transform parent,
        Vector2 min, Vector2 max)
    {
        GameObject obj = new GameObject(name,typeof(RectTransform),
            typeof(CanvasRenderer));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent,false);
        rect.anchorMin=min;
        rect.anchorMax=max;
        rect.offsetMin=rect.offsetMax=Vector2.zero;
        return rect;
    }

    private static TMP_FontAsset FindArtFont()
    {
        foreach (TMP_FontAsset font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            if (font != null && font.name.IndexOf("Paperlogy",
                    StringComparison.OrdinalIgnoreCase)>=0)
                return font;
        return TMP_Settings.defaultFontAsset;
    }

    private static TMP_Text Label(RectTransform parent,string value,
        float xMin,float xMax,float yMin,float yMax,float fontSize,Color color,
        TextAlignmentOptions align)
    {
        RectTransform rect=Rect("ResultText",parent,
            new Vector2(xMin,yMin),new Vector2(xMax,yMax));
        TextMeshProUGUI text=rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font=FindArtFont();
        text.text=value;
        text.fontSize=fontSize;
        text.color=color;
        text.alignment=align;
        text.enableWordWrapping=false;
        text.overflowMode=TextOverflowModes.Ellipsis;
        text.raycastTarget=false;
        return text;
    }

    private static void AddButton(RectTransform parent,string text,
        float xMin,float xMax,UnityEngine.Events.UnityAction action,Sprite art)
    {
        RectTransform rect=Rect("ResultButton_"+text,parent,
            new Vector2(xMin,.105f),new Vector2(xMax,.195f));
        Image background=rect.gameObject.AddComponent<Image>();
        background.color=art!=null ? Color.white :
            new Color(.40f,.31f,.53f,1f);
        background.sprite=art;
        background.type=SupportsNineSlice(art)
            ? Image.Type.Sliced : Image.Type.Simple;

        Button button=rect.gameObject.AddComponent<Button>();
        button.targetGraphic=background;
        button.navigation=new Navigation{mode=Navigation.Mode.None};
        ColorBlock colors=button.colors;
        colors.normalColor=Color.white;
        colors.highlightedColor=new Color(.95f,.90f,1f,1f);
        colors.pressedColor=new Color(.81f,.74f,.90f,1f);
        colors.selectedColor=colors.highlightedColor;
        button.colors=colors;
        button.onClick.AddListener(action);

        RectTransform caption=Rect("Caption",rect,Vector2.zero,Vector2.one);
        TextMeshProUGUI label=caption.gameObject.AddComponent<TextMeshProUGUI>();
        label.font=FindArtFont();
        label.text=text;
        label.fontSize=29f;
        label.color=art!=null ? new Color(.15f,.10f,.22f,1f):Color.white;
        label.alignment=TextAlignmentOptions.Center;
        label.raycastTarget=false;
        label.enableWordWrapping=false;
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
