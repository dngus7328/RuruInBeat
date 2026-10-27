using System.Collections;
using UnityEngine;

public class LaneHitFlash : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Animation")]
    [SerializeField] private float appearDuration = 0.04f;
    [SerializeField] private float holdDuration = 0.04f;
    [SerializeField] private float disappearDuration = 0.15f;

    [SerializeField] private float startScaleY = 0.2f;
    [SerializeField] private float endScaleY = 1.0f;

    [Header("Judgement Strength")]
    [SerializeField] private float perfectAlpha = 1.0f;
    [SerializeField] private float greatAlpha = 0.7f;
    [SerializeField] private float goodAlpha = 0.45f;

    private Vector3 originalScale;
    private Coroutine animationRoutine;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        originalScale = transform.localScale;

        SetAlpha(0f);
    }

    public void Play(NoteJudgement judgement)
    {
        Debug.Log(
            $"[LaneHitFlash] {gameObject.name} Play 호출됨 / {judgement}"
        );

        if (judgement == NoteJudgement.Miss)
            return;

        if (spriteRenderer == null)
        {
            Debug.LogError(
                $"[LaneHitFlash] {gameObject.name} SpriteRenderer 없음"
            );

            return;
        }

        if (spriteRenderer.sprite == null)
        {
            Debug.LogError(
                $"[LaneHitFlash] {gameObject.name} Sprite 없음"
            );

            return;
        }

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
        }

        animationRoutine = StartCoroutine(
            PlayRoutine(
                GetAlphaForJudgement(judgement)
            )
        );
    }

    private IEnumerator PlayRoutine(float maxAlpha)
    {
        Debug.Log(
            $"[LaneHitFlash] {gameObject.name} 애니메이션 시작"
        );

        Vector3 startScale = originalScale;
        startScale.y =
            originalScale.y * startScaleY;

        transform.localScale = startScale;

        SetAlpha(0f);

        float elapsed = 0f;

        while (elapsed < appearDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                elapsed / appearDuration
            );

            Vector3 scale = originalScale;

            scale.y = Mathf.Lerp(
                originalScale.y * startScaleY,
                originalScale.y * endScaleY,
                t
            );

            transform.localScale = scale;

            SetAlpha(
                Mathf.Lerp(
                    0f,
                    maxAlpha,
                    t
                )
            );

            yield return null;
        }

        transform.localScale = originalScale;
        SetAlpha(maxAlpha);

        yield return new WaitForSecondsRealtime(
            holdDuration
        );

        elapsed = 0f;

        while (elapsed < disappearDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                elapsed / disappearDuration
            );

            SetAlpha(
                Mathf.Lerp(
                    maxAlpha,
                    0f,
                    t
                )
            );

            yield return null;
        }

        transform.localScale = originalScale;

        SetAlpha(0f);

        animationRoutine = null;

        Debug.Log(
            $"[LaneHitFlash] {gameObject.name} 애니메이션 종료"
        );
    }

    private float GetAlphaForJudgement(
        NoteJudgement judgement
    )
    {
        switch (judgement)
        {
            case NoteJudgement.Perfect:
                return perfectAlpha;

            case NoteJudgement.Great:
                return greatAlpha;

            case NoteJudgement.Good:
                return goodAlpha;

            default:
                return 0f;
        }
    }

    private void SetAlpha(float alpha)
    {
        if (spriteRenderer == null)
            return;

        Color color = spriteRenderer.color;

        color.a = Mathf.Clamp01(alpha);

        spriteRenderer.color = color;
    }
}