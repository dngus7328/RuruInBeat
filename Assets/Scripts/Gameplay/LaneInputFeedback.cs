using System.Collections;
using UnityEngine;

public class LaneInputFeedback : MonoBehaviour
{
    [Header("Lane")]
    [SerializeField] private KeyCode triggerKey = KeyCode.Q;

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] frames;

    [Header("Playback")]
    [SerializeField] private float framesPerSecond = 24f;
    [SerializeField] private bool hideWhenIdle = true;

    private Coroutine playRoutine;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        ApplyIdleState();
    }

    private void Update()
    {
        if (Input.GetKeyDown(triggerKey))
        {
            Play();
        }
    }

    public void Play()
    {
        if (frames == null || frames.Length == 0 || spriteRenderer == null)
            return;

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
        }

        playRoutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        spriteRenderer.enabled = true;

        float frameDuration = 1f / framesPerSecond;

        for (int i = 0; i < frames.Length; i++)
        {
            spriteRenderer.sprite = frames[i];
            yield return new WaitForSeconds(frameDuration);
        }

        ApplyIdleState();
        playRoutine = null;
    }

    private void ApplyIdleState()
    {
        if (spriteRenderer == null)
            return;

        if (frames != null && frames.Length > 0)
        {
            spriteRenderer.sprite = frames[0];
        }

        spriteRenderer.enabled = !hideWhenIdle;
    }
}