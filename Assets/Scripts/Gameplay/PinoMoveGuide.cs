using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class PinoMoveGuide : MonoBehaviour
{
    [Header("Pino Controller")]
    [SerializeField]
    private PinoController pinoController;

    [Header("Input Action Names")]
    [SerializeField]
    private string leftActionName = "Pino_Left";

    [SerializeField]
    private string rightActionName = "Pino_Right";

    [SerializeField]
    private int bindingIndex = 0;

    // =========================================================
    // Key Box
    // =========================================================

    [Header("Key Box Sprites")]
    [SerializeField]
    private SpriteRenderer leftKeyBox;

    [SerializeField]
    private SpriteRenderer rightKeyBox;

    // =========================================================
    // Key Text
    // =========================================================

    [Header("Key Text")]
    [SerializeField]
    private TMP_Text leftKeyText;

    [SerializeField]
    private TMP_Text rightKeyText;

    // =========================================================
    // Press Feedback
    // =========================================================

    [Header("Press Feedback")]
    [SerializeField]
    private GuideKeyPressFeedback leftPressFeedback;

    [SerializeField]
    private GuideKeyPressFeedback rightPressFeedback;

    // =========================================================
    // Colors
    // =========================================================

    [Header("Normal Colors")]
    [SerializeField]
    private Color normalBoxColor =
        new Color(
            0.82f,
            0.72f,
            0.96f,
            1f
        );

    [SerializeField]
    private Color normalTextColor =
        Color.white;

    [Header("Blocked Colors")]
    [SerializeField]
    private Color blockedBoxColor =
        new Color(
            0.40f,
            0.36f,
            0.50f,
            0.45f
        );

    [SerializeField]
    private Color blockedTextColor =
        new Color(
            0.60f,
            0.58f,
            0.68f,
            0.55f
        );

    [Header("Pressed Colors")]
    [SerializeField]
    private Color pressedBoxColor =
        new Color(
            1f,
            0.90f,
            1f,
            1f
        );

    [SerializeField]
    private Color pressedTextColor =
        Color.white;

    [Header("Input Flash")]
    [SerializeField]
    private float flashDuration =
        0.10f;

    private float leftFlashTimer;
    private float rightFlashTimer;

    private InputAction leftAction;
    private InputAction rightAction;

    private bool inputsSubscribed;

    // =========================================================
    // Unity
    // =========================================================

    private void Start()
    {
        ConnectInputs();

        RefreshKeyTexts();

        InputBindingManager.BindingsChanged +=
            OnBindingsChanged;
    }

    private void OnEnable()
    {
        if (InputBindingManager.Instance != null)
        {
            ConnectInputs();
            RefreshKeyTexts();
        }
    }

    private void OnDisable()
    {
        DisconnectInputs();

        if (leftPressFeedback != null)
        {
            leftPressFeedback.ForceReset();
        }

        if (rightPressFeedback != null)
        {
            rightPressFeedback.ForceReset();
        }
    }

    private void OnDestroy()
    {
        DisconnectInputs();

        InputBindingManager.BindingsChanged -=
            OnBindingsChanged;
    }

    private void Update()
    {
        UpdateTimers();

        UpdateBlockedFeedback();

        UpdateVisuals();
    }

    // =========================================================
    // Binding Changed
    // =========================================================

    private void OnBindingsChanged()
    {
        DisconnectInputs();

        ConnectInputs();

        RefreshKeyTexts();
    }

    // =========================================================
    // Input
    // =========================================================

    private void ConnectInputs()
    {
        if (inputsSubscribed)
            return;

        if (InputBindingManager.Instance == null)
            return;

        leftAction =
            InputBindingManager.Instance.GetAction(
                leftActionName
            );

        rightAction =
            InputBindingManager.Instance.GetAction(
                rightActionName
            );

        if (leftAction != null)
        {
            leftAction.performed +=
                OnLeftPressed;

            leftAction.Enable();
        }

        if (rightAction != null)
        {
            rightAction.performed +=
                OnRightPressed;

            rightAction.Enable();
        }

        inputsSubscribed = true;
    }

    private void DisconnectInputs()
    {
        if (!inputsSubscribed)
            return;

        if (leftAction != null)
        {
            leftAction.performed -=
                OnLeftPressed;
        }

        if (rightAction != null)
        {
            rightAction.performed -=
                OnRightPressed;
        }

        inputsSubscribed = false;
    }

    private void OnLeftPressed(
        InputAction.CallbackContext context
    )
    {
        if (pinoController == null)
            return;

        leftFlashTimer =
            flashDuration;

        if (leftPressFeedback != null)
        {
            leftPressFeedback.PlayPress();
        }
    }

    private void OnRightPressed(
        InputAction.CallbackContext context
    )
    {
        if (pinoController == null)
            return;

        rightFlashTimer =
            flashDuration;

        if (rightPressFeedback != null)
        {
            rightPressFeedback.PlayPress();
        }
    }

    // =========================================================
    // Key Text
    // =========================================================

    private void RefreshKeyTexts()
    {
        if (InputBindingManager.Instance == null)
            return;

        InputAction currentLeft =
            InputBindingManager.Instance.GetAction(
                leftActionName
            );

        InputAction currentRight =
            InputBindingManager.Instance.GetAction(
                rightActionName
            );

        if (
            leftKeyText != null &&
            currentLeft != null &&
            bindingIndex >= 0 &&
            bindingIndex < currentLeft.bindings.Count
        )
        {
            leftKeyText.text =
                currentLeft
                    .GetBindingDisplayString(
                        bindingIndex
                    )
                    .ToUpper();
        }

        if (
            rightKeyText != null &&
            currentRight != null &&
            bindingIndex >= 0 &&
            bindingIndex < currentRight.bindings.Count
        )
        {
            rightKeyText.text =
                currentRight
                    .GetBindingDisplayString(
                        bindingIndex
                    )
                    .ToUpper();
        }
    }

    // =========================================================
    // Movement State
    // =========================================================

    private bool CanMoveLeft()
    {
        if (pinoController == null)
            return false;

        return
            pinoController.CanMoveLeft;
    }

    private bool CanMoveRight()
    {
        if (pinoController == null)
            return false;

        return
            pinoController.CanMoveRight;
    }

    // =========================================================
    // Timer
    // =========================================================

    private void UpdateTimers()
    {
        if (leftFlashTimer > 0f)
        {
            leftFlashTimer -=
                Time.unscaledDeltaTime;

            if (leftFlashTimer < 0f)
            {
                leftFlashTimer = 0f;
            }
        }

        if (rightFlashTimer > 0f)
        {
            rightFlashTimer -=
                Time.unscaledDeltaTime;

            if (rightFlashTimer < 0f)
            {
                rightFlashTimer = 0f;
            }
        }
    }

    // =========================================================
    // Blocked Feedback
    // =========================================================

    private void UpdateBlockedFeedback()
    {
        bool leftBlocked =
            !CanMoveLeft();

        bool rightBlocked =
            !CanMoveRight();

        if (leftPressFeedback != null)
        {
            leftPressFeedback.SetBlocked(
                leftBlocked
            );
        }

        if (rightPressFeedback != null)
        {
            rightPressFeedback.SetBlocked(
                rightBlocked
            );
        }
    }

    // =========================================================
    // Visual
    // =========================================================

    private void UpdateVisuals()
    {
        UpdateLeftVisual();
        UpdateRightVisual();
    }

    private void UpdateLeftVisual()
    {
        if (!CanMoveLeft())
        {
            ApplyLeftVisual(
                blockedBoxColor,
                blockedTextColor
            );

            return;
        }

        if (leftFlashTimer > 0f)
        {
            ApplyLeftVisual(
                pressedBoxColor,
                pressedTextColor
            );

            return;
        }

        ApplyLeftVisual(
            normalBoxColor,
            normalTextColor
        );
    }

    private void UpdateRightVisual()
    {
        if (!CanMoveRight())
        {
            ApplyRightVisual(
                blockedBoxColor,
                blockedTextColor
            );

            return;
        }

        if (rightFlashTimer > 0f)
        {
            ApplyRightVisual(
                pressedBoxColor,
                pressedTextColor
            );

            return;
        }

        ApplyRightVisual(
            normalBoxColor,
            normalTextColor
        );
    }

    private void ApplyLeftVisual(
        Color boxColor,
        Color textColor
    )
    {
        if (leftKeyBox != null)
        {
            leftKeyBox.color =
                boxColor;
        }

        if (leftKeyText != null)
        {
            leftKeyText.color =
                textColor;
        }
    }

    private void ApplyRightVisual(
        Color boxColor,
        Color textColor
    )
    {
        if (rightKeyBox != null)
        {
            rightKeyBox.color =
                boxColor;
        }

        if (rightKeyText != null)
        {
            rightKeyText.color =
                textColor;
        }
    }
}