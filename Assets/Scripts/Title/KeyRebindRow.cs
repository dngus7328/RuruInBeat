using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class KeyRebindRow : MonoBehaviour
{
    [Header("리바인드")]
    [SerializeField]
    private InputBindingManager bindingManager;

    [Tooltip("예: Lane_Q, Lane_W, Pino_Left")]
    [SerializeField]
    private string actionName;

    [SerializeField]
    private int bindingIndex = 0;

    [Header("UI")]
    [SerializeField]
    private Button keyButton;

    [SerializeField]
    private TMP_Text keyText;

    [Header("리바인드 대기 애니메이션")]
    [SerializeField]
    private float dotInterval = 0.3f;

    private bool isRebinding = false;

    private InputAction targetAction;

    private Coroutine waitingDotsRoutine;

    // =========================================================
    // Unity
    // =========================================================

    private void Start()
    {
        if (bindingManager == null)
        {
            bindingManager =
                InputBindingManager.Instance;
        }

        FindTargetAction();

        if (keyButton != null)
        {
            keyButton.onClick.AddListener(
                StartRebind
            );
        }

        InputBindingManager.BindingsChanged +=
            RefreshKeyText;

        RefreshKeyText();
    }

    private void Update()
    {
        if (!isRebinding)
            return;

        // 키 재지정 대기 중
        // 다른 UI를 마우스로 클릭하면 리바인드 취소
        if (Input.GetMouseButtonDown(0))
        {
            CancelRebind();
        }
    }

    private void OnDestroy()
    {
        StopWaitingDots();

        if (keyButton != null)
        {
            keyButton.onClick.RemoveListener(
                StartRebind
            );
        }

        InputBindingManager.BindingsChanged -=
            RefreshKeyText;
    }

    // =========================================================
    // Action
    // =========================================================

    private void FindTargetAction()
    {
        if (bindingManager == null)
        {
            bindingManager =
                InputBindingManager.Instance;
        }

        if (bindingManager == null)
        {
            targetAction = null;

            Debug.LogError(
                $"{name}: InputBindingManager가 없습니다."
            );

            return;
        }

        targetAction =
            bindingManager.GetAction(
                actionName
            );

        if (targetAction == null)
        {
            Debug.LogError(
                $"{name}: Action을 찾을 수 없습니다. " +
                $"Action Name = {actionName}"
            );
        }
    }

    // =========================================================
    // Rebind
    // =========================================================

    private void StartRebind()
    {
        if (isRebinding)
            return;

        if (bindingManager == null)
        {
            bindingManager =
                InputBindingManager.Instance;
        }

        if (bindingManager == null)
        {
            Debug.LogError(
                $"{name}: InputBindingManager가 없습니다."
            );

            return;
        }

        FindTargetAction();

        if (targetAction == null)
            return;

        if (
            bindingIndex < 0 ||
            bindingIndex >=
            targetAction.bindings.Count
        )
        {
            Debug.LogError(
                $"{name}: Binding Index가 잘못되었습니다."
            );

            return;
        }

        isRebinding = true;

        InputActionReference runtimeReference =
            InputActionReference.Create(
                targetAction
            );

        bindingManager.StartRebind(
            runtimeReference,
            bindingIndex,

            // 시작
            () =>
            {
                StartWaitingDots();

                if (keyButton != null)
                {
                    keyButton.interactable =
                        false;
                }
            },

            // 완료
            () =>
            {
                FinishRebind();

                FindTargetAction();

                RefreshKeyText();
            },

            // 취소
            () =>
            {
                FinishRebind();

                RefreshKeyText();
            }
        );
    }

    private void CancelRebind()
    {
        if (!isRebinding)
            return;

        if (bindingManager == null)
        {
            bindingManager =
                InputBindingManager.Instance;
        }

        if (bindingManager != null)
        {
            bindingManager.CancelCurrentRebind();
        }
        else
        {
            FinishRebind();
            RefreshKeyText();
        }
    }

    private void FinishRebind()
    {
        StopWaitingDots();

        isRebinding =
            false;

        if (keyButton != null)
        {
            keyButton.interactable =
                true;
        }
    }

    // =========================================================
    // Waiting Dots
    // =========================================================

    private void StartWaitingDots()
    {
        StopWaitingDots();

        waitingDotsRoutine =
            StartCoroutine(
                WaitingDotsRoutine()
            );
    }

    private void StopWaitingDots()
    {
        if (waitingDotsRoutine == null)
            return;

        StopCoroutine(
            waitingDotsRoutine
        );

        waitingDotsRoutine =
            null;
    }

    private IEnumerator WaitingDotsRoutine()
    {
        int dotCount = 1;

        while (true)
        {
            if (keyText != null)
            {
                keyText.text =
                    new string(
                        '.',
                        dotCount
                    );
            }

            dotCount++;

            if (dotCount > 3)
            {
                dotCount = 1;
            }

            yield return
                new WaitForSecondsRealtime(
                    Mathf.Max(
                        0.05f,
                        dotInterval
                    )
                );
        }
    }

    // =========================================================
    // UI
    // =========================================================

    private void RefreshKeyText()
    {
        if (keyText == null)
            return;

        if (isRebinding)
            return;

        if (targetAction == null)
        {
            FindTargetAction();
        }

        if (targetAction == null)
        {
            keyText.text = "-";
            return;
        }

        if (
            bindingIndex < 0 ||
            bindingIndex >=
            targetAction.bindings.Count
        )
        {
            keyText.text = "-";
            return;
        }

        string displayName =
            targetAction
                .GetBindingDisplayString(
                    bindingIndex
                );

        keyText.text =
            displayName.ToUpper();
    }
}