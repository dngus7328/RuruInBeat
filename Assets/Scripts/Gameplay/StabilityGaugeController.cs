using UnityEngine;

public class StabilityGaugeController : MonoBehaviour
{
    [Header("Gauge")]
    [SerializeField] private Transform gaugeFill;
    [SerializeField] private SpriteRenderer gaugeFillRenderer;

    [Header("Perfect Glow - Quad")]
    [SerializeField] private Transform gaugeGlow;
    [SerializeField] private Renderer gaugeGlowRenderer;

    [Header("Start Stability")]
    [Range(0f, 100f)]
    [SerializeField] private float startStabilityPercent = 70f;

    [Header("Judgement Change")]
    [SerializeField] private float perfectRecovery = 0.05f;
    [SerializeField] private float greatRecovery = 0.02f;
    [SerializeField] private float goodLoss = 0.25f;

    [Header("Gauge Animation")]
    [SerializeField] private float drainSmoothTime = 0.38f;
    [SerializeField] private float recoverySmoothTime = 0.22f;
    [SerializeField] private float maxVisualSpeed = 100f;

    [Header("Gauge Color")]
    [SerializeField] private float cautionThreshold = 45f;
    [SerializeField] private float dangerThreshold = 25f;
    [SerializeField] private float criticalThreshold = 10f;

    [SerializeField]
    private Color healthyColor =
        new Color(0.55f, 0.78f, 1.00f, 1f);

    [SerializeField]
    private Color cautionColor =
        new Color(0.95f, 0.85f, 0.45f, 1f);

    [SerializeField]
    private Color dangerColor =
        new Color(1.00f, 0.55f, 0.35f, 1f);

    [SerializeField]
    private Color criticalColor =
        new Color(1.00f, 0.28f, 0.35f, 1f);

    [Header("Perfect Glow")]
    [SerializeField]
    private Color perfectGlowColor =
        new Color(0.92f, 0.82f, 1.00f, 1f);

    [SerializeField] private float perfectGlowDuration = 0.30f;
    [SerializeField] private float perfectGlowIntensity = 1.2f;

    private const float MIN_STABILITY = 0f;
    private const float MAX_STABILITY = 100f;

    private static readonly int GlowIntensityID =
        Shader.PropertyToID("_Intensity");

    private static readonly int GlowColorID =
        Shader.PropertyToID("_Color");

    private float currentStabilityPercent;
    private float displayedStabilityPercent;
    private float displayVelocity;

    private int totalScoreUnitCount;

    private Vector3 fullFillScale;

    private Vector3 fullGlowScale;
    private Vector3 fullGlowLocalPosition;
    private float fullGlowBottomY;

    private float perfectGlowTimer;

    private MaterialPropertyBlock glowPropertyBlock;

    public float CurrentStability =>
        currentStabilityPercent;

    public float StabilityPercent =>
        currentStabilityPercent;

    public int TotalScoreUnitCount =>
        totalScoreUnitCount;

    public float LossPerScoreUnit
    {
        get
        {
            if (totalScoreUnitCount <= 0)
                return 0f;

            return
                100f /
                totalScoreUnitCount;
        }
    }

    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        if (
            gaugeFill == null ||
            gaugeFillRenderer == null
        )
        {
            Debug.LogError(
                "StabilityGaugeController: GaugeFill 연결을 확인하세요.",
                this
            );

            enabled = false;
            return;
        }

        fullFillScale =
            gaugeFill.localScale;

        if (
            gaugeGlow != null &&
            gaugeGlowRenderer != null
        )
        {
            fullGlowScale =
                gaugeGlow.localScale;

            fullGlowLocalPosition =
                gaugeGlow.localPosition;

            fullGlowBottomY =
                fullGlowLocalPosition.y -
                (fullGlowScale.y * 0.5f);

            glowPropertyBlock =
                new MaterialPropertyBlock();

            SetGlow(
                0f,
                perfectGlowColor
            );
        }

        ResetStability();
    }

    private void Update()
    {
        UpdateGaugeAnimation();
        UpdatePerfectGlow();
    }

    // =========================================================
    // Total Score Units
    // =========================================================

    public void SetTotalScoreUnitCount(
        int count
    )
    {
        totalScoreUnitCount =
            Mathf.Max(
                0,
                count
            );

        if (totalScoreUnitCount > 0)
        {
            Debug.Log(
                $"STABILITY UNIT | " +
                $"Total: {totalScoreUnitCount} | " +
                $"Loss Per MISS/HIT: {LossPerScoreUnit:F4}%"
            );
        }
    }

    // =========================================================
    // Gauge Animation
    // =========================================================

    private void UpdateGaugeAnimation()
    {
        float smoothTime;

        if (
            currentStabilityPercent <
            displayedStabilityPercent
        )
        {
            smoothTime =
                drainSmoothTime;
        }
        else
        {
            smoothTime =
                recoverySmoothTime;
        }

        displayedStabilityPercent =
            Mathf.SmoothDamp(
                displayedStabilityPercent,
                currentStabilityPercent,
                ref displayVelocity,
                smoothTime,
                maxVisualSpeed,
                Time.deltaTime
            );

        if (
            Mathf.Abs(
                displayedStabilityPercent -
                currentStabilityPercent
            ) < 0.001f
        )
        {
            displayedStabilityPercent =
                currentStabilityPercent;
        }

        ApplyGaugeVisual(
            displayedStabilityPercent
        );
    }

    private void ApplyGaugeVisual(
        float percent
    )
    {
        percent =
            Mathf.Clamp(
                percent,
                MIN_STABILITY,
                MAX_STABILITY
            );

        float normalized =
            percent /
            MAX_STABILITY;

        // =====================================================
        // Fill
        // =====================================================

        Vector3 fillScale =
            fullFillScale;

        fillScale.y =
            fullFillScale.y *
            normalized;

        gaugeFill.localScale =
            fillScale;

        gaugeFillRenderer.color =
            EvaluateGaugeColor(
                percent
            );

        // =====================================================
        // Glow Quad
        // =====================================================

        if (gaugeGlow != null)
        {
            Vector3 glowScale =
                fullGlowScale;

            glowScale.y =
                fullGlowScale.y *
                normalized;

            gaugeGlow.localScale =
                glowScale;

            Vector3 glowPosition =
                fullGlowLocalPosition;

            glowPosition.y =
                fullGlowBottomY +
                (glowScale.y * 0.5f);

            gaugeGlow.localPosition =
                glowPosition;
        }
    }

    // =========================================================
    // Gauge Color
    // =========================================================

    private Color EvaluateGaugeColor(
        float percent
    )
    {
        if (
            percent >=
            cautionThreshold
        )
        {
            float t =
                Mathf.InverseLerp(
                    cautionThreshold,
                    MAX_STABILITY,
                    percent
                );

            return
                Color.Lerp(
                    cautionColor,
                    healthyColor,
                    t
                );
        }

        if (
            percent >=
            dangerThreshold
        )
        {
            float t =
                Mathf.InverseLerp(
                    dangerThreshold,
                    cautionThreshold,
                    percent
                );

            return
                Color.Lerp(
                    dangerColor,
                    cautionColor,
                    t
                );
        }

        if (
            percent >=
            criticalThreshold
        )
        {
            float t =
                Mathf.InverseLerp(
                    criticalThreshold,
                    dangerThreshold,
                    percent
                );

            return
                Color.Lerp(
                    criticalColor,
                    dangerColor,
                    t
                );
        }

        return
            criticalColor;
    }

    // =========================================================
    // PERFECT Glow
    // =========================================================

    private void TriggerPerfectGlow()
    {
        if (gaugeGlowRenderer == null)
            return;

        perfectGlowTimer =
            perfectGlowDuration;
    }

    private void UpdatePerfectGlow()
    {
        if (
            gaugeGlowRenderer == null ||
            perfectGlowDuration <= 0f
        )
        {
            return;
        }

        if (perfectGlowTimer <= 0f)
        {
            SetGlow(
                0f,
                perfectGlowColor
            );

            return;
        }

        perfectGlowTimer -=
            Time.deltaTime;

        float progress =
            1f -
            Mathf.Clamp01(
                perfectGlowTimer /
                perfectGlowDuration
            );

        float curve =
            Mathf.Sin(
                progress *
                Mathf.PI
            );

        float intensity =
            curve *
            perfectGlowIntensity;

        SetGlow(
            intensity,
            perfectGlowColor
        );
    }

    private void SetGlow(
        float intensity,
        Color color
    )
    {
        if (gaugeGlowRenderer == null)
            return;

        if (glowPropertyBlock == null)
        {
            glowPropertyBlock =
                new MaterialPropertyBlock();
        }

        gaugeGlowRenderer.GetPropertyBlock(
            glowPropertyBlock
        );

        glowPropertyBlock.SetFloat(
            GlowIntensityID,
            intensity
        );

        glowPropertyBlock.SetColor(
            GlowColorID,
            color
        );

        gaugeGlowRenderer.SetPropertyBlock(
            glowPropertyBlock
        );
    }

    // =========================================================
    // Judgement
    // =========================================================

    public void ApplyJudgement(
        NoteJudgement judgement
    )
    {
        switch (judgement)
        {
            case NoteJudgement.Perfect:

                AddStability(
                    perfectRecovery
                );

                TriggerPerfectGlow();

                break;

            case NoteJudgement.Great:

                AddStability(
                    greatRecovery
                );

                break;

            case NoteJudgement.Good:

                LoseStability(
                    goodLoss
                );

                break;

            case NoteJudgement.Miss:

                ApplyScoreUnitLoss();

                break;
        }
    }

    // =========================================================
    // HIT
    // =========================================================

    public void ApplyHit()
    {
        /*
         * HIT도 MISS와 동일하게
         * 곡 전체 점수 단위 중 1개 몫만 감소한다.
         */
        ApplyScoreUnitLoss();
    }

    private void ApplyScoreUnitLoss()
    {
        if (totalScoreUnitCount <= 0)
        {
            Debug.LogWarning(
                "StabilityGaugeController: 전체 점수 단위 수가 0입니다."
            );

            return;
        }

        LoseStability(
            LossPerScoreUnit
        );
    }

    // =========================================================
    // Stability
    // =========================================================

    public void SetStability(
        float percent
    )
    {
        currentStabilityPercent =
            Mathf.Clamp(
                percent,
                MIN_STABILITY,
                MAX_STABILITY
            );
    }

    public void AddStability(
        float percentPoint
    )
    {
        if (percentPoint <= 0f)
            return;

        currentStabilityPercent =
            Mathf.Clamp(
                currentStabilityPercent +
                percentPoint,
                MIN_STABILITY,
                MAX_STABILITY
            );
    }

    public void LoseStability(
        float percentPoint
    )
    {
        if (percentPoint <= 0f)
            return;

        currentStabilityPercent =
            Mathf.Clamp(
                currentStabilityPercent -
                percentPoint,
                MIN_STABILITY,
                MAX_STABILITY
            );
    }

    // =========================================================
    // Reset
    // =========================================================

    public void ResetStability()
    {
        currentStabilityPercent =
            Mathf.Clamp(
                startStabilityPercent,
                MIN_STABILITY,
                MAX_STABILITY
            );

        displayedStabilityPercent =
            currentStabilityPercent;

        displayVelocity = 0f;
        perfectGlowTimer = 0f;

        ApplyGaugeVisual(
            displayedStabilityPercent
        );

        SetGlow(
            0f,
            perfectGlowColor
        );
    }

    public bool IsStabilityEmpty()
    {
        return
            currentStabilityPercent <=
            MIN_STABILITY;
    }
}