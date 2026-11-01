using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ConfirmButtonHighlight : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    [Header("Button Images")]
    [SerializeField] private Image yesImage;
    [SerializeField] private Image noImage;

    [Header("Colors")]
    [SerializeField]
    private Color normalColor =
        new Color(1f, 1f, 1f, 1f);

    [SerializeField]
    private Color hoverColor =
        new Color(1f, 1f, 1f, 1f);

    [SerializeField]
    private Color selectedColor =
        new Color(1f, 1f, 1f, 1f);

    [SerializeField]
    private Color pressedColor =
        new Color(1f, 1f, 1f, 1f);

    private bool yesHovered;
    private bool noHovered;

    private EventTrigger yesTrigger;
    private EventTrigger noTrigger;

    private void Awake()
    {
        SetupButton(
            yesButton,
            true
        );

        SetupButton(
            noButton,
            false
        );
    }

    private void OnEnable()
    {
        // 팝업 열릴 때 기본 선택 = 아니오
        if (noButton != null)
        {
            EventSystem.current?.SetSelectedGameObject(
                noButton.gameObject
            );
        }

        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    private void SetupButton(
        Button button,
        bool isYes
    )
    {
        if (button == null)
            return;

        EventTrigger trigger =
            button.GetComponent<EventTrigger>();

        if (trigger == null)
        {
            trigger =
                button.gameObject.AddComponent<EventTrigger>();
        }

        if (trigger.triggers == null)
        {
            trigger.triggers =
                new System.Collections.Generic.List<EventTrigger.Entry>();
        }

        AddEvent(
            trigger,
            EventTriggerType.PointerEnter,
            () =>
            {
                if (isYes)
                    yesHovered = true;
                else
                    noHovered = true;

                Refresh();
            }
        );

        AddEvent(
            trigger,
            EventTriggerType.PointerExit,
            () =>
            {
                if (isYes)
                    yesHovered = false;
                else
                    noHovered = false;

                Refresh();
            }
        );

        AddEvent(
            trigger,
            EventTriggerType.PointerDown,
            () =>
            {
                SetPressed(
                    isYes
                );
            }
        );

        AddEvent(
            trigger,
            EventTriggerType.PointerUp,
            () =>
            {
                Refresh();
            }
        );

        if (isYes)
            yesTrigger = trigger;
        else
            noTrigger = trigger;
    }

    private void AddEvent(
        EventTrigger trigger,
        EventTriggerType type,
        System.Action action
    )
    {
        EventTrigger.Entry entry =
            new EventTrigger.Entry();

        entry.eventID =
            type;

        entry.callback =
            new EventTrigger.TriggerEvent();

        entry.callback.AddListener(
            _ => action()
        );

        trigger.triggers.Add(
            entry
        );
    }

    private void Refresh()
    {
        GameObject selectedObject =
            EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;

        bool yesSelected =
            yesButton != null &&
            selectedObject ==
            yesButton.gameObject;

        bool noSelected =
            noButton != null &&
            selectedObject ==
            noButton.gameObject;

        if (yesImage != null)
        {
            if (yesHovered)
            {
                yesImage.color =
                    hoverColor;
            }
            else if (yesSelected)
            {
                yesImage.color =
                    selectedColor;
            }
            else
            {
                yesImage.color =
                    normalColor;
            }
        }

        if (noImage != null)
        {
            if (noHovered)
            {
                noImage.color =
                    hoverColor;
            }
            else if (noSelected)
            {
                noImage.color =
                    selectedColor;
            }
            else
            {
                noImage.color =
                    normalColor;
            }
        }
    }

    private void SetPressed(
        bool isYes
    )
    {
        if (isYes)
        {
            if (yesImage != null)
            {
                yesImage.color =
                    pressedColor;
            }
        }
        else
        {
            if (noImage != null)
            {
                noImage.color =
                    pressedColor;
            }
        }
    }
}