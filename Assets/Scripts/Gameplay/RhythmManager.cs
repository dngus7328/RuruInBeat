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

    private InputAction laneQAction;
    private InputAction laneWAction;
    private InputAction laneOAction;
    private InputAction lanePAction;

    private bool inputsSubscribed;

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

    private float judgeOffsetSeconds = 0f;

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

    public void SetJudgeOffsetMs(float milliseconds)
    {
        judgeOffsetSeconds =
            milliseconds / 1000f;
    }

    // =========================================================
    // Unity
    // =========================================================

    private void Start()
    {
        SubscribeInputs();

        combo = 0;
        score = 0;

        earnedScoreWeight = 0.0;

        if (SaveManager.Instance != null &&
            SaveManager.Instance.Data != null)
        {
            SetJudgeOffsetMs(
                SaveManager.Instance.Data.judgeOffset
            );
        }

        chartPlayer =
            GetComponent<ChartPlayer>();

        if (chartPlayer != null)
        {
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

        if (stabilityGauge != null)
        {
            stabilityGauge.ResetStability();
        }
    }

    private void OnEnable()
    {
        if (inputsSubscribed)
            return;

        if (InputBindingManager.Instance == null)
            return;

        SubscribeInputs();
    }

    private void OnDisable()
    {
        UnsubscribeInputs();
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
        if (inputsSubscribed)
            return;

        if (InputBindingManager.Instance == null)
        {
            Debug.LogError(
                "RhythmManager: InputBindingManager가 없습니다."
            );

            return;
        }

        laneQAction =
            InputBindingManager.Instance.GetAction(
                "Lane_Q"
            );

        laneWAction =
            InputBindingManager.Instance.GetAction(
                "Lane_W"
            );

        laneOAction =
            InputBindingManager.Instance.GetAction(
                "Lane_O"
            );

        lanePAction =
            InputBindingManager.Instance.GetAction(
                "Lane_P"
            );

        if (laneQAction != null)
        {
            laneQAction.performed += OnLaneQ;
            laneQAction.Enable();
        }

        if (laneWAction != null)
        {
            laneWAction.performed += OnLaneW;
            laneWAction.Enable();
        }

        if (laneOAction != null)
        {
            laneOAction.performed += OnLaneO;
            laneOAction.Enable();
        }

        if (lanePAction != null)
        {
            lanePAction.performed += OnLaneP;
            lanePAction.Enable();
        }

        inputsSubscribed = true;

        Debug.Log(
            "현재 Lane_Q 키: " +
            laneQAction?.GetBindingDisplayString(0)
        );
    }

    private void UnsubscribeInputs()
    {
        if (!inputsSubscribed)
            return;

        if (laneQAction != null)
        {
            laneQAction.performed -= OnLaneQ;
        }

        if (laneWAction != null)
        {
            laneWAction.performed -= OnLaneW;
        }

        if (laneOAction != null)
        {
            laneOAction.performed -= OnLaneO;
        }

        if (lanePAction != null)
        {
            lanePAction.performed -= OnLaneP;
        }

        inputsSubscribed = false;
    }

    private void OnLaneQ(
        InputAction.CallbackContext context
    )
    {
        if (PauseController.GameplayInputBlocked)
            return;

        TryJudge(NoteLane.Q);
    }

    private void OnLaneW(
        InputAction.CallbackContext context
    )
    {
        if (PauseController.GameplayInputBlocked)
            return;

        TryJudge(NoteLane.W);
    }

    private void OnLaneO(
        InputAction.CallbackContext context
    )
    {
        if (PauseController.GameplayInputBlocked)
            return;

        TryJudge(NoteLane.O);
    }

    private void OnLaneP(
        InputAction.CallbackContext context
    )
    {
        if (PauseController.GameplayInputBlocked)
            return;

        TryJudge(NoteLane.P);
    }

    // =========================================================
    // Lane Held
    // =========================================================

    public bool IsLaneHeld(
        NoteLane lane
    )
    {
        InputAction targetAction = null;

        switch (lane)
        {
            case NoteLane.Q:
                targetAction = laneQAction;
                break;

            case NoteLane.W:
                targetAction = laneWAction;
                break;

            case NoteLane.O:
                targetAction = laneOAction;
                break;

            case NoteLane.P:
                targetAction = lanePAction;
                break;
        }

        if (targetAction == null)
            return false;

        return targetAction.IsPressed();
    }

    // =========================================================
    // Judgement
    // =========================================================

    private void TryJudge(
        NoteLane lane
    )
    {
        RhythmNote closestNote = null;
        LongNoteRuntime closestLongNote = null;

        float closestAbsoluteError =
            float.MaxValue;

        float closestSignedError =
            0f;

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
                note.GetTimingErrorSeconds()
                + judgeOffsetSeconds;

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
                longNote.GetTimingErrorSeconds()
                + judgeOffsetSeconds;

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

        if (closestNote != null)
        {
            ResolveNote(
                closestNote,
                judgement,
                closestSignedError
            );

            return;
        }

        if (closestLongNote != null)
        {
            closestLongNote.BeginHold();

            UpdateCombo(
                judgement
            );

            AddScore(
                judgement
            );

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
    // Miss
    // =========================================================

    private void CheckMisses()
    {
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

            if (longNote.IsStarted)
                continue;

            float error =
                longNote.GetTimingErrorSeconds()
                + judgeOffsetSeconds;

            if (
                error <
                -goodWindow
            )
            {
                NoteLane missedLane =
                    longNote.Lane;

                longNote.FailStart();

                ResetCombo();

                AddScore(
                    NoteJudgement.Miss
                );

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
                note.GetTimingErrorSeconds()
                + judgeOffsetSeconds;

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
        UpdateCombo(
            NoteJudgement.Perfect
        );

        AddScore(
            NoteJudgement.Perfect
        );

        ApplyStability(
            NoteJudgement.Perfect
        );
    }

    public void ResolveLongTickMiss(
        NoteLane lane
    )
    {
        ResetCombo();

        AddScore(
            NoteJudgement.Miss
        );

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
        ResetCombo();

        AddScore(
            NoteJudgement.Miss
        );

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
        UpdateCombo(
            NoteJudgement.Perfect
        );

        AddScore(
            NoteJudgement.Perfect
        );

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

    // =========================================================
    // Total Count
    // =========================================================

    private void RefreshTotalNoteCount()
    {
        totalNoteCount =
            baseChartNoteCount +
            extraHitNoteCount;

        if (totalNoteCount < 0)
        {
            totalNoteCount = 0;
        }

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

    private bool IsLaneHitEffectEnabled()
    {
        if (
            SaveManager.Instance == null ||
            SaveManager.Instance.Data == null
        )
        {
            return true;
        }

        return
            SaveManager.Instance.Data.effectDisplay;
    }

    private void PlayLaneHitFlash(
        NoteLane lane,
        NoteJudgement judgement
    )
    {
        if (!IsLaneHitEffectEnabled())
            return;

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