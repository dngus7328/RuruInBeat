
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class PinoTiltUI : MonoBehaviour
{
    [Header("움직이는 피노 (비워 두면 이 오브젝트)")]
    [SerializeField] private RectTransform movementTarget;

    [Header("기울일 이미지 (비워 두면 이 오브젝트)")]
    [SerializeField] private RectTransform visualTarget;

    [Header("기울기")]
    [Range(0f, 40f)]
    [SerializeField] private float maxTiltAngle = 13f;

    [Min(1f)]
    [SerializeField] private float horizontalSpeedForMaxTilt = 600f;

    [Min(0f)]
    [SerializeField] private float movementDeadZone = 15f;

    [Header("반응 속도")]
    [Min(0.01f)]
    [SerializeField] private float tiltSmoothTime = 0.10f;

    [Min(0.01f)]
    [SerializeField] private float returnSmoothTime = 0.16f;

    private Vector2 lastPosition;
    private float originalZAngle;
    private float angularVelocity;
    private bool initialized;

    private void Awake()
    {
        if (movementTarget == null)
            movementTarget = GetComponent<RectTransform>();

        if (visualTarget == null)
            visualTarget = GetComponent<RectTransform>();

        originalZAngle = visualTarget.localEulerAngles.z;
        lastPosition = movementTarget.anchoredPosition;
        initialized = true;
    }

    private void OnEnable()
    {
        if (movementTarget != null)
            lastPosition = movementTarget.anchoredPosition;

        angularVelocity = 0f;
    }

    private void LateUpdate()
    {
        if (!initialized || movementTarget == null || visualTarget == null)
            return;

        Vector2 currentPosition = movementTarget.anchoredPosition;
        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f)
        {
            lastPosition = currentPosition;
            return;
        }

        float horizontalSpeed =
            (currentPosition.x - lastPosition.x) / deltaTime;

        lastPosition = currentPosition;

        float desiredTilt = 0f;

        if (Mathf.Abs(horizontalSpeed) >= movementDeadZone)
        {
            float strength = Mathf.Clamp(
                horizontalSpeed / horizontalSpeedForMaxTilt,
                -1f,
                1f
            );

            desiredTilt = -strength * maxTiltAngle;
        }

        float targetAngle = originalZAngle + desiredTilt;

        float smoothTime = Mathf.Approximately(desiredTilt, 0f)
            ? returnSmoothTime
            : tiltSmoothTime;

        float nextAngle = Mathf.SmoothDampAngle(
            visualTarget.localEulerAngles.z,
            targetAngle,
            ref angularVelocity,
            smoothTime,
            Mathf.Infinity,
            deltaTime
        );

        visualTarget.localRotation =
            Quaternion.Euler(0f, 0f, nextAngle);
    }

    private void OnDisable()
    {
        if (initialized && visualTarget != null)
        {
            visualTarget.localRotation =
                Quaternion.Euler(0f, 0f, originalZAngle);
        }

        angularVelocity = 0f;
    }
}
