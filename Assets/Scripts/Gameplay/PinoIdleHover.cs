using UnityEngine;

public class PinoIdleHover : MonoBehaviour
{
    [Header("Hover")]
    [SerializeField] private float hoverAmount = 0.035f;

    [Tooltip("1È¸ ¿Õº¹ ¼Óµµ")]
    [SerializeField] private float hoverSpeed = 0.5f;

    [Header("State")]
    [SerializeField] private bool hoverEnabled = true;

    private Vector3 baseLocalPosition;
    private float time;

    private void Awake()
    {
        baseLocalPosition = transform.localPosition;
    }

    private void OnEnable()
    {
        baseLocalPosition = transform.localPosition;
        time = 0f;
    }

    private void Update()
    {
        if (!hoverEnabled)
        {
            transform.localPosition = baseLocalPosition;
            return;
        }

        time += Time.deltaTime;

        float yOffset =
            Mathf.Sin(
                time *
                hoverSpeed *
                Mathf.PI *
                2f
            ) *
            hoverAmount;

        transform.localPosition =
            baseLocalPosition +
            new Vector3(
                0f,
                yOffset,
                0f
            );
    }

    public void SetHoverEnabled(bool enabled)
    {
        hoverEnabled = enabled;

        if (!enabled)
        {
            transform.localPosition =
                baseLocalPosition;
        }
    }

    public void ResetBasePosition()
    {
        baseLocalPosition =
            transform.localPosition;

        time = 0f;
    }
}