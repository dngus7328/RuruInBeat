using UnityEngine;
using UnityEngine.EventSystems;

public class TitleButtonAnimation : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    ISelectHandler,
    IDeselectHandler
{
    [Header("Scale")]
    [SerializeField] private float normalScale = 1f;
    [SerializeField] private float hoverScale = 1.03f;
    [SerializeField] private float selectedScale = 1.06f;
    [SerializeField] private float pressedScale = 0.98f;

    [Header("Move")]
    [SerializeField] private float hoverMove = 8f;
    [SerializeField] private float selectedMove = 18f;

    [Header("Animation")]
    [SerializeField] private float moveSpeed = 14f;
    [SerializeField] private float scaleSpeed = 16f;

    private RectTransform rectTransform;

    private Vector2 originalPosition;
    private Vector3 originalScale;

    private bool isHovered;
    private bool isSelected;
    private bool isPressed;

    private float targetMove;
    private float targetScale;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        originalPosition = rectTransform.anchoredPosition;
        originalScale = rectTransform.localScale;

        targetMove = 0f;
        targetScale = normalScale;
    }

    private void Update()
    {
        UpdatePosition();
        UpdateScale();
    }

    private void UpdatePosition()
    {
        Vector2 targetPosition =
            originalPosition + Vector2.left * targetMove;

        rectTransform.anchoredPosition =
            Vector2.Lerp(
                rectTransform.anchoredPosition,
                targetPosition,
                Time.unscaledDeltaTime * moveSpeed
            );
    }

    private void UpdateScale()
    {
        Vector3 target =
            originalScale * targetScale;

        rectTransform.localScale =
            Vector3.Lerp(
                rectTransform.localScale,
                target,
                Time.unscaledDeltaTime * scaleSpeed
            );
    }

    private void RefreshState()
    {
        if (isPressed)
        {
            targetMove = selectedMove;
            targetScale = pressedScale;
            return;
        }

        if (isSelected)
        {
            targetMove = selectedMove;
            targetScale = selectedScale;
            return;
        }

        if (isHovered)
        {
            targetMove = hoverMove;
            targetScale = hoverScale;
            return;
        }

        targetMove = 0f;
        targetScale = normalScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        RefreshState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        isPressed = false;
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

    public void OnSelect(BaseEventData eventData)
    {
        isSelected = true;
        RefreshState();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        isPressed = false;
        RefreshState();
    }
}