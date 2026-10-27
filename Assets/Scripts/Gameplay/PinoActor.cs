using System.Collections;
using UnityEngine;

public class PinoActor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PinoController controller;
    [SerializeField] private PinoSpriteAnimator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Transform visualRoot;

    [Header("Hit Effect")]
    [SerializeField]
    private Color hitFlashColor =
        new Color(1f, 0.15f, 0.15f, 1f);

    [SerializeField]
    private int hitFlashCount = 3;

    [SerializeField]
    private float hitFlashDuration = 0.10f;

    [SerializeField]
    private float hitShakeDistance = 0.04f;

    [Header("Invincibility")]
    [SerializeField]
    private float invincibilityDuration = 0.8f;

    [Header("Fail Motion")]
    [SerializeField]
    private float failMoveDistance = 0.65f;

    [SerializeField]
    private float failMoveDuration = 0.55f;

    [SerializeField]
    private float failTiltAngle = 14f;

    [SerializeField]
    private float failWobbleAngle = 4f;

    [Header("Clear Motion")]
    [SerializeField]
    private float clearMoveDistance = 0.85f;

    [SerializeField]
    private float clearMoveDuration = 0.55f;

    [SerializeField]
    private float clearFloatAmount = 0.06f;

    private Coroutine hitRoutine;
    private Coroutine stateRoutine;

    private Color originalColor;

    private Vector3 visualBasePosition;
    private Quaternion visualBaseRotation;

    private Vector3 stateStartPosition;
    private bool hasStateStartPosition;

    private float invincibleUntilTime;

    public bool IsInvincible
    {
        get
        {
            return Time.time < invincibleUntilTime;
        }
    }

    private void Awake()
    {
        if (controller == null)
        {
            controller =
                GetComponent<PinoController>();
        }

        if (animator == null)
        {
            animator =
                GetComponentInChildren<PinoSpriteAnimator>(
                    true
                );
        }

        // =====================================================
        // 중요:
        // 실제 PinoSpriteAnimator가 사용하는 오브젝트의
        // SpriteRenderer를 우선 사용한다.
        // =====================================================

        if (animator != null)
        {
            SpriteRenderer animatorRenderer =
                animator.GetComponent<SpriteRenderer>();

            if (animatorRenderer != null)
            {
                spriteRenderer =
                    animatorRenderer;
            }
        }

        if (spriteRenderer == null)
        {
            spriteRenderer =
                GetComponentInChildren<SpriteRenderer>(
                    true
                );
        }

        if (spriteRenderer != null)
        {
            visualRoot =
                spriteRenderer.transform;

            originalColor =
                spriteRenderer.color;
        }

        if (visualRoot != null)
        {
            visualBasePosition =
                visualRoot.localPosition;

            visualBaseRotation =
                visualRoot.localRotation;
        }
    }

    // =========================================================
    // HIT
    // =========================================================

    public void TakeHit()
    {
        TryTakeHit();
    }

    public bool TryTakeHit()
    {
        if (IsInvincible)
        {
            return false;
        }

        if (animator != null)
        {
            if (animator.CurrentState ==
                    PinoAnimationState.Fail ||
                animator.CurrentState ==
                    PinoAnimationState.Clear)
            {
                return false;
            }
        }

        // 무적 시작
        invincibleUntilTime =
            Time.time +
            invincibilityDuration;

        // Hit 애니메이션
        if (animator != null)
        {
            animator.PlayHit();
        }

        CancelHitEffect();

        hitRoutine =
            StartCoroutine(
                HitEffect()
            );

        return true;
    }

    private IEnumerator HitEffect()
    {
        if (spriteRenderer == null)
        {
            Debug.LogWarning(
                "PinoActor: 피노 SpriteRenderer를 찾지 못했습니다."
            );

            yield break;
        }

        // =====================================================
        // 확실하게 보이는 빨강 점멸
        // =====================================================

        for (
            int flash = 0;
            flash < hitFlashCount;
            flash++
        )
        {
            // 완전 빨강
            Color red =
                hitFlashColor;

            red.a =
                originalColor.a;

            spriteRenderer.color =
                red;

            yield return
                new WaitForSeconds(
                    hitFlashDuration
                );

            // 원래 색
            spriteRenderer.color =
                originalColor;

            yield return
                new WaitForSeconds(
                    hitFlashDuration * 0.55f
                );
        }

        RestoreHitVisual();

        hitRoutine = null;
    }

    // =========================================================
    // FAIL
    // =========================================================

    public void PlayFail()
    {
        invincibleUntilTime = 0f;

        CancelHitEffect();
        StopStateMotion();

        if (controller != null)
        {
            controller.SetControlEnabled(
                false
            );
        }

        if (animator != null)
        {
            animator.PlayFail();
        }

        SaveStateStartPosition();

        RestoreVisualTransform();

        Vector3 start =
            transform.position;

        Vector3 target =
            stateStartPosition +
            Vector3.down *
            failMoveDistance;

        stateRoutine =
            StartCoroutine(
                FailMotion(
                    start,
                    target
                )
            );
    }

    private IEnumerator FailMotion(
        Vector3 startPosition,
        Vector3 targetPosition
    )
    {
        float elapsed = 0f;

        while (
            elapsed <
            failMoveDuration
        )
        {
            elapsed +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    failMoveDuration
                );

            float moveT =
                t * t * t;

            transform.position =
                Vector3.Lerp(
                    startPosition,
                    targetPosition,
                    moveT
                );

            if (visualRoot != null)
            {
                float baseTilt =
                    Mathf.Lerp(
                        0f,
                        failTiltAngle,
                        t
                    );

                float wobble =
                    Mathf.Sin(
                        t *
                        Mathf.PI *
                        5f
                    );

                wobble *=
                    failWobbleAngle;

                wobble *=
                    1f - t;

                visualRoot.localRotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        baseTilt +
                        wobble
                    );
            }

            yield return null;
        }

        transform.position =
            targetPosition;

        if (visualRoot != null)
        {
            visualRoot.localRotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    failTiltAngle
                );
        }

        stateRoutine = null;
    }

    // =========================================================
    // CLEAR
    // =========================================================

    public void PlayClear()
    {
        invincibleUntilTime = 0f;

        CancelHitEffect();
        StopStateMotion();

        if (controller != null)
        {
            controller.SetControlEnabled(
                false
            );
        }

        if (animator != null)
        {
            animator.PlayClear();
        }

        SaveStateStartPosition();

        RestoreVisualTransform();

        Vector3 start =
            transform.position;

        Vector3 target =
            stateStartPosition +
            Vector3.up *
            clearMoveDistance;

        stateRoutine =
            StartCoroutine(
                ClearMotion(
                    start,
                    target
                )
            );
    }

    private IEnumerator ClearMotion(
        Vector3 startPosition,
        Vector3 targetPosition
    )
    {
        float elapsed = 0f;

        while (
            elapsed <
            clearMoveDuration
        )
        {
            elapsed +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    clearMoveDuration
                );

            float moveT =
                1f -
                Mathf.Pow(
                    1f - t,
                    3f
                );

            Vector3 position =
                Vector3.Lerp(
                    startPosition,
                    targetPosition,
                    moveT
                );

            float floatOffset =
                Mathf.Sin(
                    t *
                    Mathf.PI *
                    3f
                );

            floatOffset *=
                clearFloatAmount;

            floatOffset *=
                1f - t;

            position.y +=
                floatOffset;

            transform.position =
                position;

            if (visualRoot != null)
            {
                float tilt =
                    Mathf.Sin(
                        t *
                        Mathf.PI *
                        4f
                    );

                tilt *= 2f;

                tilt *=
                    1f - t;

                visualRoot.localRotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        tilt
                    );
            }

            yield return null;
        }

        transform.position =
            targetPosition;

        if (visualRoot != null)
        {
            visualRoot.localRotation =
                visualBaseRotation;
        }

        stateRoutine = null;
    }

    // =========================================================
    // RESET
    // =========================================================

    public void ResetActor()
    {
        invincibleUntilTime = 0f;

        CancelHitEffect();
        StopStateMotion();

        if (hasStateStartPosition)
        {
            transform.position =
                stateStartPosition;

            hasStateStartPosition =
                false;
        }

        RestoreHitVisual();
        RestoreVisualTransform();

        if (controller != null)
        {
            controller.SetControlEnabled(
                true
            );
        }

        if (animator != null)
        {
            animator.PlayIdle();
        }
    }

    // =========================================================
    // Utility
    // =========================================================

    private void SaveStateStartPosition()
    {
        if (hasStateStartPosition)
            return;

        stateStartPosition =
            transform.position;

        hasStateStartPosition =
            true;
    }

    private void CancelHitEffect()
    {
        if (hitRoutine != null)
        {
            StopCoroutine(
                hitRoutine
            );

            hitRoutine = null;
        }

        RestoreHitVisual();
    }

    private void RestoreHitVisual()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color =
                originalColor;
        }

        if (visualRoot != null)
        {
            visualRoot.localPosition =
                visualBasePosition;
        }
    }

    private void RestoreVisualTransform()
    {
        if (visualRoot == null)
            return;

        visualRoot.localPosition =
            visualBasePosition;

        visualRoot.localRotation =
            visualBaseRotation;
    }

    private void StopStateMotion()
    {
        if (stateRoutine == null)
            return;

        StopCoroutine(
            stateRoutine
        );

        stateRoutine = null;
    }

    // =========================================================
    // TEST
    // =========================================================

    [ContextMenu("TEST - Hit")]
    private void TestHit()
    {
        TakeHit();
    }

    [ContextMenu("TEST - Fail")]
    private void TestFail()
    {
        PlayFail();
    }

    [ContextMenu("TEST - Clear")]
    private void TestClear()
    {
        PlayClear();
    }

    [ContextMenu("TEST - Reset")]
    private void TestReset()
    {
        ResetActor();
    }
}