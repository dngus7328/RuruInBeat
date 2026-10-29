using System.Collections;
using UnityEngine;

public class SettingsFocusAnimation : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] private float moveDistance = 10f;

    [Header("시간")]
    [SerializeField] private float showDuration = 0.12f;
    [SerializeField] private float hideDuration = 0.08f;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;

    private Vector2 originalPosition;

    private Coroutine currentCoroutine;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        originalPosition = rectTransform.anchoredPosition;
    }

    public void Show(int direction)
    {
        gameObject.SetActive(true);

        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
        }

        currentCoroutine = StartCoroutine(
            ShowRoutine(direction)
        );
    }

    public void Hide()
    {
        if (!gameObject.activeSelf)
        {
            return;
        }

        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
        }

        currentCoroutine = StartCoroutine(
            HideRoutine()
        );
    }

    public void HideImmediate()
    {
        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
        }

        rectTransform.anchoredPosition = originalPosition;
        canvasGroup.alpha = 0f;

        gameObject.SetActive(false);
    }

    private IEnumerator ShowRoutine(int direction)
    {
        Vector2 startPosition = originalPosition;

        if (direction > 0)
        {
            startPosition += Vector2.up * moveDistance;
        }
        else if (direction < 0)
        {
            startPosition += Vector2.down * moveDistance;
        }

        rectTransform.anchoredPosition = startPosition;
        canvasGroup.alpha = 0f;

        float timer = 0f;

        while (timer < showDuration)
        {
            timer += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                timer / showDuration
            );

            float eased =
                1f - Mathf.Pow(1f - t, 3f);

            rectTransform.anchoredPosition =
                Vector2.Lerp(
                    startPosition,
                    originalPosition,
                    eased
                );

            canvasGroup.alpha = t;

            yield return null;
        }

        rectTransform.anchoredPosition = originalPosition;
        canvasGroup.alpha = 1f;

        currentCoroutine = null;
    }

    private IEnumerator HideRoutine()
    {
        float startAlpha = canvasGroup.alpha;

        float timer = 0f;

        while (timer < hideDuration)
        {
            timer += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                timer / hideDuration
            );

            canvasGroup.alpha =
                Mathf.Lerp(
                    startAlpha,
                    0f,
                    t
                );

            yield return null;
        }

        canvasGroup.alpha = 0f;

        rectTransform.anchoredPosition = originalPosition;

        gameObject.SetActive(false);

        currentCoroutine = null;
    }
}