using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIHoverScale :
    MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [Header("Scale")]
    [SerializeField] private float hoverScale = 1.05f;
    [SerializeField] private float pressedScale = 0.98f;

    [Header("Speed")]
    [SerializeField] private float duration = 0.1f;

    private Vector3 originalScale;
    private Coroutine scaleCoroutine;

    private bool isHovered;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;

        ScaleTo(
            originalScale * hoverScale
        );
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;

        ScaleTo(
            originalScale
        );
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        ScaleTo(
            originalScale * pressedScale
        );
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (isHovered)
        {
            ScaleTo(
                originalScale * hoverScale
            );
        }
        else
        {
            ScaleTo(
                originalScale
            );
        }
    }

    private void ScaleTo(Vector3 targetScale)
    {
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
        }

        scaleCoroutine =
            StartCoroutine(
                ScaleRoutine(targetScale)
            );
    }

    private IEnumerator ScaleRoutine(
        Vector3 targetScale
    )
    {
        Vector3 startScale =
            transform.localScale;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / duration
                );

            t =
                1f -
                Mathf.Pow(
                    1f - t,
                    3f
                );

            transform.localScale =
                Vector3.Lerp(
                    startScale,
                    targetScale,
                    t
                );

            yield return null;
        }

        transform.localScale =
            targetScale;

        scaleCoroutine = null;
    }
}