using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class KeyButtonFeedback : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference inputAction;

    [Header("Glow")]
    [SerializeField] private SpriteRenderer glowRenderer;

    [Header("Glow Settings")]
    [SerializeField]
    private Color glowColor =
        new Color(0.85f, 0.75f, 1f, 1f);

    [Range(0f, 1f)]
    [SerializeField] private float peakAlpha = 0.9f;

    [SerializeField] private float flashDuration = 0.04f;
    [SerializeField] private float fadeDuration = 0.12f;

    private Coroutine glowRoutine;

    private void Awake()
    {
        SetGlowAlpha(0f);
    }

    private void OnEnable()
    {
        if (inputAction == null)
            return;

        inputAction.action.performed +=
            OnPressed;

        inputAction.action.Enable();
    }

    private void OnDisable()
    {
        if (inputAction != null)
        {
            inputAction.action.performed -=
                OnPressed;
        }

        if (glowRoutine != null)
        {
            StopCoroutine(
                glowRoutine
            );

            glowRoutine = null;
        }

        SetGlowAlpha(0f);
    }

    private void OnPressed(
        InputAction.CallbackContext context
    )
    {
        if (glowRoutine != null)
        {
            StopCoroutine(
                glowRoutine
            );
        }

        glowRoutine =
            StartCoroutine(
                PlayGlow()
            );
    }

    private IEnumerator PlayGlow()
    {
        // ºü¸£°Ô ¹à¾ÆÁü
        float elapsed = 0f;

        while (elapsed < flashDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    flashDuration
                );

            SetGlowAlpha(
                Mathf.Lerp(
                    0f,
                    peakAlpha,
                    t
                )
            );

            yield return null;
        }

        SetGlowAlpha(
            peakAlpha
        );

        // ÀÜ±¤ÀÌ ºü¸£°Ô »ç¶óÁü
        elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    fadeDuration
                );

            float eased =
                t * t;

            SetGlowAlpha(
                Mathf.Lerp(
                    peakAlpha,
                    0f,
                    eased
                )
            );

            yield return null;
        }

        SetGlowAlpha(0f);

        glowRoutine = null;
    }

    private void SetGlowAlpha(
        float alpha
    )
    {
        if (glowRenderer == null)
            return;

        Color color =
            glowColor;

        color.a =
            Mathf.Clamp01(
                alpha
            );

        glowRenderer.color =
            color;
    }
}