using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PinoController : MonoBehaviour
{
    // =========================================================
    // Slots
    // =========================================================

    [Header("Slots")]
    [SerializeField]
    private Transform[] slots = new Transform[4];

    [Tooltip("0 = Slot_1, 1 = Slot_2, 2 = Slot_3, 3 = Slot_4")]
    [SerializeField]
    private int startSlotIndex = 1;

    // =========================================================
    // Input
    // =========================================================

    [Header("Input")]
    [SerializeField]
    private InputActionReference leftAction;

    [SerializeField]
    private InputActionReference rightAction;

    // =========================================================
    // Movement
    // =========================================================

    [Header("Movement")]
    [SerializeField]
    private float moveDuration = 0.12f;

    // =========================================================
    // Visual
    // =========================================================

    [Header("Visual")]
    [Tooltip("PinoVisual 오브젝트를 연결합니다.")]
    [SerializeField]
    private Transform visualRoot;

    [SerializeField]
    private float moveTiltAngle = 12f;

    // =========================================================
    // Runtime
    // =========================================================

    private int currentSlotIndex;

    private bool controlEnabled = true;
    private bool moving;

    private Coroutine moveRoutine;

    private Quaternion visualBaseRotation;

    // =========================================================
    // Public
    // =========================================================

    public int CurrentSlotIndex
    {
        get
        {
            return currentSlotIndex;
        }
    }

    public bool CanMoveLeft
    {
        get
        {
            return
                controlEnabled &&
                currentSlotIndex > 0;
        }
    }

    public bool CanMoveRight
    {
        get
        {
            return
                controlEnabled &&
                slots != null &&
                slots.Length > 0 &&
                currentSlotIndex < slots.Length - 1;
        }
    }

    public bool ControlEnabled
    {
        get
        {
            return controlEnabled;
        }
    }

    public bool IsMoving
    {
        get
        {
            return moving;
        }
    }

    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        if (visualRoot != null)
        {
            visualBaseRotation =
                visualRoot.localRotation;
        }
    }

    private void Start()
    {
        if (slots == null ||
            slots.Length == 0)
        {
            Debug.LogError(
                "PinoController: Slots가 연결되지 않았습니다."
            );

            return;
        }

        currentSlotIndex =
            Mathf.Clamp(
                startSlotIndex,
                0,
                slots.Length - 1
            );

        Transform startSlot =
            slots[currentSlotIndex];

        if (startSlot != null)
        {
            Vector3 position =
                transform.position;

            // 중요:
            // 슬롯의 X만 적용한다.
            // Y는 현재 Pino 위치 그대로 유지.
            position.x =
                startSlot.position.x;

            transform.position =
                position;
        }
    }

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
    }

    // =========================================================
    // Input
    // =========================================================

    private void OnLeftPressed(
        InputAction.CallbackContext context
    )
    {
        if (!CanMoveLeft)
            return;

        MoveToSlot(
            currentSlotIndex - 1
        );
    }

    private void OnRightPressed(
        InputAction.CallbackContext context
    )
    {
        if (!CanMoveRight)
            return;

        MoveToSlot(
            currentSlotIndex + 1
        );
    }

    // =========================================================
    // Move
    // =========================================================

    private void MoveToSlot(
        int targetIndex
    )
    {
        if (!controlEnabled)
            return;

        if (slots == null ||
            slots.Length == 0)
        {
            return;
        }

        targetIndex =
            Mathf.Clamp(
                targetIndex,
                0,
                slots.Length - 1
            );

        if (targetIndex ==
            currentSlotIndex)
        {
            return;
        }

        if (slots[targetIndex] == null)
            return;

        // =====================================================
        // 중요
        //
        // 입력 순간 슬롯 번호는 바로 변경한다.
        //
        // 그래서 MoveGuide는 피노가 실제로 도착하기 전에
        // 이동 가능 / 불가능 상태를 바로 반영할 수 있다.
        // =====================================================

        currentSlotIndex =
            targetIndex;

        if (moveRoutine != null)
        {
            StopCoroutine(
                moveRoutine
            );
        }

        moveRoutine =
            StartCoroutine(
                MoveRoutine(
                    targetIndex
                )
            );
    }

    private IEnumerator MoveRoutine(
        int targetIndex
    )
    {
        moving = true;

        Vector3 startPosition =
            transform.position;

        Vector3 targetPosition =
            startPosition;

        // =====================================================
        // X만 이동
        // =====================================================

        targetPosition.x =
            slots[targetIndex].position.x;

        // targetPosition.y는 건드리지 않는다.
        // 현재 Pino Y가 그대로 유지된다.

        float direction =
            Mathf.Sign(
                targetPosition.x -
                startPosition.x
            );

        float elapsed = 0f;

        float duration =
            Mathf.Max(
                0.001f,
                moveDuration
            );

        while (elapsed < duration)
        {
            elapsed +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    duration
                );

            // SmoothStep 형태
            float moveT =
                t * t *
                (3f - 2f * t);

            Vector3 position =
                Vector3.Lerp(
                    startPosition,
                    targetPosition,
                    moveT
                );

            // =================================================
            // 혹시 이동 중 다른 연출이 Y를 바꾸더라도
            // 여기서는 X 이동만 담당하도록 한다.
            // =================================================

            position.y =
                transform.position.y;

            transform.position =
                position;

            // =================================================
            // 이동 중 PinoVisual만 살짝 기울임
            // =================================================

            if (visualRoot != null)
            {
                float tiltWave =
                    Mathf.Sin(
                        t * Mathf.PI
                    );

                float angle =
                    -direction *
                    moveTiltAngle *
                    tiltWave;

                visualRoot.localRotation =
                    visualBaseRotation *
                    Quaternion.Euler(
                        0f,
                        0f,
                        angle
                    );
            }

            yield return null;
        }

        // =====================================================
        // 마지막에도 X만 정확히 맞춘다.
        // =====================================================

        Vector3 finalPosition =
            transform.position;

        finalPosition.x =
            slots[targetIndex].position.x;

        transform.position =
            finalPosition;

        if (visualRoot != null)
        {
            visualRoot.localRotation =
                visualBaseRotation;
        }

        moving = false;
        moveRoutine = null;
    }

    // =========================================================
    // Enable / Disable Control
    // =========================================================

    public void SetControlEnabled(
        bool enabled
    )
    {
        controlEnabled =
            enabled;

        if (enabled)
            return;

        if (moveRoutine != null)
        {
            StopCoroutine(
                moveRoutine
            );

            moveRoutine = null;
        }

        moving = false;

        // =====================================================
        // 현재 슬롯의 X 위치만 정리.
        //
        // Fail / Clear 등으로 변경된 Y 위치는 절대 건드리지 않음.
        // =====================================================

        if (slots != null &&
            currentSlotIndex >= 0 &&
            currentSlotIndex < slots.Length &&
            slots[currentSlotIndex] != null)
        {
            Vector3 position =
                transform.position;

            position.x =
                slots[currentSlotIndex].position.x;

            transform.position =
                position;
        }

        if (visualRoot != null)
        {
            visualRoot.localRotation =
                visualBaseRotation;
        }
    }

    // =========================================================
    // Reset
    // =========================================================

    public void ResetToStartSlot()
    {
        if (slots == null ||
            slots.Length == 0)
        {
            return;
        }

        if (moveRoutine != null)
        {
            StopCoroutine(
                moveRoutine
            );

            moveRoutine = null;
        }

        moving = false;

        currentSlotIndex =
            Mathf.Clamp(
                startSlotIndex,
                0,
                slots.Length - 1
            );

        if (slots[currentSlotIndex] != null)
        {
            Vector3 position =
                transform.position;

            // 시작 슬롯의 X만 복구
            position.x =
                slots[currentSlotIndex].position.x;

            transform.position =
                position;
        }

        if (visualRoot != null)
        {
            visualRoot.localRotation =
                visualBaseRotation;
        }
    }
}