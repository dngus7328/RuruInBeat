using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class KeyBindingLabel : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField]
    private TMP_Text keyText;

    [Header("Input Action")]
    [SerializeField]
    private InputActionReference inputAction;

    [Header("Binding")]
    [SerializeField]
    private int bindingIndex = 0;

    private void Awake()
    {
        if (keyText == null)
        {
            keyText =
                GetComponent<TMP_Text>();
        }
    }

    private void OnEnable()
    {
        InputBindingManager
            .BindingsChanged += Refresh;

        Refresh();
    }

    private void Start()
    {
        // InputBindingManager의 LoadBindings보다
        // 먼저 OnEnable이 실행된 경우를 대비
        Refresh();
    }

    private void OnDisable()
    {
        InputBindingManager
            .BindingsChanged -= Refresh;
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
            bindingIndex >=
            action.bindings.Count)
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