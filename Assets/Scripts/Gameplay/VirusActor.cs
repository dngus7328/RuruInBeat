using System.Collections;
using UnityEngine;

public class VirusActor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private VirusSpriteAnimator spriteAnimator;

    [Header("Position")]
    [Tooltip("현재 Transform Y를 완전히 등장한 위치로 사용합니다.")]
    [SerializeField] private float hiddenYOffset = 1.35f;

    [Tooltip("게임 시작 시 화면 위쪽에 숨겨둘지")]
    [SerializeField] private bool startHidden = true;

    [Header("Appear / Move / Retreat")]
    [SerializeField] private float appearDuration = 0.18f;
    [SerializeField] private float moveDuration = 0.14f;
    [SerializeField] private float retreatDuration = 0.16f;

    [Header("Fire Recoil")]
    [Tooltip("발사 순간 위쪽으로 튀는 거리")]
    [SerializeField] private float recoilDistance = 0.10f;

    [SerializeField] private float recoilOutDuration = 0.045f;
    [SerializeField] private float recoilReturnDuration = 0.08f;

    [Tooltip("발사 순간 크기 펀치")]
    [SerializeField] private float fireScalePunch = 0.04f;

    private Coroutine moveRoutine;
    private Coroutine fireRoutine;

    private float visibleY;
    private float hiddenY;

    private Vector3 baseVisualLocalPosition;
    private Vector3 baseVisualLocalScale;

    private bool isVisible;

    public bool IsVisible
    {
        get { return isVisible; }
    }

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer =
                GetComponentInChildren<SpriteRenderer>(true);
        }

        if (spriteAnimator == null)
        {
            spriteAnimator =
                GetComponentInChildren<VirusSpriteAnimator>(true);
        }

        // 실제로 화면에 보이는 SpriteRenderer의 Transform을
        // 비주얼 기준점으로 강제 사용한다.
        // Inspector에 VirusRoot 같은 잘못된 Transform이 들어 있어도
        // 런타임에서 자동으로 바로잡는다.
        if (spriteRenderer != null)
        {
            Transform rendererTransform =
                spriteRenderer.transform;

            if (rendererTransform != transform)
            {
                // SpriteRenderer가 Virus의 자식이 아니면
                // 현재 월드 위치/회전/스케일을 유지한 채 Virus 아래로 붙인다.
                if (!rendererTransform.IsChildOf(transform))
                {
                    rendererTransform.SetParent(
                        transform,
                        true
                    );

                    Debug.LogWarning(
                        "VirusActor: VirusVisual이 Virus의 자식이 아니어서 자동으로 Virus 아래에 연결했습니다."
                    );
                }

                visualRoot =
                    rendererTransform;
            }
            else
            {
                visualRoot =
                    transform;
            }
        }
        else
        {
            visualRoot =
                transform;

            Debug.LogError(
                "VirusActor: SpriteRenderer를 찾지 못했습니다."
            );
        }

        visibleY =
            transform.position.y;

        hiddenY =
            visibleY +
            hiddenYOffset;

        baseVisualLocalPosition =
            visualRoot.localPosition;

        baseVisualLocalScale =
            visualRoot.localScale;

        if (startHidden)
        {
            Vector3 position =
                transform.position;

            position.y =
                hiddenY;

            transform.position =
                position;

            isVisible =
                false;
        }
        else
        {
            isVisible =
                true;
        }
    }

    // =========================================================
    // Appear
    // =========================================================

    public void ShowAt(
        float worldX
    )
    {
        ShowAt(
            worldX,
            appearDuration
        );
    }

    public void ShowAt(
        float worldX,
        float duration
    )
    {
        // 화면에 반드시 보이도록 비주얼/렌더러를 복구한다.
        if (visualRoot != null &&
            !visualRoot.gameObject.activeSelf)
        {
            visualRoot.gameObject.SetActive(true);
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }

        StopMoveRoutine();

        Vector3 start =
            transform.position;

        start.x =
            worldX;

        if (!isVisible)
        {
            start.y =
                hiddenY;
        }

        transform.position =
            start;

        Vector3 target =
            start;

        target.x =
            worldX;

        target.y =
            visibleY;

        isVisible =
            true;

        Debug.Log(
            $"VirusActor ShowAt | X: {worldX:F3} | HiddenY: {hiddenY:F3} | VisibleY: {visibleY:F3}"
        );

        if (spriteAnimator != null)
        {
            spriteAnimator.PlayIdle();
        }

        moveRoutine =
            StartCoroutine(
                MoveTransform(
                    start,
                    target,
                    duration
                )
            );
    }

    // =========================================================
    // Move between attack positions
    // =========================================================

    public void MoveToX(
        float worldX
    )
    {
        MoveToX(
            worldX,
            moveDuration
        );
    }

    public void MoveToX(
        float worldX,
        float duration
    )
    {
        if (!isVisible)
        {
            ShowAt(
                worldX,
                duration
            );

            return;
        }

        StopMoveRoutine();

        Vector3 start =
            transform.position;

        Vector3 target =
            start;

        target.x =
            worldX;

        target.y =
            visibleY;

        moveRoutine =
            StartCoroutine(
                MoveTransform(
                    start,
                    target,
                    duration
                )
            );
    }

    // =========================================================
    // Fire
    // =========================================================

    public void PlayFire()
    {
        if (!isVisible)
            return;

        if (spriteAnimator != null)
        {
            spriteAnimator.PlayCast();
        }

        if (fireRoutine != null)
        {
            // 연속 발사 중 이전 recoil을 끊더라도
            // base 위치로 순간이동시키지 않는다.
            // 현재 보이는 위치에서 다음 recoil을 이어간다.
            StopCoroutine(
                fireRoutine
            );

            fireRoutine =
                null;
        }

        fireRoutine =
            StartCoroutine(
                FireMotion()
            );
    }

    private IEnumerator FireMotion()
    {
        if (visualRoot == null)
            yield break;

        // 연속 발사라면 현재 recoil 중인 위치/크기에서
        // 다음 발사 동작을 자연스럽게 이어간다.
        Vector3 fireStartPosition =
            visualRoot.localPosition;

        Vector3 fireStartScale =
            visualRoot.localScale;

        Vector3 recoilPosition =
            baseVisualLocalPosition +
            Vector3.up *
            recoilDistance;

        Vector3 punchScale =
            baseVisualLocalScale *
            (1f + fireScalePunch);

        float elapsed = 0f;

        while (
            elapsed <
            recoilOutDuration
        )
        {
            elapsed +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    Mathf.Max(
                        0.001f,
                        recoilOutDuration
                    )
                );

            float eased =
                1f -
                Mathf.Pow(
                    1f - t,
                    3f
                );

            visualRoot.localPosition =
                Vector3.Lerp(
                    fireStartPosition,
                    recoilPosition,
                    eased
                );

            visualRoot.localScale =
                Vector3.Lerp(
                    fireStartScale,
                    punchScale,
                    eased
                );

            yield return null;
        }

        elapsed = 0f;

        while (
            elapsed <
            recoilReturnDuration
        )
        {
            elapsed +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    Mathf.Max(
                        0.001f,
                        recoilReturnDuration
                    )
                );

            float eased =
                t * t *
                (3f - 2f * t);

            visualRoot.localPosition =
                Vector3.Lerp(
                    recoilPosition,
                    baseVisualLocalPosition,
                    eased
                );

            visualRoot.localScale =
                Vector3.Lerp(
                    punchScale,
                    baseVisualLocalScale,
                    eased
                );

            yield return null;
        }

        RestoreVisual();

        fireRoutine =
            null;
    }

    // =========================================================
    // Retreat
    // =========================================================

    public void Hide()
    {
        Hide(
            retreatDuration
        );
    }

    public void Hide(
        float duration
    )
    {
        if (!isVisible)
            return;

        StopMoveRoutine();

        Vector3 start =
            transform.position;

        Vector3 target =
            start;

        target.y =
            hiddenY;

        moveRoutine =
            StartCoroutine(
                HideRoutine(
                    start,
                    target,
                    duration
                )
            );
    }

    private IEnumerator HideRoutine(
        Vector3 start,
        Vector3 target,
        float duration
    )
    {
        yield return
            MoveTransform(
                start,
                target,
                duration
            );

        isVisible =
            false;

        RestoreVisual();

        moveRoutine =
            null;
    }

    // =========================================================
    // Common movement
    // =========================================================

    private IEnumerator MoveTransform(
        Vector3 start,
        Vector3 target,
        float duration
    )
    {
        if (duration <= 0f)
        {
            transform.position =
                target;

            moveRoutine =
                null;

            yield break;
        }

        float elapsed = 0f;

        while (
            elapsed <
            duration
        )
        {
            elapsed +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    duration
                );

            float eased =
                t * t *
                (3f - 2f * t);

            transform.position =
                Vector3.Lerp(
                    start,
                    target,
                    eased
                );

            yield return null;
        }

        transform.position =
            target;

        moveRoutine =
            null;
    }

    private void StopMoveRoutine()
    {
        if (moveRoutine == null)
            return;

        StopCoroutine(
            moveRoutine
        );

        moveRoutine =
            null;
    }

    private void RestoreVisual()
    {
        if (visualRoot == null)
            return;

        visualRoot.localPosition =
            baseVisualLocalPosition;

        visualRoot.localScale =
            baseVisualLocalScale;
    }

    // =========================================================
    // Inspector Tests
    // =========================================================

    [ContextMenu("TEST - Show")]
    private void TestShow()
    {
        ShowAt(
            transform.position.x
        );
    }

    [ContextMenu("TEST - Fire")]
    private void TestFire()
    {
        PlayFire();
    }

    [ContextMenu("TEST - Hide")]
    private void TestHide()
    {
        Hide();
    }
}
