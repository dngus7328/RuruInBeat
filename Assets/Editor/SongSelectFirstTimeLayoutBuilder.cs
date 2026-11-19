#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LuluInBeat.MouseMode.ChartSystem;

/// <summary>
/// Generates editable Image/TMP/Button scene objects ONCE for the empty
/// SongSelectScene. If the user has added/moved anything, it never regenerates.
/// Runtime selection UI changes labels, sprites and colors only, not placement.
/// </summary>
[InitializeOnLoad]
public static class SongSelectFirstTimeLayoutBuilder
{
    private const string ScenePath = "Assets/Scene/SongSelectScene.unity";
    private const string Charts = "Assets/MouseMode/ChartSystem/Charts/";

    private static readonly Color Background = new Color(.085f, .083f, .122f, 1f);
    private static readonly Color Surface = new Color(.13f, .13f, .195f, 1f);
    private static readonly Color Row = new Color(.15f, .145f, .22f, 1f);
    private static readonly Color Muted = new Color(.60f, .59f, .69f, 1f);
    private static readonly Color White = new Color(.96f, .94f, .92f, 1f);
    private static readonly Color Accent = new Color(.77f, .69f, .83f, 1f);
    private static readonly Color ButtonFill = new Color(.28f, .26f, .38f, 1f);

    static SongSelectFirstTimeLayoutBuilder()
    {
        // Opening the previously blank scene makes the real, editable UI once.
        // Not a runtime layout generator: future manual placement is untouched.
        EditorSceneManager.sceneOpened += HandleSceneOpened;
    }

    private static void HandleSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        EditorApplication.delayCall += () =>
        {
            if (scene.IsValid() && scene.isLoaded &&
                !EditorApplication.isPlayingOrWillChangePlaymode)
                BuildIfBlank(scene);
        };
    }

    [MenuItem("Tools/Lulu In Beat/Song Select/Create Layout Only If Blank")]
    private static void CreateLayoutFromMenu()
    {
        BuildIfBlank(SceneManager.GetActiveScene());
    }

    private static void BuildIfBlank(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
        {
            Debug.Log("[SongSelect] Open Assets/Scene/SongSelectScene.unity first.");
            return;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        // EXACTLY the current blank Unity scene (camera only) is permitted.
        // No UI positions/anchors can ever be overwritten by subsequent updates.
        if (roots.Length != 1 || roots[0].name != "Main Camera")
        {
            Debug.Log("[SongSelect] Existing scene objects detected; preserve all " +
                "manual layouts. No generation or re-layout performed.");
            return;
        }

        TMP_FontAsset textFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Fonts/Paperlogy-7Bold SDF.asset");
        TMP_FontAsset numberFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Fonts/Oxanium-VariableFont_wght SDF.asset");
        if (textFont == null) textFont = TMP_Settings.defaultFontAsset;
        if (numberFont == null) numberFont = textFont;

        SongSelectMenuUI.Track[] tracks = CreateCatalog();
        if (tracks.Length != 6)
        {
            Debug.LogError("[SongSelect] Six-track catalog is incomplete. " +
                "Refusing to generate a partially wired scene.");
            return;
        }

        GameObject canvasObject = new GameObject(
            "SongSelectCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        // Even when the scene was opened additively, never write into the
        // user's currently active gameplay/title scene.
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode =
            CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        RectTransform root = canvasObject.GetComponent<RectTransform>();
        SongSelectMenuUI controller =
            canvasObject.AddComponent<SongSelectMenuUI>();

        Panel("Background", root, 0f, 0f, 1f, 1f, Background);
        Panel("TopRule", root, .057f, .839f, .944f, .841f, Accent);
        Label("PageHeading", root, .062f, .905f, .63f, .964f,
            "MUSIC LIBRARY", 52f, numberFont, White,
            TextAlignmentOptions.Left);
        Label("PageSubtitle", root, .064f, .865f, .44f, .908f,
            "곡 선택  /  SELECT TRACK", 20f, textFont, Muted,
            TextAlignmentOptions.Left);
        Label("TrackCount", root, .70f, .89f, .941f, .941f,
            "06 TRACKS", 23f, numberFont, Accent,
            TextAlignmentOptions.Right);

        Image left = Panel("TrackListPanel", root,
            .056f, .165f, .477f, .821f, Surface);
        Image right = Panel("SelectedTrackPanel", root,
            .495f, .165f, .944f, .821f, Surface);

        Panel("LeftPanelTopEdge", left.rectTransform,
            0f, .993f, 1f, 1f, Accent);
        Panel("RightPanelTopEdge", right.rectTransform,
            0f, .993f, 1f, 1f, Accent);

        Label("ListHeading", left.rectTransform, .055f, .905f,
            .58f, .976f, "TRACK LIST", 24f,
            numberFont, White, TextAlignmentOptions.Left);
        Label("ListSubheading", left.rectTransform,
            .61f, .905f, .942f, .974f,
            "ALL SONGS", 17f, numberFont, Muted,
            TextAlignmentOptions.Right);

        Button[] rows = new Button[6];
        Image[] rowBackgrounds = new Image[6];
        TMP_Text[] rowLabels = new TMP_Text[6];

        for (int i = 0; i < 6; i++)
        {
            float top = .870f - .1345f * i;
            float bottom = top - .115f;

            Image rowPanel = Panel(
                "SongRow_" + (i + 1).ToString("00"),
                left.rectTransform, .054f, bottom, .946f, top, Row);
            Button rowButton = AddButton(rowPanel);
            rows[i] = rowButton;
            rowBackgrounds[i] = rowPanel;

            Label("Index", rowPanel.rectTransform,
                .034f, .17f, .12f, .84f,
                (i + 1).ToString("00"), 22f,
                numberFont, Accent, TextAlignmentOptions.Center);
            rowLabels[i] = Label("TrackName", rowPanel.rectTransform,
                .135f, .10f, .94f, .90f,
                tracks[i].displayTitle, 25f,
                textFont, White, TextAlignmentOptions.Left);
        }

        Label("InfoHeading", right.rectTransform,
            .055f, .91f, .94f, .98f,
            "NOW SELECTED", 21f, numberFont,
            Accent, TextAlignmentOptions.Left);

        // Flat album-art placeholder. Users may later replace ONLY the image
        // or assign a per-track Sprite without moving any RectTransform.
        Image cover = Panel("JacketImage_Replaceable", right.rectTransform,
            .054f, .395f, .465f, .849f,
            new Color(.19f, .175f, .26f, 1f));
        GameObject placeholder = Panel(
            "NoJacketArt_Placeholder", cover.rectTransform,
            0f, 0f, 1f, 1f, new Color(.19f, .175f, .26f, 1f)).gameObject;

        RectTransform placeholderRect = placeholder.GetComponent<RectTransform>();
        Panel("CenterLineHorizontal", placeholderRect,
            .13f, .49f, .87f, .496f, new Color(.32f, .30f, .40f, 1f));
        Panel("CenterLineVertical", placeholderRect,
            .497f, .12f, .503f, .88f, new Color(.32f, .30f, .40f, 1f));
        Label("JacketPlaceholderLabel", placeholderRect,
            .10f, .03f, .90f, .22f,
            "NO COVER ART", 17f, numberFont,
            Muted, TextAlignmentOptions.Center);

        Label("TrackLabel", right.rectTransform,
            .50f, .779f, .95f, .832f,
            "SONG TITLE", 16f, numberFont,
            Muted, TextAlignmentOptions.Left);
        TMP_Text title = Label("SelectedTitle", right.rectTransform,
            .49f, .646f, .95f, .781f,
            "BOOTBEAT", 36f, textFont,
            White, TextAlignmentOptions.Left);
        Panel("DetailRule", right.rectTransform,
            .50f, .628f, .942f, .632f, Accent);

        TMP_Text level = Label("SelectedLevel", right.rectTransform,
            .50f, .553f, .95f, .621f,
            "LEVEL  06 / 15", 26f, numberFont,
            White, TextAlignmentOptions.Left);
        TMP_Text bpm = Label("SelectedBpm", right.rectTransform,
            .50f, .482f, .95f, .552f,
            "134.27 BPM", 23f, numberFont,
            Accent, TextAlignmentOptions.Left);
        TMP_Text duration = Label("SelectedDuration", right.rectTransform,
            .50f, .41f, .95f, .479f,
            "02:20", 23f, numberFont,
            Muted, TextAlignmentOptions.Left);

        Label("DifficultyLabel", right.rectTransform,
            .06f, .285f, .55f, .347f,
            "DIFFICULTY", 18f, numberFont,
            Muted, TextAlignmentOptions.Left);
        TMP_Text difficulty = Label("DifficultyValue",
            right.rectTransform, .06f, .195f, .56f, .285f,
            "MOUSE", 24f, numberFont,
            White, TextAlignmentOptions.Left);

        Button previousDifficulty = FlatButton("PreviousDifficulty",
            right.rectTransform, .66f, .209f, .77f, .31f,
            "<", 28f, numberFont, ButtonFill, White);
        Button nextDifficulty = FlatButton("NextDifficulty",
            right.rectTransform, .80f, .209f, .91f, .31f,
            ">", 28f, numberFont, ButtonFill, White);

        Button previewButton = FlatButton("PreviewButton",
            right.rectTransform, .055f, .065f, .36f, .163f,
            "PREVIEW", 21f, numberFont, ButtonFill, White);
        TMP_Text previewStatus = Label("PreviewStatus",
            right.rectTransform, .40f, .064f, .934f, .16f,
            "PREVIEW  ON", 18f, numberFont,
            Muted, TextAlignmentOptions.Right);

        Panel("BottomRule", root,
            .057f, .140f, .944f, .142f, Accent);
        Button back = FlatButton("BackToTitle",
            root, .057f, .038f, .245f, .112f,
            "BACK TO TITLE", 22f, numberFont, ButtonFill, White);
        Label("Footnote", root,
            .31f, .043f, .68f, .10f,
            "LEFT CLICK  /  RIGHT CLICK  /  WHEEL", 15f,
            numberFont, Muted, TextAlignmentOptions.Center);
        Button play = FlatButton("PlaySelected",
            root, .748f, .038f, .943f, .112f,
            "START GAME", 25f, numberFont,
            new Color(.43f, .36f, .54f, 1f), White);

        // The source is deliberately separate from scene object positions.
        GameObject previewAudio = new GameObject(
            "SongPreviewAudio", typeof(AudioSource));
        previewAudio.transform.SetParent(root, false);
        AudioSource audio = previewAudio.GetComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.loop = false;
        audio.spatialBlend = 0f;
        audio.volume = .28f;

        GameObject eventObject = new GameObject(
            "EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        SceneManager.MoveGameObjectToScene(eventObject, scene);

        controller.ConfigureInitialLayout(tracks, rows, rowBackgrounds,
            rowLabels, title, level, bpm, duration, difficulty, previewStatus,
            cover, placeholder, previousDifficulty, nextDifficulty,
            previewButton, play, back, audio);

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        RegisterMissingBuildScenes();

        Debug.Log("[SongSelect] Editable shape-only UI was created ONCE " +
            "and saved in SongSelectScene. Future layout and font/image changes " +
            "will never be overwritten by this generator.", canvasObject);
    }

    private static SongSelectMenuUI.Track[] CreateCatalog()
    {
        string[] names =
        {
            "BOOTBEAT", "Pop'n Dash", "Full-Band Hit",
            "Cotton Pulse", "Oddbeat Shuffle", "瞬奏 -SHUNSOU-"
        };
        string[][] chartIds =
        {
            new[] { "BOOTBEAT_MOUSE_LV6", "BOOTBEAT_MOUSE_LV11" },
            new[] { "popn_dash" },
            new[] { "FullBandHit_MOUSE" },
            new[] { "RhythmGameTheme_MOUSE" },
            new[] { "ArcadeGroove_MOUSE" },
            new[] { "MabatakiNoSenritsu_MOUSE" }
        };
        string[][] sceneNames =
        {
            new[] { "Mouse_BOOTBEAT", "Mouse_BOOTBEAT_Challenge" },
            new[] { "Mouse_PopnDash" },
            new[] { "Mouse_FullBandHit" },
            new[] { "Mouse_RhythmGameTheme" },
            new[] { "Mouse_ArcadeGroove" },
            new[] { "Mouse_MabatakiNoSenritsu" }
        };

        var catalog = new SongSelectMenuUI.Track[names.Length];

        for (int i = 0; i < names.Length; i++)
        {
            MouseModeChartAsset[] assets =
                new MouseModeChartAsset[chartIds[i].Length];

            for (int j = 0; j < assets.Length; j++)
            {
                string path = Charts + chartIds[i][j] + ".asset";
                assets[j] = AssetDatabase.LoadAssetAtPath<MouseModeChartAsset>(path);
                if (assets[j] == null)
                {
                    Debug.LogError("[SongSelect] Chart not found: " + path +
                        ". Pull latest GitHub files first.");
                    return Array.Empty<SongSelectMenuUI.Track>();
                }
            }

            catalog[i] = new SongSelectMenuUI.Track
            {
                displayTitle = names[i],
                charts = assets,
                sceneNames = sceneNames[i]
            };
        }

        return catalog;
    }

    private static Image Panel(string name, RectTransform parent,
        float left, float bottom, float right, float top, Color color)
    {
        GameObject go = new GameObject(
            name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(left, bottom);
        rect.anchorMax = new Vector2(right, top);
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        Image image = go.GetComponent<Image>();
        image.sprite = null;  // a plain Unity UI quad: no PNG needed
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text Label(string name, RectTransform parent,
        float left, float bottom, float right, float top,
        string value, float fontSize, TMP_FontAsset font,
        Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(left, bottom);
        rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        label.text = value;
        label.font = font;
        label.fontSize = fontSize;
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Max(11f, fontSize * .55f);
        label.fontSizeMax = fontSize;
        label.alignment = align;
        label.color = color;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    private static Button AddButton(Image background)
    {
        background.raycastTarget = true;
        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.08f, 1.12f, 1f);
        colors.pressedColor = new Color(.80f, .79f, .86f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(.6f, .6f, .6f, .55f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        return button;
    }

    private static Button FlatButton(string name, RectTransform parent,
        float left, float bottom, float right, float top,
        string caption, float fontSize, TMP_FontAsset font,
        Color background, Color foreground)
    {
        Image panel = Panel(name, parent, left, bottom, right, top, background);
        Button button = AddButton(panel);
        Label("ButtonText", panel.rectTransform,
            .04f, .06f, .96f, .94f,
            caption, fontSize, font, foreground,
            TextAlignmentOptions.Center);
        return button;
    }

    private static void RegisterMissingBuildScenes()
    {
        // Append only; keep all existing scenes, order and enable flags.
        var scenes = new List<EditorBuildSettingsScene>(
            EditorBuildSettings.scenes);
        string[] needed =
        {
            "Assets/Scene/SongSelectScene.unity",
            "Assets/Scene/Mouse.unity",
            "Assets/Scene/Mouse_BOOTBEAT.unity",
            "Assets/Scene/Mouse_BOOTBEAT_Challenge.unity",
            "Assets/Scene/Mouse_PopnDash.unity",
            "Assets/Scene/Mouse_FullBandHit.unity",
            "Assets/Scene/Mouse_RhythmGameTheme.unity",
            "Assets/Scene/Mouse_ArcadeGroove.unity",
            "Assets/Scene/Mouse_MabatakiNoSenritsu.unity"
        };
        bool modified = false;
        foreach (string scenePath in needed)
        {
            bool exists = scenes.Exists(s => s.path == scenePath);
            if (!exists && AssetDatabase.LoadAssetAtPath<SceneAsset>(
                scenePath) != null)
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                modified = true;
            }
        }

        if (modified)
            EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
