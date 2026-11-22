using System.Collections;
using System.Collections.Generic;
using LuluInBeat.MouseMode.ChartSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pause and resume for the RHYTHM scene (no Pino required).
/// Put this component on GameplayCanvas/PlayArea.
/// Reuse the existing PausePanel; its frame does not move or fade.
/// Only foreground buttons and text animate.
/// </summary>
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class MouseRhythmPauseUI : MonoBehaviour
{
    [Header("기존 게임 UI")]
    [SerializeField] private GameObject pausePanel;
    [Tooltip("기존 PlayArea/NoteLayer. 비워두면 자동으로 찾습니다.")]
    [SerializeField] private RectTransform noteLayer;

    // Separate gate from MouseBootSequenceUI: its GO unlock depends on Pino
    // in older scenes, while RHYTHM deliberately has no Pino.
    private Text initialCountdownText;
    private bool sawInitialGo;
    private bool initialGoFinished;

    [Header("ESC / 창 포커스")]
    [SerializeField] private bool handleEscape = true;
    [SerializeField] private bool pauseOnFocusLost = true;

    [Header("일시정지 메뉴 연출 - 프레임은 고정")]
    [SerializeField, Range(0.08f, 0.8f)] private float pauseEnterSeconds = 0.26f;
    [SerializeField, Range(0.08f, 0.8f)] private float pauseExitSeconds = 0.18f;
    [SerializeField, Range(0f, 60f)] private float pauseRisePixels = 12f;
    [SerializeField, Range(0.85f, 1f)] private float pauseStartScale = 0.96f;
    [Tooltip("선택 사항: 일시정지 창 안쪽 버튼/글자 영역. 비워도 됩니다.")]
    [SerializeField] private RectTransform pauseMenuContent;

    [Header("재개 3-2-1-GO!")]
    [SerializeField] private TMP_FontAsset resumeFont;
    [SerializeField, Range(36f, 160f)] private float resumeFontSize = 105f;
    [SerializeField, Range(0.3f, 2f)] private float countStepSeconds = 0.85f;
    [SerializeField, Range(0.1f, 1.5f)] private float goHoldSeconds = 0.45f;
    [SerializeField] private Color resumeTextColor = new Color(0.94f, 0.91f, 1f, 1f);

    private enum PauseState { None, Manual, FocusLost, ResumeCountdown }
    private PauseState state;
    private bool focused;
    private bool ownsGlobalPause;
    private float savedTimeScale = 1f;
    private bool savedAudioPause;

    private sealed class AnimatedItem
    {
        public RectTransform rect;
        public CanvasGroup group;
        public Vector2 position;
        public Vector3 scale;
        public float alpha;
        public bool movePosition;
    }

    private readonly List<AnimatedItem> items = new List<AnimatedItem>();
    private CanvasGroup panelGroup;
    private float panelOriginalAlpha = 1f;
    private bool panelOriginalInteractable = true;
    private bool panelOriginalBlocksRaycasts = true;
    private float animationProgress;
    private bool panelWantsToShow;
    private Coroutine resumeRoutine;
    private GameObject resumeVisual;
    private RectTransform resumeRect;
    private TextMeshProUGUI resumeText;
    private CanvasGroup resumeGroup;

    public bool IsPaused { get { return state != PauseState.None; } }

    private bool CanOpenPauseMenu()
    {
        // The first GO! must have appeared AND naturally disappeared.
        // This gate works without Pino and without editing the boot script.
        return initialGoFinished;
    }

    private void Awake()
    {
        if (noteLayer == null)
            noteLayer = transform.Find("NoteLayer") as RectTransform;

        if (pausePanel == null)
        {
            // Include inactive objects. Restrict the lookup to this scene.
            RectTransform[] rects = FindObjectsOfType<RectTransform>(true);
            foreach (RectTransform rect in rects)
            {
                if (rect != null && rect.name == "PausePanel" &&
                    rect.gameObject.scene == gameObject.scene)
                {
                    pausePanel = rect.gameObject;
                    break;
                }
            }
        }

        if (noteLayer == null || pausePanel == null)
        {
            Debug.LogError("[MouseRhythmPause] PlayArea/NoteLayer와 PausePanel 연결을 확인해 주세요.", this);
            enabled = false;
            return;
        }

        BuildSharedArtPauseMenuIfEmpty();
        PreparePanelAnimation();
        HidePanelImmediately();
    }

    private void Start()
    {
        focused = Application.isFocused;
    }

    private void Update()
    {
        UpdateInitialGoGate();

        if (focused != Application.isFocused)
            HandleFocus(Application.isFocused);

        if (handleEscape && Application.isFocused && CanOpenPauseMenu() &&
            Input.GetKeyDown(KeyCode.Escape))
        {
            if (state == PauseState.None) PauseGame();
            else if (state == PauseState.Manual) ResumeGame();
        }
    }

    private void UpdateInitialGoGate()
    {
        if (initialGoFinished || noteLayer == null)
            return;

        if (initialCountdownText == null)
        {
            Transform target = noteLayer.Find("StartCountdown");
            if (target == null)
                return;

            initialCountdownText = target.GetComponent<Text>();
            if (initialCountdownText == null)
                return;
        }

        if (initialCountdownText.gameObject.activeSelf)
        {
            string cue = initialCountdownText.text.Trim();
            if ((cue == "GO" || cue == "GO!") &&
                Application.isFocused && Time.timeScale > 0f &&
                !AudioListener.pause)
            {
                sawInitialGo = true;
            }
        }
        else if (sawInitialGo)
        {
            initialGoFinished = true;
        }
    }

    private void LateUpdate()
    {
        UpdatePanelAnimation();
    }

    /// <summary>Connect the existing pause button here, if any.</summary>
    public void PauseGame()
    {
        if (!isActiveAndEnabled || !CanOpenPauseMenu() || state != PauseState.None)
            return;
        BeginPause(PauseState.Manual);
    }

    /// <summary>Connect the PausePanel resume button to this method.</summary>
    public void ResumeGame()
    {
        if (state != PauseState.Manual && state != PauseState.FocusLost)
            return;

        // During initial READY/3/2/1/GO, continue the original countdown,
        // instead of starting a second countdown on top of it.
        if (!CanOpenPauseMenu())
        {
            FinishPauseImmediately();
            return;
        }

        state = PauseState.ResumeCountdown;
        panelWantsToShow = false;
        resumeRoutine = StartCoroutine(ResumeAfterPanelExit());
    }

    private void BeginPause(PauseState reason)
    {
        if (state != PauseState.None)
            return;
        if (reason == PauseState.Manual && !CanOpenPauseMenu())
            return;

        savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        savedAudioPause = AudioListener.pause;
        ownsGlobalPause = true;
        state = reason;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        panelWantsToShow = CanOpenPauseMenu();
        if (panelWantsToShow && pausePanel != null)
            pausePanel.SetActive(true);
    }

    private void HandleFocus(bool hasFocus)
    {
        if (focused == hasFocus)
            return;
        focused = hasFocus;

        if (!hasFocus)
        {
            if (state == PauseState.None && pauseOnFocusLost)
                BeginPause(PauseState.FocusLost);
        }
        else if (state == PauseState.FocusLost)
        {
            ResumeGame();
        }
        // While a resume countdown is visible, its coroutine waits for focus
        // and restarts from 3 after focus returns.
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (isActiveAndEnabled)
            HandleFocus(hasFocus && Application.isFocused);
    }

    private IEnumerator ResumeAfterPanelExit()
    {
        while (state == PauseState.ResumeCountdown &&
               pausePanel != null && pausePanel.activeSelf)
            yield return null;

        if (state != PauseState.ResumeCountdown)
        {
            resumeRoutine = null;
            yield break;
        }

        EnsureResumeVisual();
        if (resumeVisual == null)
        {
            resumeRoutine = null;
            FinishPauseImmediately();
            yield break;
        }

        resumeVisual.SetActive(true);
        string[] labels = { "3", "2", "1", "GO!" };
        int index = 0;
        float elapsed = 0f;
        bool hadFocusLoss = false;

        while (state == PauseState.ResumeCountdown && index < labels.Length)
        {
            if (!Application.isFocused)
            {
                hadFocusLoss = true;
                resumeGroup.alpha = 0f;
                yield return null;
                continue;
            }

            if (hadFocusLoss)
            {
                index = 0;
                elapsed = 0f;
                hadFocusLoss = false;
            }

            float duration = index == 3 ? goHoldSeconds : countStepSeconds;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            resumeText.text = labels[index];
            float pop = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.22f), 3f);
            resumeRect.localScale = Vector3.one * Mathf.Lerp(1.16f, 1f, pop);
            float fadeIn = Mathf.Clamp01(t / 0.10f);
            float fadeOut = 1f - Mathf.Clamp01((t - (index == 3 ? 0.65f : 0.80f)) /
                                                   (index == 3 ? 0.35f : 0.20f));
            resumeGroup.alpha = fadeIn * fadeOut;

            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= duration)
            {
                elapsed = 0f;
                index++;
            }
            yield return null;
        }

        // If focus was lost exactly at the last frame, restart the countdown.
        if (state == PauseState.ResumeCountdown && !Application.isFocused)
        {
            resumeGroup.alpha = 0f;
            while (!Application.isFocused)
                yield return null;
            resumeRoutine = StartCoroutine(ResumeAfterFocusRestart());
            yield break;
        }

        resumeRoutine = null;
        if (state == PauseState.ResumeCountdown)
            FinishPauseImmediately();
    }

    private IEnumerator ResumeAfterFocusRestart()
    {
        if (resumeVisual != null) resumeVisual.SetActive(false);
        resumeRoutine = null;
        // Retain pause, then start 3-2-1-GO again with the panel already hidden.
        if (state == PauseState.ResumeCountdown)
            resumeRoutine = StartCoroutine(ResumeAfterPanelExit());
        yield break;
    }

    private void FinishPauseImmediately()
    {
        if (resumeRoutine != null)
        {
            StopCoroutine(resumeRoutine);
            resumeRoutine = null;
        }
        if (resumeVisual != null) resumeVisual.SetActive(false);
        state = PauseState.None;
        panelWantsToShow = false;

        if (ownsGlobalPause)
        {
            Time.timeScale = savedTimeScale;
            AudioListener.pause = savedAudioPause;
            ownsGlobalPause = false;
        }
        HidePanelImmediately();
    }

    private void EnsureResumeVisual()
    {
        if (resumeVisual != null) return;

        resumeVisual = new GameObject("RhythmResumeCountdown_Runtime",
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(TextMeshProUGUI), typeof(CanvasGroup));
        resumeRect = resumeVisual.GetComponent<RectTransform>();
        resumeRect.SetParent(transform, false);
        resumeRect.anchorMin = new Vector2(0.5f, 0.5f);
        resumeRect.anchorMax = new Vector2(0.5f, 0.5f);
        resumeRect.pivot = new Vector2(0.5f, 0.5f);
        resumeRect.anchoredPosition = Vector2.zero;
        resumeRect.sizeDelta = new Vector2(480f, 156f);
        resumeRect.SetAsLastSibling();

        resumeText = resumeVisual.GetComponent<TextMeshProUGUI>();
        if (resumeFont != null) resumeText.font = resumeFont;
        resumeText.fontSize = resumeFontSize;
        resumeText.fontStyle = FontStyles.Bold;
        resumeText.alignment = TextAlignmentOptions.Center;
        resumeText.enableWordWrapping = false;
        resumeText.raycastTarget = false;
        resumeText.color = resumeTextColor;
        resumeText.outlineColor = new Color(0.13f, 0.10f, 0.21f, 0.96f);
        resumeText.outlineWidth = 0.08f;

        resumeGroup = resumeVisual.GetComponent<CanvasGroup>();
        resumeGroup.alpha = 0f;
        resumeGroup.interactable = false;
        resumeGroup.blocksRaycasts = false;
        resumeVisual.SetActive(false);
    }


    // The current Mouse scene's PausePanel is only a full-screen dimmer.
    // Place a compact, clickable menu inside it. ESC remains the primary input.
    private void BuildSharedArtPauseMenuIfEmpty()
    {
        if (pausePanel == null || pausePanel.transform.childCount != 0)
            return;
        Sprite panelSprite = Resources.Load<Sprite>("RuruArt/UI/UI_Panel");
        Sprite buttonSprite = Resources.Load<Sprite>("RuruArt/UI/UI_Button");
        if (panelSprite == null || buttonSprite == null) return;

        GameObject container = new GameObject("PauseArtMenu",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform area = container.GetComponent<RectTransform>();
        area.SetParent(pausePanel.transform, false);
        area.anchorMin = new Vector2(.30f,.34f);
        area.anchorMax = new Vector2(.70f,.66f);
        area.offsetMin = area.offsetMax = Vector2.zero;
        Image frame=container.GetComponent<Image>();
        frame.sprite=panelSprite;
        frame.type=Image.Type.Sliced;
        frame.color=Color.white;
        frame.raycastTarget=false;

        GameObject labelObject=new GameObject("PauseHeading",
            typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));
        RectTransform title=labelObject.GetComponent<RectTransform>();
        title.SetParent(area,false);
        title.anchorMin=new Vector2(.08f,.57f);
        title.anchorMax=new Vector2(.92f,.87f);
        title.offsetMin=title.offsetMax=Vector2.zero;
        TextMeshProUGUI heading=labelObject.GetComponent<TextMeshProUGUI>();
        heading.font=TMP_Settings.defaultFontAsset;
        heading.text="PAUSED";
        heading.fontSize=48f;
        heading.color=new Color(.15f,.10f,.22f,1f);
        heading.alignment=TextAlignmentOptions.Center;
        heading.raycastTarget=false;

        // Short control reminder instead of an empty pause window. The
        // instruction is read-only and never steals clicks from the game.
        GameObject helpObject=new GameObject("PauseControlHint",
            typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));
        RectTransform helpRect=helpObject.GetComponent<RectTransform>();
        helpRect.SetParent(area,false);
        helpRect.anchorMin=new Vector2(.09f,.43f);
        helpRect.anchorMax=new Vector2(.91f,.62f);
        helpRect.offsetMin=helpRect.offsetMax=Vector2.zero;
        TextMeshProUGUI help=helpObject.GetComponent<TextMeshProUGUI>();
        help.font=TMP_Settings.defaultFontAsset;
        help.text="잠깐 쉬어 가자.\\n마우스 이동  ·  클릭 / 휠로 연주";
        help.fontSize=22f;
        help.color=new Color(.25f,.18f,.36f,1f);
        help.alignment=TextAlignmentOptions.Center;
        help.enableWordWrapping=true;
        help.raycastTarget=false;

        GameObject buttonObject=new GameObject("ResumeFromPause",
            typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),
            typeof(Button));
        RectTransform rect=buttonObject.GetComponent<RectTransform>();
        rect.SetParent(area,false);
        rect.anchorMin=new Vector2(.28f,.18f);
        rect.anchorMax=new Vector2(.72f,.42f);
        rect.offsetMin=rect.offsetMax=Vector2.zero;
        Image buttonImage=buttonObject.GetComponent<Image>();
        buttonImage.sprite=buttonSprite;
        buttonImage.type=Image.Type.Sliced;
        buttonImage.color=Color.white;
        Button button=buttonObject.GetComponent<Button>();
        button.targetGraphic=buttonImage;
        button.navigation=new Navigation{mode=Navigation.Mode.None};
        button.onClick.AddListener(ResumeGame);

        GameObject textObject=new GameObject("ResumeLabel",
            typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));
        RectTransform textRect=textObject.GetComponent<RectTransform>();
        textRect.SetParent(rect,false);
        textRect.anchorMin=Vector2.zero;
        textRect.anchorMax=Vector2.one;
        textRect.offsetMin=textRect.offsetMax=Vector2.zero;
        TextMeshProUGUI text=textObject.GetComponent<TextMeshProUGUI>();
        text.font=TMP_Settings.defaultFontAsset;
        text.text="계속";
        text.fontSize=27f;
        text.color=new Color(.15f,.10f,.22f,1f);
        text.alignment=TextAlignmentOptions.Center;
        text.raycastTarget=false;
    }

    private void PreparePanelAnimation()
    {
        panelGroup = pausePanel.GetComponent<CanvasGroup>();
        if (panelGroup == null)
            panelGroup = pausePanel.AddComponent<CanvasGroup>();
        panelOriginalAlpha = panelGroup.alpha;
        panelOriginalInteractable = panelGroup.interactable;
        panelOriginalBlocksRaycasts = panelGroup.blocksRaycasts;

        RectTransform scope = pauseMenuContent;
        if (scope == null || !scope.IsChildOf(pausePanel.transform))
            scope = pausePanel.transform as RectTransform;
        if (scope == null) return;

        foreach (Button button in scope.GetComponentsInChildren<Button>(true))
            RegisterItem(button.transform as RectTransform);
        foreach (TextMeshProUGUI label in scope.GetComponentsInChildren<TextMeshProUGUI>(true))
            if (label.GetComponentInParent<Button>() == null)
                RegisterItem(label.transform as RectTransform);
        foreach (Text label in scope.GetComponentsInChildren<Text>(true))
            if (label.GetComponentInParent<Button>() == null)
                RegisterItem(label.transform as RectTransform);
    }

    private void RegisterItem(RectTransform rect)
    {
        if (rect == null || rect.gameObject == pausePanel) return;
        foreach (AnimatedItem item in items)
            if (item.rect == rect) return;

        CanvasGroup group = rect.GetComponent<CanvasGroup>();
        if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();
        RectTransform parentRect = rect.parent as RectTransform;
        bool canMove = parentRect != null && parentRect.GetComponent<LayoutGroup>() == null;
        items.Add(new AnimatedItem
        {
            rect = rect,
            group = group,
            position = rect.anchoredPosition,
            scale = rect.localScale,
            alpha = group.alpha,
            movePosition = canMove
        });
    }

    private void UpdatePanelAnimation()
    {
        if (panelGroup == null) return;
        float target = panelWantsToShow ? 1f : 0f;
        float duration = panelWantsToShow ? pauseEnterSeconds : pauseExitSeconds;
        animationProgress = Mathf.MoveTowards(animationProgress, target,
            Time.unscaledDeltaTime / Mathf.Max(0.01f, duration));
        ApplyPanelAnimation();
        if (!panelWantsToShow && animationProgress <= 0f && pausePanel.activeSelf)
            pausePanel.SetActive(false);
    }

    private void ApplyPanelAnimation()
    {
        if (panelGroup == null) return;
        float t = Mathf.Clamp01(animationProgress);
        float smooth = t * t * (3f - 2f * t);
        // Keep the whole window/frame fixed at all times.
        panelGroup.alpha = panelOriginalAlpha;
        bool canClick = panelWantsToShow && t >= 0.99f;
        panelGroup.interactable = panelOriginalInteractable && canClick;
        panelGroup.blocksRaycasts = panelOriginalBlocksRaycasts && canClick;

        foreach (AnimatedItem item in items)
        {
            if (item.rect == null || item.group == null) continue;
            item.group.alpha = item.alpha * smooth;
            item.rect.localScale = item.scale * Mathf.Lerp(pauseStartScale, 1f, smooth);
            if (item.movePosition)
                item.rect.anchoredPosition = item.position +
                    Vector2.down * (pauseRisePixels * (1f - smooth));
        }
    }

    private void HidePanelImmediately()
    {
        if (pausePanel == null) return;
        panelWantsToShow = false;
        animationProgress = 0f;
        if (panelGroup != null)
        {
            panelGroup.alpha = panelOriginalAlpha;
            panelGroup.interactable = panelOriginalInteractable;
            panelGroup.blocksRaycasts = panelOriginalBlocksRaycasts;
        }
        foreach (AnimatedItem item in items)
        {
            if (item.rect != null)
            {
                item.rect.anchoredPosition = item.position;
                item.rect.localScale = item.scale;
            }
            if (item.group != null) item.group.alpha = item.alpha;
        }
        pausePanel.SetActive(false);
    }

    private void OnDisable()
    {
        if (ownsGlobalPause) FinishPauseImmediately();
    }

    private void OnDestroy()
    {
        if (ownsGlobalPause) FinishPauseImmediately();
        if (resumeVisual != null) Destroy(resumeVisual);
    }
}
