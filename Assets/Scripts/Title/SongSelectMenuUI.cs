using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LuluInBeat.MouseMode.ChartSystem;

/// <summary>
/// Song selection built from editor-created MouseModeChartAsset files.
/// All existing user-authored RectTransforms, anchors, sizes and hierarchy
/// remain untouched. Only text, colors, sprites and button state are updated.
/// </summary>
[DisallowMultipleComponent]
public sealed class SongSelectMenuUI : MonoBehaviour
{
    // Legacy scene references are retained so a previously generated
    // SongSelectScene still functions without replacing the user's layout.
    [Serializable]
    public sealed class Track
    {
        public string displayTitle;
        public Sprite jacket;
        public MouseModeChartAsset[] charts = Array.Empty<MouseModeChartAsset>();
        public string[] sceneNames = Array.Empty<string>();
    }

    [SerializeField, HideInInspector]
    private Track[] tracks = Array.Empty<Track>();

    [Header("Automatic catalog: updated when editor chart .assets change")]
    [SerializeField] private SongSelectChartCatalog catalog;

    [Header("Optional manual chart asset override (empty = auto catalog)")]
    [SerializeField] private MouseModeChartAsset[] chartFiles =
        Array.Empty<MouseModeChartAsset>();

    [Header("Existing editable list slots")]
    [SerializeField] private Button[] trackButtons = Array.Empty<Button>();
    [SerializeField] private Image[] trackRowBackgrounds = Array.Empty<Image>();
    [SerializeField] private TMP_Text[] trackRowLabels = Array.Empty<TMP_Text>();

    [Header("Selected track info")]
    [SerializeField] private TMP_Text selectedTitle;
    [SerializeField] private TMP_Text selectedLevel;
    [SerializeField] private TMP_Text selectedBpm;
    [SerializeField] private TMP_Text selectedDuration;
    [SerializeField] private TMP_Text selectedDifficulty;
    [SerializeField] private TMP_Text previewStatus;
    [SerializeField] private Image jacketImage;
    [SerializeField] private GameObject jacketPlaceholder;

    [Header("Buttons")]
    [SerializeField] private Button previousDifficultyButton;
    [SerializeField] private Button nextDifficultyButton;
    [SerializeField] private Button previewButton;
    [SerializeField] private Button playButton;
    [SerializeField] private Button backButton;
    [SerializeField] private AudioSource previewSource;

    [Header("Shared mouse gameplay scene")]
    [SerializeField] private string gameplaySceneName = "Mouse";

    [Header("Selection colors and short preview")]
    [SerializeField] private Color normalRow =
        new Color(.15f, .145f, .22f, 1f);
    [SerializeField] private Color selectedRow =
        new Color(.32f, .27f, .43f, 1f);
    [SerializeField, Range(0f, 1f)] private float previewVolume = .28f;
    [SerializeField, Range(5f, 25f)] private float previewSegmentSeconds = 15f;

    private sealed class SongEntry
    {
        public AudioClip clip;
        public string title;
        public Sprite jacket;
        public readonly List<MouseModeChartAsset> charts =
            new List<MouseModeChartAsset>();
    }

    private readonly List<SongEntry> songs = new List<SongEntry>();
    private Sprite fallbackJacket;
    private TMP_Text trackCountLabel;
    private TMP_Text[] rowNumbers;
    private RectTransform listArea;
    private int selectedIndex;
    private int difficultyIndex;
    private bool previewEnabled = true;
    private float previewStart, previewEnd;

#if UNITY_EDITOR
    /// <summary>First-time wiring only. Never called by runtime.</summary>
    public void ConfigureInitialLayout(
        Button[] buttons, Image[] backgrounds, TMP_Text[] rowTitles,
        TMP_Text title, TMP_Text level, TMP_Text bpm,
        TMP_Text duration, TMP_Text difficulty, TMP_Text status,
        Image cover, GameObject noCover, Button previousDifficulty,
        Button nextDifficulty, Button preview, Button play, Button back,
        AudioSource source)
    {
        trackButtons = buttons;
        trackRowBackgrounds = backgrounds;
        trackRowLabels = rowTitles;
        selectedTitle = title;
        selectedLevel = level;
        selectedBpm = bpm;
        selectedDuration = duration;
        selectedDifficulty = difficulty;
        previewStatus = status;
        jacketImage = cover;
        jacketPlaceholder = noCover;
        previousDifficultyButton = previousDifficulty;
        nextDifficultyButton = nextDifficulty;
        previewButton = preview;
        playButton = play;
        backButton = back;
        previewSource = source;
    }
#endif

    private void Awake()
    {
        // Lookup only; no creation, resizing, reparenting or positioning.
        fallbackJacket = jacketImage != null ? jacketImage.sprite : null;
        listArea = transform.Find("TrackListPanel") as RectTransform;
        trackCountLabel = transform.Find("TrackCount") != null ?
            transform.Find("TrackCount").GetComponent<TMP_Text>() : null;

        rowNumbers = new TMP_Text[trackButtons.Length];
        for (int i = 0; i < trackButtons.Length; i++)
        {
            int rowIndex = i;
            if (trackButtons[i] == null) continue;
            Transform number = trackButtons[i].transform.Find("Index");
            if (number != null)
                rowNumbers[i] = number.GetComponent<TMP_Text>();

            trackButtons[i].onClick.AddListener(() =>
            {
                int pageStart = selectedIndex / PageSize * PageSize;
                SelectTrack(pageStart + rowIndex);
            });
        }

        if (previousDifficultyButton != null)
            previousDifficultyButton.onClick.AddListener(
                () => ChangeDifficulty(-1));
        if (nextDifficultyButton != null)
            nextDifficultyButton.onClick.AddListener(
                () => ChangeDifficulty(1));
        if (previewButton != null)
            previewButton.onClick.AddListener(TogglePreview);
        if (playButton != null)
            playButton.onClick.AddListener(PlaySelected);
        if (backButton != null)
            backButton.onClick.AddListener(BackToTitle);

        if (previewSource != null)
        {
            previewSource.playOnAwake = false;
            previewSource.loop = false;
            previewSource.volume = previewVolume;
        }

        BuildCatalog();
        SelectTrack(0);
    }

    private int PageSize
    {
        get { return Mathf.Max(1, trackButtons.Length); }
    }

    private void BuildCatalog()
    {
        songs.Clear();
        var unique = new HashSet<MouseModeChartAsset>();
        MouseModeChartAsset[] input = chartFiles;
        if (input == null || input.Length == 0)
        {
            if (catalog == null)
                catalog = Resources.Load<SongSelectChartCatalog>(
                    "SongSelectChartCatalog");
            input = catalog != null ? catalog.chartFiles : null;
        }

        if (input != null)
            foreach (MouseModeChartAsset chart in input)
                RegisterChart(chart, unique, null);

        // Backwards-compatible fallback for a scene created by the original
        // six-song script before catalog auto-import has completed.
        if (songs.Count == 0 && tracks != null)
        {
            foreach (Track item in tracks)
            {
                if (item == null || item.charts == null) continue;
                foreach (MouseModeChartAsset chart in item.charts)
                    RegisterChart(chart, unique, item.jacket);
            }
        }

        foreach (SongEntry song in songs)
        {
            song.charts.Sort((a, b) =>
                a.data.level.CompareTo(b.data.level));

            MouseModeChartAsset first = song.charts[0];
            song.title = string.IsNullOrWhiteSpace(first.displayTitle)
                ? first.name : first.displayTitle;
        }

        // Auto-import may not finish the first time the editor loads.
        if (songs.Count == 0)
            Debug.LogWarning("[SongSelect] No valid MouseModeChartAsset with " +
                "audio and level 1..15. Open Unity Editor and refresh the chart catalog.");
    }

    private void RegisterChart(MouseModeChartAsset chart,
        HashSet<MouseModeChartAsset> unique, Sprite jacket)
    {
        if (chart == null || !unique.Add(chart) || chart.song == null ||
            chart.data == null || chart.data.level < 1 ||
            chart.data.level > 15 || chart.data.notes == null ||
            chart.data.notes.Count == 0)
            return;

        SongEntry found = null;
        foreach (SongEntry existing in songs)
            if (existing.clip == chart.song) { found = existing; break; }

        if (found == null)
        {
            found = new SongEntry
            {
                clip = chart.song,
                title = chart.displayTitle,
                jacket = jacket
            };
            songs.Add(found);
        }
        if (found.jacket == null && jacket != null)
            found.jacket = jacket;
        found.charts.Add(chart);
    }

    private SongEntry CurrentSong
    {
        get
        {
            return selectedIndex >= 0 && selectedIndex < songs.Count
                ? songs[selectedIndex] : null;
        }
    }

    private MouseModeChartAsset CurrentChart
    {
        get
        {
            SongEntry song = CurrentSong;
            return song == null || song.charts.Count == 0
                ? null : song.charts[
                    Mathf.Clamp(difficultyIndex, 0,
                        song.charts.Count - 1)];
        }
    }

    private void SelectTrack(int index)
    {
        if (songs.Count == 0)
        {
            RefreshList();
            RefreshDetails();
            return;
        }
        if (index < 0 || index >= songs.Count) return;
        selectedIndex = index;
        difficultyIndex = 0;
        RefreshList();
        RefreshDetails();
    }

    private void ChangeDifficulty(int direction)
    {
        SongEntry song = CurrentSong;
        if (song == null || song.charts.Count < 2) return;
        difficultyIndex =
            (difficultyIndex + direction + song.charts.Count) %
            song.charts.Count;
        RefreshDetails();
    }

    private void RefreshList()
    {
        int pageStart = selectedIndex / PageSize * PageSize;
        if (trackCountLabel != null)
        {
            int page = selectedIndex / PageSize + 1;
            int pages = Mathf.Max(1,
                Mathf.CeilToInt(songs.Count / (float)PageSize));
            trackCountLabel.text = songs.Count.ToString("00") +
                " TRACKS   " + page + "/" + pages;
        }

        for (int slot = 0; slot < trackButtons.Length; slot++)
        {
            int index = pageStart + slot;
            bool valid = index < songs.Count;
            if (trackButtons[slot] != null)
                trackButtons[slot].interactable = valid;
            if (slot < trackRowLabels.Length &&
                trackRowLabels[slot] != null)
            {
                trackRowLabels[slot].text = valid
                    ? songs[index].title + "   LV " +
                      songs[index].charts[0].data.level.ToString("00")
                    : "";
            }

            if (rowNumbers[slot] != null)
                rowNumbers[slot].text = valid
                    ? (index + 1).ToString("00") : "";
            if (slot < trackRowBackgrounds.Length &&
                trackRowBackgrounds[slot] != null)
                trackRowBackgrounds[slot].color = valid &&
                    index == selectedIndex ? selectedRow : normalRow;
        }
    }

    private void RefreshDetails()
    {
        SongEntry song = CurrentSong;
        MouseModeChartAsset chart = CurrentChart;
        bool valid = chart != null && chart.data != null;

        if (selectedTitle != null)
            selectedTitle.text = song != null
                ? song.title : "NO SONGS FOUND";
        if (selectedLevel != null)
            selectedLevel.text = valid
                ? "LEVEL  " + chart.data.level.ToString("00") + " / 15"
                : "LEVEL  -- / 15";
        if (selectedDifficulty != null)
            selectedDifficulty.text = valid
                ? (chart.data.difficulty ?? "MOUSE").Replace("_", " ")
                : "NO CHART";
        if (selectedBpm != null)
            selectedBpm.text = valid
                ? chart.data.bpm.ToString("0.##") + " BPM" : "-- BPM";
        if (selectedDuration != null)
        {
            float seconds = chart != null && chart.song != null
                ? chart.song.length : 0f;
            selectedDuration.text = seconds > 0f
                ? Mathf.FloorToInt(seconds / 60f).ToString("00") + ":" +
                  Mathf.FloorToInt(seconds % 60f).ToString("00")
                : "--:--";
        }

        int count = song != null ? song.charts.Count : 0;
        if (previousDifficultyButton != null)
            previousDifficultyButton.interactable = count > 1;
        if (nextDifficultyButton != null)
            nextDifficultyButton.interactable = count > 1;
        if (playButton != null)
            playButton.interactable = valid &&
                Application.CanStreamedLevelBeLoaded(gameplaySceneName);

        if (jacketImage != null)
        {
            Sprite art = song != null && song.jacket != null
                ? song.jacket : fallbackJacket;
            bool hasArt = art != null;
            jacketImage.sprite = art;
            jacketImage.color = hasArt ? Color.white :
                new Color(.19f, .175f, .26f, 1f);
            jacketImage.preserveAspect = hasArt;
            if (jacketPlaceholder != null)
                jacketPlaceholder.SetActive(!hasArt);
        }

        StartPreview();
    }

    private void StartPreview()
    {
        if (previewSource == null) return;
        previewSource.Stop();
        MouseModeChartAsset chart = CurrentChart;
        AudioClip clip = chart != null ? chart.song : null;
        previewSource.clip = clip;
        if (clip == null)
        {
            if (previewStatus != null)
                previewStatus.text = "PREVIEW UNAVAILABLE";
            return;
        }

        previewStart = Mathf.Min(clip.length * .22f,
            Mathf.Max(0f, clip.length - previewSegmentSeconds - 1f));
        previewEnd = Mathf.Min(clip.length - .2f,
            previewStart + previewSegmentSeconds);
        if (previewEnd <= previewStart)
        {
            previewStart = 0f;
            previewEnd = clip.length;
        }

        if (previewEnabled)
        {
            previewSource.time = previewStart;
            previewSource.Play();
        }
        if (previewStatus != null)
            previewStatus.text = previewEnabled
                ? "PREVIEW  ON" : "PREVIEW  OFF";
    }

    private void Update()
    {
        // Mouse wheel selects tracks without moving any user-placed UI row.
        if (listArea != null && songs.Count > 0 &&
            RectTransformUtility.RectangleContainsScreenPoint(
                listArea, Input.mousePosition))
        {
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > .01f)
                SelectTrack(Mathf.Clamp(selectedIndex -
                    (wheel > 0f ? 1 : -1), 0, songs.Count - 1));
        }

        if (!previewEnabled || previewSource == null ||
            previewSource.clip == null)
            return;

        if (!previewSource.isPlaying ||
            previewSource.time >= previewEnd)
        {
            previewSource.Stop();
            previewSource.time = previewStart;
            previewSource.Play();
        }
    }

    private void TogglePreview()
    {
        previewEnabled = !previewEnabled;
        StartPreview();
    }

    private void PlaySelected()
    {
        MouseModeChartAsset chart = CurrentChart;
        if (chart == null || !Application.CanStreamedLevelBeLoaded(
            gameplaySceneName))
        {
            Debug.LogError("[SongSelect] Missing chart or game scene: " +
                gameplaySceneName);
            return;
        }
        if (previewSource != null)
            previewSource.Stop();

        // The shared Mouse scene reads this before startup/countdown.
        MouseModeSongSelection.Choose(chart);
        SceneManager.LoadScene(gameplaySceneName);
    }

    private void BackToTitle()
    {
        if (previewSource != null)
            previewSource.Stop();
        MouseModeSongSelection.Choose(null);
        if (Application.CanStreamedLevelBeLoaded("TitleScene"))
            SceneManager.LoadScene("TitleScene");
        else
            Debug.LogError("[SongSelect] TitleScene is not in Build Settings.");
    }
}
