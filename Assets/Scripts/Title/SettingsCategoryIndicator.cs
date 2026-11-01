using System.Collections;
using UnityEngine;

public class SettingsCategoryIndicator : MonoBehaviour
{
    [Header("Selection Indicator")]
    [SerializeField]
    private RectTransform indicator;

    [Header("Category Targets")]
    [SerializeField]
    private RectTransform[] categoryTargets;

    [Header("Animation")]
    [SerializeField]
    private float moveDuration = 0.14f;

    private Coroutine moveRoutine;
    private int currentIndex = 0;

    private void Start()
    {
        if (
            indicator == null ||
            categoryTargets == null ||
            categoryTargets.Length == 0
        )
        {
            return;
        }

        currentIndex =
            Mathf.Clamp(
                currentIndex,
                0,
                categoryTargets.Length - 1
            );

        MoveInstant(
            currentIndex
        );
    }

    // =========================================================
    // Public
    // =========================================================

    public void SelectCategory(
        int index
    )
    {
        if (indicator == null)
            return;

        if (
            categoryTargets == null ||
            categoryTargets.Length == 0
        )
        {
            return;
        }

        if (
            index < 0 ||
            index >= categoryTargets.Length
        )
        {
            return;
        }

        if (
            moveRoutine != null
        )
        {
            StopCoroutine(
                moveRoutine
            );
        }

        currentIndex = index;

        moveRoutine =
            StartCoroutine(
                MoveRoutine(
                    categoryTargets[index]
                )
            );
    }

    // =========================================================
    // Animation
    // =========================================================

    private IEnumerator MoveRoutine(
        RectTransform target
    )
    {
        Vector2 startPosition =
            indicator.anchoredPosition;

        Vector2 targetPosition =
            new Vector2(
                indicator.anchoredPosition.x,
                target.anchoredPosition.y
            );

        float timer = 0f;

        float duration =
            Mathf.Max(
                0.01f,
                moveDuration
            );

        while (
            timer < duration
        )
        {
            timer +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    timer / duration
                );

            // 부드럽게 가속 후 감속
            float eased =
                t * t *
                (3f - 2f * t);

            indicator.anchoredPosition =
                Vector2.Lerp(
                    startPosition,
                    targetPosition,
                    eased
                );

            yield return null;
        }

        indicator.anchoredPosition =
            targetPosition;

        moveRoutine = null;
    }

    // =========================================================
    // Instant
    // =========================================================

    private void MoveInstant(
        int index
    )
    {
        RectTransform target =
            categoryTargets[index];

        indicator.anchoredPosition =
            new Vector2(
                indicator.anchoredPosition.x,
                target.anchoredPosition.y
            );
    }
}