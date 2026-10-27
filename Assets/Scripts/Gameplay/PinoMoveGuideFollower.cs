using UnityEngine;

public class PinoMoveGuideFollower : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform pino;

    [Header("Follow Position")]
    [SerializeField] private float xOffset = 0f;

    [Tooltip("피노 기준 가이드의 고정 높이. 음수면 피노보다 아래.")]
    [SerializeField] private float yOffset = -0.15f;

    private float basePinoY;
    private float fixedZ;

    private void Awake()
    {
        if (pino != null)
        {
            // 플레이 시작 당시 피노의 기본 높이만 기억
            basePinoY = pino.position.y;
        }

        fixedZ = transform.position.z;
    }

    private void LateUpdate()
    {
        if (pino == null)
            return;

        Vector3 position = transform.position;

        // 좌우 슬롯 이동만 현재 피노를 따라감
        position.x =
            pino.position.x +
            xOffset;

        // Y는 피노의 "시작 위치" 기준으로만 고정
        // Idle / Hit / Fail / Clear 움직임은 따라가지 않음
        position.y =
            basePinoY +
            yOffset;

        position.z =
            fixedZ;

        transform.position =
            position;
    }
}