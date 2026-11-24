using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Scene-specific presentation polish for existing authored Unity screens.
/// Does not change songs, note judgement, save state, button callbacks,
/// input bindings, or the original RectTransform layouts.
/// </summary>
[DisallowMultipleComponent]
public sealed class RuruScenePresentation : MonoBehaviour
{
    private const string Title = "TitleScene";
    private const string Songs = "SongSelectScene";
    private const string Tutorial = "Tutorial";

    private string loadedScene;
    private SongSelectMenuUI songMenu;
    private SettingsCategoryController settings;
    private RectTransform selectedCard;
    private RectTransform selectedCover;
    private CanvasGroup coverGroup;
    private CanvasGroup cardGroup;
    private Vector3 selectedScale;
    private Vector3 coverScale;
    private int lastSong = -2;
    private int lastDifficulty = -2;
    private int lastSettings = -2;
    private float selectionAge = 10f;

    private readonly List<RectTransform> settingsPages =
        new List<RectTransform>(5);
    private readonly Dictionary<RectTransform, Vector2> pageRest =
        new Dictionary<RectTransform, Vector2>();

    private RectTransform conversation;
    private RectTransform dialoguePanel;
    private CanvasGroup dialogueGroup;
    private Vector3 dialogueScale;
    private TMP_Text speaker;
    private TMP_Text dialogue;
    private string dialogueKey;
    private float dialogueAge = 10f;

    private RectTransform luluPortrait;
    private RectTransform pinoPortrait;
    private CanvasGroup luluPortraitGroup;
    private CanvasGroup pinoPortraitGroup;
    private Vector2 luluPortraitPosition;
    private Vector2 pinoPortraitPosition;
    private RectTransform tutorialGuide;
    private CanvasGroup guideGroup;
    private Vector2 guideRest;
    private float guideTargetAlpha = 1f;
    private Canvas rootCanvas;
    private RectTransform rootCanvasRect;
    private readonly Vector3[] mouseCorners = new Vector3[4];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= Attach;
        SceneManager.sceneLoaded += Attach;
    }

    private static void Attach(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != Title && scene.name != Songs &&
            scene.name != Tutorial)
            return;

        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.GetComponent<RuruScenePresentation>() != null)
                return;

        GameObject visual = new GameObject("RuruScenePresentation");
        SceneManager.MoveGameObjectToScene(visual, scene);
        visual.AddComponent<RuruScenePresentation>().loadedScene = scene.name;
    }

    private IEnumerator Start()
    {
        // Existing controllers build some UI in Awake/Start.
        yield return null;
        yield return null;

        if (loadedScene == Songs)
            BindSongSelect();
        else if (loadedScene == Title)
            BindSettings();
        else if (loadedScene == Tutorial)
            BindTutorial();
    }

    private void BindSongSelect()
    {
        songMenu = FindAnySceneComponent<SongSelectMenuUI>();
        if (songMenu == null) return;
        selectedCard = FindRect(songMenu.transform, "SelectedTrackPanel");
        if (selectedCard == null) return;
        selectedScale = selectedCard.localScale;
        cardGroup = EnsureGroup(selectedCard);
        Transform playAction = FindChild(selectedCard, "PlaySelected");
        if (playAction != null)
        {
            Button play = playAction.GetComponent<Button>();
            if (play != null)
            {
                playAction.gameObject.SetActive(true);
                play.onClick.AddListener(songMenu.StartSelectedSongFromUI);
            }
        }
        Transform cover = FindChild(selectedCard, "JacketImage_Replaceable");
        if (cover != null)
        {
            selectedCover = cover as RectTransform;
            if (selectedCover != null)
            {
                coverScale = selectedCover.localScale;
                coverGroup = EnsureGroup(selectedCover);
            }
        }
        lastSong = songMenu.ArcadeSelectedIndex;
        lastDifficulty = songMenu.ArcadeDifficultyIndex;
        selectionAge = 0f;
    }

    private void BindSettings()
    {
        settings = FindAnySceneComponent<SettingsCategoryController>();
        if (settings == null) return;

        string[] names =
        {
            "GamePage", "SoundPage", "DisplayPage", "ControlPage", "EtcPage"
        };
        for (int i = 0; i < names.Length; i++)
        {
            RectTransform page = FindInScene(names[i]);
            if (page == null) continue;
            settingsPages.Add(page);
            pageRest[page] = page.anchoredPosition;
            EnsureGroup(page);
        }
        BuildMouseControlPage();
        lastSettings = settings.GetSelectedIndex();
        AnimateCurrentPage(true);
    }

    private void BuildMouseControlPage()
    {
        RectTransform controlPage = FindInScene("ControlPage");
        if (controlPage == null) return;
        if (FindChild(controlPage, "MouseControlsVisual") != null) return;

        string[] obsolete =
        {
            "Lane1KeyRow", "Lane2KeyRow", "Lane3KeyRow", "Lane4KeyRow",
            "PinoLeftKeyRow", "PinoRightKeyRow", "ResetControlsButton"
        };
        foreach (string name in obsolete)
        {
            Transform legacy = FindChild(controlPage, name);
            if (legacy != null) legacy.gameObject.SetActive(false);
        }

        GameObject holder = new GameObject("MouseControlsVisual",
            typeof(RectTransform));
        RectTransform host = holder.GetComponent<RectTransform>();
        host.SetParent(controlPage, false);
        host.anchorMin = new Vector2(.06f, .09f);
        host.anchorMax = new Vector2(.94f, .91f);
        host.offsetMin = Vector2.zero;
        host.offsetMax = Vector2.zero;

        TMP_Text existing = controlPage.GetComponentInChildren<TMP_Text>(true);
        TMP_FontAsset font = existing != null ?
            existing.font : TMP_Settings.defaultFontAsset;
        Sprite rowArt = Resources.Load<Sprite>("RuruArt/UI/UI_Panel");

        string[] titles =
        {
            "마우스 이동", "왼쪽 클릭", "오른쪽 클릭", "마우스 휠"
        };
        string[] descriptions =
        {
            "피노 이동", "L 노트 판정", "R 노트 판정",
            "W 노트 시작 및 유지"
        };
        string[] icons =
        {
            "Guide_MouseMove", "Guide_LeftClick",
            "Guide_RightClick", "Guide_Wheel"
        };

        for (int i = 0; i < titles.Length; i++)
        {
            float top = .98f - i * .245f;
            RectTransform row = NewRect("MouseAction_" + i, host,
                new Vector2(.02f, top - .215f), new Vector2(.98f, top));
            Image rowImage = row.gameObject.AddComponent<Image>();
            rowImage.color = rowArt != null ? Color.white :
                new Color(.17f,.13f,.23f,.94f);
            rowImage.raycastTarget = false;
            rowImage.sprite = rowArt;
            if (rowArt != null && rowArt.border.sqrMagnitude > 0f)
                rowImage.type = Image.Type.Sliced;

            RectTransform iconRect = NewRect("ActionIcon", row,
                new Vector2(.035f,.12f), new Vector2(.18f,.90f));
            Image iconImage = iconRect.gameObject.AddComponent<Image>();
            iconImage.sprite = Resources.Load<Sprite>(
                "RuruArt/Tutorial/" + icons[i]);
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            NewLabel("ActionTitle", row,
                new Vector2(.21f,.51f), new Vector2(.96f,.90f),
                titles[i], font, 26f, new Color(.20f,.14f,.30f,1f));
            NewLabel("ActionDescription", row,
                new Vector2(.21f,.11f), new Vector2(.96f,.53f),
                descriptions[i], font, 21f,
                new Color(.39f,.30f,.50f,1f));
        }
    }

    private static RectTransform NewRect(string name, Transform parent,
        Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static void NewLabel(string name, Transform parent,
        Vector2 min, Vector2 max, string content,
        TMP_FontAsset font, float fontSize, Color ink)
    {
        RectTransform rect = NewRect(name, parent, min, max);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = fontSize;
        label.color = ink;
        label.text = content;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
    }

    private void BindTutorial()
    {
        conversation = FindInScene("TutorialConversation");
        dialoguePanel = FindInScene("DialoguePanel");
        tutorialGuide = FindInScene("TutorialGuide");
        speaker = FindInScene("Speaker") != null ?
            FindInScene("Speaker").GetComponent<TMP_Text>() : null;
        dialogue = FindInScene("Dialogue") != null ?
            FindInScene("Dialogue").GetComponent<TMP_Text>() : null;

        if (dialoguePanel != null)
        {
            dialogueGroup = EnsureGroup(dialoguePanel);
            dialogueScale = dialoguePanel.localScale;
        }
        CorrectNarrativeArtReadability();
        BuildOriginalCharacterStage();

        if (tutorialGuide != null)
        {
            guideGroup = EnsureGroup(tutorialGuide);
            guideGroup.interactable = false;
            guideGroup.blocksRaycasts = false;
            guideRest = tutorialGuide.anchoredPosition;

            // Compact top-right guidance, replacing the mid-playfield
            // obstruction. Keep its current content, progress and callbacks.
            tutorialGuide.anchorMin = new Vector2(.735f, .765f);
            tutorialGuide.anchorMax = new Vector2(.985f, .925f);
            tutorialGuide.offsetMin = Vector2.zero;
            tutorialGuide.offsetMax = Vector2.zero;
            tutorialGuide.anchoredPosition = Vector2.zero;

            rootCanvas = tutorialGuide.GetComponentInParent<Canvas>();
            rootCanvasRect = rootCanvas != null ?
                rootCanvas.GetComponent<RectTransform>() : null;

            // None of the runtime-created tutorial UI can consume rhythm
            // clicks or pointer movement while gameplay is active.
            foreach (Graphic graphic in tutorialGuide.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }
        dialogueAge = 10f;
    }

    private void CorrectNarrativeArtReadability()
    {
        Sprite cleanPanel = Resources.Load<Sprite>("RuruArt/UI/UI_Panel");
        Sprite cleanButton = Resources.Load<Sprite>("RuruArt/UI/UI_Button");
        if (conversation != null)
        {
            Image dimmer = conversation.GetComponent<Image>();
            if (dimmer != null)
                dimmer.color = new Color(.07f, .052f, .10f, .72f);
        }

        // The legacy confirmation art has large internal transparent margins.
        // Used as a dialogue backing, it leaves dark text on transparent black.
        // Replace only the backing, not the original asset or text callbacks.
        if (dialoguePanel != null && cleanPanel != null)
        {
            Image panel = dialoguePanel.GetComponent<Image>();
            if (panel != null)
            {
                panel.sprite = cleanPanel;
                panel.type = Image.Type.Sliced;
                panel.color = Color.white;
            }
        }
        if (tutorialGuide != null && cleanPanel != null)
        {
            Image guideBacking = tutorialGuide.GetComponent<Image>();
            if (guideBacking != null)
            {
                guideBacking.sprite = cleanPanel;
                guideBacking.type = Image.Type.Sliced;
                guideBacking.color = Color.white;
            }
        }

        if (dialoguePanel == null || cleanButton == null) return;
        string[] actions = { "NextDialogue", "RetryTutorial", "LeaveTutorial" };
        foreach (string action in actions)
        {
            Transform buttonRoot = FindChild(dialoguePanel, action);
            if (buttonRoot == null) continue;
            Image backing = buttonRoot.GetComponent<Image>();
            if (backing != null)
            {
                backing.sprite = cleanButton;
                backing.type = Image.Type.Sliced;
                backing.color = Color.white;
            }
            TMP_Text label = buttonRoot.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.color = new Color(.17f, .11f, .27f, 1f);
        }
    }

    private void BuildOriginalCharacterStage()
    {
        if (conversation == null) return;
        Sprite lulu = Resources.Load<Sprite>(
            "RuruArt/Characters/Lulu_Master_Cutout");
        Sprite pino = Resources.Load<Sprite>(
            "RuruArt/Characters/Pino_Master_Cutout");
        // The original artist-supplied images are the only source. Never
        // substitute reconstructed or AI-generated characters.
        luluPortrait = CreateOriginalPortrait("OriginalLuluPortrait",
            conversation, lulu, new Vector2(.025f, .25f),
            new Vector2(.40f, .965f));
        pinoPortrait = CreateOriginalPortrait("OriginalPinoPortrait",
            conversation, pino, new Vector2(.635f, .27f),
            new Vector2(.975f, .96f));
        if (luluPortrait != null)
        {
            luluPortraitGroup = EnsureGroup(luluPortrait);
            luluPortraitPosition = luluPortrait.anchoredPosition;
        }
        if (pinoPortrait != null)
        {
            pinoPortraitGroup = EnsureGroup(pinoPortrait);
            pinoPortraitPosition = pinoPortrait.anchoredPosition;
            // The tutorial's small portrait stays in the layout but is not
            // drawn twice when the approved full-size cutout is available.
            RectTransform smallPortrait = FindInScene("PinoPortrait");
            if (smallPortrait != null)
            {
                Image smallImage = smallPortrait.GetComponent<Image>();
                if (smallImage != null) smallImage.enabled = false;
            }
        }
    }

    private static RectTransform CreateOriginalPortrait(string name,
        RectTransform root, Sprite sprite, Vector2 min, Vector2 max)
    {
        if (sprite == null) return null;
        GameObject go = new GameObject(name, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(root, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        // Child dialogue panels and their buttons render over portraits.
        rect.SetAsFirstSibling();
        return rect;
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
        if (loadedScene == Songs)
            UpdateSongs(dt);
        else if (loadedScene == Title)
            UpdateSettings(dt);
        else if (loadedScene == Tutorial)
            UpdateTutorial(dt);
    }

    private void UpdateSongs(float dt)
    {
        if (songMenu == null || selectedCard == null) return;

        int current = songMenu.ArcadeSelectedIndex;
        int difficulty = songMenu.ArcadeDifficultyIndex;
        if (current != lastSong || difficulty != lastDifficulty)
        {
            lastSong = current;
            lastDifficulty = difficulty;
            selectionAge = 0f;
        }

        selectionAge += dt;
        float t = EaseOut(Mathf.Clamp01(selectionAge / .22f));
        // Only subtle content response: do not overwrite the cartridge
        // shelf motion and never slide a cartridge onto the jacket.
        selectedCard.localScale =
            Vector3.LerpUnclamped(selectedScale * .985f, selectedScale, t);
        if (cardGroup != null)
            cardGroup.alpha = Mathf.Lerp(.76f, 1f, t);
        if (selectedCover != null)
        {
            selectedCover.localScale = Vector3.LerpUnclamped(
                coverScale * .97f, coverScale, t);
            if (coverGroup != null)
                coverGroup.alpha = Mathf.Lerp(.55f, 1f, t);
        }
    }

    private void UpdateSettings(float dt)
    {
        if (settings == null) return;

        int selected = settings.GetSelectedIndex();
        if (selected != lastSettings)
        {
            lastSettings = selected;
            AnimateCurrentPage(false);
        }

        for (int i = 0; i < settingsPages.Count; i++)
        {
            RectTransform page = settingsPages[i];
            if (page == null || !page.gameObject.activeInHierarchy)
                continue;
            CanvasGroup group = EnsureGroup(page);
            float speed = 1f - Mathf.Exp(-17f * dt);
            group.alpha = Mathf.Lerp(group.alpha, 1f, speed);
            page.anchoredPosition = Vector2.Lerp(
                page.anchoredPosition, pageRest[page], speed);
        }
    }

    private void AnimateCurrentPage(bool immediate)
    {
        if (settings == null) return;

        for (int i = 0; i < settingsPages.Count; i++)
        {
            RectTransform page = settingsPages[i];
            if (page == null) continue;
            page.anchoredPosition = pageRest[page];
            CanvasGroup group = EnsureGroup(page);
            bool show = page.gameObject.activeSelf;
            if (show && !immediate)
            {
                page.anchoredPosition += Vector2.right * 12f;
                group.alpha = .15f;
            }
            else
            {
                group.alpha = 1f;
            }
        }
    }

    private void UpdateTutorial(float dt)
    {
        if (dialoguePanel != null && dialogueGroup != null &&
            dialoguePanel.gameObject.activeInHierarchy &&
            speaker != null && dialogue != null)
        {
            string current = speaker.text + "|" + dialogue.text;
            if (current != dialogueKey)
            {
                dialogueKey = current;
                dialogueAge = 0f;
            }
            dialogueAge += dt;
            float t = EaseOut(Mathf.Clamp01(dialogueAge / .20f));
            dialoguePanel.localScale =
                Vector3.LerpUnclamped(dialogueScale * .975f, dialogueScale, t);
            dialogueGroup.alpha = Mathf.Lerp(.58f, 1f, t);
        }

        if (conversation != null && conversation.gameObject.activeInHierarchy)
        {
            bool luluSpeaking = speaker != null && speaker.text == "루루";
            float portraitBlend = 1f - Mathf.Exp(-10f * dt);
            if (luluPortraitGroup != null)
            {
                luluPortraitGroup.alpha = Mathf.Lerp(luluPortraitGroup.alpha,
                    luluSpeaking ? 1f : .50f, portraitBlend);
                luluPortrait.anchoredPosition = Vector2.Lerp(
                    luluPortrait.anchoredPosition,
                    luluPortraitPosition + Vector2.right * (luluSpeaking ? 9f : 0f), portraitBlend);
            }
            if (pinoPortraitGroup != null)
            {
                pinoPortraitGroup.alpha = Mathf.Lerp(pinoPortraitGroup.alpha,
                    luluSpeaking ? .50f : 1f, portraitBlend);
                pinoPortrait.anchoredPosition = Vector2.Lerp(
                    pinoPortrait.anchoredPosition,
                    pinoPortraitPosition + Vector2.left * (luluSpeaking ? 0f : 9f), portraitBlend);
            }
        }

        if (tutorialGuide == null || guideGroup == null ||
            !tutorialGuide.gameObject.activeInHierarchy)
            return;

        guideTargetAlpha = 1f;
        if (rootCanvasRect != null)
        {
            // Fade the hint out of the way while the cursor is nearby.
            Camera camera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : rootCanvas.worldCamera;
            bool overlaps = RectTransformUtility.RectangleContainsScreenPoint(
                tutorialGuide, Input.mousePosition, camera);
            guideTargetAlpha = overlaps ? .12f : 1f;
        }
        float blend = 1f - Mathf.Exp(-14f * dt);
        guideGroup.alpha = Mathf.Lerp(guideGroup.alpha, guideTargetAlpha, blend);
        guideGroup.interactable = false;
        guideGroup.blocksRaycasts = false;
    }

    private static float EaseOut(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private static CanvasGroup EnsureGroup(Component component)
    {
        CanvasGroup group = component.GetComponent<CanvasGroup>();
        if (group == null) group = component.gameObject.AddComponent<CanvasGroup>();
        return group;
    }

    private static Transform FindChild(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform result = FindChild(child, name);
            if (result != null) return result;
        }
        return null;
    }

    private static RectTransform FindRect(Transform root, string name)
    {
        return FindChild(root, name) as RectTransform;
    }

    private static T FindAnySceneComponent<T>() where T : Component
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null) return component;
        }
        return null;
    }

    private static RectTransform FindInScene(string name)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            RectTransform result = FindRect(root.transform, name);
            if (result != null) return result;
        }
        return null;
    }
}
