using UnityEngine;
using UnityEngine.EventSystems;

public class SettingsCategoryButtonAnimation : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler
{
    [Header("이동 거리")]

    // 선택 전 미리보기 상태
    [SerializeField] private float focusMove = 2f;

    // 실제 선택된 상태
    [SerializeField] private float selectedMove = 5f;

    // 마우스로 누르고 있는 순간
    [SerializeField] private float pressedMove = 2f;

    [Header("이동 속도")]
    [SerializeField] private float moveSpeed = 18f;

    private RectTransform rectTransform;

    private Vector2 originalPosition;

    private bool isFocused;
    private bool isSelected;
    private bool isPressed;

    private float targetMove;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        originalPosition = rectTransform.anchoredPosition;

        targetMove = 0f;
    }

    private void Update()
    {
        Vector2 targetPosition =
            originalPosition +
            Vector2.right * targetMove;

        rectTransform.anchoredPosition =
            Vector2.Lerp(
                rectTransform.anchoredPosition,
                targetPosition,
                Time.unscaledDeltaTime * moveSpeed
            );
    }

    private void RefreshState()
    {
        // 클릭해서 누르고 있는 순간
        if (isPressed)
        {
            targetMove = pressedMove;
            return;
        }

        // 실제 선택된 버튼
        if (isSelected)
        {
            targetMove = selectedMove;
            return;
        }

        // 마우스 Hover / 키보드 미리보기
        if (isFocused)
        {
            targetMove = focusMove;
            return;
        }

        // 아무 상태 아님
        targetMove = 0f;
    }

    public void SetFocused(bool focused)
    {
        isFocused = focused;
        RefreshState();
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        RefreshState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        RefreshState();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        RefreshState();
    }
}