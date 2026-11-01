using System.Collections;
using UnityEngine;

public class KeyConflictWarningAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private RectTransform panelRoot;

    [SerializeField]
    private CanvasGroup canvasGroup;

    [Header("Open")]
    [SerializeField]
    private float openDuration = 0.16f;

    [SerializeField]
    private float openOffsetY = 18f;

    [Header("Close")]
    [SerializeField]
    private float closeDuration = 0.10f;

    [SerializeField]
    private float closeOffsetY = 12f;

    private Vector2 basePosition;
    private Coroutine currentCoroutine;

    private void Awake()
    {
        if (panelRoot == null)
        {
            panelRoot =
                transform as RectTransform;
        }

        if (canvasGroup == null)
        {
            canvasGroup =
                GetComponent<CanvasGroup>();
        }

        if (panelRoot != null)
        {
            basePosition =
                panelRoot.anchoredPosition;
        }
    }

    private void OnEnable()
    {
        // 팝업이 켜지는 순간부터
        // 버튼 클릭을 받을 수 있도록 강제
        if (canvasGroup != null)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }

    public void Show()
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
        }

        currentCoroutine =
            StartCoroutine(
                ShowRoutine()
            );
    }

    public void Hide()
    {
        if (!gameObject.activeSelf)
            return;

        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
        }

        currentCoroutine =
            StartCoroutine(
                HideRoutine()
            );
    }

    private IEnumerator ShowRoutine()
    {
        // ★ 중요
        // 애니메이션 중에도 Raycast 허용
        if (canvasGroup != null)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 0f;
        }

        Vector2 startPosition =
            basePosition +
            new Vector2(
                0f,
                openOffsetY
            );

        if (panelRoot != null)
        {
            panelRoot.anchoredPosition =
                startPosition;
        }

        float timer = 0f;

        while (timer < openDuration)
        {
            timer +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    timer /
                    Mathf.Max(
                        0.001f,
                        openDuration
                    )
                );

            float eased =
                1f -
                Mathf.Pow(
                    1f - t,
                    3f
                );

            if (panelRoot != null)
            {
                panelRoot.anchoredPosition =
                    Vector2.Lerp(
                        startPosition,
                        basePosition,
                        eased
                    );
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha =
                    eased;

                // 혹시 다른 코드가 건드려도
                // 계속 클릭 가능하게 유지
                canvasGroup.interactable =
                    true;

                canvasGroup.blocksRaycasts =
                    true;
            }

            yield return null;
        }

        if (panelRoot != null)
        {
            panelRoot.anchoredPosition =
                basePosition;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        currentCoroutine = null;
    }

    private IEnumerator HideRoutine()
    {
        Vector2 startPosition =
            panelRoot != null
                ? panelRoot.anchoredPosition
                : basePosition;

        Vector2 endPosition =
            basePosition +
            new Vector2(
                0f,
                closeOffsetY
            );

        float startAlpha =
            canvasGroup != null
                ? canvasGroup.alpha
                : 1f;

        // 닫기 애니메이션 시작 순간부터
        // 추가 클릭만 차단
        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        float timer = 0f;

        while (timer < closeDuration)
        {
            timer +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    timer /
                    Mathf.Max(
                        0.001f,
                        closeDuration
                    )
                );

            float eased =
                1f -
                Mathf.Pow(
                    1f - t,
                    3f
                );

            if (panelRoot != null)
            {
                panelRoot.anchoredPosition =
                    Vector2.Lerp(
                        startPosition,
                        endPosition,
                        eased
                    );
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha =
                    Mathf.Lerp(
                        startAlpha,
                        0f,
                        eased
                    );
            }

            yield return null;
        }

        if (panelRoot != null)
        {
            panelRoot.anchoredPosition =
                basePosition;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        currentCoroutine = null;

        gameObject.SetActive(false);
    }

    public void ForceHide()
    {
        if (currentCoroutine != null)
        {
            StopCoroutine(
                currentCoroutine
            );

            currentCoroutine = null;
        }

        if (panelRoot != null)
        {
            panelRoot.anchoredPosition =
                basePosition;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        gameObject.SetActive(false);
    }
}