using System.Collections;
using UnityEngine;

public enum VirusAnimationState
{
    Idle,
    Cast
}

public class VirusSpriteAnimator : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Idle")]
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField] private float idleFps = 8f;

    [Header("Cast")]
    [SerializeField] private Sprite[] castFrames;
    [SerializeField] private float castFps = 12f;

    private Coroutine animationRoutine;

    private VirusAnimationState currentState =
        VirusAnimationState.Idle;

    public VirusAnimationState CurrentState
    {
        get { return currentState; }
    }

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer =
                GetComponent<SpriteRenderer>();

            if (spriteRenderer == null)
            {
                spriteRenderer =
                    GetComponentInChildren<SpriteRenderer>(true);
            }
        }
    }

    private void OnEnable()
    {
        PlayIdle();
    }

    // =========================================================
    // Idle
    // =========================================================

    public void PlayIdle()
    {
        currentState =
            VirusAnimationState.Idle;

        StopCurrentAnimation();

        if (idleFrames == null ||
            idleFrames.Length == 0)
        {
            return;
        }

        animationRoutine =
            StartCoroutine(
                PlayLoop(
                    idleFrames,
                    idleFps
                )
            );
    }

    // =========================================================
    // Cast
    // =========================================================

    public void PlayCast()
    {
        // 연속 발사 중 이미 Cast가 재생되고 있다면
        // 매 발사마다 0프레임으로 되감지 않는다.
        // 기존 Cast를 끝까지 재생시켜 끊기는 느낌을 막는다.
        if (currentState ==
                VirusAnimationState.Cast &&
            animationRoutine != null)
        {
            return;
        }

        currentState =
            VirusAnimationState.Cast;

        StopCurrentAnimation();

        if (castFrames == null ||
            castFrames.Length == 0)
        {
            PlayIdle();
            return;
        }

        animationRoutine =
            StartCoroutine(
                PlayCastThenIdle()
            );
    }

    private IEnumerator PlayCastThenIdle()
    {
        float frameTime =
            1f /
            Mathf.Max(
                1f,
                castFps
            );

        for (int i = 0;
             i < castFrames.Length;
             i++)
        {
            if (spriteRenderer != null &&
                castFrames[i] != null)
            {
                spriteRenderer.sprite =
                    castFrames[i];
            }

            yield return
                new WaitForSeconds(
                    frameTime
                );
        }

        animationRoutine =
            null;

        PlayIdle();
    }

    // =========================================================
    // Loop
    // =========================================================

    private IEnumerator PlayLoop(
        Sprite[] frames,
        float fps
    )
    {
        float frameTime =
            1f /
            Mathf.Max(
                1f,
                fps
            );

        int index = 0;

        while (true)
        {
            if (spriteRenderer != null &&
                frames[index] != null)
            {
                spriteRenderer.sprite =
                    frames[index];
            }

            index++;

            if (index >=
                frames.Length)
            {
                index = 0;
            }

            yield return
                new WaitForSeconds(
                    frameTime
                );
        }
    }

    // =========================================================
    // Common
    // =========================================================

    private void StopCurrentAnimation()
    {
        if (animationRoutine == null)
            return;

        StopCoroutine(
            animationRoutine
        );

        animationRoutine =
            null;
    }

    // =========================================================
    // Inspector Test
    // =========================================================

    [ContextMenu("TEST - Idle")]
    private void TestIdle()
    {
        PlayIdle();
    }

    [ContextMenu("TEST - Cast")]
    private void TestCast()
    {
        PlayCast();
    }
}
