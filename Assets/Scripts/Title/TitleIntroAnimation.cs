using System.Collections;
using UnityEngine;

public class TitleIntroAnimation : MonoBehaviour
{
    [Header("대상")]
    [SerializeField] private CanvasGroup logoGroup;
    [SerializeField] private RectTransform logoRect;

    [SerializeField] private CanvasGroup startGroup;
    [SerializeField] private RectTransform startRect;

    [SerializeField] private CanvasGroup settingsGroup;
    [SerializeField] private RectTransform settingsRect;

    [SerializeField] private CanvasGroup exitGroup;
    [SerializeField] private RectTransform exitRect;

    [Header("연출")]
    [SerializeField] private float logoMoveDistance = 20f;
    [SerializeField] private float buttonMoveDistance = 18f;

    [SerializeField] private float logoDuration = 0.25f;
    [SerializeField] private float buttonDuration = 0.18f;

    [SerializeField] private float buttonInterval = 0.07f;

    [Header("입력")]
    [SerializeField] private TitleMenuController menuController;

    private Vector2 logoTargetPosition;
    private Vector2 startTargetPosition;
    private Vector2 settingsTargetPosition;
    private Vector2 exitTargetPosition;

    private void Awake()
    {
        logoTargetPosition = logoRect.anchoredPosition;
        startTargetPosition = startRect.anchoredPosition;
        settingsTargetPosition = settingsRect.anchoredPosition;
        exitTargetPosition = exitRect.anchoredPosition;

        PrepareElement(
            logoGroup,
            logoRect,
            logoTargetPosition + Vector2.down * logoMoveDistance
        );

        PrepareElement(
            startGroup,
            startRect,
            startTargetPosition + Vector2.right * buttonMoveDistance
        );

        PrepareElement(
            settingsGroup,
            settingsRect,
            settingsTargetPosition + Vector2.right * buttonMoveDistance
        );

        PrepareElement(
            exitGroup,
            exitRect,
            exitTargetPosition + Vector2.right * buttonMoveDistance
        );

        if (menuController != null)
        {
            menuController.SetInputEnabled(false);
        }
    }

    private void Start()
    {
        StartCoroutine(PlayIntro());
    }

    private IEnumerator PlayIntro()
    {
        yield return AnimateElement(
            logoGroup,
            logoRect,
            logoTargetPosition,
            logoDuration
        );

        yield return AnimateElement(
            startGroup,
            startRect,
            startTargetPosition,
            buttonDuration
        );

        yield return new WaitForSecondsRealtime(buttonInterval);

        yield return AnimateElement(
            settingsGroup,
            settingsRect,
            settingsTargetPosition,
            buttonDuration
        );

        yield return new WaitForSecondsRealtime(buttonInterval);

        yield return AnimateElement(
            exitGroup,
            exitRect,
            exitTargetPosition,
            buttonDuration
        );

        if (menuController != null)
        {
            menuController.SetInputEnabled(true);
            menuController.SelectStartButton();
        }
    }

    private void PrepareElement(
        CanvasGroup group,
        RectTransform rect,
        Vector2 startPosition
    )
    {
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        rect.anchoredPosition = startPosition;
    }

    private IEnumerator AnimateElement(
        CanvasGroup group,
        RectTransform rect,
        Vector2 targetPosition,
        float duration
    )
    {
        Vector2 startPosition = rect.anchoredPosition;

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(timer / duration);

            t = 1f - Mathf.Pow(1f - t, 3f);

            rect.anchoredPosition =
                Vector2.LerpUnclamped(startPosition, targetPosition, t);

            group.alpha = t;

            yield return null;
        }

        rect.anchoredPosition = targetPosition;
        group.alpha = 1f;

        group.interactable = true;
        group.blocksRaycasts = true;
    }
}