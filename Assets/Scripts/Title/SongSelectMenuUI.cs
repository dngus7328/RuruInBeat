using System;
using System.Collections;
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
    [Header("PC music library / no layout movement")]
    [SerializeField] private bool enableMatteSelectionOutline = true;
    [Header("Arcade-only interface translation")]
    [SerializeField] private bool useArcadeLocalization = false;
    [SerializeField] private SongSelectLanguage interfaceLanguage =
        SongSelectLanguage.Korean;
    private TMP_Text[] rowLevelLabels;

    // A language switch can be connected to any future setting button.
    // Song titles and chart data are never translated.
    public void SetLanguage(SongSelectLanguage next)
    {
        interfaceLanguage = next;
        if (!useArcadeLocalization) return;
        ApplyArcadeLabels();
        RefreshList();
        RefreshDetails();
    }

    public void CycleLanguage()
    {
        SetLanguage((SongSelectLanguage)(((int)interfaceLanguage + 1) % 3));
    }

    private string Localized(string key, string original)
    {
        return useArcadeLocalization
            ? SongSelectLocalization.Get(interfaceLanguage, key) : original;
    }
    [SerializeField] private Color selectedOutline =
        new Color(.86f, .75f, .90f, .85f);
    [SerializeField, Range(.05f, .4f)] private float previewFadeIn = .22f;
    private enum SortMode { Title, Level, BPM }
    private SortMode sortMode = SortMode.Title;
    private Outline[] rowOutlines;
    private TMP_Text sortLabel;
    private TMP_Text artistLabel;
    private TMP_Text cartridgeLabel;
    private float previewShownVolume;
    private int lastPreviewSecond = -1;

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
    private bool cartridgeClickArmed;
    private bool gameLaunchInProgress;
    private SongSelectArcadeMotion arcadeMotion;

    // Read-only feedback for the dedicated cartridge-scene animator.
    // The original music library scene keeps its current behavior.
    public int ArcadeSelectedIndex => songs.Count == 0 ? -1 : selectedIndex;
    public int ArcadeDifficultyIndex => difficultyIndex;
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
        arcadeMotion = GetComponent<SongSelectArcadeMotion>();
        listArea = transform.Find("TrackListPanel") as RectTransform;
        trackCountLabel = transform.Find("TrackCount") != null ?
            transform.Find("TrackCount").GetComponent<TMP_Text>() : null;

        sortLabel = ChildText("TrackListPanel/ListSubheading");
        artistLabel = ChildText("SelectedTrackPanel/TrackLabel");
        cartridgeLabel = ChildText("SelectedTrackPanel/" +
            "JacketImage_Replaceable/NoJacketArt_Placeholder/" +
            "JacketPlaceholderLabel");
        rowOutlines = new Outline[trackButtons.Length];
        rowNumbers = new TMP_Text[trackButtons.Length];
        rowLevelLabels = new TMP_Text[trackButtons.Length];
        for (int i = 0; i < trackButtons.Length; i++)
        {
            int rowIndex = i;
            if (trackButtons[i] == null) continue;
            if (enableMatteSelectionOutline &&
                i < trackRowBackgrounds.Length &&
                trackRowBackgrounds[i] != null)
            {
                Image image = trackRowBackgrounds[i];
                Outline border = image.GetComponent<Outline>();
                if (border == null)
                    border = image.gameObject.AddComponent<Outline>();
                border.effectDistance = new Vector2(1.2f, -1.2f);
                border.useGraphicAlpha = false;
                rowOutlines[i] = border;
            }
            Transform number = trackButtons[i].transform.Find("Index");
            if (number != null)
                rowNumbers[i] = number.GetComponent<TMP_Text>();
            Transform levelBadge = trackButtons[i].transform.Find(
                "Arcade_RowLevel");
            if (levelBadge != null)
                rowLevelLabels[i] = levelBadge.GetComponent<TMP_Text>();

            trackButtons[i].onClick.AddListener(() =>
            {
                int pageStart = selectedIndex / PageSize * PageSize;
                OnCartridgeClicked(pageStart + rowIndex);
            });
        }

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
            previewSource.volume = 0f;
            previewShownVolume = 0f;
        }

        if (useArcadeLocalization)
            ApplyArcadeLabels();
        else
            ApplyPrototypeGameConsoleLabels();
        SetupAudioDeckVisual();
        BuildCatalog();
        SortSongs(false);
        SelectTrack(0);
    }


    private void SetupAudioDeckVisual()
    {
        // Add only a new visual CHILD to an existing empty jacket slot.
        // Do not modify any previously authored RectTransform on the scene.
        if (jacketPlaceholder == null || previewSource == null)
            return;
        if (jacketPlaceholder.transform.Find("SoundChipSpectrumUI") != null)
            return;

        GameObject child = new GameObject("SoundChipSpectrumUI",
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(SongSelectSpectrumUI));
        RectTransform r = child.GetComponent<RectTransform>();
        r.SetParent(jacketPlaceholder.transform, false);
        r.anchorMin = new Vector2(.10f, .15f);
        r.anchorMax = new Vector2(.90f, .85f);
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        child.GetComponent<SongSelectSpectrumUI>().AttachSource(
            previewSource);
        r.SetAsFirstSibling();
    }

    // Temporary copy only. The user's edited Unity text survives future updates.
    // The final artwork is swapped through MouseModeChartAsset.songSelectJacket.
    private void ApplyPrototypeGameConsoleLabels()
    {
        SetPrototypeLabel("PageHeading", "MUSIC LIBRARY",
            "LULU IN BEAT");
        SetPrototypeLabel("PageSubtitle",
            "곡 선택  /  SELECT TRACK", "ARCADE RHYTHM / SONG SELECT");
        SetPrototypeLabel("TrackListPanel/ListHeading", "TRACK LIST",
            "BEAT CARTRIDGES");
        SetPrototypeLabel("SelectedTrackPanel/InfoHeading",
            "NOW SELECTED", "CARTRIDGE LOADED");
        SetPrototypeLabel("PlaySelected/ButtonText",
            "START GAME", "LOAD & PLAY");
        SetPrototypeLabel("BackToTitle/ButtonText",
            "BACK TO TITLE", "RETURN TO TITLE");
    }

    private void ApplyArcadeLabels()
    {
        SetInterfaceText("PageHeading", "heading");
        SetInterfaceText("PageSubtitle", "subtitle");
        SetInterfaceText("TrackListPanel/ListHeading", "list");
        SetInterfaceText("TrackListPanel/ListSubheading", "hint");
        SetInterfaceText("SelectedTrackPanel/InfoHeading", "inserted");
        SetInterfaceText("SelectedTrackPanel/DifficultyLabel", "difficulty");
        SetInterfaceText("SelectedTrackPanel/PreviewButton/ButtonText", "listen");
        SetInterfaceText("BackToTitle/ButtonText", "back");
        SetInterfaceText("SelectedTrackPanel/PlaySelected/ButtonText", "play");
        SetInterfaceText("Footnote", "mouseHint");
        SetInterfaceText(
            "SelectedTrackPanel/JacketImage_Replaceable/" +
            "NoJacketArt_Placeholder/JacketPlaceholderLabel", "noJacket");
    }

    private void SetInterfaceText(string path, string key)
    {
        TMP_Text label = ChildText(path);
        if (label != null)
            label.text = SongSelectLocalization.Get(interfaceLanguage, key);
    }

    private void SetPrototypeLabel(string path, string original,
        string replacement)
    {
        TMP_Text label = ChildText(path);
        if (label != null && label.text.Trim() == original)
            label.text = replacement;
    }

    private TMP_Text ChildText(string path)
    {
        Transform child = transform.Find(path);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private void SortSongs(bool keepSelection)
    {
        AudioClip selected = keepSelection && CurrentSong != null
            ? CurrentSong.clip : null;
        songs.Sort((a, b) =>
        {
            int cmp = 0;
            if (sortMode == SortMode.Level)
                cmp = a.charts[0].data.level.CompareTo(
                    b.charts[0].data.level);
            else if (sortMode == SortMode.BPM)
                cmp = a.charts[0].data.bpm.CompareTo(
                    b.charts[0].data.bpm);
            return cmp != 0 ? cmp :
                StringComparer.OrdinalIgnoreCase.Compare(a.title, b.title);
        });
        if (selected != null)
            for (int i = 0; i < songs.Count; i++)
                if (songs[i].clip == selected)
                {
                    selectedIndex = i;
                    break;
                }
        if (sortLabel != null)
        {
            if (!useArcadeLocalization)
                sortLabel.text = "SORT : " +
                    sortMode.ToString().ToUpperInvariant() + " [F2]";
            else
            {
                string key = sortMode == SortMode.Title ? "sortTitle" :
                    sortMode == SortMode.Level ? "sortLevel" : "sortBpm";
                sortLabel.text = Localized("sort", "SORT") + ": " +
                    Localized(key, key) + " [F2]";
            }
        }
    }

    private void CycleSort()
    {
        cartridgeClickArmed = false;
        sortMode = (SortMode)(((int)sortMode + 1) % 3);
        SortSongs(true);
        RefreshList();
        RefreshDetails();
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

            // One playable chart per song. When older chart variants share
            // the AudioClip, prefer the real MOUSE chart and ignore all
            // other variants without touching the source assets.
            int preferred = song.charts.FindIndex(c =>
                string.Equals(c.data.difficulty, "MOUSE",
                    StringComparison.OrdinalIgnoreCase));
            MouseModeChartAsset primary =
                song.charts[preferred >= 0 ? preferred : 0];
            song.charts.Clear();
            song.charts.Add(primary);

            // Prefer the song's normal display name over a placeholder
            // or a difficulty suffix such as " - Challenge".
            MouseModeChartAsset titleChart = song.charts[0];
            foreach (MouseModeChartAsset candidate in song.charts)
            {
                string title = candidate.displayTitle;
                if (string.IsNullOrWhiteSpace(title) ||
                    title == "New Song" || title.Contains("_") ||
                    title.Contains(" - Challenge"))
                    continue;
                titleChart = candidate;
                break;
            }
            song.title = string.IsNullOrWhiteSpace(titleChart.displayTitle)
                ? titleChart.name : titleChart.displayTitle;
        }

        // Auto-import may not finish the first time the editor loads.
        if (songs.Count == 0)
            Debug.LogWarning("[SongSelect] No valid MouseModeChartAsset with " +
                "audio and level 1..15. Open Unity Editor and refresh the chart catalog.");
    }

    private void RegisterChart(MouseModeChartAsset chart,
        HashSet<MouseModeChartAsset> unique, Sprite jacket)
    {
        if (chart == null || chart.hideFromSongSelect ||
            !unique.Add(chart) || chart.song == null ||
            chart.data == null || chart.data.level < 1 ||
            chart.data.level > 15 || chart.data.notes == null ||
            chart.data.notes.Count == 0)
            return;

        SongEntry found = null;
        foreach (SongEntry existing in songs)
            if (existing.clip == chart.song) { found = existing; break; }

        Sprite catalogCover = chart.songSelectJacket != null
            ? chart.songSelectJacket : jacket;
        if (found == null)
        {
            found = new SongEntry
            {
                clip = chart.song,
                title = chart.displayTitle,
                jacket = catalogCover
            };
            songs.Add(found);
        }
        if (found.jacket == null && catalogCover != null)
            found.jacket = catalogCover;
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
                ? null : song.charts[0];
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
        if (index < 0 || index >= songs.Count || gameLaunchInProgress)
            return;

        selectedIndex = index;
        difficultyIndex = 0;
        cartridgeClickArmed = false;
        RefreshList();
        RefreshDetails();
    }

    private void OnCartridgeClicked(int index)
    {
        if (index < 0 || index >= songs.Count || gameLaunchInProgress)
            return;

        // Selection made on entry, or by the wheel/keyboard, is not armed:
        // the first physical click only selects; a repeat click plays.
        if (useArcadeLocalization && cartridgeClickArmed &&
            selectedIndex == index)
        {
            PlaySelected();
            return;
        }

        SelectTrack(index);
        if (useArcadeLocalization)
        {
            cartridgeClickArmed = true;
            if (arcadeMotion != null)
                arcadeMotion.PulseSelection();
        }
    }

    private void RefreshList()
    {
        int pageStart = selectedIndex / PageSize * PageSize;
        if (trackCountLabel != null)
        {
            int page = selectedIndex / PageSize + 1;
            int pages = Mathf.Max(1,
                Mathf.CeilToInt(songs.Count / (float)PageSize));
            trackCountLabel.text = songs.Count.ToString("00") + " " +
                Localized("count", "TRACKS") + "   " +
                page + "/" + pages;
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
                trackRowLabels[slot].text = !valid ? "" :
                    useArcadeLocalization ? songs[index].title :
                    songs[index].title + "   LV " +
                    songs[index].charts[0].data.level.ToString("00");
            }

            if (rowLevelLabels != null && slot < rowLevelLabels.Length &&
                rowLevelLabels[slot] != null)
                rowLevelLabels[slot].text = valid
                    ? Localized("rowLevel", "LV ") +
                      songs[index].charts[0].data.level.ToString("00")
                    : "";

            if (rowNumbers[slot] != null)
                rowNumbers[slot].text = valid
                    ? (index + 1).ToString("00") : "";
            if (slot < trackRowBackgrounds.Length &&
                trackRowBackgrounds[slot] != null)
                trackRowBackgrounds[slot].color = valid &&
                    index == selectedIndex ? selectedRow : normalRow;
            if (rowOutlines != null && slot < rowOutlines.Length &&
                rowOutlines[slot] != null)
                rowOutlines[slot].effectColor = valid &&
                    index == selectedIndex ? selectedOutline :
                    new Color(0f, 0f, 0f, 0f);
        }
    }

    private void RefreshDetails()
    {
        SongEntry song = CurrentSong;
        MouseModeChartAsset chart = CurrentChart;
        bool valid = chart != null && chart.data != null;

        if (selectedTitle != null)
            selectedTitle.text = song != null
                ? song.title : Localized("noSongs", "NO SONGS FOUND");
        if (artistLabel != null)
            artistLabel.text = valid &&
                !string.IsNullOrWhiteSpace(chart.artist)
                ? chart.artist.ToUpperInvariant()
                : (useArcadeLocalization ? "" :
                    Localized("defaultArtist", "LULU / RHYTHM LIBRARY"));
        if (cartridgeLabel != null)
            cartridgeLabel.text = useArcadeLocalization
                ? Localized("noJacket", "JACKET ART COMING SOON")
                : (song != null
                    ? Localized("cartridge", "CARTRIDGE") + "  " +
                      (selectedIndex + 1).ToString("00")
                    : Localized("noCartridge", "NO CARTRIDGE"));
        if (selectedLevel != null)
            selectedLevel.text = valid
                ? Localized("level", "LEVEL") + "  " +
                  chart.data.level.ToString("00") +
                  (useArcadeLocalization ? "" : " / 15")
                : Localized("level", "LEVEL") +
                  (useArcadeLocalization ? "  --" : "  -- / 15");
        if (selectedDifficulty != null)
            selectedDifficulty.text = valid
                ? (useArcadeLocalization
                    ? SongSelectLocalization.DifficultyName(
                        interfaceLanguage, chart.data.difficulty)
                    : (chart.data.difficulty ?? "MOUSE").Replace("_", " "))
                : Localized("noChart", "NO CHART");
        if (selectedBpm != null)
            selectedBpm.text = valid
                ? chart.data.bpm.ToString("0") + " BPM" : "-- BPM";
        if (selectedDuration != null)
        {
            float seconds = chart != null && chart.song != null
                ? chart.song.length : 0f;
            selectedDuration.text = seconds > 0f
                ? (useArcadeLocalization
                      ? Localized("duration", "DURATION") + "  " : "") +
                  Mathf.FloorToInt(seconds / 60f).ToString("00") + ":" +
                  Mathf.FloorToInt(seconds % 60f).ToString("00")
                : "--:--";
        }

        // Only a single chart is available; difficulty selection is gone.
        if (previousDifficultyButton != null)
            previousDifficultyButton.interactable = false;
        if (nextDifficultyButton != null)
            nextDifficultyButton.interactable = false;
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
        previewShownVolume = 0f;
        lastPreviewSecond = -1;
        MouseModeChartAsset chart = CurrentChart;
        AudioClip clip = chart != null ? chart.song : null;
        previewSource.clip = clip;
        if (clip == null)
        {
            if (previewStatus != null)
                previewStatus.text = Localized("unavailable",
                    "PREVIEW UNAVAILABLE");
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
                ? Localized("on", "PREVIEW  ON")
                : Localized("off", "PREVIEW  OFF");
    }

    private void Update()
    {
        // Never restart a stopped preview or accept another action while
        // the selected cartridge is animating into the game.
        if (gameLaunchInProgress) return;

        // PC library navigation; gameplay remains mouse-only.
        if (Input.GetKeyDown(KeyCode.F2)) CycleSort();
        if (Input.GetKeyDown(KeyCode.DownArrow))
            SelectTrack(Mathf.Min(selectedIndex + 1, songs.Count - 1));
        if (Input.GetKeyDown(KeyCode.UpArrow))
            SelectTrack(Mathf.Max(0, selectedIndex - 1));
        if (Input.GetKeyDown(KeyCode.PageDown))
            SelectTrack(Mathf.Min(selectedIndex + PageSize, songs.Count - 1));
        if (Input.GetKeyDown(KeyCode.PageUp))
            SelectTrack(Mathf.Max(0, selectedIndex - PageSize));
        if (Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter)) PlaySelected();
        if (Input.GetKeyDown(KeyCode.Escape)) BackToTitle();

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

        if (previewSource == null || previewSource.clip == null) return;
        if (!previewEnabled)
        {
            previewSource.volume = 0f;
            return;
        }

        previewShownVolume = Mathf.MoveTowards(
            previewShownVolume, previewVolume,
            Time.unscaledDeltaTime * previewVolume /
                Mathf.Max(.04f, previewFadeIn));
        previewSource.volume = previewShownVolume;

        if (!previewSource.isPlaying || previewSource.time >= previewEnd)
        {
            previewSource.Stop();
            previewSource.time = previewStart;
            previewSource.volume = 0f;
            previewShownVolume = 0f;
            previewSource.Play();
        }

        int seconds = Mathf.FloorToInt(Mathf.Max(0f,
            previewSource.time - previewStart));
        if (seconds != lastPreviewSecond && previewStatus != null)
        {
            lastPreviewSecond = seconds;
            previewStatus.text = (useArcadeLocalization ? "" : "PREVIEW ") +
                seconds.ToString("00") + " / " +
                Mathf.FloorToInt(Mathf.Max(0f,
                    previewEnd - previewStart)).ToString("00") +
                Localized("seconds", "s");
        }
    }

    private void TogglePreview()
    {
        previewEnabled = !previewEnabled;
        StartPreview();
    }

    private void PlaySelected()
    {
        if (gameLaunchInProgress) return;
        MouseModeChartAsset chart = CurrentChart;
        if (chart == null || !Application.CanStreamedLevelBeLoaded(
            gameplaySceneName))
        {
            Debug.LogError("[SongSelect] Missing chart or game scene: " +
                gameplaySceneName);
            return;
        }

        if (useArcadeLocalization && arcadeMotion != null &&
            arcadeMotion.isActiveAndEnabled)
            StartCoroutine(PlayWithCartridgeAnimation(chart));
        else
            LoadSelectedChart(chart);
    }

    private IEnumerator PlayWithCartridgeAnimation(
        MouseModeChartAsset chart)
    {
        gameLaunchInProgress = true;
        if (previewSource != null)
            previewSource.Stop();
        if (playButton != null)
            playButton.interactable = false;

        arcadeMotion.BeginInsertion();
        yield return new WaitForSecondsRealtime(
            arcadeMotion.InsertionSeconds);
        LoadSelectedChart(chart);
    }

    private void LoadSelectedChart(MouseModeChartAsset chart)
    {
        if (previewSource != null)
            previewSource.Stop();
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
