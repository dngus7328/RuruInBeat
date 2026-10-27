using UnityEngine;

public enum PinoAnimationState
{
    Idle,
    Hit,
    Fail,
    Clear
}

public class PinoSpriteAnimator : MonoBehaviour
{
    [Header("Renderer")]
    [SerializeField]
    private SpriteRenderer spriteRenderer;

    // =========================================================
    // Idle
    // =========================================================

    [Header("Idle")]
    [SerializeField]
    private Sprite[] idleFrames;

    [SerializeField]
    private float idleFPS = 6f;

    [SerializeField]
    private bool idlePingPong = true;

    // =========================================================
    // Hit
    // =========================================================

    [Header("Hit")]
    [SerializeField]
    private Sprite[] hitFrames;

    [SerializeField]
    private float hitFPS = 14f;

    // =========================================================
    // Fail
    // =========================================================

    [Header("Fail")]
    [SerializeField]
    private Sprite[] failFrames;

    [SerializeField]
    private float failFPS = 10f;

    // =========================================================
    // Clear
    // =========================================================

    [Header("Clear")]
    [SerializeField]
    private Sprite[] clearFrames;

    [SerializeField]
    private float clearFPS = 8f;

    [SerializeField]
    private bool clearLoop = true;

    // =========================================================
    // Runtime
    // =========================================================

    private PinoAnimationState currentState =
        PinoAnimationState.Idle;

    private int frameIndex;
    private int idleDirection = 1;

    private float timer;

    // =========================================================
    // Public
    // =========================================================

    public PinoAnimationState CurrentState
    {
        get
        {
            return currentState;
        }
    }

    public bool IsIdle
    {
        get
        {
            return
                currentState ==
                PinoAnimationState.Idle;
        }
    }

    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer =
                GetComponent<SpriteRenderer>();
        }
    }

    private void Start()
    {
        PlayIdle();
    }

    private void Update()
    {
        Sprite[] frames =
            GetCurrentFrames();

        if (frames == null ||
            frames.Length == 0)
        {
            return;
        }

        float fps =
            GetCurrentFPS();

        if (fps <= 0f)
            return;

        timer +=
            Time.deltaTime;

        float frameDuration =
            1f / fps;

        while (timer >= frameDuration)
        {
            timer -=
                frameDuration;

            AdvanceFrame(
                frames
            );
        }
    }

    // =========================================================
    // Play
    // =========================================================

    public void PlayIdle()
    {
        currentState =
            PinoAnimationState.Idle;

        frameIndex = 0;
        idleDirection = 1;
        timer = 0f;

        ApplyFrame(
            idleFrames
        );
    }

    public void PlayHit()
    {
        if (currentState ==
            PinoAnimationState.Fail)
        {
            return;
        }

        if (currentState ==
            PinoAnimationState.Clear)
        {
            return;
        }

        currentState =
            PinoAnimationState.Hit;

        frameIndex = 0;
        timer = 0f;

        ApplyFrame(
            hitFrames
        );
    }

    public void PlayFail()
    {
        currentState =
            PinoAnimationState.Fail;

        frameIndex = 0;
        timer = 0f;

        ApplyFrame(
            failFrames
        );
    }

    public void PlayClear()
    {
        currentState =
            PinoAnimationState.Clear;

        frameIndex = 0;
        timer = 0f;

        ApplyFrame(
            clearFrames
        );
    }

    // =========================================================
    // Frame
    // =========================================================

    private void AdvanceFrame(
        Sprite[] frames
    )
    {
        switch (currentState)
        {
            case PinoAnimationState.Idle:
                AdvanceIdle(
                    frames
                );
                break;

            case PinoAnimationState.Hit:
                frameIndex++;

                if (frameIndex >=
                    frames.Length)
                {
                    PlayIdle();
                    return;
                }

                ApplyFrame(
                    frames
                );

                break;

            case PinoAnimationState.Fail:
                if (frameIndex <
                    frames.Length - 1)
                {
                    frameIndex++;

                    ApplyFrame(
                        frames
                    );
                }

                break;

            case PinoAnimationState.Clear:
                frameIndex++;

                if (frameIndex >=
                    frames.Length)
                {
                    if (clearLoop)
                    {
                        frameIndex = 0;
                    }
                    else
                    {
                        frameIndex =
                            frames.Length - 1;
                    }
                }

                ApplyFrame(
                    frames
                );

                break;
        }
    }

    private void AdvanceIdle(
        Sprite[] frames
    )
    {
        if (!idlePingPong ||
            frames.Length <= 1)
        {
            frameIndex++;

            if (frameIndex >=
                frames.Length)
            {
                frameIndex = 0;
            }

            ApplyFrame(
                frames
            );

            return;
        }

        frameIndex +=
            idleDirection;

        if (frameIndex >=
            frames.Length)
        {
            frameIndex =
                Mathf.Max(
                    0,
                    frames.Length - 2
                );

            idleDirection =
                -1;
        }
        else if (frameIndex < 0)
        {
            frameIndex =
                Mathf.Min(
                    1,
                    frames.Length - 1
                );

            idleDirection =
                1;
        }

        ApplyFrame(
            frames
        );
    }

    // =========================================================
    // Frames / FPS
    // =========================================================

    private Sprite[] GetCurrentFrames()
    {
        switch (currentState)
        {
            case PinoAnimationState.Idle:
                return idleFrames;

            case PinoAnimationState.Hit:
                return hitFrames;

            case PinoAnimationState.Fail:
                return failFrames;

            case PinoAnimationState.Clear:
                return clearFrames;
        }

        return null;
    }

    private float GetCurrentFPS()
    {
        switch (currentState)
        {
            case PinoAnimationState.Idle:
                return idleFPS;

            case PinoAnimationState.Hit:
                return hitFPS;

            case PinoAnimationState.Fail:
                return failFPS;

            case PinoAnimationState.Clear:
                return clearFPS;
        }

        return 0f;
    }

    private void ApplyFrame(
        Sprite[] frames
    )
    {
        if (spriteRenderer == null)
            return;

        if (frames == null ||
            frames.Length == 0)
        {
            return;
        }

        frameIndex =
            Mathf.Clamp(
                frameIndex,
                0,
                frames.Length - 1
            );

        spriteRenderer.sprite =
            frames[frameIndex];
    }
}