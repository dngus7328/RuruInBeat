using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum NoteLane
{
    Q,
    W,
    O,
    P
}

public enum NoteJudgement
{
    Perfect,
    Great,
    Good,
    Miss
}

public class RhythmManager : MonoBehaviour
{
    private const int MAX_SCORE = 1000000;

    [Header("References")]
    [SerializeField] private Transform judgeLine;
    [SerializeField] private JudgementDisplay judgementDisplay;

    [Header("Stability")]
    [SerializeField] private StabilityGaugeController stabilityGauge;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference laneQAction;
    [SerializeField] private InputActionReference laneWAction;
    [SerializeField] private InputActionReference laneOAction;
    [SerializeField] private InputActionReference lanePAction;

    [Header("Combo")]
    [SerializeField] private ComboDisplay comboDisplay;

    [Header("Score")]
    [SerializeField] private ScoreDisplay scoreDisplay;

    [Header("Lane Hit Flash")]
    [SerializeField] private LaneHitFlash hitFlashQ;
    [SerializeField] private LaneHitFlash hitFlashW;
    [SerializeField] private LaneHitFlash hitFlashO;
    [SerializeField] private LaneHitFlash hitFlashP;

    [Header("Judgement Windows")]
    [SerializeField] private float perfectWindow = 0.050f;
    [SerializeField] private float greatWindow = 0.100f;
    [SerializeField] private float goodWindow = 0.160f;

    [Header("Score Weights")]
    [Range(0f, 1f)]
    [SerializeField] private float perfectWeight = 1.0f;

    [Range(0f, 1f)]
    [SerializeField] private float greatWeight = 0.7f;

    [Range(0f, 1f)]
    [SerializeField] private float goodWeight = 0.3f;

    private readonly List<RhythmNote> activeNotes =
        new List<RhythmNote>();

    private readonly List<LongNoteRuntime> activeLongNotes =
        new List<LongNoteRuntime>();

    private ChartPlayer chartPlayer;

    private int combo;
    private int score;

    private int baseChartNoteCount;
    private int extraHitNoteCount;
    private int totalNoteCount;

    private double earnedScoreWeight;

    // =========================================================
    // Public
    // =========================================================

    public float JudgeY
    {
        get
        {
            if (judgeLine == null)
                return 0f;

            return judgeLine.position.y;
        }
    }

    public int CurrentCombo => combo;
    public int CurrentScore => score;

    // =========================================================
    // Unity
    // =========================================================

    private void OnEnable()
    {
        SubscribeInputs();
    }

    private void OnDisable()
    {
        UnsubscribeInputs();
    }

    private void Start()
    {
        combo = 0;
        score = 0;

        earnedScoreWeight = 0.0;

        chartPlayer =
            GetComponent<ChartPlayer>();

        if (chartPlayer != null)
        {
            // Tap / Error / Long 시작 판정 + Long Tick 전부 포함
            baseChartNoteCount =
                chartPlayer.TotalScoreUnitCount;
        }
        else
        {
            baseChartNoteCount = 0;

            Debug.LogWarning(
                "RhythmManager: ChartPlayer를 찾지 못했습니다."
            );
        }

        RefreshTotalNoteCount();

        if (comboDisplay != null)
        {
            comboDisplay.ResetCombo();
        }

        if (scoreDisplay != null)
        {
            scoreDisplay.SetScore(0);
        }

        // 안정화 게이지는 게임 시작 시 설정된 시작값으로 초기화
        // 현재 StabilityGaugeController 기준 70%
        if (stabilityGauge != null)
        {
            stabilityGauge.ResetStability();
        }
    }

    private void Update()
    {
        CheckMisses();
    }

    // =========================================================
    // Input
    // =========================================================

    private void SubscribeInputs()
    {
        if (laneQAction != null)
        {
            laneQAction.action.performed +=
                OnLaneQ;

            laneQAction.action.Enable();
        }

        if (laneWAction != null)
        {
            laneWAction.action.performed +=
                OnLaneW;

            laneWAction.action.Enable();
        }

        if (laneOAction != null)
        {
            laneOAction.action.performed +=
                OnLaneO;

            laneOAction.action.Enable();
        }

        if (lanePAction != null)
        {
            lanePAction.action.performed +=
                OnLaneP;

            lanePAction.action.Enable();
        }
    }

    private void UnsubscribeInputs()
    {
        if (laneQAction != null)
        {
            laneQAction.action.performed -=
                OnLaneQ;
        }

        if (laneWAction != null)
        {
            laneWAction.action.performed -=
                OnLaneW;
        }

        if (laneOAction != null)
        {
            laneOAction.action.performed -=
                OnLaneO;
        }

        if (lanePAction != null)
        {
            lanePAction.action.performed -=
                OnLaneP;
        }
    }

    private void OnLaneQ(
        InputAction.CallbackContext context
    )
    {
        TryJudge(NoteLane.Q);
    }

    private void OnLaneW(
        InputAction.CallbackContext context
    )
    {
        TryJudge(NoteLane.W);
    }

    private void OnLaneO(
        InputAction.CallbackContext context
    )
    {
        TryJudge(NoteLane.O);
    }

    private void OnLaneP(
        InputAction.CallbackContext context
    )
    {
        TryJudge(NoteLane.P);
    }

    // =========================================================
    // Lane Held
    // LongNoteRuntime에서 사용
    // =========================================================

    public bool IsLaneHeld(
        NoteLane lane
    )
    {
        InputActionReference targetAction =
            null;

        switch (lane)
        {
            case NoteLane.Q:
                targetAction =
                    laneQAction;
                break;

            case NoteLane.W:
                targetAction =
                    laneWAction;
                break;

            case NoteLane.O:
                targetAction =
                    laneOAction;
                break;

            case NoteLane.P:
                targetAction =
                    lanePAction;
                break;
        }

        if (targetAction == null)
            return false;

        if (targetAction.action == null)
            return false;

        return
            targetAction.action.IsPressed();
    }

    // =========================================================
    // Normal Note Judgement
    // =========================================================

    private void TryJudge(
        NoteLane lane
    )
    {
        RhythmNote closestNote =
            null;

        LongNoteRuntime closestLongNote =
            null;

        float closestAbsoluteError =
            float.MaxValue;

        float closestSignedError =
            0f;

        // =====================================================
        // Normal Note
        // =====================================================

        foreach (
            RhythmNote note
            in activeNotes
        )
        {
            if (note == null)
                continue;

            if (note.IsJudged)
                continue;

            if (note.Lane != lane)
                continue;

            float error =
                note.GetTimingErrorSeconds();

            float absoluteError =
                Mathf.Abs(error);

            if (
                absoluteError <
                closestAbsoluteError
            )
            {
                closestAbsoluteError =
                    absoluteError;

                closestSignedError =
                    error;

                closestNote =
                    note;

                closestLongNote =
                    null;
            }
        }

        // =====================================================
        // Long Note Start
        // =====================================================

        foreach (
            LongNoteRuntime longNote
            in activeLongNotes
        )
        {
            if (longNote == null)
                continue;

            if (longNote.IsResolved)
                continue;

            if (longNote.IsStarted)
                continue;

            if (longNote.Lane != lane)
                continue;

            float error =
                longNote.GetTimingErrorSeconds();

            float absoluteError =
                Mathf.Abs(error);

            if (
                absoluteError <
                closestAbsoluteError
            )
            {
                closestAbsoluteError =
                    absoluteError;

                closestSignedError =
                    error;

                closestLongNote =
                    longNote;

                closestNote =
                    null;
            }
        }

        if (
            closestNote == null &&
            closestLongNote == null
        )
        {
            return;
        }

        if (
            closestAbsoluteError >
            goodWindow
        )
        {
            return;
        }

        NoteJudgement judgement;

        if (
            closestAbsoluteError <=
            perfectWindow
        )
        {
            judgement =
                NoteJudgement.Perfect;
        }
        else if (
            closestAbsoluteError <=
            greatWindow
        )
        {
            judgement =
                NoteJudgement.Great;
        }
        else
        {
            judgement =
                NoteJudgement.Good;
        }

        // =====================================================
        // Normal Note Resolve
        // =====================================================

        if (closestNote != null)
        {
            ResolveNote(
                closestNote,
                judgement,
                closestSignedError
            );

            return;
        }

        // =====================================================
        // Long Note Start Resolve
        // =====================================================

        if (closestLongNote != null)
        {
            closestLongNote.BeginHold();

            UpdateCombo(
                judgement
            );

            AddScore(
                judgement
            );

            // 안정화 적용
            ApplyStability(
                judgement
            );

            if (
                judgementDisplay !=
                null
            )
            {
                judgementDisplay.Show(
                    judgement
                );
            }

            PlayLaneHitFlash(
                lane,
                judgement
            );

            string timing;

            if (closestSignedError > 0f)
            {
                timing = "EARLY";
            }
            else if (
                closestSignedError < 0f
            )
            {
                timing = "LATE";
            }
            else
            {
                timing = "EXACT";
            }

            Debug.Log(
                $"LONG START {judgement.ToString().ToUpper()} | " +
                $"{timing} | " +
                $"{Mathf.Abs(closestSignedError) * 1000f:F1}ms | " +
                $"Lane: {lane} | " +
                $"Combo: {combo} | " +
                $"Score: {score}"
            );
        }
    }

    // =========================================================
    // Normal Miss
    // =========================================================

    private void CheckMisses()
    {
        // =====================================================
        // Long Note Start Miss
        // =====================================================

        for (
            int i =
                activeLongNotes.Count - 1;
            i >= 0;
            i--
        )
        {
            LongNoteRuntime longNote =
                activeLongNotes[i];

            if (longNote == null)
            {
                activeLongNotes.RemoveAt(i);
                continue;
            }

            if (longNote.IsResolved)
            {
                activeLongNotes.RemoveAt(i);
                continue;
            }

            // 이미 시작된 롱노트는 LongNoteRuntime이
            // endTime까지 자체적으로 관리한다.
            if (longNote.IsStarted)
                continue;

            float error =
                longNote.GetTimingErrorSeconds();

            if (
                error <
                -goodWindow
            )
            {
                NoteLane missedLane =
                    longNote.Lane;

                // FailStart 내부에서 UnregisterLongNote를 호출한다.
                longNote.FailStart();

                ResetCombo();

                AddScore(
                    NoteJudgement.Miss
                );

                // 안정화 적용
                ApplyStability(
                    NoteJudgement.Miss
                );

                if (
                    judgementDisplay !=
                    null
                )
                {
                    judgementDisplay.Show(
                        NoteJudgement.Miss
                    );
                }

                Debug.Log(
                    $"LONG START MISS | " +
                    $"Lane: {missedLane} | " +
                    $"Combo: {combo} | " +
                    $"Score: {score}"
                );
            }
        }

        // =====================================================
        // Normal Note Miss
        // =====================================================

        for (
            int i =
                activeNotes.Count - 1;
            i >= 0;
            i--
        )
        {
            RhythmNote note =
                activeNotes[i];

            if (note == null)
            {
                activeNotes.RemoveAt(i);
                continue;
            }

            if (note.IsJudged)
            {
                activeNotes.RemoveAt(i);
                continue;
            }

            float error =
                note.GetTimingErrorSeconds();

            if (
                error <
                -goodWindow
            )
            {
                note.Resolve(
                    NoteJudgement.Miss
                );

                ResetCombo();

                AddScore(
                    NoteJudgement.Miss
                );

                // 안정화 적용
                ApplyStability(
                    NoteJudgement.Miss
                );

                if (
                    judgementDisplay !=
                    null
                )
                {
                    judgementDisplay.Show(
                        NoteJudgement.Miss
                    );
                }

                Debug.Log(
                    $"MISS | " +
                    $"Lane: {note.Lane} | " +
                    $"Combo: {combo} | " +
                    $"Score: {score}"
                );

                activeNotes.RemoveAt(i);
            }
        }
    }

    // =========================================================
    // Normal Resolve
    // =========================================================

    private void ResolveNote(
        RhythmNote note,
        NoteJudgement judgement,
        float signedError
    )
    {
        NoteLane lane =
            note.Lane;

        note.Resolve(
            judgement
        );

        UpdateCombo(
            judgement
        );

        AddScore(
            judgement
        );

        // 안정화 적용
        ApplyStability(
            judgement
        );

        if (
            judgementDisplay !=
            null
        )
        {
            judgementDisplay.Show(
                judgement
            );
        }

        PlayLaneHitFlash(
            lane,
            judgement
        );

        string timing;

        if (signedError > 0f)
        {
            timing = "EARLY";
        }
        else if (
            signedError < 0f
        )
        {
            timing = "LATE";
        }
        else
        {
            timing = "EXACT";
        }

        Debug.Log(
            $"{judgement.ToString().ToUpper()} | " +
            $"{timing} | " +
            $"{Mathf.Abs(signedError) * 1000f:F1}ms | " +
            $"Lane: {lane} | " +
            $"Combo: {combo} | " +
            $"Score: {score}"
        );

        activeNotes.Remove(
            note
        );
    }

    // =========================================================
    // Long Notes
    // =========================================================

    public void RegisterLongNote(
        LongNoteRuntime longNote
    )
    {
        if (longNote == null)
            return;

        if (
            !activeLongNotes.Contains(
                longNote
            )
        )
        {
            activeLongNotes.Add(
                longNote
            );
        }
    }

    public void UnregisterLongNote(
        LongNoteRuntime longNote
    )
    {
        if (longNote == null)
            return;

        activeLongNotes.Remove(
            longNote
        );
    }

    public void ResolveLongTickHit(
        NoteLane lane
    )
    {
        /*
         * Long Tick 성공은 독립 채점 단위.
         * 홀드 중이므로 별도 타이밍 판정은 없고 PERFECT 가중치로 처리한다.
         * 판정 텍스트/레인 플래시는 반복 출력하지 않는다.
         */
        UpdateCombo(
            NoteJudgement.Perfect
        );

        AddScore(
            NoteJudgement.Perfect
        );

        // 안정화 적용
        ApplyStability(
            NoteJudgement.Perfect
        );
    }

    public void ResolveLongTickMiss(
        NoteLane lane
    )
    {
        /*
         * 롱노트 시작 실패 또는 홀드 중 키를 놓친 Tick.
         * 롱노트 자체는 계속 화면에 남아 흐르고,
         * 각 실패 Tick마다 MISS 피드백을 다시 낸다.
         */
        ResetCombo();

        AddScore(
            NoteJudgement.Miss
        );

        // 안정화 적용
        ApplyStability(
            NoteJudgement.Miss
        );

        if (
            judgementDisplay !=
            null
        )
        {
            judgementDisplay.Show(
                NoteJudgement.Miss
            );
        }
    }

    // =========================================================
    // Hit Note
    // =========================================================

    public void RegisterHitNoteTarget()
    {
        extraHitNoteCount++;

        RefreshTotalNoteCount();

        RecalculateScore();
    }

    public void ResolveHitNoteHit()
    {
        // HIT은 콤보 종료
        ResetCombo();

        // HIT은 점수상 MISS와 동일하게 0점 처리
        AddScore(
            NoteJudgement.Miss
        );

        // 안정화는 일반 MISS가 아니라
        // 피격 전용 감소량을 적용한다.
        if (stabilityGauge != null)
        {
            stabilityGauge.ApplyHit();
        }

        if (
            judgementDisplay !=
            null
        )
        {
            judgementDisplay.ShowHit();
        }

        Debug.Log(
            $"HIT | " +
            $"Combo: {combo} | " +
            $"Score: {score}"
        );
    }

    public void ResolveHitNoteAvoid()
    {
        // 회피 성공 = PERFECT
        UpdateCombo(
            NoteJudgement.Perfect
        );

        AddScore(
            NoteJudgement.Perfect
        );

        // 안정화 적용
        ApplyStability(
            NoteJudgement.Perfect
        );

        if (
            judgementDisplay !=
            null
        )
        {
            judgementDisplay.Show(
                NoteJudgement.Perfect
            );
        }

        Debug.Log(
            $"AVOID PERFECT | " +
            $"Combo: {combo} | " +
            $"Score: {score}"
        );
    }

    private void RefreshTotalNoteCount()
    {
        totalNoteCount =
            baseChartNoteCount +
            extraHitNoteCount;

        if (totalNoteCount < 0)
        {
            totalNoteCount = 0;
        }

        // 곡 전체 점수 단위 수를 안정화 게이지에 전달한다.
        // MISS / HIT 1회 감소량은
        // 100% / 곡 전체 점수 단위 수로 계산된다.
        if (stabilityGauge != null)
        {
            stabilityGauge.SetTotalScoreUnitCount(
                totalNoteCount
            );
        }
    }

    // =========================================================
    // Stability
    // =========================================================

    private void ApplyStability(
        NoteJudgement judgement
    )
    {
        if (stabilityGauge == null)
            return;

        stabilityGauge.ApplyJudgement(
            judgement
        );
    }

    // =========================================================
    // Combo
    // =========================================================

    private void UpdateCombo(
        NoteJudgement judgement
    )
    {
        if (
            judgement ==
                NoteJudgement.Good ||
            judgement ==
                NoteJudgement.Miss
        )
        {
            ResetCombo();

            return;
        }

        combo++;

        if (comboDisplay != null)
        {
            comboDisplay.SetCombo(
                combo
            );
        }
    }

    private void ResetCombo()
    {
        combo = 0;

        if (comboDisplay != null)
        {
            comboDisplay.ResetCombo();
        }
    }

    // =========================================================
    // Score
    // =========================================================

    private void AddScore(
        NoteJudgement judgement
    )
    {
        if (totalNoteCount <= 0)
            return;

        double weight =
            0.0;

        switch (judgement)
        {
            case NoteJudgement.Perfect:
                weight =
                    perfectWeight;
                break;

            case NoteJudgement.Great:
                weight =
                    greatWeight;
                break;

            case NoteJudgement.Good:
                weight =
                    goodWeight;
                break;

            case NoteJudgement.Miss:
                weight =
                    0.0;
                break;
        }

        earnedScoreWeight +=
            weight;

        RecalculateScore();
    }

    private void RecalculateScore()
    {
        if (totalNoteCount <= 0)
        {
            score = 0;

            if (scoreDisplay != null)
            {
                scoreDisplay.SetScore(
                    0
                );
            }

            return;
        }

        double normalizedScore =
            MAX_SCORE *
            earnedScoreWeight /
            totalNoteCount;

        score =
            Mathf.Clamp(
                Mathf.RoundToInt(
                    (float)normalizedScore
                ),
                0,
                MAX_SCORE
            );

        if (scoreDisplay != null)
        {
            scoreDisplay.SetScore(
                score
            );
        }
    }

    // =========================================================
    // Lane Hit Flash
    // =========================================================

    private void PlayLaneHitFlash(
        NoteLane lane,
        NoteJudgement judgement
    )
    {
        LaneHitFlash targetFlash =
            null;

        switch (lane)
        {
            case NoteLane.Q:
                targetFlash =
                    hitFlashQ;
                break;

            case NoteLane.W:
                targetFlash =
                    hitFlashW;
                break;

            case NoteLane.O:
                targetFlash =
                    hitFlashO;
                break;

            case NoteLane.P:
                targetFlash =
                    hitFlashP;
                break;
        }

        if (targetFlash != null)
        {
            targetFlash.Play(
                judgement
            );
        }
    }

    // =========================================================
    // Normal Notes
    // =========================================================

    public void RegisterNote(
        RhythmNote note
    )
    {
        if (note == null)
            return;

        if (
            !activeNotes.Contains(
                note
            )
        )
        {
            activeNotes.Add(
                note
            );
        }
    }

    public void UnregisterNote(
        RhythmNote note
    )
    {
        if (note == null)
            return;

        activeNotes.Remove(
            note
        );
    }
}