using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputBindingManager : MonoBehaviour
{
    public static InputBindingManager Instance { get; private set; }

    private const string SaveKey = "LULU_INPUT_BINDINGS";

    public static event Action BindingsChanged;

    public static event Action<
        string,
        string,
        string
    > BindingConflictDetected;

    [Header("Input")]
    [SerializeField]
    private InputActionAsset inputActions;

    private InputActionRebindingExtensions
        .RebindingOperation currentRebind;

    // =========================================================
    // Conflict Pending
    // =========================================================

    private InputAction pendingTargetAction;
    private int pendingTargetBindingIndex = -1;

    private InputAction pendingConflictAction;
    private int pendingConflictBindingIndex = -1;

    private string pendingNewPath;
    private string pendingOldTargetPath;

    private bool hasPendingConflict;

    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        if (
            Instance != null &&
            Instance != this
        )
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        LoadBindings();
    }

    private void OnDestroy()
    {
        CancelCurrentRebind();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    // =========================================================
    // Action
    // =========================================================

    public InputAction GetAction(
        string actionName
    )
    {
        if (inputActions == null)
        {
            Debug.LogError(
                "InputBindingManager: InputActionAsset이 없습니다."
            );

            return null;
        }

        InputAction action =
            inputActions.FindAction(
                actionName,
                false
            );

        if (action == null)
        {
            Debug.LogError(
                $"InputBindingManager: " +
                $"{actionName} Action을 찾을 수 없습니다."
            );
        }

        return action;
    }

    // =========================================================
    // Rebind
    // =========================================================

    public void StartRebind(
        InputAction action,
        int bindingIndex,
        Action onStarted = null,
        Action onCompleted = null,
        Action onCancelled = null
    )
    {
        if (action == null)
        {
            Debug.LogError(
                "InputBindingManager: InputAction이 없습니다."
            );

            return;
        }

        if (
            bindingIndex < 0 ||
            bindingIndex >= action.bindings.Count
        )
        {
            Debug.LogError(
                $"InputBindingManager: 잘못된 Binding Index. " +
                $"Action={action.name}, Index={bindingIndex}"
            );

            return;
        }

        if (hasPendingConflict)
        {
            return;
        }

        CancelCurrentRebind();

        string oldPath =
            action.bindings[
                bindingIndex
            ].effectivePath;

        action.Disable();

        onStarted?.Invoke();

        currentRebind =
            action
                .PerformInteractiveRebinding(
                    bindingIndex
                )
                .WithControlsHavingToMatchPath(
                    "<Keyboard>"
                )
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

                        HandleRebindComplete(
                            action,
                            bindingIndex,
                            oldPath
                        );

                        onCompleted?.Invoke();
                    }
                );

        currentRebind.Start();
    }

    // =========================================================
    // Rebind Complete
    // =========================================================

    private void HandleRebindComplete(
        InputAction targetAction,
        int targetBindingIndex,
        string oldTargetPath
    )
    {
        string newPath =
            targetAction.bindings[
                targetBindingIndex
            ].effectivePath;

        InputAction conflictAction;
        int conflictBindingIndex;

        bool conflictFound =
            FindConflict(
                targetAction,
                targetBindingIndex,
                newPath,
                out conflictAction,
                out conflictBindingIndex
            );

        // 중복 아님
        if (!conflictFound)
        {
            SaveBindings();

            BindingsChanged?.Invoke();

            return;
        }

        // 중복 발견
        pendingTargetAction =
            targetAction;

        pendingTargetBindingIndex =
            targetBindingIndex;

        pendingConflictAction =
            conflictAction;

        pendingConflictBindingIndex =
            conflictBindingIndex;

        pendingNewPath =
            newPath;

        pendingOldTargetPath =
            oldTargetPath;

        hasPendingConflict =
            true;

        // 확인 전까지 원래 키로 되돌림
        targetAction.ApplyBindingOverride(
            targetBindingIndex,
            oldTargetPath
        );

        BindingsChanged?.Invoke();

        string keyName =
            GetKeyDisplayName(
                newPath
            );

        BindingConflictDetected?.Invoke(
            keyName,
            GetFriendlyActionName(
                targetAction.name
            ),
            GetFriendlyActionName(
                conflictAction.name
            )
        );
    }

    // =========================================================
    // Conflict Search
    // =========================================================

    private bool FindConflict(
        InputAction targetAction,
        int targetBindingIndex,
        string newPath,
        out InputAction conflictAction,
        out int conflictBindingIndex
    )
    {
        conflictAction = null;
        conflictBindingIndex = -1;

        if (inputActions == null)
        {
            return false;
        }

        foreach (
            InputActionMap map
            in inputActions.actionMaps
        )
        {
            foreach (
                InputAction action
                in map.actions
            )
            {
                if (
                    !IsRebindableAction(
                        action.name
                    )
                )
                {
                    continue;
                }

                for (
                    int i = 0;
                    i < action.bindings.Count;
                    i++
                )
                {
                    if (
                        action == targetAction &&
                        i == targetBindingIndex
                    )
                    {
                        continue;
                    }

                    InputBinding binding =
                        action.bindings[i];

                    if (
                        binding.isComposite ||
                        binding.isPartOfComposite
                    )
                    {
                        continue;
                    }

                    string path =
                        binding.effectivePath;

                    if (
                        string.Equals(
                            path,
                            newPath,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        conflictAction =
                            action;

                        conflictBindingIndex =
                            i;

                        return true;
                    }
                }
            }
        }

        return false;
    }

    private bool IsRebindableAction(
        string actionName
    )
    {
        return
            actionName == "Lane_Q" ||
            actionName == "Lane_W" ||
            actionName == "Lane_O" ||
            actionName == "Lane_P" ||
            actionName == "Pino_Left" ||
            actionName == "Pino_Right";
    }

    // =========================================================
    // Conflict Confirm
    // =========================================================

    public void ConfirmPendingSwap()
    {
        if (!hasPendingConflict)
        {
            return;
        }

        if (
            pendingTargetAction == null ||
            pendingConflictAction == null
        )
        {
            ClearPendingConflict();
            return;
        }

        pendingTargetAction
            .ApplyBindingOverride(
                pendingTargetBindingIndex,
                pendingNewPath
            );

        pendingConflictAction
            .ApplyBindingOverride(
                pendingConflictBindingIndex,
                pendingOldTargetPath
            );

        SaveBindings();

        ClearPendingConflict();

        BindingsChanged?.Invoke();
    }

    public void CancelPendingConflict()
    {
        if (!hasPendingConflict)
        {
            return;
        }

        ClearPendingConflict();

        BindingsChanged?.Invoke();
    }

    private void ClearPendingConflict()
    {
        pendingTargetAction = null;
        pendingTargetBindingIndex = -1;

        pendingConflictAction = null;
        pendingConflictBindingIndex = -1;

        pendingNewPath = null;
        pendingOldTargetPath = null;

        hasPendingConflict = false;
    }

    // =========================================================
    // Rebind Cancel
    // =========================================================

    public void CancelCurrentRebind()
    {
        if (currentRebind == null)
        {
            return;
        }

        currentRebind.Cancel();

        currentRebind = null;
    }

    // =========================================================
    // Save / Load
    // =========================================================

    public void SaveBindings()
    {
        if (inputActions == null)
        {
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
            return;
        }

        if (!PlayerPrefs.HasKey(SaveKey))
        {
            return;
        }

        string json =
            PlayerPrefs.GetString(
                SaveKey
            );

        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        inputActions
            .LoadBindingOverridesFromJson(
                json
            );

        BindingsChanged?.Invoke();
    }

    // =========================================================
    // Reset
    // 외부 Reset 전용 스크립트가 이것만 호출
    // =========================================================

    public void ResetAllBindings()
    {
        if (inputActions == null)
        {
            return;
        }

        CancelCurrentRebind();
        ClearPendingConflict();

        inputActions
            .RemoveAllBindingOverrides();

        PlayerPrefs.DeleteKey(
            SaveKey
        );

        PlayerPrefs.Save();

        BindingsChanged?.Invoke();
    }

    // =========================================================
    // Display
    // =========================================================

    private string GetKeyDisplayName(
        string path
    )
    {
        if (string.IsNullOrEmpty(path))
        {
            return "?";
        }

        return InputControlPath
            .ToHumanReadableString(
                path,
                InputControlPath
                    .HumanReadableStringOptions
                    .OmitDevice
            )
            .ToUpper();
    }

    private string GetFriendlyActionName(
        string actionName
    )
    {
        switch (actionName)
        {
            case "Lane_Q":
                return "1번 레인";

            case "Lane_W":
                return "2번 레인";

            case "Lane_O":
                return "3번 레인";

            case "Lane_P":
                return "4번 레인";

            case "Pino_Left":
                return "피노 왼쪽 이동";

            case "Pino_Right":
                return "피노 오른쪽 이동";

            default:
                return actionName;
        }
    }
}