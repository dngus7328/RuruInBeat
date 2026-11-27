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
    // Reuse the user's original TitleUI selected-category illustration.
    // Only the actually selected song row displays this sprite.
    // Other rows keep their original flat background and click behavior.
    [SerializeField] private Sprite selectedRowArtwork;
    [SerializeField, Range(0f, 1f)] private float previewVolume = .28f;
    [SerializeField, Range(5f, 25f)] private float previewSegmentSeconds = 15f;
    [Header("PC music library / no layout movement")]
    [SerializeField] private bool enableMatteSelectionOutline = true;
    [Header("Arcade-only interface translation")]
    [SerializeField] private bool useArcadeLocalization = false;
    [SerializeField] private SongSelectLanguage interfaceLanguage =
        SongSelectLanguage.Korean;
    private TMP_Text[] rowLevelLabels;
    private Image[] cartridgeFaces, cartridgeCovers;
    private Image selectedSongBackdrop;
    private TMP_Text selectedSongOriginalName;
    private TMP_Text selectedSongLevelCaption;
    private Image[] selectedRowMarks;
    private Sprite cartridgeNormal, cartridgeActive, cartridgeEmpty;
    private TMP_Text bestRecord;
    private RectTransform previewMeter;

    // Language changes display labels only. Chart assets, audio and
    // original song titles remain unchanged.
    public void SetLanguage(SongSelectLanguage next)
    {
        interfaceLanguage = next;
        if (!useArcadeLocalization) return;
        ApplyArcadeLabels();
        // In title-sort mode, changing the UI language also reorders the
        // displayed Korean/English/Japanese titles consistently.
        SortSongs(true);
        if (browseUI != null) browseUI.SetLanguage(next);
        UpdatePreviewToggleAppearance();
        UpdateSelectionHint();
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
    private SongSelectBrowseUI browseUI;
    private readonly List<SongEntry> matchedSongs = new List<SongEntry>();
    private string searchQuery = "";
    private int levelMin=1;
    private int levelMax=15;
    // Sentinel bounds always include future songs whose BPM differs from
    // today's six demo tracks.
    private int bpmMin=int.MinValue;
    private int bpmMax=int.MaxValue;
    private int pageIndex;

    public int CurrentPage => pageIndex;
    public int LevelMin => levelMin;
    public int LevelMax => levelMax;
    public int BpmMin => bpmMin==int.MinValue ? BpmBoundMin :
        Mathf.Clamp(bpmMin,BpmBoundMin,BpmBoundMax);
    public int BpmMax => bpmMax==int.MaxValue ? BpmBoundMax :
        Mathf.Clamp(bpmMax,BpmBoundMin,BpmBoundMax);
    public int BpmBoundMin
    {
        get
        {
            if(songs.Count==0)return 60;
            int lowest=int.MaxValue;
            foreach(SongEntry song in songs)
                lowest=Mathf.Min(lowest,
                    Mathf.RoundToInt(song.charts[0].data.bpm));
            return Mathf.Max(1,Mathf.FloorToInt((lowest-10)/10f)*10);
        }
    }
    public int BpmBoundMax
    {
        get
        {
            if(songs.Count==0)return 240;
            int highest=0;
            foreach(SongEntry song in songs)
                highest=Mathf.Max(highest,
                    Mathf.RoundToInt(song.charts[0].data.bpm));
            return Mathf.CeilToInt((highest+10)/10f)*10;
        }
    }
    public int CurrentSortIndex => (int)sortMode;
    public int MatchCount => matchedSongs.Count;
    public int PageCount => Mathf.Max(1,
        Mathf.CeilToInt(matchedSongs.Count / (float)PageSize));

    // Read-only feedback for the dedicated cartridge-scene animator.
    // The original music library scene keeps its current behavior.
    public int ArcadeSelectedIndex =>
        matchedSongs.Count == 0 ? -1 : selectedIndex;
    public int ArcadeDifficultyIndex => difficultyIndex;
    // A cartridge is only visibly lifted after the player's first click.
    public bool ArcadeCartridgeRaised => cartridgeClickArmed;
    public float PreviewProgress
    {
        get
        {
            if (previewSource == null || previewSource.clip == null ||
                !previewEnabled || !previewSource.isPlaying)
                return 0f;
            return Mathf.Clamp01((previewSource.time - previewStart) /
                Mathf.Max(.01f, previewEnd - previewStart));
        }
    }

    // The user's ON/OFF choice survives closing the screen and restarting
    // the game. ON loops the selected song preview; OFF stops all preview audio.
    private const string PreviewPreferenceKey =
        "LuluInBeat.SongSelect.AutoplayPreview";
    private bool previewEnabled = true;
    public bool PreviewEnabled => previewEnabled;
    private float previewStart, previewEnd;
    private TMP_Text previewToggleCaption;
    private Image previewToggleBackground;
    private static readonly Color PreviewOnColor =
        new Color(.54f, .38f, .55f, 1f);
    private static readonly Color PreviewOffColor =
        new Color(.21f, .18f, .27f, 1f);

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
        previewEnabled = true; // Selection automatically previews the track.
        arcadeMotion = GetComponent<SongSelectArcadeMotion>();
        if (previewButton != null)
        {
            previewToggleBackground = previewButton.targetGraphic as Image;
            previewToggleCaption = previewButton.GetComponentInChildren<TMP_Text>(
                true);
        }
        listArea = transform.Find("TrackListPanel") as RectTransform;
        trackCountLabel = transform.Find("TrackCount") != null ?
            transform.Find("TrackCount").GetComponent<TMP_Text>() : null;

        // The existing Footnote doubles as a short contextual instruction.
        // It is updated after every selection; no additional HUD is spawned.
        sortLabel = ChildText("Footnote");
        artistLabel = ChildText("SelectedTrackPanel/TrackLabel");
        cartridgeLabel = ChildText("SelectedTrackPanel/" +
            "JacketImage_Replaceable/NoJacketArt_Placeholder/" +
            "JacketPlaceholderLabel");
        rowOutlines = new Outline[trackButtons.Length];
        rowNumbers = new TMP_Text[trackButtons.Length];
        rowLevelLabels = new TMP_Text[trackButtons.Length];
        for (int i = 0; i < trackButtons.Length; i++)
        {
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

            // Wire the actual cartridge button. Polling mouse-down separately
            // misses some UI events and can double-trigger when Button handles
            // the same gesture. One release = exactly one selection.
            int slot = i;
            trackButtons[i].onClick.AddListener(
                () => OnCartridgeClicked(pageIndex * PageSize + slot));
        }

        if (previewButton != null)
            previewButton.onClick.AddListener(TogglePreview);
        if (backButton != null)
            backButton.onClick.AddListener(BackToTitle);
        if(playButton!=null && playButton.onClick.GetPersistentEventCount()==0)
            playButton.onClick.AddListener(PlaySelected);

        if (previewSource != null)
        {
            previewSource.playOnAwake = false;
            previewSource.loop = false;
            previewSource.volume = 0f;
            previewShownVolume = 0f;
        }

        BuildBeatDeckLayout();
        if (useArcadeLocalization)
            ApplyArcadeLabels();
        else
            ApplyPrototypeGameConsoleLabels();
        UpdatePreviewToggleAppearance();
        // The empty-jacket visual is now a plain reserved cover slot.
        // Do not spawn a fake spectrum until the real jacket is assigned.
        browseUI = GetComponent<SongSelectBrowseUI>();
        if (browseUI == null)
            browseUI = gameObject.AddComponent<SongSelectBrowseUI>();
        browseUI.Initialize(this,
            selectedTitle != null ? selectedTitle.font :
                TMP_Settings.defaultFontAsset, interfaceLanguage);

        BuildCatalog();
        SortSongs(false);
    }



    // The beat deck is an illustrated cartridge library. Each independent PNG
    // is a physical part; all real song titles, artwork and stats remain live.
    // Compact music library. One selected track, one list, one launch action.
    private static readonly Color LibraryInk = new Color32(32, 29, 40, 255);
    private static readonly Color LibraryPaper = new Color32(242, 239, 235, 255);
    private static readonly Color LibraryMuted = new Color32(173, 169, 181, 255);
    private static readonly Color LibraryAccent = new Color32(157, 222, 201, 255);
    private static readonly Color LibraryRule = new Color32(205, 200, 204, 255);

    private void BuildBeatDeckLayout()
    {
        if(listArea==null || transform.Find("SelectedTrackPanel")==null) return;
        if(arcadeMotion!=null) arcadeMotion.enabled=false;
        var detail=transform.Find("SelectedTrackPanel") as RectTransform;
        var background=transform.Find("Background")?.GetComponent<Image>();
        if(background!=null)
        {
            background.sprite=null; background.type=Image.Type.Simple;
            background.color=LibraryInk; background.transform.SetAsFirstSibling();
            RuruConsoleUI.Strip(background.transform,"LibraryIndexSurface",.485f,0,1,1,LibraryPaper);
        }
        foreach(Transform child in transform)
            if(child.name.StartsWith("Arcade_",StringComparison.Ordinal) ||
                child.name=="BottomRule" || child.name=="HeaderRule" || child.name=="TopRule" ||
                child.name=="PageSubtitle" || child.name=="Footnote")
                child.gameObject.SetActive(false);
        foreach(Transform child in listArea)
            if(child.name.StartsWith("Arcade_",StringComparison.Ordinal) || child.name=="LeftPanelTopEdge" ||
                child.name=="ListHeading" || child.name=="ListSubheading")
                child.gameObject.SetActive(false);
        foreach(Transform child in detail)
            if(child.name.StartsWith("Arcade_",StringComparison.Ordinal) || child.name=="RightPanelTopEdge" ||
                child.name=="DetailRule" || child.name=="DifficultyLabel" || child.name=="InfoHeading")
                child.gameObject.SetActive(false);
        var shelfBg=listArea.GetComponent<Image>();
        if(shelfBg!=null){shelfBg.sprite=null;shelfBg.color=Color.clear;shelfBg.raycastTarget=false;}
        var detailBg=detail.GetComponent<Image>();
        if(detailBg!=null){detailBg.sprite=null;detailBg.color=Color.clear;detailBg.raycastTarget=false;}

        // Reference canvas: left content x=116..824, right x=1008..1804.
        Place(detail,.060f,.125f,.430f,.815f);
        Place(listArea,.525f,.145f,.940f,.875f);
        var heading=ChildText("PageHeading");
        if(heading!=null)
        {
            Place(heading.rectTransform,.060f,.865f,.430f,.925f);
            CompactText(heading,36,LibraryPaper,TextAlignmentOptions.MidlineLeft);
        }
        if(backButton!=null)
        {
            Place(backButton.transform as RectTransform,.060f,.936f,.132f,.977f);
            CompactButton(backButton,false);
        }

        cartridgeFaces=new Image[trackButtons.Length];
        cartridgeCovers=new Image[trackButtons.Length];
        selectedRowMarks=new Image[trackButtons.Length];
        for(int i=0;i<trackButtons.Length;i++)
        {
            if(trackButtons[i]==null) continue;
            var row=trackButtons[i].transform as RectTransform;
            float top=.734f-i*.112f;
            Place(row,0,top-.093f,1,top);
            row.localRotation=Quaternion.identity;row.localScale=Vector3.one;
            foreach(Transform child in row)
                if(child.name.StartsWith("Arcade_",StringComparison.Ordinal) && child.name!="Arcade_RowLevel")
                    child.gameObject.SetActive(false);
            if(i<trackRowBackgrounds.Length && trackRowBackgrounds[i]!=null)
            {trackRowBackgrounds[i].color=Color.clear;trackRowBackgrounds[i].raycastTarget=false;}
            if(rowOutlines!=null && rowOutlines[i]!=null) rowOutlines[i].enabled=false;
            var face=DeckImage(row,"LibraryRowSurface",0,0,1,1,null);
            face.transform.SetAsFirstSibling();face.raycastTarget=true;
            trackButtons[i].targetGraphic=face;
            trackButtons[i].transition=Selectable.Transition.ColorTint;
            var cb=trackButtons[i].colors;
            cb.normalColor=Color.white;cb.highlightedColor=new Color(.88f,.91f,.88f);
            cb.pressedColor=new Color(.72f,.82f,.77f);cb.selectedColor=Color.white;
            cb.fadeDuration=.08f;trackButtons[i].colors=cb;
            trackButtons[i].navigation=new Navigation{mode=Navigation.Mode.None};
            cartridgeFaces[i]=face;
            RuruConsoleUI.Strip(row,"IndexRule",0,0,1,.024f,LibraryRule);
            selectedRowMarks[i]=RuruConsoleUI.Strip(row,"LoadedMarker",0,0,.006f,1,LibraryAccent);
            var cover=DeckImage(row,"LibraryCover",.085f,.13f,.158f,.87f,null);
            cover.preserveAspect=true;cartridgeCovers[i]=cover;
            if(i<trackRowLabels.Length && trackRowLabels[i]!=null)
            {
                var title=trackRowLabels[i];
                Place(title.rectTransform,.186f,.10f,.824f,.90f);
                CompactText(title,25,LibraryInk,TextAlignmentOptions.MidlineLeft);
            }
            if(rowNumbers[i]!=null)
            {
                rowNumbers[i].gameObject.SetActive(true);
                Place(rowNumbers[i].rectTransform,.022f,.1f,.073f,.9f);
                CompactText(rowNumbers[i],17,LibraryInk,TextAlignmentOptions.Center);
            }
            if(rowLevelLabels[i]!=null)
            {
                Place(rowLevelLabels[i].rectTransform,.854f,.1f,.975f,.9f);
                CompactText(rowLevelLabels[i],23,LibraryInk,TextAlignmentOptions.MidlineRight);
            }
        }
        RuruConsoleUI.Text(listArea,"ColumnNumber","No.",.022f,.751f,.075f,.79f,16,LibraryInk);
        RuruConsoleUI.Text(listArea,"ColumnTrack","곡명",.186f,.751f,.75f,.79f,17,LibraryInk);
        RuruConsoleUI.Text(listArea,"ColumnLevel","레벨",.854f,.751f,.975f,.79f,17,LibraryInk,TextAlignmentOptions.MidlineRight);

        // Cover is real catalog art; no simulated cartridge, giant frame or wing ornament.
        if(jacketImage!=null)
        {
            Place(jacketImage.rectTransform,0,.480f,.535f,.990f);
            jacketImage.preserveAspect=true;
        }
        selectedSongLevelCaption=RuruConsoleUI.Text(detail,"LoadedLevelHeading","레벨",.585f,.860f,.96f,.925f,19,LibraryMuted);
        if(selectedLevel!=null)
        {
            Place(selectedLevel.rectTransform,.585f,.760f,.98f,.860f);
            CompactText(selectedLevel,50,LibraryAccent,TextAlignmentOptions.MidlineLeft);
        }
        if(selectedBpm!=null)
        {
            Place(selectedBpm.rectTransform,.585f,.665f,.98f,.735f);
            CompactText(selectedBpm,24,LibraryPaper,TextAlignmentOptions.MidlineLeft);
        }
        if(selectedDuration!=null)
        {
            Place(selectedDuration.rectTransform,.585f,.595f,.98f,.665f);
            CompactText(selectedDuration,22,LibraryMuted,TextAlignmentOptions.MidlineLeft);
        }
        if(selectedTitle!=null)
        {
            Place(selectedTitle.rectTransform,0,.360f,1,.450f);
            CompactText(selectedTitle,38,LibraryPaper,TextAlignmentOptions.MidlineLeft);
        }
        selectedSongOriginalName=RuruConsoleUI.Text(detail,"LoadedOriginalTitle","",0,.305f,1,.360f,23,LibraryMuted);
        if(artistLabel!=null)
        {
            Place(artistLabel.rectTransform,0,.255f,1,.305f);
            CompactText(artistLabel,20,LibraryMuted,TextAlignmentOptions.MidlineLeft);
        }
        RuruConsoleUI.Strip(detail,"RecordDivider",0,.240f,1,.243f,new Color32(87,81,96,255));
        bestRecord=RuruConsoleUI.Text(detail,"BestRecord","",0,.148f,1,.225f,25,LibraryPaper);
        if(selectedDifficulty!=null) selectedDifficulty.gameObject.SetActive(false);
        if(previousDifficultyButton!=null) previousDifficultyButton.gameObject.SetActive(false);
        if(nextDifficultyButton!=null) nextDifficultyButton.gameObject.SetActive(false);
        if(previewStatus!=null) previewStatus.gameObject.SetActive(false);
        if(previewButton!=null) previewButton.gameObject.SetActive(false);
        if(playButton!=null)
        {
            playButton.gameObject.SetActive(true);
            Place(playButton.transform as RectTransform,0,.030f,.300f,.107f);
            CompactButton(playButton,true);
        }
    }

    private static void CompactText(TMP_Text label,float size,Color ink,TextAlignmentOptions alignment)
    {
        if(label==null)return;
        label.font=RuruConsoleUI.Font();label.color=ink;label.alignment=alignment;
        label.fontSize=size;label.enableAutoSizing=true;label.fontSizeMin=size*.75f;label.fontSizeMax=size;
        label.enableWordWrapping=false;label.overflowMode=TextOverflowModes.Ellipsis;
    }

    private static void CompactButton(Button button,bool primary)
    {
        var old=button.targetGraphic;
        var art=DeckImage(button.transform,"LibraryActionSurface",0,0,1,1,null);
        art.color=primary?LibraryAccent:LibraryInk;
        art.raycastTarget=true;art.transform.SetAsFirstSibling();
        if(old!=null){old.color=Color.clear;old.raycastTarget=false;}
        button.targetGraphic=art;
        button.transition=Selectable.Transition.ColorTint;
        var colors=button.colors;
        colors.normalColor=Color.white;colors.highlightedColor=new Color(.90f,.94f,.92f);
        colors.pressedColor=new Color(.75f,.80f,.78f);colors.disabledColor=new Color(.5f,.5f,.5f,.7f);
        colors.selectedColor=Color.white;colors.fadeDuration=.08f;button.colors=colors;
        foreach(var label in button.GetComponentsInChildren<TMP_Text>(true))
            CompactText(label,primary?24:20,primary?LibraryInk:LibraryPaper,TextAlignmentOptions.Center);
    }

    private static void Place(RectTransform rt,float l,float b,float r,float t)
    {
        if(rt==null)return;
        rt.anchorMin=new Vector2(l,b);rt.anchorMax=new Vector2(r,t);
        rt.offsetMin=rt.offsetMax=Vector2.zero;
    }
    private static Image DeckImage(Transform parent,string name,float l,float b,float r,float t,Sprite sprite)
    {
        var image=RuruConsoleUI.Region(parent,name,l,b,r,t).gameObject.AddComponent<Image>();
        image.sprite=sprite;image.raycastTarget=false;return image;
    }
    private static void StyleDeckButton(Button button,bool primary)
    {
        var old=button.targetGraphic;
        var art=RuruConsoleUI.Frame(button.transform,"BeatDeckKey",0,0,1,1,primary?RuruConsoleUI.MainKey:RuruConsoleUI.AltKey);
        art.transform.SetAsFirstSibling();art.raycastTarget=true;
        if(old!=null) old.color=Color.clear;
        button.targetGraphic=art;
        foreach(var t in button.GetComponentsInChildren<TMP_Text>(true))
        {
            t.color=primary?RuruConsoleUI.Ink:RuruConsoleUI.Paper;
            t.font=RuruConsoleUI.Font();t.enableAutoSizing=true;t.fontSizeMin=20;t.fontSizeMax=primary?38:27;
        }
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
        var header = ChildText("PageHeading");
        if(header != null) header.text = "곡 선택";
        SetPrototypeLabel("PageSubtitle",
            "곡 선택  /  SELECT TRACK", "ARCADE RHYTHM / SONG SELECT");
        SetPrototypeLabel("TrackListPanel/ListHeading", "TRACK LIST",
            "BEAT CARTRIDGES");
        SetPrototypeLabel("SelectedTrackPanel/InfoHeading",
            "NOW SELECTED", "CARTRIDGE LOADED");
        var startLabel = playButton != null ? playButton.GetComponentInChildren<TMP_Text>(true) : null;
        if(startLabel != null) startLabel.text = "게임 시작";
        var returnLabel = backButton != null ? backButton.GetComponentInChildren<TMP_Text>(true) : null;
        if(returnLabel != null) returnLabel.text = "뒤로";
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
        AudioClip previous = keepSelection && CurrentSong != null
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
                StringComparer.OrdinalIgnoreCase.Compare(
                    useArcadeLocalization
                        ? SongSelectLocalization.DisplaySongTitle(
                            interfaceLanguage, a.title) : a.title,
                    useArcadeLocalization
                        ? SongSelectLocalization.DisplaySongTitle(
                            interfaceLanguage, b.title) : b.title);
        });

        ApplyBrowseFilters(previous);
    }

    private void ApplyBrowseFilters(AudioClip previous)
    {
        matchedSongs.Clear();
        string query = (searchQuery ?? "").Trim();
        foreach (SongEntry entry in songs)
        {
            int level = entry.charts[0].data.level;
            if (level < levelMin || level > levelMax) continue;

            // Exact, user-chosen BPM boundaries. The unfiltered state
            // includes the full catalog even when newly imported tracks
            // exceed a hard-coded limit.
            int bpm = Mathf.RoundToInt(entry.charts[0].data.bpm);
            if (bpm < BpmMin || bpm > BpmMax) continue;

            // The same search works with Korean names, the original chart
            // titles, or their supported English and Japanese aliases.
            if (query.Length > 0 &&
                !SongSelectLocalization.SongTitleMatches(
                    entry.title, query))
                continue;
            matchedSongs.Add(entry);
        }

        int chosen = previous == null ? -1 :
            matchedSongs.FindIndex(x => x.clip == previous);
        selectedIndex = matchedSongs.Count == 0 ? -1 :
            (chosen >= 0 ? chosen : 0);
        pageIndex = selectedIndex < 0 ? 0 : selectedIndex / PageSize;
        difficultyIndex = 0;
        cartridgeClickArmed = false;
        RefreshList();
        RefreshDetails();
        UpdateSelectionHint();
        RefreshBrowseControls();
    }

    public void SetSearchQuery(string value)
    {
        if (gameLaunchInProgress) return;
        string next = value ?? "";
        if (string.Equals(searchQuery, next, StringComparison.Ordinal))
            return;
        searchQuery = next;
        ApplyBrowseFilters(null);
        if (arcadeMotion != null) arcadeMotion.PulsePage(0);
    }

    public void SetLevelRange(int minimum,int maximum)
    {
        if(gameLaunchInProgress)return;
        int high=Mathf.Clamp(maximum,1,15);
        if(levelMin==1 && high==levelMax)return;
        levelMin=1;
        levelMax=high;
        ApplyBrowseFilters(null);
    }

    public void SetBpmRange(int minimum,int maximum)
    {
        if(gameLaunchInProgress)return;
        int high=Mathf.Clamp(maximum,BpmBoundMin,BpmBoundMax);
        if(BpmMin==BpmBoundMin && high==BpmMax)return;
        // The lower endpoint remains fixed. A full-range upper bound must
        // also include charts imported after this screen opened.
        bpmMin=int.MinValue;
        bpmMax=high>=BpmBoundMax ? int.MaxValue : high;
        ApplyBrowseFilters(null);
    }

    public void ResetBrowseFilters()
    {
        if(gameLaunchInProgress)return;
        if(levelMin==1 && levelMax==15 &&
            bpmMin==int.MinValue && bpmMax==int.MaxValue)return;
        levelMin=1;
        levelMax=15;
        bpmMin=int.MinValue;
        bpmMax=int.MaxValue;
        ApplyBrowseFilters(null);
    }

    public void CycleSortFromUI()
    {
        CycleSort();
    }

    public void ChangePage(int direction)
    {
        if (gameLaunchInProgress || matchedSongs.Count == 0)
            return;
        int target = Mathf.Clamp(pageIndex + direction, 0,
            PageCount - 1);
        if (target == pageIndex) return;
        pageIndex = target;
        selectedIndex = target * PageSize;
        cartridgeClickArmed = false;
        RefreshList();
        RefreshDetails();
        UpdateSelectionHint();
        RefreshBrowseControls();
        if (arcadeMotion != null) arcadeMotion.PulsePage(direction);
    }

    private void RefreshBrowseControls()
    {
        if (browseUI != null)
            browseUI.Refresh(levelMin,levelMax,BpmMin,BpmMax,
                BpmBoundMin,BpmBoundMax,(int)sortMode,pageIndex,
                PageCount,matchedSongs.Count);
    }

    private void UpdateSelectionHint()
    {
        if (sortLabel == null) return;
        // Keep the selection gesture discoverable. The first click chooses a
        // cartridge; the second starts the song. Filters and missing charts
        // get an actionable hint instead of a silent disabled play button.
        if (matchedSongs.Count == 0)
            sortLabel.text = Localized("helpNoMatches",
                "곡이 안 보여? 검색어를 지우거나 필터를 초기화해 봐.");
        else if (CurrentChart == null || CurrentChart.data == null)
            sortLabel.text = Localized("helpNoChart",
                "이 곡은 아직 플레이할 수 없어. 다른 카트리지를 골라 봐.");
        else if (cartridgeClickArmed)
            sortLabel.text = Localized("helpAgain",
                "곡 선택 완료! 같은 카트리지를 한 번 더 누르면 시작해.");
        else
            sortLabel.text = Localized("helpSelect",
                "카트리지를 눌러 곡을 고르고, 다시 누르면 연주 시작!");
    }

    private void CycleSort()
    {
        cartridgeClickArmed = false;
        sortMode = (SortMode)(((int)sortMode + 1) % 3);
        SortSongs(true);
        RefreshBrowseControls();
        if (arcadeMotion != null) arcadeMotion.PulsePage(0);
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
            return selectedIndex >= 0 &&
                selectedIndex < matchedSongs.Count
                ? matchedSongs[selectedIndex] : null;
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
        if (index < 0 || index >= matchedSongs.Count ||
            gameLaunchInProgress) return;

        selectedIndex = index;
        pageIndex = selectedIndex / PageSize;
        difficultyIndex = 0;
        cartridgeClickArmed = false;
        RefreshList();
        RefreshDetails();
        UpdateSelectionHint();
        RefreshBrowseControls();
    }

    private void OnCartridgeClicked(int index)
    {
        if (index < 0 || index >= matchedSongs.Count ||
            gameLaunchInProgress) return;

        // Selection made on entry, or by the wheel/keyboard, is not armed:
        // the first physical click only selects; a repeat click plays.
        if (cartridgeClickArmed && selectedIndex == index)
        {
            Debug.Log("[SongSelect] Second cartridge click: " +
                matchedSongs[index].title + " -> game");
            PlaySelected();
            return;
        }

        SelectTrack(index);
        cartridgeClickArmed = true;
        UpdateSelectionHint();
        Debug.Log("[SongSelect] First cartridge click: " +
            matchedSongs[index].title + " selected");
        if (arcadeMotion != null)
            arcadeMotion.LiftCartridge(index);
    }

    private void RefreshList()
    {
        int first = pageIndex * PageSize;
        if (trackCountLabel != null)
        {
            // Duplicate counts are not meaningful when a player can
            // already see every cartridge and no additional page exists.
            trackCountLabel.gameObject.SetActive(false);
        }

        for (int slot = 0; slot < trackButtons.Length; slot++)
        {
            int index = first + slot;
            bool valid = index >= 0 && index < matchedSongs.Count;
            SongEntry entry = valid ? matchedSongs[index] : null;

            Button row = trackButtons[slot];
            if (row != null)
            {
                row.gameObject.SetActive(valid);
                row.interactable = valid;
            }
            if (slot < trackRowLabels.Length &&
                trackRowLabels[slot] != null)
                trackRowLabels[slot].text = valid
                    ? (useArcadeLocalization
                        ? SongSelectLocalization.DisplaySongTitle(
                            interfaceLanguage, entry.title)
                        : entry.title) : "";

            if (rowLevelLabels != null && slot < rowLevelLabels.Length &&
                rowLevelLabels[slot] != null)
                rowLevelLabels[slot].text = valid ?
                    entry.charts[0].data.level.ToString("00") : "";
            if (rowNumbers[slot] != null)
                rowNumbers[slot].text = valid
                    ? (index + 1).ToString("00") : "";
            if(cartridgeFaces!=null && slot<cartridgeFaces.Length && cartridgeFaces[slot]!=null)
            {
                bool current=valid && index==selectedIndex;
                cartridgeFaces[slot].sprite=null;
                cartridgeFaces[slot].color=current?LibraryInk:LibraryPaper;
                if(selectedRowMarks!=null && selectedRowMarks[slot]!=null)
                    selectedRowMarks[slot].enabled=current;
                Color ink=current?LibraryPaper:LibraryInk;
                if(slot<trackRowLabels.Length && trackRowLabels[slot]!=null) trackRowLabels[slot].color=ink;
                if(rowNumbers[slot]!=null) rowNumbers[slot].color=current?LibraryAccent:LibraryInk;
                if(rowLevelLabels[slot]!=null) rowLevelLabels[slot].color=current?LibraryAccent:LibraryInk;
                if(cartridgeCovers[slot]!=null) cartridgeCovers[slot].sprite=valid && entry.jacket!=null?entry.jacket:cartridgeEmpty;
            }
            else if (slot < trackRowBackgrounds.Length &&
                trackRowBackgrounds[slot] != null)
            {
                Image rowImage = trackRowBackgrounds[slot];
                bool isCurrent = valid && index == selectedIndex;
                // The existing image remains intact for every unselected row.
                // The user-authored TitleUI sprite is applied only to the
                // selected one; no extra frame, highlight or icon is created.
                rowImage.sprite = isCurrent ? selectedRowArtwork : null;
                rowImage.type = Image.Type.Simple;
                rowImage.color = isCurrent && selectedRowArtwork != null
                    ? Color.white : (isCurrent ? selectedRow : normalRow);
            }
            if (rowOutlines != null && slot < rowOutlines.Length &&
                rowOutlines[slot] != null && cartridgeFaces==null)
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
                ? (useArcadeLocalization
                    ? SongSelectLocalization.DisplaySongTitle(
                        interfaceLanguage, song.title)
                    : song.title)
                : Localized("noSongs", "NO SONGS FOUND");
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
        // The selected cartridge already carries its level. The detail
        // panel shows the selected track and one restrained context line,
        // rather than repeating three heading/value boxes.
        float seconds = chart != null && chart.song != null
            ? chart.song.length : 0f;
        string duration = seconds > 0f
            ? Mathf.FloorToInt(seconds / 60f).ToString("00") + ":" +
              Mathf.FloorToInt(seconds % 60f).ToString("00")
            : "--:--";

        if (selectedLevel != null)
        {
            if (useArcadeLocalization && cartridgeFaces==null)
                selectedLevel.text = valid
                    ? chart.data.bpm.ToString("0") + " BPM"
                    : "";
            else
                selectedLevel.text = valid
                    ? chart.data.level.ToString("00")
                    : "--";
        }

        if (selectedDifficulty != null)
            selectedDifficulty.text = valid
                ? (useArcadeLocalization
                    ? SongSelectLocalization.DifficultyName(
                        interfaceLanguage, chart.data.difficulty)
                    : (chart.data.difficulty ?? "MOUSE").Replace("_", " "))
                : Localized("noChart", "NO CHART");

        if (selectedBpm != null)
            selectedBpm.text = valid
                ? chart.data.bpm.ToString("0") + " BPM"
                : "-- BPM";
        // Play duration is not part of this compact PC song-select view.
        // Keep the original serialized reference for legacy scenes without
        // deleting the object or breaking older prefab bindings.
        if (selectedDuration != null)
        {
            selectedDuration.text = duration;
            selectedDuration.gameObject.SetActive(cartridgeFaces!=null);
        }

        // Only a single chart is available; difficulty selection is gone.
        if (previousDifficultyButton != null)
            previousDifficultyButton.interactable = false;
        if (nextDifficultyButton != null)
            nextDifficultyButton.interactable = false;
        if (playButton != null)
            playButton.interactable = valid &&
                Application.CanStreamedLevelBeLoaded(gameplaySceneName);

        if(selectedSongBackdrop!=null)
        {
            selectedSongBackdrop.sprite=chart!=null?chart.backgroundImage:null;
            selectedSongBackdrop.enabled=selectedSongBackdrop.sprite!=null;
        }
        if(selectedSongOriginalName!=null)
            selectedSongOriginalName.text=song!=null && selectedTitle!=null && selectedTitle.text!=song.title?song.title:"";
        if(selectedSongLevelCaption!=null) selectedSongLevelCaption.text=Localized("level","LEVEL");
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

        if(bestRecord!=null)
        {
            int best=valid && SaveManager.Instance!=null ? SaveManager.Instance.GetBestScore(chart.data.songId):0;
            bestRecord.text="최고 기록  "+(best>0?best.ToString("D7"):"-------");
        }
        StartPreview();
    }

    private void StartPreview()
    {
        if (previewSource == null) return;

        // Stop the old track first so filtered/changed selections never
        // leave another song playing after the toggle has been turned OFF.
        previewSource.Stop();
        previewSource.volume = 0f;
        previewShownVolume = 0f;
        lastPreviewSecond = -1;
        UpdatePreviewToggleAppearance();

        MouseModeChartAsset chart = CurrentChart;
        AudioClip clip = chart != null ? chart.song : null;
        previewSource.clip = clip;
        if (clip == null)
        {
            if (previewStatus != null)
                previewStatus.text = Localized("unavailable",
                    "미리듣기 불가");
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

        if (!previewEnabled)
        {
            if (previewStatus != null)
                previewStatus.text = ""; // OFF is already on the toggle.
            return;
        }

        previewSource.time = previewStart;
        previewSource.Play();

        if (previewStatus != null)
            previewStatus.text = "00 / " +
                Mathf.CeilToInt(previewEnd - previewStart).ToString("00") +
                Localized("seconds", "초");
    }

    private void UpdatePreviewToggleAppearance()
    {
        if (previewToggleCaption != null)
            previewToggleCaption.text = Localized("previewMode",
                "미리듣기") + " " + (previewEnabled
                ? Localized("previewOn", "ON")
                : Localized("previewOff", "OFF"));
        if (previewToggleBackground != null)
        {
            if(cartridgeFaces!=null)
            {
                var graphic=previewButton!=null?previewButton.targetGraphic as Image:null;
                if(graphic!=null) graphic.sprite=previewEnabled?RuruConsoleUI.MainKey:RuruConsoleUI.AltKey;
                if(previewToggleCaption!=null) previewToggleCaption.color=previewEnabled?RuruConsoleUI.Ink:RuruConsoleUI.Paper;
            }
            else previewToggleBackground.color = previewEnabled ? PreviewOnColor : PreviewOffColor;
        }
    }

    private void Update()
    {
        if(previewMeter!=null) previewMeter.anchorMax=new Vector2(PreviewProgress,1f);
        // Never restart preview or accept more clicks during insertion.
        if (gameLaunchInProgress) return;

        // Actual cartridge Button.onClick callbacks own selection and launch.
        // Do not handle the same click a second time in Update().
        if (gameLaunchInProgress) return;

        // Do not consume typing keys while the search field has focus.
        bool searching = browseUI != null && browseUI.SearchFocused;
        if (!searching)
        {
            if (Input.GetKeyDown(KeyCode.F2)) CycleSort();
            if (Input.GetKeyDown(KeyCode.DownArrow))
                SelectTrack(Mathf.Min(selectedIndex + 1,
                    matchedSongs.Count - 1));
            if (Input.GetKeyDown(KeyCode.UpArrow))
                SelectTrack(Mathf.Max(0,selectedIndex - 1));
            if (Input.GetKeyDown(KeyCode.PageDown)) ChangePage(1);
            if (Input.GetKeyDown(KeyCode.PageUp)) ChangePage(-1);
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // Escape first dismisses the focused filter drawer.
                // Pressing it again returns to the title.
                if(browseUI == null || !browseUI.TryCloseFilterPanel())
                    BackToTitle();
            }
        }

        // Mouse wheel turns a full 2x3 page; the right-side jacket/preview
        // stays in place. One wheel event is never a launch click.
        if (listArea != null && matchedSongs.Count > 0 &&
            (browseUI == null ||
                !browseUI.BlocksCartridgeClick(Input.mousePosition)) &&
            RectTransformUtility.RectangleContainsScreenPoint(
                listArea, Input.mousePosition))
        {
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > .01f)
                ChangePage(wheel > 0 ? -1 : 1);
        }

        if (previewSource == null || previewSource.clip == null) return;
        if (!previewEnabled)
        {
            if (previewSource.isPlaying) previewSource.Stop();
            previewSource.volume = 0f;
            previewShownVolume = 0f;
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
        if (gameLaunchInProgress) return;
        previewEnabled = !previewEnabled;
        PlayerPrefs.SetInt(PreviewPreferenceKey, previewEnabled ? 1 : 0);
        PlayerPrefs.Save();
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

        // Second click starts the song directly. Do not create a clone,
        // slide the selected cartridge sideways or fly it into the jacket.
        gameLaunchInProgress = true;
        LoadSelectedChart(chart);
    }

    private void LoadSelectedChart(MouseModeChartAsset chart)
    {
        if (previewSource != null)
            previewSource.Stop();
        MouseModeSongSelection.Choose(chart);
        if (!SceneLoadingTransition.Load(gameplaySceneName))
        {
            // Failed transition: do not leave the menu permanently locked.
            MouseModeSongSelection.Choose(null);
            gameLaunchInProgress = false;
        }
    }

    private void BackToTitle()
    {
        if (gameLaunchInProgress) return;
        if (!SceneLoadingTransition.Load("TitleScene")) return;
        gameLaunchInProgress = true;
        if (previewSource != null)
            previewSource.Stop();
        MouseModeSongSelection.Choose(null);
    }
}
