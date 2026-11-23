using System;
using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Scene art integration for the existing PC mouse-rhythm game.
/// Uses only independent transparent PNG layers and user-approved original
/// Lulu/Pino cutouts. Song/score/chart/control logic is not rewritten.
/// </summary>
[DisallowMultipleComponent]
public sealed class RuruVisualSceneComposer : MonoBehaviour
{
    private const string Songs = "SongSelectScene";
    private const string Title = "TitleScene";
    private const string Tutorial = "Tutorial";
    private static bool registered;
    private string sceneName;

    private SongSelectMenuUI songMenu;
    private Image songBackdrop;
    private Image infoArtwork;
    private readonly Image[] rowArt = new Image[6];
    private readonly Vector3[] rowBaseScale = new Vector3[6];
    private int previousSong = -99;
    private int previousDifficulty = -99;
    private float changeAge = 10f;

    private GameObject settingsPanel;
    private CanvasGroup settingsGroup;
    private bool settingsWasActive;
    private float settingsAge;

    private RectTransform conversation;
    private RectTransform dialoguePanel;
    private RectTransform tutorialGuide;
    private Image storyRoom;
    private Image luluPortrait;
    private Image pinoPortrait;
    private CanvasGroup luluGroup;
    private CanvasGroup pinoGroup;
    private CanvasGroup dialogueGroup;
    private TMP_Text speakerLabel;
    private TMP_Text mainDialogue;
    private string previousDialogueKey;
    private float dialogueAge = 10f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        registered = true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != Songs && scene.name != Title && scene.name != Tutorial)
            return;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.GetComponent<RuruVisualSceneComposer>() != null)
                return;

        GameObject go = new GameObject("RuruVisualSceneComposer");
        SceneManager.MoveGameObjectToScene(go, scene);
        RuruVisualSceneComposer visual = go.AddComponent<RuruVisualSceneComposer>();
        visual.sceneName = scene.name;
    }

    private IEnumerator Start()
    {
        // SongSelectBrowseUI and MouseTutorialController build control objects
        // during Awake/Start; bind only after those existing systems finish.
        yield return null;
        yield return null;
        if (sceneName == Songs) BuildSongSelect();
        if (sceneName == Title) BindSettings();
        if (sceneName == Tutorial) BuildTutorial();
    }

    private void BuildSongSelect()
    {
        songMenu = FindSceneComponent<SongSelectMenuUI>();
        if (songMenu == null) return;

        Transform background = FindScene("Background");
        Sprite bgArt = Art("SongSelect/Backdrop");
        if (background != null && bgArt != null)
        {
            songBackdrop = background.GetComponent<Image>();
            if (songBackdrop != null)
            {
                songBackdrop.sprite = bgArt;
                songBackdrop.type = Image.Type.Simple;
                songBackdrop.color = Color.white;
                songBackdrop.preserveAspect = false;
            }
        }
        RectTransform shelf = FindRect(songMenu.transform, "TrackListPanel");
        RectTransform info = FindRect(songMenu.transform, "SelectedTrackPanel");
        if (shelf != null)
            OverlayImage("Ruru_ShelfArt", shelf, Art("SongSelect/ShelfPanel"), false);
        if (info != null)
        {
            infoArtwork = OverlayImage("Ruru_InfoArt", info,
                Art("SongSelect/InfoPanel"), false);
            RectTransform cover = FindRect(info, "JacketImage_Replaceable");
            if (cover != null)
                OverlayImage("Ruru_JacketMount", cover,
                    Art("SongSelect/JacketMount"), true);
            SetupActualPlayButton(info);
        }

        for (int i = 0; i < 6; i++)
        {
            RectTransform card = shelf != null ?
                FindRect(shelf, "SongRow_" + (i+1).ToString("00")) : null;
            if (card == null) continue;
            rowArt[i] = OverlayImage("Ruru_TrackArtwork", card,
                Art("SongSelect/TrackSlot_Normal"), false);
            if (rowArt[i] != null)
                rowBaseScale[i] = rowArt[i].rectTransform.localScale;
        }
        previousSong = songMenu.ArcadeSelectedIndex;
        previousDifficulty = songMenu.ArcadeDifficultyIndex;
        changeAge = 0f;
        UpdateSongSelect(0f);
    }

    private void SetupActualPlayButton(RectTransform info)
    {
        RectTransform playRoot = FindRect(info, "PlaySelected");
        if (playRoot == null) return;
        Button play = playRoot.GetComponent<Button>();
        if (play == null) return;
        Sprite art = Art("SongSelect/ActionButton");
        Image background = playRoot.GetComponent<Image>();
        if (background != null && art != null)
        {
            background.sprite = art;
            background.type = Image.Type.Sliced;
            background.color = Color.white;
        }

        // The original scene contains the button but has it inactive.
        // The game's existing private PlaySelected path remains authoritative.
        MethodInfo launch = typeof(SongSelectMenuUI).GetMethod("PlaySelected",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (launch == null) return;
        play.onClick.AddListener(() =>
        {
            if (songMenu != null) launch.Invoke(songMenu, null);
        });
        playRoot.gameObject.SetActive(true);
        TMP_Text label = playRoot.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = "플레이";
            label.color = new Color(.97f,.92f,1f,1f);
        }
    }

    private void UpdateSongSelect(float dt)
    {
        if (songMenu == null) return;
        int selected = songMenu.ArcadeSelectedIndex;
        int difficulty = songMenu.ArcadeDifficultyIndex;
        if (previousSong != selected || previousDifficulty != difficulty)
        {
            previousSong = selected;
            previousDifficulty = difficulty;
            changeAge = 0f;
        }
        changeAge += dt;
        float t = EaseOut(Mathf.Clamp01(changeAge / .22f));
        Sprite idle = Art("SongSelect/TrackSlot_Normal");
        Sprite active = Art("SongSelect/TrackSlot_Active");
        for (int i = 0; i < rowArt.Length; i++)
        {
            Image img = rowArt[i];
            if (img == null) continue;
            bool isSelected = selected >= 0 && selected % 6 == i;
            img.sprite = isSelected && active != null ? active : idle;
            if (img.sprite == null) img.enabled = false;
            else img.enabled = true;
            Vector3 target = rowBaseScale[i] * (isSelected ? 1.012f : 1f);
            img.rectTransform.localScale = Vector3.Lerp(
                img.rectTransform.localScale, target,
                1f - Mathf.Exp(-15f * Mathf.Min(dt,.05f)));
        }
        if (infoArtwork != null)
            infoArtwork.color = new Color(1f,1f,1f,
                Mathf.Lerp(.83f,1f,t));
    }

    private void BindSettings()
    {
        Transform settings = FindScene("SettingsPanel");
        if (settings == null) return;
        settingsPanel = settings.gameObject;
        settingsGroup = settings.GetComponent<CanvasGroup>();
        if (settingsGroup == null)
            settingsGroup = settings.gameObject.AddComponent<CanvasGroup>();
        settingsWasActive = settingsPanel.activeInHierarchy;
        settingsAge = settingsWasActive ? 1f : 0f;
        // Existing TitleScene settings art, controls and animations stay intact.
    }

    private void UpdateSettings(float dt)
    {
        if (settingsPanel == null || settingsGroup == null) return;
        bool now = settingsPanel.activeInHierarchy;
        if (now && !settingsWasActive) settingsAge = 0f;
        settingsWasActive = now;
        if (!now) return;
        settingsAge += dt;
        settingsGroup.alpha = EaseOut(Mathf.Clamp01(settingsAge / .20f));
        // Input must not become clickable through an invisible panel.
        settingsGroup.blocksRaycasts = settingsAge >= .18f;
        settingsGroup.interactable = settingsAge >= .18f;
    }

    private void BuildTutorial()
    {
        conversation = FindScene("TutorialConversation") as RectTransform;
        dialoguePanel = FindScene("DialoguePanel") as RectTransform;
        tutorialGuide = FindScene("TutorialGuide") as RectTransform;
        if (conversation == null || dialoguePanel == null) return;

        Sprite room = Art("Story/LuluRoom_Background");
        storyRoom = OverlayImage("Ruru_RoomIllustration", conversation,room,false);
        Sprite panelSprite = Art("Tutorial/DialoguePanel");
        ApplyMainImage(dialoguePanel, panelSprite);
        dialogueGroup = EnsureGroup(dialoguePanel);

        speakerLabel = FindRect(dialoguePanel,"Speaker") != null ?
            FindRect(dialoguePanel,"Speaker").GetComponent<TMP_Text>() : null;
        mainDialogue = FindRect(dialoguePanel,"Dialogue") != null ?
            FindRect(dialoguePanel,"Dialogue").GetComponent<TMP_Text>() : null;
        if (speakerLabel != null)
        {
            speakerLabel.color = new Color(.98f,.94f,1f,1f);
            RectTransform nameTag = FullImage("Ruru_SpeakerTag", dialoguePanel,
                Art("Tutorial/SpeakerPlate"),
                new Vector2(.028f,.67f),new Vector2(.29f,.965f));
            if (nameTag != null) nameTag.SetAsFirstSibling();
        }
        if (mainDialogue != null)
            mainDialogue.color = new Color(.20f,.14f,.28f,1f);

        string[] buttons = {"NextDialogue", "RetryTutorial", "LeaveTutorial"};
        foreach (string buttonName in buttons)
        {
            RectTransform button = FindRect(dialoguePanel, buttonName);
            if (button == null) continue;
            ApplyMainImage(button, Art("Tutorial/DialogueButton"));
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.color = Color.white;
        }

        // Original confirmed character PNGs: no redrawing or new expressions.
        luluPortrait = Portrait("Ruru_Lulu",conversation,
            Art("Characters/Lulu_Master"),new Vector2(.06f,.30f),
            new Vector2(.40f,.94f));
        pinoPortrait = Portrait("Ruru_Pino",conversation,
            Art("Characters/Pino_Master"),new Vector2(.60f,.30f),
            new Vector2(.96f,.91f));
        if (luluPortrait != null) luluGroup = EnsureGroup(luluPortrait);
        if (pinoPortrait != null) pinoGroup = EnsureGroup(pinoPortrait);
        if (storyRoom != null) storyRoom.rectTransform.SetAsFirstSibling();
        if (luluPortrait != null) luluPortrait.rectTransform.SetSiblingIndex(1);
        if (pinoPortrait != null) pinoPortrait.rectTransform.SetSiblingIndex(2);
        // The dialogue stays above character feet and keeps its existing input.
        dialoguePanel.SetAsLastSibling();

        Transform oldPortrait = FindChild(dialoguePanel,"PinoPortrait");
        if (oldPortrait != null && pinoPortrait != null)
        {
            Image oldImage = oldPortrait.GetComponent<Image>();
            if (oldImage != null) oldImage.enabled = false;
        }
        if (tutorialGuide != null)
        {
            ApplyMainImage(tutorialGuide,Art("Tutorial/GuidePanel"));
            tutorialGuide.anchorMin = new Vector2(.735f,.77f);
            tutorialGuide.anchorMax = new Vector2(.985f,.945f);
            tutorialGuide.offsetMin = Vector2.zero;
            tutorialGuide.offsetMax = Vector2.zero;
            CanvasGroup group = EnsureGroup(tutorialGuide);
            group.interactable = false;
            group.blocksRaycasts = false;
            foreach (Graphic graphic in tutorialGuide.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }
        dialogueAge = 0f;
    }

    private void UpdateTutorial(float dt)
    {
        if (conversation == null || !conversation.gameObject.activeInHierarchy)
            return;
        bool isLulu = speakerLabel == null || speakerLabel.text == "루루";
        float k = 1f - Mathf.Exp(-14f * Mathf.Min(dt,.05f));
        if (luluGroup != null)
            luluGroup.alpha = Mathf.Lerp(luluGroup.alpha,isLulu?1f:.53f,k);
        if (pinoGroup != null)
            pinoGroup.alpha = Mathf.Lerp(pinoGroup.alpha,isLulu?.53f:1f,k);
        if (storyRoom != null)
        {
            // Existing checkpoint overlays show the playable field; narrative
            // opening gets its room only when the tutorial says "게임 속 이상 현상".
            TMP_Text heading = FindScene("SceneLabel") != null ?
                FindScene("SceneLabel").GetComponent<TMP_Text>() : null;
            bool prologue = heading != null &&
                heading.text.StartsWith("게임 속 이상 현상",StringComparison.Ordinal);
            float target = prologue ? .88f : .22f;
            Color color = storyRoom.color;
            color.a = Mathf.Lerp(color.a,target,k);
            storyRoom.color = color;
        }
        if (dialogueGroup != null && mainDialogue != null)
        {
            string key = (speakerLabel != null ? speakerLabel.text : "")+
                "|"+mainDialogue.text;
            if (key != previousDialogueKey)
            {
                previousDialogueKey = key;
                dialogueAge = 0f;
            }
            dialogueAge += dt;
            dialogueGroup.alpha = Mathf.Lerp(.63f,1f,
                EaseOut(Mathf.Clamp01(dialogueAge/.18f)));
        }
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
        if (sceneName == Songs) UpdateSongSelect(dt);
        else if (sceneName == Title) UpdateSettings(dt);
        else if (sceneName == Tutorial) UpdateTutorial(dt);
    }

    private static Sprite Art(string path)
    {
        return Resources.Load<Sprite>("RuruArt/Scenes/" + path);
    }

    private static float EaseOut(float t) { return 1f-Mathf.Pow(1f-t,3f); }

    private static CanvasGroup EnsureGroup(Component obj)
    {
        CanvasGroup g = obj.GetComponent<CanvasGroup>();
        return g != null ? g : obj.gameObject.AddComponent<CanvasGroup>();
    }

    private static void ApplyMainImage(RectTransform root, Sprite sprite)
    {
        if (root == null || sprite == null) return;
        Image img = root.GetComponent<Image>();
        if (img == null) img = root.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.type = sprite.border.sqrMagnitude > 0f ?
            Image.Type.Sliced : Image.Type.Simple;
        img.color = Color.white;
    }

    private static Image OverlayImage(string name, RectTransform parent,
        Sprite sprite, bool last)
    {
        if (parent == null || sprite == null) return null;
        Transform existing = parent.Find(name);
        RectTransform rect = existing != null ? existing as RectTransform : null;
        if (rect == null)
        {
            GameObject go = new GameObject(name,typeof(RectTransform),
                typeof(CanvasRenderer),typeof(Image));
            rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent,false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
        Image img = rect.GetComponent<Image>();
        img.sprite = sprite;
        img.type = sprite.border.sqrMagnitude > 0f ?
            Image.Type.Sliced : Image.Type.Simple;
        img.color = Color.white;
        img.raycastTarget = false;
        if (last) rect.SetAsLastSibling(); else rect.SetAsFirstSibling();
        return img;
    }

    private static RectTransform FullImage(string name,RectTransform parent,
        Sprite sprite,Vector2 min,Vector2 max)
    {
        if (parent == null || sprite == null) return null;
        GameObject go = new GameObject(name,typeof(RectTransform),
            typeof(CanvasRenderer),typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent,false);
        rect.anchorMin=min;rect.anchorMax=max;
        rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;
        Image image=go.GetComponent<Image>();
        image.sprite=sprite;
        image.type=sprite.border.sqrMagnitude>0f?
            Image.Type.Sliced:Image.Type.Simple;
        image.raycastTarget=false;
        return rect;
    }

    private static Image Portrait(string name,RectTransform parent,
        Sprite sprite,Vector2 min,Vector2 max)
    {
        RectTransform rect=FullImage(name,parent,sprite,min,max);
        if (rect==null) return null;
        Image img=rect.GetComponent<Image>();
        img.preserveAspect=true;
        return img;
    }

    private static Transform FindChild(Transform root,string name)
    {
        if(root==null)return null;
        if(root.name==name)return root;
        foreach(Transform child in root)
        {
            Transform result=FindChild(child,name);
            if(result!=null)return result;
        }
        return null;
    }
    private static RectTransform FindRect(Transform root,string name)
    {
        return FindChild(root,name) as RectTransform;
    }
    private static Transform FindScene(string name)
    {
        Scene scene=SceneManager.GetActiveScene();
        if(!scene.IsValid())return null;
        foreach(GameObject root in scene.GetRootGameObjects())
        {
            Transform result=FindChild(root.transform,name);
            if(result!=null)return result;
        }
        return null;
    }
    private static T FindSceneComponent<T>() where T:Component
    {
        Scene scene=SceneManager.GetActiveScene();
        if(!scene.IsValid())return null;
        foreach(GameObject root in scene.GetRootGameObjects())
        {
            T result=root.GetComponentInChildren<T>(true);
            if(result!=null)return result;
        }
        return null;
    }
}