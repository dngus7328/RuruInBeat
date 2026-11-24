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
    private RuruStorySequence authoredOpening;
    private RuruStoryStage authoredStage;
    private bool HasAuthoredOpening => authoredOpening != null && authoredOpening.Count > 0;
    private int OpeningCount => HasAuthoredOpening ? authoredOpening.Count : Opening.Length;
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
        authoredOpening = Resources.Load<RuruStorySequence>("Story/TutorialOpening");
        if (HasAuthoredOpening)
            authoredStage = RuruStoryStage.Create((RectTransform)dialogueOverlay.transform);
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
        if (HasAuthoredOpening)
        {
            RuruStorySequence.Line line = authoredOpening.lines[introIndex];
            speakerText.text = line != null ? line.speaker : "";
            dialogueText.text = line != null ? line.dialogue : "";
            dialogueHeading.text = authoredOpening.sceneTitle;
            dialogueHeading.gameObject.SetActive(!string.IsNullOrEmpty(authoredOpening.sceneTitle));
            if (authoredStage != null) authoredStage.Show(authoredOpening, introIndex);
            conversationPortrait.gameObject.SetActive(false);
        }
        else
        {
            speakerText.text = Speaker[introIndex];
            dialogueText.text = Opening[introIndex];
            dialogueHeading.text = "게임 속 이상 현상  /  " +
                (introIndex + 1).ToString("00") + " - " +
                Opening.Length.ToString("00");
            UpdatePortrait();
        }
        dialogueButtonLabel.text =
            introIndex == OpeningCount - 1 ? "연습 시작" : "다음";
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

        if (introIndex + 1 < OpeningCount)
        {
            introIndex++;
            ShowOpeningLine();
            return;
        }

        if (authoredStage != null) authoredStage.Clear();
        dialogueHeading.gameObject.SetActive(true);
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
                new Color(.30f,.62f,.54f,1f) : new Color(.48f,.34f,.67f,1f);
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
        RectTransform screen = AddRect("TutorialConversation", transform,
            Vector2.zero, Vector2.one);
        dialogueOverlay = screen.gameObject;
        AddImage(screen, new Color(.065f, .052f, .085f, .94f), true);

        RectTransform panel = AddRect("DialoguePanel", screen,
            new Vector2(.16f, .10f), new Vector2(.84f, .38f));
        Image dialoguePanelImage = AddImage(panel,
            new Color(.18f, .14f, .24f, 1f), true);
        RuruOriginalUITheme originalUI = Resources.Load<RuruOriginalUITheme>("RuruOriginalUITheme");
        Sprite dialogueArt = originalUI != null && originalUI.confirmationPanel != null
            ? originalUI.confirmationPanel
            : Resources.Load<Sprite>("RuruArt/UI/UI_Panel");
        if (dialogueArt != null)
        {
            dialoguePanelImage.sprite = dialogueArt;
            dialoguePanelImage.type = HasBorders(dialogueArt)
                ? Image.Type.Sliced : Image.Type.Simple;
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
        speakerText.fontStyle = FontStyles.Bold;

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
        RuruOriginalUITheme originalUI = Resources.Load<RuruOriginalUITheme>("RuruOriginalUITheme");
        Sprite panelArt = originalUI != null && originalUI.confirmationPanel != null
            ? originalUI.confirmationPanel
            : Resources.Load<Sprite>("RuruArt/UI/UI_Panel");
        if (panelArt != null)
        {
            guideBackground.sprite = panelArt;
            guideBackground.type = HasBorders(panelArt)
                ? Image.Type.Sliced : Image.Type.Simple;
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

        // A live, non-interactive progress meter: the player sees the goal
        // advancing without another blocking tutorial popup.
        RectTransform track = AddRect("LessonProgressTrack", root,
            new Vector2(.355f, .065f), new Vector2(.925f, .105f));
        guideProgressTrack = AddImage(track, new Color(.31f,.24f,.41f,1f), false);
        RectTransform fill = AddRect("LessonProgressFill", track,
            Vector2.zero, Vector2.one);
        guideProgressFill = AddImage(fill, new Color(.48f,.34f,.67f,1f), false);
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
        RuruOriginalUITheme originalUI = Resources.Load<RuruOriginalUITheme>("RuruOriginalUITheme");
        Sprite buttonArt = originalUI != null && originalUI.titleButton != null
            ? originalUI.titleButton
            : Resources.Load<Sprite>("RuruArt/UI/UI_Button");
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
            buttonArt != null ? new Color(.15f,.10f,.22f,1f) : Color.white,
            TextAlignmentOptions.Center);
        captionLabel.text = caption;
        return button;
    }
}
