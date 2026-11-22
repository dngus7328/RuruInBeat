using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class TitleMenuController : MonoBehaviour
{
    [Header("메인 메뉴 버튼")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;

    [Header("설정 창")]
    [SerializeField] private GameObject settingsPanel;

    [Header("나가기 확인 창")]
    [SerializeField] private ExitConfirmController exitConfirmController;

    [Header("씬")]
    [SerializeField] private string songSelectSceneName = "SongSelectScene";

    private Button[] menuButtons;

    private int currentIndex = 0;
    private bool settingsOpened = false;
    private bool inputEnabled = true;

    private void Awake()
    {
        menuButtons = new Button[]
        {
            startButton,
            settingsButton,
            exitButton
        };

        if (startButton != null)
        {
            startButton.onClick.AddListener(
                StartGame
            );
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(
                OpenSettings
            );
        }

        if (exitButton != null)
        {
            exitButton.onClick.AddListener(
                OpenExitConfirm
            );
        }

        for (int i = 0; i < menuButtons.Length; i++)
        {
            AddPointerEnterEvent(
                menuButtons[i],
                i
            );
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    private void Start()
    {
        currentIndex = 0;

        if (inputEnabled)
        {
            SelectCurrentButton();
        }
    }

    private void Update()
    {
        if (!inputEnabled)
        {
            return;
        }

        if (settingsOpened)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseSettings();
            }

            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            MoveSelection(-1);
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            MoveSelection(1);
        }

        if (
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter)
        )
        {
            PressCurrentButton();
        }
    }

    private void MoveSelection(int direction)
    {
        if (
            menuButtons == null ||
            menuButtons.Length == 0
        )
        {
            return;
        }

        currentIndex += direction;

        if (currentIndex < 0)
        {
            currentIndex =
                menuButtons.Length - 1;
        }
        else if (
            currentIndex >=
            menuButtons.Length
        )
        {
            currentIndex = 0;
        }

        SelectCurrentButton();
    }

    private void SelectCurrentButton()
    {
        if (!inputEnabled)
        {
            return;
        }

        if (
            menuButtons == null ||
            menuButtons.Length == 0
        )
        {
            return;
        }

        if (
            currentIndex < 0 ||
            currentIndex >= menuButtons.Length
        )
        {
            return;
        }

        Button button =
            menuButtons[currentIndex];

        if (button == null)
        {
            return;
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(
                button.gameObject
            );
        }
    }

    private void PressCurrentButton()
    {
        if (!inputEnabled)
        {
            return;
        }

        if (
            currentIndex < 0 ||
            currentIndex >= menuButtons.Length
        )
        {
            return;
        }

        Button button =
            menuButtons[currentIndex];

        if (
            button == null ||
            !button.interactable
        )
        {
            return;
        }

        button.onClick.Invoke();
    }

    private void AddPointerEnterEvent(
        Button button,
        int index
    )
    {
        if (button == null)
        {
            return;
        }

        EventTrigger eventTrigger =
            button.GetComponent<EventTrigger>();

        if (eventTrigger == null)
        {
            eventTrigger =
                button.gameObject
                    .AddComponent<EventTrigger>();
        }

        if (eventTrigger.triggers == null)
        {
            eventTrigger.triggers =
                new List<EventTrigger.Entry>();
        }

        EventTrigger.Entry entry =
            new EventTrigger.Entry
            {
                eventID =
                    EventTriggerType.PointerEnter
            };

        entry.callback.AddListener(
            (data) =>
            {
                if (
                    !inputEnabled ||
                    settingsOpened
                )
                {
                    return;
                }

                currentIndex =
                    index;

                SelectCurrentButton();
            }
        );

        eventTrigger.triggers.Add(
            entry
        );
    }

    public void SetInputEnabled(
        bool enabled
    )
    {
        inputEnabled =
            enabled;

        if (!enabled)
        {
            if (EventSystem.current != null)
            {
                EventSystem.current
                    .SetSelectedGameObject(null);
            }
        }
    }

    public void SelectStartButton()
    {
        currentIndex = 0;

        SelectCurrentButton();
    }

    public void StartGame()
    {
        if (!inputEnabled)
        {
            return;
        }

        if (
            string.IsNullOrEmpty(
                songSelectSceneName
            )
        )
        {
            Debug.LogWarning(
                "Song Select 씬 이름이 설정되지 않았습니다."
            );

            return;
        }

        // First entry is the dedicated, playable tutorial. Once completed,
        // the same Start button opens the normal song list.
        SaveManager save = SaveManager.Instance;
        bool needsTutorial = save == null || save.Data == null ||
            !save.Data.tutorialCompleted;
        string destination = needsTutorial ? "Tutorial" : songSelectSceneName;
        if (SceneLoadingTransition.Load(destination))
            SetInputEnabled(false);
    }

    public void OpenSettings()
    {
        if (!inputEnabled)
        {
            return;
        }

        if (settingsPanel == null)
        {
            Debug.Log(
                "Settings Panel이 연결되지 않았습니다."
            );

            return;
        }

        settingsOpened = true;

        settingsPanel.SetActive(
            true
        );

        if (EventSystem.current != null)
        {
            EventSystem.current
                .SetSelectedGameObject(null);
        }
    }

    public void CloseSettings()
    {
        if (settingsPanel == null)
        {
            return;
        }

        settingsOpened = false;

        settingsPanel.SetActive(
            false
        );

        currentIndex = 1;

        SelectCurrentButton();
    }

    // =========================================================
    // Exit
    // =========================================================

    public void OpenExitConfirm()
    {
        if (!inputEnabled)
        {
            return;
        }

        if (exitConfirmController == null)
        {
            Debug.LogWarning(
                "ExitConfirmController가 연결되지 않았습니다."
            );

            return;
        }

        // 뒤의 타이틀 메뉴 입력 정지
        SetInputEnabled(false);

        exitConfirmController
            .OpenConfirmFromTitle(this);
    }

    public void RestoreAfterExitCancel()
    {
        inputEnabled = true;

        currentIndex = 2;

        SelectCurrentButton();
    }
}