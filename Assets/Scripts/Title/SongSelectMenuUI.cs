using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LuluInBeat.MouseMode.ChartSystem;

/// <summary>
/// Data and button behavior for the editable, one-time authored SongSelectScene.
/// This component NEVER changes RectTransforms, anchors, positions or hierarchy.
/// Replace colors, fonts, images, and scene layout freely in the Unity Editor.
/// </summary>
[DisallowMultipleComponent]
public sealed class SongSelectMenuUI : MonoBehaviour
{
    [Serializable]
    public sealed class Track
    {
        public string displayTitle;
        public Sprite jacket;
        public MouseModeChartAsset[] charts = Array.Empty<MouseModeChartAsset>();
        public string[] sceneNames = Array.Empty<string>();
    }

    [Header("Tracks (song artwork can be replaced later)")]
    [SerializeField] private Track[] tracks = Array.Empty<Track>();

    [Header("List of six songs")]
    [SerializeField] private Button[] trackButtons = Array.Empty<Button>();
    [SerializeField] private Image[] trackRowBackgrounds = Array.Empty<Image>();
    [SerializeField] private TMP_Text[] trackRowLabels = Array.Empty<TMP_Text>();

    [Header("Details (the script changes text, never geometry)")]
    [SerializeField] private TMP_Text selectedTitle;
    [SerializeField] private TMP_Text selectedLevel;
    [SerializeField] private TMP_Text selectedBpm;
    [SerializeField] private TMP_Text selectedDuration;
    [SerializeField] private TMP_Text selectedDifficulty;
    [SerializeField] private TMP_Text previewStatus;
    [SerializeField] private Image jacketImage;
    [SerializeField] private GameObject jacketPlaceholder;

    [Header("Controls")]
    [SerializeField] private Button previousDifficultyButton;
    [SerializeField] private Button nextDifficultyButton;
    [SerializeField] private Button previewButton;
    [SerializeField] private Button playButton;
    [SerializeField] private Button backButton;
    [SerializeField] private AudioSource previewSource;

    [Header("State colors (only colors change while playing)")]
    [SerializeField] private Color normalRow = new Color(.15f, .145f, .22f, 1f);
    [SerializeField] private Color selectedRow = new Color(.32f, .27f, .43f, 1f);
    [SerializeField, Range(0f, 1f)] private float previewVolume = .28f;
    [SerializeField, Range(5f, 25f)] private float previewSegmentSeconds = 15f;

    private int selectedIndex;
    private int difficultyIndex;
    private bool previewEnabled = true;
    private float previewStart;
    private float previewEnd;
    private Sprite fallbackJacketSprite;

#if UNITY_EDITOR
    /// <summary>Called only by the one-time editor builder on the previously blank scene.</summary>
    public void ConfigureInitialLayout(
        Track[] initialTracks, Button[] buttons, Image[] backgrounds,
        TMP_Text[] rowTitles, TMP_Text title, TMP_Text level, TMP_Text bpm,
        TMP_Text duration, TMP_Text difficulty, TMP_Text status,
        Image cover, GameObject noCover, Button previousDifficulty,
        Button nextDifficulty, Button preview, Button play, Button back,
        AudioSource source)
    {
        tracks = initialTracks;
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
        // Preserve an image the user manually assigned to the existing slot.
        // Per-song jackets override it, but the fallback is never destroyed.
        fallbackJacketSprite = jacketImage != null ? jacketImage.sprite : null;

        for (int i = 0; i < trackButtons.Length; i++)
        {
            int trackIndex = i;
            if (trackButtons[i] != null)
                trackButtons[i].onClick.AddListener(() => SelectTrack(trackIndex));
        }

        if (previousDifficultyButton != null)
            previousDifficultyButton.onClick.AddListener(() => ChangeDifficulty(-1));
        if (nextDifficultyButton != null)
            nextDifficultyButton.onClick.AddListener(() => ChangeDifficulty(1));
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

        SelectTrack(0);
    }

    private Track CurrentTrack
    {
        get
        {
            if (tracks == null || tracks.Length == 0) return null;
            return tracks[Mathf.Clamp(selectedIndex, 0, tracks.Length - 1)];
        }
    }

    private MouseModeChartAsset CurrentChart
    {
        get
        {
            Track track = CurrentTrack;
            if (track == null || track.charts == null ||
                track.charts.Length == 0)
                return null;
            return track.charts[Mathf.Clamp(difficultyIndex, 0,
                track.charts.Length - 1)];
        }
    }

    private void SelectTrack(int index)
    {
        if (tracks == null || index < 0 || index >= tracks.Length)
            return;

        selectedIndex = index;
        difficultyIndex = 0;
        RefreshList();
        RefreshDetails();
    }

    private void RefreshList()
    {
        for (int i = 0; i < trackRowBackgrounds.Length; i++)
            if (trackRowBackgrounds[i] != null)
                trackRowBackgrounds[i].color =
                    i == selectedIndex ? selectedRow : normalRow;

        for (int i = 0; i < trackRowLabels.Length; i++)
        {
            if (trackRowLabels[i] == null || i >= tracks.Length)
                continue;

            Track track = tracks[i];
            int level = track.charts != null && track.charts.Length > 0 &&
                track.charts[0] != null && track.charts[0].data != null
                ? track.charts[0].data.level : 0;
            trackRowLabels[i].text = track.displayTitle +
                (level > 0 ? "       LV " + level.ToString("00") : "");
        }
    }

    private void RefreshDetails()
    {
        Track track = CurrentTrack;
        MouseModeChartAsset chart = CurrentChart;
        if (track == null)
            return;

        if (selectedTitle != null)
            selectedTitle.text = track.displayTitle;

        bool playable = chart != null && chart.data != null;
        if (selectedLevel != null)
            selectedLevel.text = playable
                ? "LEVEL  " + chart.data.level.ToString("00") + " / 15"
                : "LEVEL  -- / 15";

        if (selectedBpm != null)
            selectedBpm.text = playable
                ? chart.data.bpm.ToString("0.##") + " BPM"
                : "-- BPM";

        if (selectedDifficulty != null)
            selectedDifficulty.text = playable
                ? chart.data.difficulty.Replace("_", " ")
                : "NO CHART";

        if (selectedDuration != null)
        {
            float seconds = chart != null && chart.song != null
                ? chart.song.length : 0f;
            selectedDuration.text = seconds > 0f
                ? Mathf.FloorToInt(seconds / 60f).ToString("00") + ":" +
                  Mathf.FloorToInt(seconds % 60f).ToString("00")
                : "--:--";
        }

        int count = track.charts == null ? 0 : track.charts.Length;
        if (previousDifficultyButton != null)
            previousDifficultyButton.interactable = count > 1;
        if (nextDifficultyButton != null)
            nextDifficultyButton.interactable = count > 1;
        if (playButton != null)
        {
            bool sceneReady = track.sceneNames != null &&
                difficultyIndex < track.sceneNames.Length &&
                !string.IsNullOrEmpty(track.sceneNames[difficultyIndex]) &&
                Application.CanStreamedLevelBeLoaded(
                    track.sceneNames[difficultyIndex]);
            playButton.interactable = playable && sceneReady;
        }

        // Only images are swapped when assigned. Geometry stays in the scene.
        if (jacketImage != null)
        {
            Sprite art = track.jacket != null
                ? track.jacket : fallbackJacketSprite;
            bool hasArt = art != null;
            jacketImage.sprite = art;
            jacketImage.color = hasArt
                ? Color.white : new Color(.19f, .175f, .26f, 1f);
            jacketImage.preserveAspect = hasArt;
            if (jacketPlaceholder != null)
                jacketPlaceholder.SetActive(!hasArt);
        }

        StartPreview();
    }

    private void ChangeDifficulty(int delta)
    {
        Track track = CurrentTrack;
        if (track == null || track.charts == null ||
            track.charts.Length <= 1) return;

        difficultyIndex = (difficultyIndex + delta + track.charts.Length) %
            track.charts.Length;
        RefreshDetails();
    }

    private void StartPreview()
    {
        if (previewSource == null)
            return;

        previewSource.Stop();
        MouseModeChartAsset chart = CurrentChart;
        AudioClip clip = chart == null ? null : chart.song;

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
        if (!previewEnabled || previewSource == null ||
            previewSource.clip == null)
            return;

        if (!previewSource.isPlaying || previewSource.time >= previewEnd)
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
        Track track = CurrentTrack;
        if (track == null || track.sceneNames == null ||
            difficultyIndex >= track.sceneNames.Length)
            return;

        string scene = track.sceneNames[difficultyIndex];
        if (string.IsNullOrEmpty(scene) ||
            !Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError("[SongSelect] Build Settings is missing: " + scene);
            return;
        }

        if (previewSource != null)
            previewSource.Stop();
        SceneManager.LoadScene(scene);
    }

    private void BackToTitle()
    {
        if (previewSource != null)
            previewSource.Stop();

        if (Application.CanStreamedLevelBeLoaded("TitleScene"))
            SceneManager.LoadScene("TitleScene");
        else
            Debug.LogError("[SongSelect] TitleScene is not in Build Settings.");
    }
}
