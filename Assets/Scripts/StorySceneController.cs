using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class StorySceneController : MonoBehaviour
{
    [Header("직접 씬을 실행할 때 재생할 스토리")]
    [SerializeField] private StoryData previewStory;
    [SerializeField] private string directExitScene = "SongSelectScene";
    [Header("스토리 화면 UI (Hierarchy에서 직접 수정 가능)")]
    [SerializeField] private Canvas storyCanvas;
    [SerializeField] private Image background, leftPortrait, rightPortrait;
    [SerializeField] private TMP_Text titleLabel, speakerLabel, bodyLabel, nextLabel;
    [SerializeField] private Button nextButton;
    [SerializeField] private AudioSource musicSource, soundSource;

    private StoryData story;
    private RuruOriginalUITheme theme;
    private int index, visible, total;
    private int leftExpression = 1, rightExpression = 1;
    private float elapsed;
    private bool leaving, finished;
    private bool routed;

    private void Start()
    {
        if (storyCanvas == null) BuildUI();
        if (background == null || leftPortrait == null || rightPortrait == null ||
            titleLabel == null || speakerLabel == null || bodyLabel == null ||
            nextLabel == null || nextButton == null)
        {
            Debug.LogError("[Story] UI 연결 누락. StorySceneController의 Inspector를 확인하세요.");
            StoryFlow.Leave(directExitScene);
            return;
        }
        theme = Resources.Load<RuruOriginalUITheme>("RuruOriginalUITheme");
        nextButton.onClick.AddListener(Next);
        routed = StoryFlow.HasRoute;
        Begin(routed ? StoryFlow.Current.story : previewStory);
    }

    private void OnDestroy()
    {
        if (nextButton != null) nextButton.onClick.RemoveListener(Next);
    }

    private void Begin(StoryData data)
    {
        story = data;
        index = 0;
        leftExpression = rightExpression = 1;
        finished = false;
        if (musicSource != null)
        {
            musicSource.Stop();
            musicSource.clip = story != null ? story.music : null;
            if (musicSource.clip != null) musicSource.Play();
        }
        if (soundSource != null) soundSource.Stop();
        if (story == null || !story.HasLines)
        {
            titleLabel.text = "STORY";
            speakerLabel.text = "";
            bodyLabel.text = "스토리 데이터가 비어 있습니다.";
            bodyLabel.maxVisibleCharacters = int.MaxValue;
            nextLabel.text = "돌아가기";
            leftPortrait.enabled = rightPortrait.enabled = false;
            background.sprite = null;
            background.color = RuruConsoleUI.Ink;
            total = visible = 0;
            finished = true;
            return;
        }
        SkipNullLines();
        ShowLine();
    }

    private void SkipNullLines()
    {
        while (index < story.lines.Length && story.lines[index] == null) index++;
    }

    private void ShowLine()
    {
        var line = story.lines[index];
        titleLabel.text = string.IsNullOrWhiteSpace(story.title) ? story.name : story.title;
        speakerLabel.text = line.speaker ?? "";
        bodyLabel.maxVisibleCharacters = int.MaxValue;
        bodyLabel.text = line.text ?? "";
        bodyLabel.ForceMeshUpdate();
        total = bodyLabel.textInfo.characterCount;
        elapsed = 0;
        visible = story.secondsPerCharacter <= 0 ? total : 0;
        bodyLabel.maxVisibleCharacters = visible;
        bool lulu = line.speaker == "루루", pino = line.speaker == "피노";
        if (line.expression >= 0)
        {
            if (lulu) leftExpression = line.expression;
            if (pino) rightExpression = line.expression;
        }
        SetPortrait(leftPortrait, line.leftPortrait, theme != null ? theme.luluDialoguePortraits : null,
            leftExpression, line.hideLeft, !pino);
        SetPortrait(rightPortrait, line.rightPortrait, theme != null ? theme.pinoDialoguePortraits : null,
            rightExpression, line.hideRight, !lulu);
        background.sprite = line.background != null ? line.background : story.background;
        background.color = background.sprite != null ? Color.white : RuruConsoleUI.Ink;
        bool more = false;
        for (int i = index + 1; i < story.lines.Length; i++)
            if (story.lines[i] != null) { more = true; break; }
        nextLabel.text = more ? "다음" : "마무리";
        if (soundSource != null)
        {
            soundSource.Stop();
            if (line.sound != null) soundSource.PlayOneShot(line.sound);
        }
    }

    public static Sprite Portrait(Sprite custom, Sprite[] common, int expression)
    {
        if (custom != null) return custom;
        if (common == null || common.Length == 0) return null;
        if (expression >= 0 && expression < common.Length && common[expression] != null)
            return common[expression];
        return common[Mathf.Min(1, common.Length - 1)];
    }

    private static void SetPortrait(Image image, Sprite custom, Sprite[] common, int expression,
        bool hide, bool active)
    {
        image.sprite = Portrait(custom, common, expression);
        image.enabled = !hide && image.sprite != null;
        image.color = active ? Color.white : new Color(.68f,.64f,.73f,1f);
    }

    private void Update()
    {
        if (leaving) return;
        var save = SaveManager.Instance;
        if (save != null && save.Data != null)
        {
            if (musicSource != null) musicSource.volume = Mathf.Clamp01(save.Data.masterVolume * save.Data.musicVolume);
            if (soundSource != null) soundSource.volume = Mathf.Clamp01(save.Data.masterVolume * save.Data.sfxVolume);
        }
        if (story == null || finished || visible >= total) return;
        elapsed += Time.unscaledDeltaTime;
        float step = Mathf.Max(.001f, story.secondsPerCharacter);
        int count = Mathf.Min(total - visible, Mathf.FloorToInt(elapsed / step));
        if (count == 0) return;
        elapsed -= count * step;
        visible += count;
        bodyLabel.maxVisibleCharacters = visible;
    }

    public void Next()
    {
        if (leaving || SceneLoadingTransition.IsLoading) return;
        if (visible < total)
        {
            visible = total;
            bodyLabel.maxVisibleCharacters = total;
            return;
        }
        if (!finished && story != null && story.HasLines)
        {
            index++;
            SkipNullLines();
            if (index < story.lines.Length) { ShowLine(); return; }
            finished = true;
            if (routed)
            {
                StoryData next = StoryFlow.FinishCurrent();
                if (next != null) { Begin(next); return; }
            }
        }
        leaving = StoryFlow.Leave(directExitScene);
    }

    // Also invoked by the editor command to create a persistent, editable hierarchy.
    public void BuildUI()
    {
        if (storyCanvas != null) return;
        var canvasObject = new GameObject("StoryCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvasObject, gameObject.scene);
        storyCanvas = canvasObject.GetComponent<Canvas>();
        storyCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080);
        scaler.matchWidthOrHeight = .5f;
        if (GetComponentInChildren<EventSystem>(true) == null)
        {
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            events.transform.SetParent(transform, false);
        }
        if (GetComponentInChildren<Camera>(true) == null)
        {
            var cameraObject = new GameObject("StoryCamera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RuruConsoleUI.Ink;
            camera.orthographic = true;
            camera.cullingMask = 0;
        }
        var stage = RuruConsoleUI.Stage(storyCanvas.transform, "StoryStage");
        background = RuruConsoleUI.Strip(stage,"Background",0,0,1,1,RuruConsoleUI.Ink);
        leftPortrait = RuruConsoleUI.Strip(stage,"LeftPortrait",.06f,.26f,.48f,.93f,Color.white);
        rightPortrait = RuruConsoleUI.Strip(stage,"RightPortrait",.52f,.26f,.94f,.93f,Color.white);
        leftPortrait.preserveAspect = rightPortrait.preserveAspect = true;
        leftPortrait.enabled = rightPortrait.enabled = false;
        titleLabel = RuruConsoleUI.Text(stage,"StoryTitle","STORY",.065f,.92f,.94f,.975f,28,RuruConsoleUI.Paper);
        var surface = Resources.Load<Sprite>("RuruArt/UI/StudioDialogue");
        var panel = RuruConsoleUI.Region(stage,"DialoguePanel",.045f,.065f,.955f,.31f);
        var panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.sprite = surface;
        panelImage.color = surface != null ? Color.white : RuruConsoleUI.Paper;
        panelImage.raycastTarget = false;
        var nameplate = RuruConsoleUI.Frame(panel,"Nameplate",.025f,.89f,.20f,1.15f,RuruConsoleUI.AltKey).rectTransform;
        speakerLabel = RuruConsoleUI.Text(nameplate,"Speaker","",.05f,.1f,.95f,.9f,32,RuruConsoleUI.Paper,TextAlignmentOptions.Center);
        bodyLabel = RuruConsoleUI.Text(panel,"Dialogue","",.035f,.26f,.965f,.86f,43,RuruConsoleUI.Ink,TextAlignmentOptions.TopLeft);
        bodyLabel.enableWordWrapping = true;
        nextButton = RuruConsoleUI.Key(panel,"NextDialogue","다음",.79f,.04f,.965f,.22f,true,()=>{});
        nextLabel = nextButton.GetComponentInChildren<TMP_Text>();
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        soundSource = gameObject.AddComponent<AudioSource>();
        soundSource.playOnAwake = false;
    }
}
