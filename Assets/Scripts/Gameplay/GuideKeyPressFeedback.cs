using UnityEngine;

public class GuideKeyPressFeedback : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Transform visualRoot;

    [Header("Press Scale")]
    [Tooltip("원래 크기에 곱해지는 값입니다. 1.04 = 4% 확대")]
    [SerializeField] private float pressScaleMultiplier = 1.04f;

    [Header("Timing")]
    [SerializeField] private float growDuration = 0.035f;
    [SerializeField] private float holdDuration = 0.015f;
    [SerializeField] private float returnDuration = 0.08f;

    private Vector3 originalScale;

    private bool blocked;

    private enum State
    {
        Idle,
        Growing,
        Holding,
        Returning
    }

    private State state = State.Idle;

    private float timer;

    private Vector3 pressedScale;

    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        originalScale =
            visualRoot.localScale;

        CalculatePressedScale();
    }

    private void OnEnable()
    {
        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        originalScale =
            visualRoot.localScale;

        CalculatePressedScale();

        state = State.Idle;
        timer = 0f;
    }

    private void Update()
    {
        if (visualRoot == null)
            return;

        switch (state)
        {
            case State.Idle:
                break;

            case State.Growing:
                UpdateGrowing();
                break;

            case State.Holding:
                UpdateHolding();
                break;

            case State.Returning:
                UpdateReturning();
                break;
        }
    }

    // =========================================================
    // Scale
    // =========================================================

    private void CalculatePressedScale()
    {
        pressedScale =
            new Vector3(
                originalScale.x * pressScaleMultiplier,
                originalScale.y * pressScaleMultiplier,
                originalScale.z
            );
    }

    // =========================================================
    // Press
    // =========================================================

    public void PlayPress()
    {
        if (blocked)
            return;

        CalculatePressedScale();

        timer = 0f;
        state = State.Growing;
    }

    // =========================================================
    // Grow
    // =========================================================

    private void UpdateGrowing()
    {
        timer +=
            Time.unscaledDeltaTime;

        float duration =
            Mathf.Max(
                0.001f,
                growDuration
            );

        float t =
            Mathf.Clamp01(
                timer / duration
            );

        float eased =
            1f -
            Mathf.Pow(
                1f - t,
                3f
            );

        visualRoot.localScale =
            Vector3.Lerp(
                originalScale,
                pressedScale,
                eased
            );

        if (t >= 1f)
        {
            visualRoot.localScale =
                pressedScale;

            timer = 0f;
            state = State.Holding;
        }
    }

    // =========================================================
    // Hold
    // =========================================================

    private void UpdateHolding()
    {
        timer +=
            Time.unscaledDeltaTime;

        if (timer >= holdDuration)
        {
            timer = 0f;
            state = State.Returning;
        }
    }

    // =========================================================
    // Return
    // =========================================================

    private void UpdateReturning()
    {
        timer +=
            Time.unscaledDeltaTime;

        float duration =
            Mathf.Max(
                0.001f,
                returnDuration
            );

        float t =
            Mathf.Clamp01(
                timer / duration
            );

        float eased =
            1f -
            Mathf.Pow(
                1f - t,
                3f
            );

        visualRoot.localScale =
            Vector3.Lerp(
                pressedScale,
                originalScale,
                eased
            );

        if (t >= 1f)
        {
            visualRoot.localScale =
                originalScale;

            timer = 0f;
            state = State.Idle;
        }
    }

    // =========================================================
    // Block
    // =========================================================

    public void SetBlocked(bool value)
    {
        blocked = value;

        if (blocked &&
            state != State.Idle)
        {
            ForceReset();
        }
    }

    // =========================================================
    // Reset
    // =========================================================

    public void ForceReset()
    {
        state = State.Idle;
        timer = 0f;

        if (visualRoot != null)
        {
            visualRoot.localScale =
                originalScale;
        }
    }
}