using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class LaneInputFeedback : MonoBehaviour
{
    [Header("Input Action")]
    [SerializeField] private string actionName = "Lane_Q";

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] frames;

    [Header("Playback")]
    [SerializeField] private float framesPerSecond = 24f;
    [SerializeField] private bool hideWhenIdle = true;

    private Coroutine playRoutine;
    private InputAction inputAction;
    private bool subscribed;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer =
                GetComponent<SpriteRenderer>();
        }

        ApplyIdleState();
    }

    private void Start()
    {
        SubscribeInput();
    }

    private void OnEnable()
    {
        if (InputBindingManager.Instance != null)
        {
            SubscribeInput();
        }
    }

    private void OnDisable()
    {
        UnsubscribeInput();
    }

    private void OnDestroy()
    {
        UnsubscribeInput();
    }

    // =========================================================
    // Input
    // =========================================================

    private void SubscribeInput()
    {
        if (subscribed)
            return;

        if (InputBindingManager.Instance == null)
        {
            Debug.LogError(
                $"{name}: InputBindingManager가 없습니다."
            );

            return;
        }

        inputAction =
            InputBindingManager.Instance.GetAction(
                actionName
            );

        if (inputAction == null)
        {
            Debug.LogError(
                $"{name}: Input Action을 찾지 못했습니다. " +
                $"Action = {actionName}"
            );

            return;
        }

        inputAction.performed += OnInputPerformed;

        inputAction.Enable();

        subscribed = true;
    }

    private void UnsubscribeInput()
    {
        if (!subscribed)
            return;

        if (inputAction != null)
        {
            inputAction.performed -= OnInputPerformed;
        }

        subscribed = false;
    }

    private void OnInputPerformed(
        InputAction.CallbackContext context
    )
    {
        if (PauseController.GameplayInputBlocked)
            return;

        Play();
    }

    // =========================================================
    // Feedback
    // =========================================================

    public void Play()
    {
        if (
            frames == null ||
            frames.Length == 0 ||
            spriteRenderer == null
        )
        {
            return;
        }

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
        }

        playRoutine =
            StartCoroutine(
                PlayRoutine()
            );
    }

    private IEnumerator PlayRoutine()
    {
        spriteRenderer.enabled = true;

        float frameDuration =
            1f / framesPerSecond;

        for (
            int i = 0;
            i < frames.Length;
            i++
        )
        {
            spriteRenderer.sprite =
                frames[i];

            yield return
                new WaitForSeconds(
                    frameDuration
                );
        }

        ApplyIdleState();

        playRoutine = null;
    }

    private void ApplyIdleState()
    {
        if (spriteRenderer == null)
            return;

        if (
            frames != null &&
            frames.Length > 0
        )
        {
            spriteRenderer.sprite =
                frames[0];
        }

        spriteRenderer.enabled =
            !hideWhenIdle;
    }
}