using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class PinoMoveGuide : MonoBehaviour
{
    [Header("Pino Controller")]
    [SerializeField]
    private PinoController pinoController;

    [Header("Input Actions")]
    [SerializeField]
    private InputActionReference leftAction;

    [SerializeField]
    private InputActionReference rightAction;

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

    // =========================================================
    // Unity
    // =========================================================

    private void OnEnable()
    {
        if (leftAction != null)
        {
            leftAction.action.performed +=
                OnLeftPressed;

            leftAction.action.Enable();
        }

        if (rightAction != null)
        {
            rightAction.action.performed +=
                OnRightPressed;

            rightAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (leftAction != null)
        {
            leftAction.action.performed -=
                OnLeftPressed;
        }

        if (rightAction != null)
        {
            rightAction.action.performed -=
                OnRightPressed;
        }

        if (leftPressFeedback != null)
        {
            leftPressFeedback.ForceReset();
        }

        if (rightPressFeedback != null)
        {
            rightPressFeedback.ForceReset();
        }
    }

    private void Update()
    {
        UpdateTimers();

        UpdateBlockedFeedback();

        UpdateVisuals();
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
    // Input
    // =========================================================

    private void OnLeftPressed(
        InputAction.CallbackContext context
    )
    {
        /*
         * PinoController도 같은 입력을 받는다.
         *
         * 여기서는 이동시키지 않고
         * UI 반응만 담당한다.
         */

        if (pinoController == null)
            return;

        /*
         * 입력 직후 Controller가 이미
         * CurrentSlotIndex를 갱신했을 수도 있으므로
         * '현재 CanMoveLeft'만으로
         * 눌림 효과를 막지 않는다.
         *
         * 끝 슬롯에서의 Block 상태는
         * Update에서 바로 반영된다.
         */
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