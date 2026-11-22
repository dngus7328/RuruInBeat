using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LuluInBeat.MouseMode.ChartSystem;

/// <summary>
/// Authored first-play tutorial: a short conversation, six playable chapters,
/// and a playable-world return to song select. Uses the actual Mouse rhythm,
/// Pino and bullet systems; never substitutes button simulations for them.
/// The separate Tutorial scene shares the game's existing art and layout.
/// </summary>
[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public sealed class MouseTutorialController : MonoBehaviour
{
    private const string TutorialScene = "Tutorial";
    private const string SongSelectScene = "SongSelectScene";

    private static readonly float[] ChapterBeats = { 0f, 16f, 41f, 68f, 102f, 132f, 178f };
    private static readonly string[] ChapterNames =
    {
        "피노 움직이기", "왼쪽 클릭", "오른쪽 클릭",
        "마우스 휠", "탄막 피하기", "실전 연습"
    };
    private static readonly string[] ChapterGuides =
    {
        "마우스를 움직여 피노를 이동시켜 봐.",
        "L 표시가 있는 노트의 원이 겹칠 때 왼쪽 버튼을 눌러.",
        "이번에는 R 노트야. 원이 겹칠 때 오른쪽 버튼을 눌러.",
        "W 노트는 휠을 돌려 시작해. 끝날 때까지 계속 돌려 봐.",
        "붉은 공격을 피해 움직여. 피노가 맞으면 안정화가 줄어들어.",
        "이제 노트 판정과 탄막 회피를 함께 해 봐."
    };
    private static readonly string[] Speaker =
    {
        "루루", "피노", "루루", "피노"
    };
    private static readonly string[] Opening =
    {
        "…게임이 켜졌네. 그런데, 화면이 조금 이상해.",
        "루루! 바이러스 반응이 보여. 먼저 조작을 확인하자.",
        "…응. 피노, 같이 해 보자.",
        "좋아. 천천히 하나씩 익히면 돼. 연습이 끝나면 곡을 고를 수 있어."
    };
    private static readonly string[] Closing =
    {
        "…이제 어떻게 플레이하는지 조금 알 것 같아.",
        "연습은 여기까지야! 원할 때 언제든 다시 연습할 수 있어."
    };

    private MouseBootSequenceUI boot;
    private MouseModeChartPlayer chartPlayer;
    private MouseModeBulletDirector bullets;
    private PinoMouseCursorUI pinoController;
    private RectTransform pinoRect;
    private Canvas gameplayCanvas;
    private TMP_FontAsset gameFont;

    private GameObject dialogueOverlay;
    private GameObject guideRoot;
    private TextMeshProUGUI speakerText;
    private TextMeshProUGUI dialogueText;
    private TextMeshProUGUI dialogueHeading;
    private TextMeshProUGUI dialogueButtonLabel;
    private Image conversationPortrait;
    private TextMeshProUGUI guideTitle;
    private TextMeshProUGUI guideBody;
    private TextMeshProUGUI guideProgress;
    private Image guideIcon;
    private Button nextButton;
    private Button replayButton;
    private Button songSelectButton;

    private readonly bool[] passed = new bool[6];
    private readonly int[] noteSuccesses = new int[6];
    private readonly float[] distanceMoved = new float[6];
    private Vector2 previousPinoPosition;
    private bool hasPreviousPosition;
    private int currentChapter = -1;
    private int introIndex;
    private int outroIndex;
    private int hitsAtDodgeStart;
    private bool playStarted;
    private bool completed;
    private bool changingScene;
    private bool checkpointOpen;
    private bool resumingCheckpoint;
    private float storedTimeScale = 1f;
    private bool storedAudioPause;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterBootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapWhenOpenedDirectly()
    {
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || scene.name != TutorialScene)
            return;

        foreach (Canvas canvas in FindObjectsOfType<Canvas>(true))
        {
            if (canvas.gameObject.scene != scene ||
                canvas.name != "GameplayCanvas")
                continue;

            if (canvas.GetComponent<MouseTutorialController>() == null)
                canvas.gameObject.AddComponent<MouseTutorialController>();
            return;
        }
        Debug.LogError("[Tutorial] GameplayCanvas is missing.");
    }

    private void Awake()
    {
        gameplayCanvas = GetComponent<Canvas>();
        boot = FindObjectOfType<MouseBootSequenceUI>();
        chartPlayer = FindObjectOfType<MouseModeChartPlayer>();
        bullets = FindObjectOfType<MouseModeBulletDirector>();
        pinoController = FindObjectOfType<PinoMouseCursorUI>();
        pinoRect = pinoController != null
            ? pinoController.transform as RectTransform : null;

        if (gameplayCanvas == null || boot == null ||
            chartPlayer == null || pinoController == null ||
            chartPlayer.ActiveChartAsset == null ||
            chartPlayer.ActiveChartAsset.data == null)
        {
            Debug.LogError("[Tutorial] The tutorial scene is missing game systems.");
            enabled = false;
            return;
        }

        // sceneLoaded is after every scene Awake, but before Start.
        // Let MouseBootSequenceUI run its genuine power-on/READY/GO only
        // after the conversation finishes.
        boot.enabled = false;
        if (bullets != null)
        {
            bullets.SetTutorialSafety(true);
            bullets.PinoHit += OnPinoHit;
        }
        chartPlayer.ChartNoteJudged += OnNoteJudged;
        chartPlayer.SongCompleted += OnSongFinished;

        gameFont = LocateGameFont();
        BuildDialogue();
        BuildGuide();
        ShowOpening();
    }

    private void OnDestroy()
    {
        if (checkpointOpen)
            RestoreGameplayClock();
        if (bullets != null)
            bullets.PinoHit -= OnPinoHit;
        if (chartPlayer != null)
        {
            chartPlayer.ChartNoteJudged -= OnNoteJudged;
            chartPlayer.SongCompleted -= OnSongFinished;
        }
    }

    private static TMP_FontAsset LocateGameFont()
    {
        TMP_FontAsset fallback = TMP_Settings.defaultFontAsset;
        foreach (TMP_Text label in FindObjectsOfType<TMP_Text>(true))
        {
            if (label != null && label.font != null)
            {
                if (label.font.name.IndexOf("Paperlogy",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                    return label.font;
                if (fallback == null)
                    fallback = label.font;
            }
        }
        // The scene already serializes Paperlogy on the Pino/Note scripts,
        // even when its TMP labels have not been instantiated yet.
        foreach (TMP_FontAsset loaded in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (loaded != null && loaded.name.IndexOf("Paperlogy",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return loaded;
        }
        return fallback;
    }

    private void Update()
    {
        if (!playStarted || completed || changingScene || checkpointOpen ||
            chartPlayer == null || !chartPlayer.IsChartPlaying ||
            boot == null || !boot.IsGameplayStarted ||
            pinoController == null || pinoController.IsPaused)
            return;

        float songTime = chartPlayer.SongElapsedSeconds;
        MouseModeChartData chart = chartPlayer.ActiveChartAsset.data;
        if (songTime >= MouseModeChartTiming.BeatToSeconds(chart, ChapterBeats[6]))
        {
            FinishTutorial();
            return;
        }

        int chapter = 0;
        while (chapter < 5 &&
            songTime >= MouseModeChartTiming.BeatToSeconds(chart,
                ChapterBeats[chapter + 1]))
            chapter++;

        if (chapter != currentChapter)
        {
            if (currentChapter >= 0)
                FinalizeChapter(currentChapter);

            currentChapter = chapter;
            previousPinoPosition = pinoRect != null
                ? pinoRect.anchoredPosition : Vector2.zero;
            hasPreviousPosition = true;
            if (currentChapter == 4)
                hitsAtDodgeStart = bullets != null ? bullets.HitCount : 0;
            UpdateGuide();
            if (chapter > 0)
            {
                ShowCheckpoint(chapter);
                return;
            }
        }

        if (hasPreviousPosition && pinoRect != null)
        {
            Vector2 now = pinoRect.anchoredPosition;
            distanceMoved[currentChapter] += Vector2.Distance(now, previousPinoPosition);
            previousPinoPosition = now;
        }

        if (IsSuccessful(currentChapter) && !passed[currentChapter])
        {
            passed[currentChapter] = true;
            UpdateGuide();
        }
    }

    private void OnNoteJudged(string judgement)
    {
        if (!playStarted || completed || currentChapter < 0 ||
            judgement == "MISS")
            return;

        // Each authored chapter contains exactly its taught note family.
        // The last chapter mixes all three, proving they work together.
        if (currentChapter == 1 || currentChapter == 2 ||
            currentChapter == 3 || currentChapter == 5)
        {
            noteSuccesses[currentChapter]++;
            if (IsSuccessful(currentChapter))
            {
                passed[currentChapter] = true;
                UpdateGuide();
            }
        }
    }

    private void OnPinoHit(int stability)
    {
        // The actual battle system handles the hit feedback; the lesson
        // only checks whether the player navigated the practice section.
        if (currentChapter == 4)
            UpdateGuide();
    }

    private bool IsSuccessful(int chapter)
    {
        switch (chapter)
        {
            case 0: return distanceMoved[0] >= 110f;
            case 1:
            case 2:
            case 3: return noteSuccesses[chapter] >= 1;
            case 4:
                // Only award dodge completion after both guided attacks
                // have passed, not before the first projectile appears.
                return chartPlayer != null &&
                    chartPlayer.SongElapsedSeconds >=
                        MouseModeChartTiming.BeatToSeconds(
                            chartPlayer.ActiveChartAsset.data, 126f) &&
                    distanceMoved[4] >= 100f &&
                    (bullets == null || bullets.HitCount - hitsAtDodgeStart <= 1);
            case 5:
                return noteSuccesses[5] >= 2 && distanceMoved[5] >= 60f;
            default: return false;
        }
    }

    private void FinalizeChapter(int chapter)
    {
        if (chapter < 0 || chapter >= passed.Length)
            return;
        passed[chapter] = passed[chapter] || IsSuccessful(chapter);
    }

    private void OnSongFinished()
    {
        if (!completed && playStarted)
            FinishTutorial();
    }

    private void ShowOpening()
    {
        completed = false;
        playStarted = false;
        introIndex = 0;
        dialogueOverlay.SetActive(true);
        guideRoot.SetActive(false);
        replayButton.gameObject.SetActive(false);
        songSelectButton.gameObject.SetActive(false);
        nextButton.gameObject.SetActive(true);
        ShowOpeningLine();
    }

    private void ShowOpeningLine()
    {
        speakerText.text = Speaker[introIndex];
        dialogueText.text = Opening[introIndex];
        dialogueHeading.text = "첫 접속  /  프롤로그";
        dialogueButtonLabel.text =
            introIndex == Opening.Length - 1 ? "연습 시작" : "다음";
        UpdatePortrait();
    }

    private void NextDialogue()
    {
        if (changingScene)
            return;
        if (completed)
        {
            AdvanceClosing();
            return;
        }
        if (checkpointOpen)
        {
            ContinueAfterCheckpoint();
            return;
        }

        if (introIndex + 1 < Opening.Length)
        {
            introIndex++;
            ShowOpeningLine();
            return;
        }

        dialogueOverlay.SetActive(false);
        guideRoot.SetActive(true);
        playStarted = true;
        boot.enabled = true;
        UpdateGuide();
    }

    private void ShowCheckpoint(int chapter)
    {
        checkpointOpen = true;
        storedTimeScale = Time.timeScale;
        storedAudioPause = AudioListener.pause;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        pinoController.ExitGameplay();

        dialogueOverlay.SetActive(true);
        nextButton.gameObject.SetActive(true);
        replayButton.gameObject.SetActive(false);
        songSelectButton.gameObject.SetActive(false);
        dialogueHeading.text = "첫 접속  /  훈련 " + (chapter + 1) + " / 06";
        speakerText.text = "피노";
        dialogueText.text = passed[chapter - 1]
            ? "좋아! 다음은 " + ChapterNames[chapter] + "이야. " +
              ChapterGuides[chapter]
            : "이번에는 " + ChapterNames[chapter] + "을 연습하자. " +
              ChapterGuides[chapter];
        dialogueButtonLabel.text = "계속";
        UpdatePortrait();
    }

    private void ContinueAfterCheckpoint()
    {
        if (!checkpointOpen || resumingCheckpoint)
            return;
        StartCoroutine(ResumeAfterGuideClick());
    }

    private IEnumerator ResumeAfterGuideClick()
    {
        resumingCheckpoint = true;
        // Do not treat the UI "continue" click as a failed rhythm note.
        // The clock remains paused until the left/right mouse press
        // that activated the button is completely released.
        while (Input.GetMouseButton(0) || Input.GetMouseButton(1))
            yield return null;
        yield return null;

        RestoreGameplayClock();
        dialogueOverlay.SetActive(false);
        pinoController.EnterGameplay();
        previousPinoPosition = pinoRect != null
            ? pinoRect.anchoredPosition : Vector2.zero;
        resumingCheckpoint = false;
    }

    private void RestoreGameplayClock()
    {
        if (!checkpointOpen)
            return;
        checkpointOpen = false;
        Time.timeScale = storedTimeScale;
        AudioListener.pause = storedAudioPause;
    }

    private void AdvanceClosing()
    {
        if (outroIndex + 1 < Closing.Length)
        {
            outroIndex++;
            speakerText.text = outroIndex == 0 ? "루루" : "피노";
            dialogueText.text = Closing[outroIndex];
            dialogueButtonLabel.text = outroIndex == Closing.Length - 1
                ? "마무리" : "다음";
            UpdatePortrait();
            return;
        }

        nextButton.gameObject.SetActive(false);
        replayButton.gameObject.SetActive(true);
        songSelectButton.gameObject.SetActive(true);
    }

    private void UpdatePortrait()
    {
        if (conversationPortrait == null)
            return;
        bool pinoSpeaking = speakerText != null && speakerText.text == "피노";
        conversationPortrait.gameObject.SetActive(pinoSpeaking &&
            conversationPortrait.sprite != null);
    }

    private void FinishTutorial()
    {
        if (completed || changingScene)
            return;

        FinalizeChapter(currentChapter);
        completed = true;
        chartPlayer.StopChart();
        pinoController.ExitGameplay();
        if (bullets != null)
            bullets.enabled = false;

        SaveManager save = SaveManager.Instance;
        if (save != null && save.Data != null)
        {
            save.Data.tutorialCompleted = true;
            save.Save();
        }

        int count = 0;
        foreach (bool chapter in passed)
            if (chapter) count++;

        dialogueOverlay.SetActive(true);
        guideRoot.SetActive(false);
        nextButton.gameObject.SetActive(true);
        replayButton.gameObject.SetActive(false);
        songSelectButton.gameObject.SetActive(false);
        dialogueButtonLabel.text = "다음";
        outroIndex = 0;
        dialogueHeading.text = "첫 접속  /  연습 완료  /  " +
            count + " / " + passed.Length + "단계 성공";
        speakerText.text = "루루";
        dialogueText.text = count == passed.Length
            ? Closing[0] : "…조금 놓쳤지만, 어떤 느낌인지는 알 것 같아.";
        UpdatePortrait();
    }

    private void GoToSongSelect()
    {
        if (changingScene) return;
        changingScene = SceneLoadingTransition.Load(SongSelectScene);
    }

    private void RepeatTutorial()
    {
        if (changingScene) return;
        changingScene = SceneLoadingTransition.Load(TutorialScene);
    }

    private void UpdateGuide()
    {
        if (guideRoot == null || currentChapter < 0)
        {
            if (guideRoot != null)
            {
                guideTitle.text = "훈련 준비";
                guideBody.text = "시작 신호가 나오면 마우스를 움직여 봐.";
                guideProgress.text = "01 / 06";
            }
            return;
        }

        bool good = passed[currentChapter] || IsSuccessful(currentChapter);
        guideTitle.text = good
            ? "완료  ·  " + ChapterNames[currentChapter]
            : ChapterNames[currentChapter];
        guideBody.text = good
            ? "좋아! 다음 단계까지 계속 플레이해 봐."
            : ChapterGuides[currentChapter];
        guideProgress.text = (currentChapter + 1).ToString("00") + " / 06";
        if (guideIcon != null)
        {
            string[] icons = { "Guide_MouseMove", "Guide_LeftClick",
                "Guide_RightClick", "Guide_Wheel" };
            bool visible = currentChapter >= 0 && currentChapter < icons.Length;
            Sprite iconSprite = visible
                ? Resources.Load<Sprite>("RuruArt/Tutorial/" + icons[currentChapter])
                : null;
            guideIcon.sprite = iconSprite;
            guideIcon.gameObject.SetActive(iconSprite != null);
            RectTransform bodyRect = guideBody.rectTransform;
            bodyRect.anchorMin = new Vector2(iconSprite != null ? .34f : .075f, .10f);
            bodyRect.anchorMax = new Vector2(.93f, .48f);
            bodyRect.offsetMin = Vector2.zero;
            bodyRect.offsetMax = Vector2.zero;
        }
    }

    private void BuildDialogue()
    {
        RectTransform screen = AddRect("TutorialConversation", transform,
            Vector2.zero, Vector2.one);
        dialogueOverlay = screen.gameObject;
        AddImage(screen, new Color(.065f, .052f, .085f, .94f), true);

        RectTransform panel = AddRect("DialoguePanel", screen,
            new Vector2(.16f, .10f), new Vector2(.84f, .38f));
        Image dialoguePanelImage = AddImage(panel,
            new Color(.18f, .14f, .24f, 1f), true);
        Sprite dialogueArt = Resources.Load<Sprite>("RuruArt/UI/UI_Panel");
        if (dialogueArt != null)
        {
            dialoguePanelImage.sprite = dialogueArt;
            dialoguePanelImage.type = Image.Type.Sliced;
            dialoguePanelImage.color = Color.white;
        }

        RectTransform heading = AddRect("SceneLabel", screen,
            new Vector2(.17f, .46f), new Vector2(.83f, .54f));
        dialogueHeading = AddLabel(heading, 25f, 
            new Color(.83f, .74f, .90f, 1f), TextAlignmentOptions.Center);

        RectTransform speaker = AddRect("Speaker", panel,
            new Vector2(.035f, .72f), new Vector2(.45f, .91f));
        speakerText = AddLabel(speaker, 30f,
            dialogueArt != null ? new Color(.15f,.10f,.22f,1f) : Color.white,
            TextAlignmentOptions.MidlineLeft);

        RectTransform body = AddRect("Dialogue", panel,
            new Vector2(.035f, .27f), new Vector2(.79f, .70f));
        dialogueText = AddLabel(body, 27f,
            dialogueArt != null ? new Color(.18f,.13f,.25f,1f)
                : new Color(.94f, .92f, .97f, 1f),
            TextAlignmentOptions.MidlineLeft);
        dialogueText.enableWordWrapping = true;

        // Use Pino's existing in-game sprite instead of inventing new
        // character art. The portrait never changes its design or proportions.
        RectTransform portrait = AddRect("PinoPortrait", panel,
            new Vector2(.805f, .27f), new Vector2(.972f, .94f));
        conversationPortrait = AddImage(portrait, Color.white, false);
        Image originalPino = pinoController.GetComponent<Image>();
        if (originalPino != null)
            conversationPortrait.sprite = originalPino.sprite;
        conversationPortrait.preserveAspect = true;
        conversationPortrait.gameObject.SetActive(false);

        nextButton = CreateButton("NextDialogue", panel,
            new Vector2(.78f, .02f), new Vector2(.97f, .25f),
            "다음", NextDialogue, out dialogueButtonLabel);

        replayButton = CreateButton("RetryTutorial", panel,
            new Vector2(.50f, .02f), new Vector2(.71f, .25f),
            "다시 연습", RepeatTutorial, out _);

        songSelectButton = CreateButton("LeaveTutorial", panel,
            new Vector2(.73f, .02f), new Vector2(.97f, .25f),
            "곡 선택", GoToSongSelect, out _);

        screen.SetAsLastSibling();
    }

    private void BuildGuide()
    {
        // The guidance is placed to the right of the existing play field.
        // It must not intercept mouse movement, clicks or wheel input.
        RectTransform root = AddRect("TutorialGuide", transform,
            new Vector2(.70f, .20f), new Vector2(.975f, .43f));
        guideRoot = root.gameObject;
        Image guideBackground = AddImage(root,
            new Color(.14f, .11f, .19f, .93f), false);
        Sprite panelArt = Resources.Load<Sprite>("RuruArt/UI/UI_Panel");
        if (panelArt != null)
        {
            guideBackground.sprite = panelArt;
            guideBackground.type = Image.Type.Sliced;
            guideBackground.color = Color.white;
        }
        RectTransform iconArea = AddRect("ControlIcon", root,
            new Vector2(.055f, .075f), new Vector2(.33f, .57f));
        guideIcon = AddImage(iconArea, Color.white, false);
        guideIcon.preserveAspect = true;

        RectTransform number = AddRect("ChapterIndex", root,
            new Vector2(.075f, .74f), new Vector2(.925f, .86f));
        guideProgress = AddLabel(number, 24f,
            panelArt != null ? new Color(.35f,.23f,.47f,1f)
                : new Color(.78f, .70f, .86f, 1f),
            TextAlignmentOptions.MidlineLeft);

        RectTransform title = AddRect("ChapterTitle", root,
            new Vector2(.075f, .52f), new Vector2(.925f, .72f));
        guideTitle = AddLabel(title, 26f,
            panelArt != null ? new Color(.15f,.10f,.22f,1f) : Color.white,
            TextAlignmentOptions.MidlineLeft);

        RectTransform body = AddRect("ChapterDescription", root,
            new Vector2(.34f, .10f), new Vector2(.93f, .48f));
        guideBody = AddLabel(body, 22f,
            panelArt != null ? new Color(.25f,.18f,.34f,1f)
                : new Color(.89f, .86f, .93f, 1f),
            TextAlignmentOptions.MidlineLeft);
        guideBody.enableWordWrapping = true;
        root.SetAsLastSibling();
    }

    private static RectTransform AddRect(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject obj = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static Image AddImage(RectTransform rect, Color color, bool blocksInput)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = blocksInput;
        return image;
    }

    private TextMeshProUGUI AddLabel(RectTransform rect,
        float size, Color color, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI label =
            rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (gameFont != null)
            label.font = gameFont;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }

    private Button CreateButton(string name, Transform parent,
        Vector2 min, Vector2 max, string caption,
        UnityEngine.Events.UnityAction action,
        out TextMeshProUGUI captionLabel)
    {
        RectTransform rect = AddRect(name, parent, min, max);
        Image image = AddImage(rect,
            new Color(.36f, .28f, .44f, 1f), true);
        Sprite buttonArt = Resources.Load<Sprite>("RuruArt/UI/UI_Button");
        if (buttonArt != null)
        {
            image.sprite = buttonArt;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(action);
        RectTransform textRect = AddRect("Label", rect,
            new Vector2(.02f, .02f), new Vector2(.98f, .98f));
        captionLabel = AddLabel(textRect, 22f,
            buttonArt != null ? new Color(.15f,.10f,.22f,1f) : Color.white,
            TextAlignmentOptions.Center);
        captionLabel.text = caption;
        return button;
    }
}
