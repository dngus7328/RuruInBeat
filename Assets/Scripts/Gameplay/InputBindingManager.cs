using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputBindingManager : MonoBehaviour
{
    private const string SaveKey =
        "LULU_INPUT_BINDINGS";

    public static event Action BindingsChanged;

    [Header("Input")]
    [SerializeField]
    private InputActionAsset inputActions;

    private InputActionRebindingExtensions
        .RebindingOperation currentRebind;

    private void Awake()
    {
        LoadBindings();
    }

    private void OnDestroy()
    {
        CancelCurrentRebind();
    }

    // =========================================================
    // Rebinding
    // =========================================================

    public void StartRebind(
        InputActionReference actionReference,
        int bindingIndex,
        Action onStarted = null,
        Action onCompleted = null,
        Action onCancelled = null
    )
    {
        if (actionReference == null)
        {
            Debug.LogError(
                "InputBindingManager: " +
                "InputActionReference가 없습니다."
            );

            return;
        }

        InputAction action =
            actionReference.action;

        if (action == null)
        {
            Debug.LogError(
                "InputBindingManager: " +
                "InputAction을 찾을 수 없습니다."
            );

            return;
        }

        if (bindingIndex < 0 ||
            bindingIndex >= action.bindings.Count)
        {
            Debug.LogError(
                $"InputBindingManager: " +
                $"잘못된 Binding Index입니다. " +
                $"Action={action.name}, " +
                $"Index={bindingIndex}"
            );

            return;
        }

        CancelCurrentRebind();

        action.Disable();

        onStarted?.Invoke();

        currentRebind =
            action
                .PerformInteractiveRebinding(
                    bindingIndex
                )

                // 현재는 PC 키보드 게임 기준
                .WithControlsHavingToMatchPath(
                    "<Keyboard>"
                )

                // ESC는 키로 등록하지 않고 취소용
                .WithCancelingThrough(
                    "<Keyboard>/escape"
                )

                .OnCancel(
                    operation =>
                    {
                        operation.Dispose();

                        currentRebind = null;

                        action.Enable();

                        onCancelled?.Invoke();
                    }
                )

                .OnComplete(
                    operation =>
                    {
                        operation.Dispose();

                        currentRebind = null;

                        action.Enable();

                        SaveBindings();

                        BindingsChanged?.Invoke();

                        onCompleted?.Invoke();
                    }
                );

        currentRebind.Start();
    }

    public void CancelCurrentRebind()
    {
        if (currentRebind == null)
            return;

        currentRebind.Cancel();
    }

    // =========================================================
    // Save / Load
    // =========================================================

    public void SaveBindings()
    {
        if (inputActions == null)
        {
            Debug.LogError(
                "InputBindingManager: " +
                "Input Actions Asset이 연결되지 않았습니다."
            );

            return;
        }

        string json =
            inputActions
                .SaveBindingOverridesAsJson();

        PlayerPrefs.SetString(
            SaveKey,
            json
        );

        PlayerPrefs.Save();
    }

    public void LoadBindings()
    {
        if (inputActions == null)
        {
            Debug.LogError(
                "InputBindingManager: " +
                "Input Actions Asset이 연결되지 않았습니다."
            );

            return;
        }

        if (!PlayerPrefs.HasKey(SaveKey))
            return;

        string json =
            PlayerPrefs.GetString(
                SaveKey
            );

        if (string.IsNullOrEmpty(json))
            return;

        inputActions
            .LoadBindingOverridesFromJson(
                json
            );

        BindingsChanged?.Invoke();
    }

    // =========================================================
    // Reset
    // =========================================================

    public void ResetAllBindings()
    {
        if (inputActions == null)
            return;

        CancelCurrentRebind();

        inputActions
            .RemoveAllBindingOverrides();

        PlayerPrefs.DeleteKey(
            SaveKey
        );

        PlayerPrefs.Save();

        BindingsChanged?.Invoke();
    }
}