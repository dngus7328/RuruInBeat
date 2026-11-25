using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ExitConfirmController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    [Header("Popup")]
    [SerializeField] private GameObject confirmPanel;
    [SerializeField] private TMP_Text messageText;

    private TitleMenuController titleMenuController;

    private void Awake()
    {
        if (yesButton != null)
        {
            yesButton.onClick.AddListener(
                ConfirmExit
            );
        }

        if (noButton != null)
        {
            noButton.onClick.AddListener(
                CloseConfirm
            );
        }

        if (confirmPanel != null)
        {
            StylePopup();
            confirmPanel.SetActive(false);
        }
    }

    private void StylePopup()
    {
        var root=confirmPanel.transform;
        // Keep the original listeners, modal and serialization. Replace only the face of the dialog.
        foreach(Transform child in root)
        {
            if(child.name!="Image")continue;
            var rt=child as RectTransform;
            var image=child.GetComponent<Image>();
            if(rt==null||image==null)continue;
            if(rt.anchorMin==rt.anchorMax && rt.sizeDelta.x>400f)
            {
                var art=Resources.Load<Sprite>("RuruArt/UI/DialogueSurface");
                if(art!=null){image.sprite=art;image.type=Image.Type.Simple;image.color=Color.white;}
            }
        }
        if(messageText!=null){messageText.color=RuruConsoleUI.Ink;messageText.font=RuruConsoleUI.Font();messageText.enableAutoSizing=true;messageText.fontSizeMin=20;messageText.fontSizeMax=36;}
        StyleChoice(yesButton,false);StyleChoice(noButton,true);
    }

    private static void StyleChoice(Button button,bool primary)
    {
        if(button==null)return;
        var image=button.targetGraphic as Image;
        if(image!=null){image.sprite=primary?RuruConsoleUI.MainKey:RuruConsoleUI.AltKey;image.type=Image.Type.Sliced;image.color=Color.white;}
        foreach(var label in button.GetComponentsInChildren<TMP_Text>(true))
        {label.color=primary?RuruConsoleUI.Ink:RuruConsoleUI.Paper;label.font=RuruConsoleUI.Font();label.enableAutoSizing=true;label.fontSizeMin=18;label.fontSizeMax=32;}
        button.transition=Selectable.Transition.ColorTint;
    }

    private void OnDestroy()
    {
        if (yesButton != null)
        {
            yesButton.onClick.RemoveListener(
                ConfirmExit
            );
        }

        if (noButton != null)
        {
            noButton.onClick.RemoveListener(
                CloseConfirm
            );
        }
    }

    private void Update()
    {
        if (
            confirmPanel == null ||
            !confirmPanel.activeSelf
        )
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseConfirm();
        }
    }

    // =========================================================
    // Open
    // =========================================================

    public void OpenConfirmFromTitle(
        TitleMenuController controller
    )
    {
        titleMenuController =
            controller;

        if (messageText != null)
        {
            messageText.text =
                "정말 게임을 종료하시겠습니까?";
        }

        if (confirmPanel != null)
        {
            confirmPanel.SetActive(true);
        }

        // 기본 선택 = 아니오
        if (
            EventSystem.current != null &&
            noButton != null
        )
        {
            EventSystem.current.SetSelectedGameObject(
                noButton.gameObject
            );
        }
    }

    // =========================================================
    // Close
    // =========================================================

    private void CloseConfirm()
    {
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }

        if (titleMenuController != null)
        {
            titleMenuController
                .RestoreAfterExitCancel();
        }

        titleMenuController = null;
    }

    // =========================================================
    // Exit
    // =========================================================

    private void ConfirmExit()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveNow();
        }

#if UNITY_EDITOR

        EditorApplication.isPlaying =
            false;

#else

        Application.Quit();

#endif
    }
}