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
        Finish(false);
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
        float elapsed = chartPlayer.SongElapsedSeconds;
        int hitCount = bulletDirector != null ? bulletDirector.HitCount : 0;
        int perfect = chartPlayer.PerfectCount;
        int great = chartPlayer.GreatCount;
        int good = chartPlayer.GoodCount;
        int miss = chartPlayer.MissCount;
        MouseModeChartAsset selection = MouseModeSongSelection.CurrentChart != null
            ? MouseModeSongSelection.CurrentChart : chartPlayer.ActiveChartAsset;

        if (songLabel != null)
            songLabel.text = selection != null &&
                !string.IsNullOrWhiteSpace(selection.displayTitle)
                ? selection.displayTitle : "SONG RESULT";
        if (resultLabel != null)
            resultLabel.text = stabilityFailed ? "FAILED" : "RESULT";
        if (scoreLabel != null)
            scoreLabel.text = score.ToString("N0");
        if (rankLabel != null)
            rankLabel.text = "RANK  " + (stabilityFailed ? "FAILED" : RankForScore(score));
        if (breakdownLabel != null)
            breakdownLabel.text = "PERFECT " + perfect +
                "     GREAT " + great +
                "     GOOD " + good +
                "     MISS " + miss;
        if (hitLabel != null)
            hitLabel.text = "HITS " + hitCount +
                "       TIME " + FormatTime(elapsed);

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
        // Minimal placeholder only. No neon, frame redesign or imported art.
        // Replacing this later with an authored ResultPanel remains possible.
        resultPanel = new GameObject("ResultPanel_Runtime",
            typeof(RectTransform), typeof(Image));
        RectTransform panel = resultPanel.GetComponent<RectTransform>();
        panel.SetParent(transform, false);
        Stretch(panel);
        Image background = resultPanel.GetComponent<Image>();
        background.color = new Color(0.09f, 0.075f, 0.13f, 0.98f);
        background.raycastTarget = true;

        Color primary = new Color(0.96f, 0.94f, 0.98f, 1f);
        Color muted = new Color(0.73f, 0.67f, 0.79f, 1f);

        resultLabel = Label(panel, "RESULT", .67f, .77f, 44, primary);
        songLabel = Label(panel, "SONG RESULT", .60f, .67f, 24, muted);
        scoreLabel = Label(panel, "0", .43f, .60f, 68, primary);
        rankLabel = Label(panel, "RANK", .37f, .45f, 37, primary);
        breakdownLabel = Label(panel, "", .29f, .35f, 22, primary);
        hitLabel = Label(panel, "", .23f, .28f, 21, muted);

        AddButton(panel, "RETRY", .27f, .49f, ReloadSong);
        AddButton(panel, "SONG SELECT", .51f, .73f, ReturnToSongs);
        resultPanel.SetActive(false);
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
        GameObject obj = new GameObject(name, typeof(RectTransform));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static TMP_Text Label(RectTransform parent, string value,
        float yMin, float yMax, float fontSize, Color color)
    {
        RectTransform rect = Rect("Result_" + value.Replace(" ", "_"),
            parent, new Vector2(.10f, yMin), new Vector2(.90f, yMax));
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        return text;
    }

    private static void AddButton(RectTransform parent, string text,
        float xMin, float xMax, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = Rect("ResultButton_" + text.Replace(" ", "_"),
            parent, new Vector2(xMin, .085f), new Vector2(xMax, .18f));
        Image background = rect.gameObject.AddComponent<Image>();
        background.color = new Color(.26f, .22f, .32f, 1f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(action);

        RectTransform textRect = Rect("Label", rect, Vector2.zero, Vector2.one);
        TMP_Text label = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = 23f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
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
