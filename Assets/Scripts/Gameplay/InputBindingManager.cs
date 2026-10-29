using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputBindingManager : MonoBehaviour
{
    public static InputBindingManager Instance { get; private set; }

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
        if (Instance != null && Instance != this)
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
            Instance = null;
    }

    public InputAction GetAction(string actionName)
    {
        if (inputActions == null)
        {
            Debug.LogError("InputBindingManager: Input Actions Asset이 연결되지 않았습니다.");
            return null;
        }

        InputAction action = inputActions.FindAction(actionName, false);

        if (action == null)
        {
            Debug.LogError($"InputBindingManager: Action을 찾을 수 없습니다. Name={actionName}");
        }

        return action;
    }

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
            Debug.LogError("InputBindingManager: InputActionReference가 없습니다.");
            return;
        }

        InputAction action = actionReference.action;

        if (action == null)
        {
            Debug.LogError("InputBindingManager: InputAction을 찾을 수 없습니다.");
            return;
        }

        if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
        {
            Debug.LogError(
                "InputBindingManager: 잘못된 Binding Index입니다. " +
                $"Action={action.name}, Index={bindingIndex}"
            );
            return;
        }

        CancelCurrentRebind();
        action.Disable();
        onStarted?.Invoke();

        currentRebind =
            action
                .PerformInteractiveRebinding(bindingIndex)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithCancelingThrough("<Keyboard>/escape")
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

                        Debug.Log(
                            $"리바인드 완료: {action.name} = {action.GetBindingDisplayString(bindingIndex)}"
                        );
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

    public void SaveBindings()
    {
        if (inputActions == null)
        {
            Debug.LogError("InputBindingManager: Input Actions Asset이 연결되지 않았습니다.");
            return;
        }

        string json = inputActions.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();

        Debug.Log("InputBindingManager: 키 설정 저장 완료");
    }

    public void LoadBindings()
    {
        if (inputActions == null)
        {
            Debug.LogError("InputBindingManager: Input Actions Asset이 연결되지 않았습니다.");
            return;
        }

        if (!PlayerPrefs.HasKey(SaveKey))
            return;

        string json = PlayerPrefs.GetString(SaveKey);

        if (string.IsNullOrEmpty(json))
            return;

        inputActions.LoadBindingOverridesFromJson(json);
        BindingsChanged?.Invoke();

        Debug.Log("InputBindingManager: 저장된 키 설정 불러오기 완료");
    }

    public void ResetAllBindings()
    {
        if (inputActions == null)
            return;

        CancelCurrentRebind();
        inputActions.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
        BindingsChanged?.Invoke();

        Debug.Log("InputBindingManager: 키 설정 초기화 완료");
    }
}
