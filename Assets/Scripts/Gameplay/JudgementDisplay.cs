using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class JudgementDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image judgementImage;
    [SerializeField] private RectTransform judgementRect;

    [Header("Judgement Sprites")]
    [SerializeField] private Sprite perfectSprite;
    [SerializeField] private Sprite greatSprite;
    [SerializeField] private Sprite goodSprite;
    [SerializeField] private Sprite missSprite;
    [SerializeField] private Sprite hitSprite;

    [Header("Auto Size")]
    [Tooltip("판정 이미지 원본 비율을 유지하면서 자동으로 크기를 맞춥니다.")]
    [SerializeField] private bool autoFitSprite = true;

    [Min(1f)]
    [Tooltip("기본 판정 이미지 높이. 짧은 글자는 이 높이를 기준으로 표시됩니다.")]
    [SerializeField] private float targetHeight = 90f;

    [Min(1f)]
    [Tooltip("판정 이미지가 너무 길면 이 폭을 넘지 않도록 전체 크기를 자동 축소합니다.")]
    [SerializeField] private float maxWidth = 360f;

    [Header("Appear Animation")]
    [SerializeField] private float startScale = 0.65f;
    [SerializeField] private float popScale = 1.15f;
    [SerializeField] private float popDuration = 0.07f;
    [SerializeField] private float settleDuration = 0.08f;

    [Header("Hold")]
    [SerializeField] private float holdDuration = 0.12f;

    [Header("Disappear Animation")]
    [SerializeField] private float floatDistance = 55f;
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private float endScale = 0.95f;

    private Coroutine animationCoroutine;
    private Vector2 originalPosition;
    private Vector2 originalSize;

    private void Awake()
    {
        if (judgementImage == null)
        {
            judgementImage =
                GetComponent<Image>();
        }

        if (judgementRect == null)
        {
            judgementRect =
                GetComponent<RectTransform>();
        }

        if (judgementRect != null)
        {
            originalPosition =
                judgementRect.anchoredPosition;

            originalSize =
                judgementRect.sizeDelta;
        }

        if (judgementImage != null)
        {
            judgementImage.preserveAspect =
                true;
        }

        HideImmediately();
    }

    public void Show(
        NoteJudgement judgement
    )
    {
        if (judgementImage == null ||
            judgementRect == null)
        {
            return;
        }

        Sprite targetSprite =
            GetJudgementSprite(
                judgement
            );

        ShowSprite(
            targetSprite
        );
    }

    public void ShowHit()
    {
        if (judgementImage == null ||
            judgementRect == null)
        {
            return;
        }

        /*
         * HIT 전용 이미지가 연결돼 있으면 사용.
         * 아직 연결하지 않았다면 MISS 이미지를 fallback으로 사용한다.
         */
        Sprite targetSprite =
            hitSprite != null
                ? hitSprite
                : missSprite;

        ShowSprite(
            targetSprite
        );
    }

    private Sprite GetJudgementSprite(
        NoteJudgement judgement
    )
    {
        switch (judgement)
        {
            case NoteJudgement.Perfect:
                return perfectSprite;

            case NoteJudgement.Great:
                return greatSprite;

            case NoteJudgement.Good:
                return goodSprite;

            case NoteJudgement.Miss:
                return missSprite;

            default:
                return null;
        }
    }

    private void ShowSprite(
        Sprite sprite
    )
    {
        if (sprite == null)
        {
            Debug.LogWarning(
                "JudgementDisplay: 표시할 Sprite가 없습니다.",
                this
            );

            return;
        }

        judgementImage.sprite =
            sprite;

        judgementImage.preserveAspect =
            true;

        ApplyAutoSize(
            sprite
        );

        if (animationCoroutine != null)
        {
            StopCoroutine(
                animationCoroutine
            );
        }

        ResetVisual();

        animationCoroutine =
            StartCoroutine(
                PlayAnimation()
            );
    }

    private void ApplyAutoSize(
        Sprite sprite
    )
    {
        if (judgementRect == null ||
            sprite == null)
        {
            return;
        }

        if (!autoFitSprite)
        {
            judgementRect.sizeDelta =
                originalSize;

            return;
        }

        float spriteWidth =
            Mathf.Max(
                1f,
                sprite.rect.width
            );

        float spriteHeight =
            Mathf.Max(
                1f,
                sprite.rect.height
            );

        float aspect =
            spriteWidth /
            spriteHeight;

        float height =
            targetHeight;

        float width =
            height *
            aspect;

        /*
         * PERFECT처럼 글자가 길어서 width가 너무 커지면
         * 가로만 찌그러뜨리지 않고,
         * 가로/세로를 같은 비율로 함께 줄인다.
         */
        if (width > maxWidth)
        {
            float fitScale =
                maxWidth /
                width;

            width *=
                fitScale;

            height *=
                fitScale;
        }

        judgementRect.sizeDelta =
            new Vector2(
                width,
                height
            );
    }

    private IEnumerator PlayAnimation()
    {
        judgementImage.enabled =
            true;

        float elapsed = 0f;

        while (elapsed <
               popDuration)
        {
            elapsed +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    popDuration
                );

            float easedT =
                EaseOutCubic(
                    t
                );

            float scale =
                Mathf.Lerp(
                    startScale,
                    popScale,
                    easedT
                );

            judgementRect.localScale =
                Vector3.one *
                scale;

            SetAlpha(
                t
            );

            yield return null;
        }

        judgementRect.localScale =
            Vector3.one *
            popScale;

        SetAlpha(
            1f
        );

        elapsed = 0f;

        while (elapsed <
               settleDuration)
        {
            elapsed +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    settleDuration
                );

            float easedT =
                EaseOutCubic(
                    t
                );

            float scale =
                Mathf.Lerp(
                    popScale,
                    1f,
                    easedT
                );

            judgementRect.localScale =
                Vector3.one *
                scale;

            yield return null;
        }

        judgementRect.localScale =
            Vector3.one;

        if (holdDuration >
            0f)
        {
            yield return
                new WaitForSeconds(
                    holdDuration
                );
        }

        elapsed = 0f;

        Vector2 startPosition =
            originalPosition;

        Vector2 targetPosition =
            originalPosition +
            Vector2.up *
            floatDistance;

        while (elapsed <
               fadeDuration)
        {
            elapsed +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    fadeDuration
                );

            float moveT =
                EaseOutCubic(
                    t
                );

            judgementRect.anchoredPosition =
                Vector2.Lerp(
                    startPosition,
                    targetPosition,
                    moveT
                );

            float scale =
                Mathf.Lerp(
                    1f,
                    endScale,
                    t
                );

            judgementRect.localScale =
                Vector3.one *
                scale;

            float alpha =
                1f -
                EaseInQuadratic(
                    t
                );

            SetAlpha(
                alpha
            );

            yield return null;
        }

        HideImmediately();

        animationCoroutine =
            null;
    }

    private void ResetVisual()
    {
        judgementImage.enabled =
            true;

        judgementRect.anchoredPosition =
            originalPosition;

        judgementRect.localScale =
            Vector3.one *
            startScale;

        SetAlpha(
            0f
        );
    }

    private void HideImmediately()
    {
        if (judgementRect != null)
        {
            judgementRect.anchoredPosition =
                originalPosition;

            judgementRect.localScale =
                Vector3.one;
        }

        SetAlpha(
            0f
        );
    }

    private void SetAlpha(
        float alpha
    )
    {
        if (judgementImage == null)
            return;

        Color color =
            judgementImage.color;

        color.a =
            Mathf.Clamp01(
                alpha
            );

        judgementImage.color =
            color;
    }

    private float EaseOutCubic(
        float t
    )
    {
        return
            1f -
            Mathf.Pow(
                1f - t,
                3f
            );
    }

    private float EaseInQuadratic(
        float t
    )
    {
        return
            t * t;
    }
}
