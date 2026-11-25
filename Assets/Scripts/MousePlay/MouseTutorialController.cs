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
    // Short, action-first instructions. The current action always remains visible
    // beside the play field while the dialogue window is closed.
    private static readonly string[] ChapterGuides =
    {
        "마우스를 움직이면 피노가 따라와.",
        "L 노트의 두 원이 겹치면 왼쪽 클릭!",
        "R 노트의 두 원이 겹치면 오른쪽 클릭!",
        "W 노트는 휠을 돌려 시작해. 끝날 때까지 계속 돌려 줘.",
        "붉은 탄막은 피해야 해. 마우스를 움직여 봐.",
        "노트를 맞히면서 붉은 탄막도 피해 봐."
    };
    private static readonly string[] ChapterSuccess =
    {
        "피노가 움직였어! 이제 손에 익었지?",
        "좋아! 왼쪽 클릭 타이밍을 잡았어.",
        "맞았어! 오른쪽 클릭도 잘했어.",
        "휠 노트도 해냈어!",
        "무사히 피해냈어. 계속 가 보자!",
        "연주와 회피를 함께 해냈어!"
    };
    private static readonly string[] ChapterLead =
    {
        "마우스를 움직이면 피노가 따라와. 먼저 가볍게 움직여 보자.",
        "이번에는 L 노트야. 원이 겹치는 순간 왼쪽 클릭!",
        "R 노트는 오른쪽 클릭이야. 앞에서 배운 박자를 기억해.",
        "이번엔 W 노트. 휠을 돌려 시작하고 끝까지 돌려 줘.",
        "빨간 건 노트가 아니야. 피노를 움직여 탄막을 피해!",
        "마지막이야. 노트 판정과 탄막 회피를 함께 해 보자."
    };
    private static readonly string[] Speaker =
    {
        "루루", "피노", "루루", "피노",
        "루루", "피노", "루루", "피노"
    };
    private static readonly string[] Opening =
    {
        "…이상하네. 평소에 하던 리듬게임이랑 화면이 달라.",
        "루루! 게임 안에 바이러스 반응이 있어! 조심해야겠어.",
        "…피노, 괜찮아? 너도 저 안에서 움직일 수 있어?",
        "응! 마우스를 움직이면 내가 따라가. 먼저 같이 연습해 보자.",
        "저 원은… 노트에 가까워질수록 겹치는 것 같아.",
        "맞아! 원이 겹칠 때 눌러 줘. L은 왼쪽, R은 오른쪽 클릭이야.",
        "…W는 휠이고, 빨간 공격은 피해 가면서 연주하면 되는 거네.",
        "정답! 준비됐으면 연습을 시작하자. 내가 옆에서 알려 줄게!"
    };
    private static readonly string[] Closing =
    {
        "…응. 이제 어떻게 움직이고 연주하는지 알겠어.",
        "잘했어! 곡을 고르러 가자. 다시 익히고 싶으면 연습도 할 수 있어."
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
    private RectTransform conversationNameplate;
    private RuruOriginalUITheme conversationTheme;
    // Indices refer to the authored expression sprites in the shared theme.
    // Lulu: thinking, neutral, dissatisfied, encouraging.
    // Pino: worried, neutral, confident, introducing, questioning.
    private static readonly int[] OpeningPortraits = { 2, 0, 0, 3, 1, 1, 1, 2 };
    private TextMeshProUGUI guideTitle;
    private TextMeshProUGUI guideBody;
    private TextMeshProUGUI guideProgress;
    private Image guideIcon;
    private Image guideProgressTrack;
    private Image guideProgressFill;
    private Coroutine dialogueRevealRoutine;
    private int guideIconChapter = -1;
    private float nextGuideRefresh;
    private float feedbackExpires;
    private string feedbackMessage;
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
        return RuruConsoleUI.Font();
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
            guideIconChapter = -1;
            feedbackMessage = null;
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
            feedbackMessage = null;
            UpdateGuide();
        }
        else if (Time.unscaledTime >= nextGuideRefresh)
        {
            nextGuideRefresh = Time.unscaledTime + 0.15f;
            UpdateGuide();
        }
    }

    private void OnNoteJudged(string judgement)
    {
        if (!playStarted || completed || currentChapter < 0)
            return;
        if (judgement == "MISS")
        {
            ShowLiveFeedback("괜찮아. 원이 만나는 순간에 맞춰 봐.", 1.7f);
            return;
        }

        // Each authored chapter contains exactly its taught note family.
        // The last chapter mixes all three, proving they work together.
        if (currentChapter == 1 || currentChapter == 2 ||
            currentChapter == 3 || currentChapter == 5)
        {
            noteSuccesses[currentChapter]++;
            ShowLiveFeedback(judgement == "PERFECT" ?
                "딱 맞았어! 좋은 타이밍이야." :
                "좋아, 타이밍을 잡았어!", 1.0f);
            if (IsSuccessful(currentChapter))
            {
                passed[currentChapter] = true;
                UpdateGuide();
            }
        }
    }

    private void OnPinoHit(int stability)
    {
        // Preserve the gameplay damage feedback; the extra guide tells the
        // player what to do instead of repeating a failure message.
        if (currentChapter >= 4)
            ShowLiveFeedback("앗, 탄막이야! 빨간 공격에서 멀어져 봐.", 2.0f);
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
        dialogueHeading.text = "게임 속 이상 현상  /  " +
            (introIndex + 1).ToString("00") + " - " +
            Opening.Length.ToString("00");
        dialogueButtonLabel.text =
            introIndex == Opening.Length - 1 ? "연습 시작" : "다음";
        UpdatePortrait();
        StartDialogueReveal();
    }

    private void NextDialogue()
    {
        if (changingScene)
            return;
        if (FinishCurrentDialogueReveal())
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
        dialogueHeading.text = "훈련 안내  /  " +
            (chapter + 1).ToString("00") + " - 06";
        bool previousDone = passed[chapter - 1];
        speakerText.text = previousDone && chapter % 2 == 0 ? "루루" : "피노";
        string reaction = previousDone ? ChapterSuccess[chapter - 1] :
            "조금 놓쳐도 괜찮아. 다음 곡에서 다시 시도할 수 있어.";
        dialogueText.text = reaction + "\n" + ChapterLead[chapter];
        dialogueButtonLabel.text = "계속";
        UpdatePortrait();
        StartDialogueReveal();
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
            StartDialogueReveal();
            return;
        }

        nextButton.gameObject.SetActive(false);
        replayButton.gameObject.SetActive(true);
        songSelectButton.gameObject.SetActive(true);
    }


    // Dialogue is presented one letter at a time like a game conversation.
    // Clicking Next mid-line reveals that line immediately, without advancing
    // past information the player has not had a chance to read.
    private void StartDialogueReveal()
    {
        if (dialogueText == null) return;
        if (dialogueRevealRoutine != null)
        {
            StopCoroutine(dialogueRevealRoutine);
            dialogueRevealRoutine = null;
        }
        dialogueText.maxVisibleCharacters = 0;
        dialogueRevealRoutine = StartCoroutine(RevealDialogueRoutine());
    }

    private bool FinishCurrentDialogueReveal()
    {
        if (dialogueRevealRoutine == null) return false;
        StopCoroutine(dialogueRevealRoutine);
        dialogueRevealRoutine = null;
        if (dialogueText != null)
            dialogueText.maxVisibleCharacters = int.MaxValue;
        return true;
    }

    private IEnumerator RevealDialogueRoutine()
    {
        dialogueText.ForceMeshUpdate();
        int visible = 0;
        int total = dialogueText.textInfo.characterCount;
        float timer = 0f;
        while (visible < total)
        {
            timer += Time.unscaledDeltaTime;
            while (timer >= .028f && visible < total)
            {
                timer -= .028f;
                visible++;
                dialogueText.maxVisibleCharacters = visible;
            }
            yield return null;
        }
        dialogueText.maxVisibleCharacters = int.MaxValue;
        dialogueRevealRoutine = null;
    }

    private void UpdatePortrait()
    {
        if (conversationPortrait == null || speakerText == null) return;
        bool pinoSpeaking = speakerText.text == "피노";
        int expression = 1;
        if (completed)
        {
            if (pinoSpeaking) expression = 2;
            else
            {
                bool allPassed = true;
                foreach (bool step in passed) allPassed &= step;
                expression = allPassed ? 3 : 0;
            }
        }
        else if (checkpointOpen)
        {
            bool success = currentChapter > 0 && passed[currentChapter - 1];
            expression = pinoSpeaking ? (success ? 2 : 0) : (success ? 3 : 0);
            if (pinoSpeaking && !success && currentChapter == 1) expression = 4;
        }
        else if (introIndex >= 0 && introIndex < OpeningPortraits.Length)
            expression = OpeningPortraits[introIndex];

        var portraits = conversationTheme == null ? null :
            (pinoSpeaking ? conversationTheme.pinoDialoguePortraits : conversationTheme.luluDialoguePortraits);
        Sprite sprite = portraits != null && expression < portraits.Length ? portraits[expression] : null;
        if (sprite == null && portraits != null && portraits.Length > 1) sprite = portraits[1];
        conversationPortrait.sprite = sprite;
        conversationPortrait.gameObject.SetActive(sprite != null);
        // The portraits occupy the stage behind the dialogue, never its text.
        var rect = conversationPortrait.rectTransform;
        rect.anchorMin = new Vector2(pinoSpeaking ? .51f : .035f, 0f);
        rect.anchorMax = new Vector2(pinoSpeaking ? .965f : .49f, .94f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        if (conversationNameplate != null)
        {
            var image = conversationNameplate.GetComponent<Image>();
            if (image != null)
                image.color = pinoSpeaking ? new Color32(222,201,247,255) : new Color32(193,170,229,255);
        }
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
        dialogueHeading.text = "연습 결과  /  " +
            count + " - " + passed.Length + "단계 달성";
        speakerText.text = "루루";
        dialogueText.text = count == passed.Length
            ? Closing[0] :
              "…완벽하진 않았지만, 여기까지 해냈어. 다시 연습할 수도 있겠지.";
        UpdatePortrait();
        StartDialogueReveal();
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

    private void ShowLiveFeedback(string message, float duration)
    {
        feedbackMessage = message;
        feedbackExpires = Time.unscaledTime + duration;
        UpdateGuide();
    }

    private float ChapterProgress(int chapter)
    {
        if (chapter < 0 || chapter >= passed.Length) return 0f;
        if (passed[chapter] || IsSuccessful(chapter)) return 1f;
        if (chapter == 0) return Mathf.Clamp01(distanceMoved[0] / 110f);
        if (chapter <= 3) return Mathf.Clamp01(noteSuccesses[chapter]);
        if (chapter == 5)
            return Mathf.Min(Mathf.Clamp01(noteSuccesses[5] / 2f),
                Mathf.Clamp01(distanceMoved[5] / 60f));
        if (chartPlayer == null || chartPlayer.ActiveChartAsset == null)
            return Mathf.Clamp01(distanceMoved[4] / 100f);
        MouseModeChartData data = chartPlayer.ActiveChartAsset.data;
        float from = (float)MouseModeChartTiming.BeatToSeconds(data, 102f);
        float to = (float)MouseModeChartTiming.BeatToSeconds(data, 126f);
        float time = Mathf.InverseLerp(from, to, chartPlayer.SongElapsedSeconds);
        return Mathf.Min(time, Mathf.Clamp01(distanceMoved[4] / 100f));
    }

    private string ChapterProgressDetail(int chapter)
    {
        switch (chapter)
        {
            case 0: return "이동  " +
                Mathf.Min(110, Mathf.FloorToInt(distanceMoved[0])) + " / 110";
            case 1:
            case 2:
            case 3: return "성공  " + Mathf.Min(1, noteSuccesses[chapter]) + " / 1";
            case 4: return "회피 진행  " +
                Mathf.FloorToInt(ChapterProgress(chapter) * 100f) + "%";
            case 5: return "노트  " + Mathf.Min(2, noteSuccesses[5]) +
                " / 2   ·   이동  " +
                Mathf.Min(60, Mathf.FloorToInt(distanceMoved[5])) + " / 60";
            default: return "";
        }
    }

    private void UpdateGuide()
    {
        if (guideRoot == null) return;
        if (currentChapter < 0)
        {
            guideTitle.text = "훈련 준비";
            guideBody.text = "READY 신호 뒤에 마우스를 움직여 봐.";
            guideProgress.text = "01 / 06   ·   준비";
            if (guideProgressFill != null) guideProgressFill.fillAmount = 0f;
            if (guideIcon != null && guideIconChapter < 0)
            {
                Sprite ready = Resources.Load<Sprite>(
                    "RuruArt/Tutorial/Guide_MouseMove");
                guideIcon.sprite = ready;
                guideIcon.gameObject.SetActive(ready != null);
            }
            return;
        }

        bool good = passed[currentChapter] || IsSuccessful(currentChapter);
        guideTitle.text = good
            ? "완료  ·  " + ChapterNames[currentChapter]
            : ChapterNames[currentChapter];
        guideProgress.text = (currentChapter + 1).ToString("00") +
            " / 06   ·   " + (good ? "완료" : "연습 중");

        bool temporary = !good && !string.IsNullOrEmpty(feedbackMessage) &&
            Time.unscaledTime < feedbackExpires;
        guideBody.text = good
            ? ChapterSuccess[currentChapter]
            : temporary ? feedbackMessage
            : ChapterGuides[currentChapter] + "\n" +
                ChapterProgressDetail(currentChapter);
        if (guideProgressFill != null)
        {
            guideProgressFill.fillAmount = ChapterProgress(currentChapter);
            guideProgressFill.color = good ?
                RuruConsoleUI.Mint : RuruConsoleUI.Lilac;
        }
        if (guideIcon != null && guideIconChapter != currentChapter)
        {
            string[] icons = { "Guide_MouseMove", "Guide_LeftClick",
                "Guide_RightClick", "Guide_Wheel", "Guide_Dodge", "Guide_Mix" };
            Sprite iconSprite = Resources.Load<Sprite>(
                "RuruArt/Tutorial/" + icons[currentChapter]);
            guideIcon.sprite = iconSprite;
            guideIcon.gameObject.SetActive(iconSprite != null);
            RectTransform bodyRect = guideBody.rectTransform;
            bodyRect.anchorMin = new Vector2(iconSprite != null ? .34f : .075f, .14f);
            bodyRect.anchorMax = new Vector2(.93f, .48f);
            bodyRect.offsetMin = Vector2.zero;
            bodyRect.offsetMax = Vector2.zero;
            guideIconChapter = currentChapter;
        }
    }

    private void BuildDialogue()
    {
        conversationTheme = Resources.Load<RuruOriginalUITheme>("RuruOriginalUITheme");
        RectTransform screen = AddRect("TutorialConversation", transform,
            Vector2.zero, Vector2.one);
        dialogueOverlay = screen.gameObject;
        AddImage(screen, new Color(.075f, .055f, .105f, .57f), true);
        RectTransform stage = RuruConsoleUI.Stage(screen, "DialogueStage");

        // Original transparent character illustrations, not the gameplay cursor
        // or a flattened generated dialogue image.
        RectTransform portrait = AddRect("DialoguePortrait", stage,
            new Vector2(.035f, 0f), new Vector2(.49f, .94f));
        conversationPortrait = AddImage(portrait, Color.white, false);
        conversationPortrait.preserveAspect = true;
        conversationPortrait.gameObject.SetActive(false);

        RectTransform heading = AddRect("SceneLabel", stage,
            new Vector2(.045f, .935f), new Vector2(.955f, .978f));
        dialogueHeading = AddLabel(heading, 24f, RuruConsoleUI.Paper,
            TextAlignmentOptions.MidlineLeft);

        RectTransform panel = AddRect("DialoguePanel", stage,
            new Vector2(.055f, .055f), new Vector2(.945f, .305f));
        var surface = AddImage(panel, new Color(.13f,.095f,.20f,.96f), true);
        surface.sprite = RuruConsoleUI.Shell;
        surface.type = Image.Type.Sliced;
        surface.color = new Color(.86f,.84f,.92f,.98f);
        RuruConsoleUI.Strip(panel,"TopAccent",.035f,.962f,.965f,.973f,
            new Color32(208,185,239,255));

        conversationNameplate = AddRect("Nameplate", panel,
            new Vector2(.028f,.91f), new Vector2(.196f,1.16f));
        var nameArt = AddImage(conversationNameplate, Color.white, false);
        nameArt.sprite=RuruConsoleUI.MainKey;
        nameArt.type=Image.Type.Sliced;
        RectTransform speaker = AddRect("Speaker", conversationNameplate,
            new Vector2(.12f,.10f), new Vector2(.88f,.90f));
        speakerText = AddLabel(speaker, 32f, RuruConsoleUI.Ink,
            TextAlignmentOptions.Center);

        RectTransform body = AddRect("Dialogue", panel,
            new Vector2(.04f,.25f), new Vector2(.96f,.86f));
        dialogueText = AddLabel(body, 34f, RuruConsoleUI.Paper,
            TextAlignmentOptions.TopLeft);
        dialogueText.enableWordWrapping = true;
        dialogueText.overflowMode = TextOverflowModes.Overflow;

        nextButton = CreateButton("NextDialogue", panel,
            new Vector2(.79f,.055f), new Vector2(.965f,.235f),
            "다음", NextDialogue, out dialogueButtonLabel);
        replayButton = CreateButton("RetryTutorial", panel,
            new Vector2(.60f,.055f), new Vector2(.78f,.235f),
            "다시 연습", RepeatTutorial, out _);
        songSelectButton = CreateButton("LeaveTutorial", panel,
            new Vector2(.79f,.055f), new Vector2(.965f,.235f),
            "곡 선택", GoToSongSelect, out _);

        screen.SetAsLastSibling();
    }

    private void BuildGuide()
    {
        // The guidance is placed to the right of the existing play field.
        // It must not intercept mouse movement, clicks or wheel input.
        RectTransform root = AddRect("TutorialGuide", transform,
            new Vector2(.715f, .17f), new Vector2(.969f, .405f));
        guideRoot = root.gameObject;
        Image guideBackground = AddImage(root,
            new Color(.14f, .11f, .19f, .93f), false);
        Sprite panelArt = Resources.Load<Sprite>("RuruArt/UI/TrainingCard");
        if(panelArt==null) panelArt=RuruConsoleUI.Shell;
        if (panelArt != null)
        {
            guideBackground.sprite = panelArt;
            guideBackground.type = HasBorders(panelArt)
                ? Image.Type.Sliced : Image.Type.Simple;
            guideBackground.color = Color.white;
        }
        RectTransform iconArea = AddRect("ControlIcon", root,
            new Vector2(.055f, .10f), new Vector2(.33f, .50f));
        guideIcon = AddImage(iconArea, Color.white, false);
        guideIcon.preserveAspect = true;

        RectTransform number = AddRect("ChapterIndex", root,
            new Vector2(.075f, .79f), new Vector2(.925f, .96f));
        guideProgress = AddLabel(number, 24f,
            RuruConsoleUI.Ink,
            TextAlignmentOptions.MidlineLeft);

        RectTransform title = AddRect("ChapterTitle", root,
            new Vector2(.075f, .55f), new Vector2(.925f, .75f));
        guideTitle = AddLabel(title, 26f,
            RuruConsoleUI.Paper,
            TextAlignmentOptions.MidlineLeft);

        RectTransform body = AddRect("ChapterDescription", root,
            new Vector2(.34f, .10f), new Vector2(.93f, .48f));
        guideBody = AddLabel(body, 22f,
            RuruConsoleUI.Paper,
            TextAlignmentOptions.MidlineLeft);
        guideBody.enableWordWrapping = true;

        // A live, non-interactive progress meter: the player sees the goal
        // advancing without another blocking tutorial popup.
        RectTransform track = AddRect("LessonProgressTrack", root,
            new Vector2(.355f, .065f), new Vector2(.925f, .105f));
        guideProgressTrack = AddImage(track, new Color(.31f,.24f,.41f,1f), false);
        RectTransform fill = AddRect("LessonProgressFill", track,
            Vector2.zero, Vector2.one);
        guideProgressFill = AddImage(fill, RuruConsoleUI.Lilac, false);
        guideProgressFill.type = Image.Type.Filled;
        guideProgressFill.fillMethod = Image.FillMethod.Horizontal;
        guideProgressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        guideProgressFill.fillAmount = 0f;
        root.SetAsLastSibling();
    }

    private static bool HasBorders(Sprite sprite)
    {
        if (sprite == null) return false;
        Vector4 border = sprite.border;
        return border.x > 0f || border.y > 0f ||
               border.z > 0f || border.w > 0f;
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
        label.enableAutoSizing = true;
        label.fontSizeMin = size * .72f;
        label.fontSizeMax = size;
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
        bool primary = name=="NextDialogue" || name=="LeaveTutorial";
        Sprite buttonArt = primary?RuruConsoleUI.MainKey:RuruConsoleUI.AltKey;
        if (buttonArt != null)
        {
            image.sprite = buttonArt;
            image.type = HasBorders(buttonArt)
                ? Image.Type.Sliced : Image.Type.Simple;
            image.color = Color.white;
        }
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(.95f,.91f,1f,1f);
        colors.pressedColor = new Color(.82f,.76f,.92f,1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(.67f,.64f,.70f,.45f);
        button.colors = colors;
        button.onClick.AddListener(action);
        RectTransform textRect = AddRect("Label", rect,
            new Vector2(.02f, .02f), new Vector2(.98f, .98f));
        captionLabel = AddLabel(textRect, 22f,
            primary?RuruConsoleUI.Ink:RuruConsoleUI.Paper,
            TextAlignmentOptions.Center);
        captionLabel.text = caption;
        return button;
    }
}
