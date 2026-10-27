using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class RebindButton : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputBindingManager bindingManager;
    [SerializeField] private Button rebindButton;
    [SerializeField] private TMP_Text keyText;

    [Header("Input Action")]
    [SerializeField] private InputActionReference inputAction;
    [SerializeField] private int bindingIndex = 0;

    [Header("Display")]
    [SerializeField] private string waitingText = "...";

    private void Awake()
    {
        if (rebindButton == null)
        {
            rebindButton =
                GetComponent<Button>();
        }
    }

    private void OnEnable()
    {
        if (rebindButton != null)
        {
            rebindButton.onClick.AddListener(
                StartRebind
            );
        }

        InputBindingManager.BindingsChanged +=
            Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (rebindButton != null)
        {
            rebindButton.onClick.RemoveListener(
                StartRebind
            );
        }

        InputBindingManager.BindingsChanged -=
            Refresh;
    }

    private void StartRebind()
    {
        if (bindingManager == null)
        {
            Debug.LogError(
                "RebindButton: InputBindingManager가 연결되지 않았습니다."
            );

            return;
        }

        if (inputAction == null)
        {
            Debug.LogError(
                "RebindButton: Input Action이 연결되지 않았습니다."
            );

            return;
        }

        bindingManager.StartRebind(
            inputAction,
            bindingIndex,

            // 시작
            () =>
            {
                if (keyText != null)
                {
                    keyText.text =
                        waitingText;
                }

                if (rebindButton != null)
                {
                    rebindButton.interactable =
                        false;
                }
            },

            // 완료
            () =>
            {
                if (rebindButton != null)
                {
                    rebindButton.interactable =
                        true;
                }

                Refresh();
            },

            // 취소
            () =>
            {
                if (rebindButton != null)
                {
                    rebindButton.interactable =
                        true;
                }

                Refresh();
            }
        );
    }

    public void Refresh()
    {
        if (keyText == null)
            return;

        if (inputAction == null)
        {
            keyText.text = "?";
            return;
        }

        InputAction action =
            inputAction.action;

        if (action == null)
        {
            keyText.text = "?";
            return;
        }

        if (bindingIndex < 0 ||
            bindingIndex >= action.bindings.Count)
        {
            keyText.text = "?";
            return;
        }

        string display =
            action.GetBindingDisplayString(
                bindingIndex
            );

        if (string.IsNullOrEmpty(display))
        {
            keyText.text = "?";
            return;
        }

        keyText.text =
            display.ToUpper();
    }
}