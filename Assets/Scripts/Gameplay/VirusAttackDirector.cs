using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VirusAttackDirector : MonoBehaviour
{
    private class HitCue
    {
        public NoteLane lane;
        public double fireTime;
        public float laneX;

        public bool sequenceStart;
        public bool sequenceEnd;

        public bool appeared;
        public bool moved;
        public bool warned;
        public bool fired;
        public bool retreated;
    }

    [Header("References")]
    [SerializeField] private SongManager songManager;
    [SerializeField] private ChartPlayer chartPlayer;
    [SerializeField] private VirusActor virusActor;

    [Header("Lanes")]
    [SerializeField] private BoxCollider2D laneQ;
    [SerializeField] private BoxCollider2D laneW;
    [SerializeField] private BoxCollider2D laneO;
    [SerializeField] private BoxCollider2D laneP;

    [Header("Timing")]
    [Tooltip("Hit Note가 생성되기 몇 초 전에 바이러스가 먼저 등장할지")]
    [SerializeField] private float appearLeadTime = 0.50f;

    [Tooltip("다른 레인으로 이동할 때 발사 몇 초 전에 이동을 시작할지")]
    [SerializeField] private float moveLeadTime = 0.30f;

    [Tooltip("발사 직전 경고 펄스를 시작하는 시간")]
    [SerializeField] private float warningLeadTime = 0.18f;

    [Tooltip("연속 공격의 마지막 발사 뒤 퇴장까지 기다리는 시간")]
    [SerializeField] private float retreatDelay = 0.22f;

    [Tooltip("두 Hit Note 생성 간격이 이 값 이하면 같은 연속 공격으로 처리")]
    [SerializeField] private float continuousGap = 0.90f;

    [Header("Warning Telegraph")]
    [Tooltip("발사 직전 바이러스가 잠깐 물드는 색")]
    [SerializeField]
    private Color warningColor =
        new Color(1f, 0.58f, 0.62f, 1f);

    [Range(1, 4)]
    [SerializeField] private int warningPulseCount = 2;

    [Header("Movement")]
    [SerializeField] private float appearDuration = 0.18f;
    [SerializeField] private float moveDuration = 0.14f;
    [SerializeField] private float retreatDuration = 0.16f;

    private readonly List<HitCue> hitCues =
        new List<HitCue>();

    private bool initialized;
    private bool waitingLogShown;

    private Coroutine warningRoutine;
    private SpriteRenderer warningRenderer;
    private Color warningBaseColor = Color.white;

    private void Awake()
    {
        AutoFindReferences();
        CacheWarningRenderer();
    }

    private void Start()
    {
        TryBuildHitCues();
    }

    private void Update()
    {
        // Script 실행 순서 때문에 Start 시점에 Chart가 아직 준비되지 않았어도
        // ChartPlayer.CurrentChart가 생기는 순간 다시 빌드한다.
        if (!initialized)
        {
            TryBuildHitCues();
            return;
        }

        if (songManager == null ||
            virusActor == null)
        {
            return;
        }

        double songTime =
            songManager.SongTime;

        for (int i = 0;
             i < hitCues.Count;
             i++)
        {
            ProcessCue(
                hitCues[i],
                songTime
            );
        }
    }

    // =========================================================
    // Reference
    // =========================================================

    private void AutoFindReferences()
    {
        if (songManager == null)
        {
            songManager =
                FindObjectOfType<SongManager>();
        }

        if (chartPlayer == null)
        {
            chartPlayer =
                FindObjectOfType<ChartPlayer>();
        }

        if (virusActor == null)
        {
            virusActor =
                FindObjectOfType<VirusActor>();
        }

        if (laneQ == null)
            laneQ = FindLaneCollider("Lane_Q");

        if (laneW == null)
            laneW = FindLaneCollider("Lane_W");

        if (laneO == null)
            laneO = FindLaneCollider("Lane_O");

        if (laneP == null)
            laneP = FindLaneCollider("Lane_P");
    }

    private BoxCollider2D FindLaneCollider(
        string objectName
    )
    {
        BoxCollider2D[] colliders =
            FindObjectsOfType<BoxCollider2D>();

        for (int i = 0;
             i < colliders.Length;
             i++)
        {
            BoxCollider2D collider =
                colliders[i];

            if (collider != null &&
                collider.gameObject.name ==
                objectName)
            {
                return collider;
            }
        }

        return null;
    }

    // =========================================================
    // Warning Telegraph
    // =========================================================

    private void CacheWarningRenderer()
    {
        if (virusActor == null)
            return;

        warningRenderer =
            virusActor.GetComponentInChildren<SpriteRenderer>(
                true
            );

        if (warningRenderer != null)
        {
            warningBaseColor =
                warningRenderer.color;
        }
    }

    private void PlayWarning()
    {
        if (warningRenderer == null)
        {
            CacheWarningRenderer();
        }

        if (warningRenderer == null)
            return;

        if (warningRoutine != null)
        {
            StopCoroutine(
                warningRoutine
            );

            warningRenderer.color =
                warningBaseColor;
        }

        warningRoutine =
            StartCoroutine(
                WarningPulseRoutine()
            );
    }

    private void StopWarning()
    {
        if (warningRoutine != null)
        {
            StopCoroutine(
                warningRoutine
            );

            warningRoutine =
                null;
        }

        if (warningRenderer != null)
        {
            warningRenderer.color =
                warningBaseColor;
        }
    }

    private IEnumerator WarningPulseRoutine()
    {
        float duration =
            Mathf.Max(
                0.04f,
                warningLeadTime
            );

        int pulses =
            Mathf.Max(
                1,
                warningPulseCount
            );

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed +=
                Time.deltaTime;

            float normalized =
                Mathf.Clamp01(
                    elapsed / duration
                );

            float wave =
                Mathf.Sin(
                    normalized *
                    Mathf.PI *
                    pulses
                );

            float amount =
                Mathf.Abs(
                    wave
                );

            warningRenderer.color =
                Color.Lerp(
                    warningBaseColor,
                    warningColor,
                    amount
                );

            yield return null;
        }

        warningRenderer.color =
            warningBaseColor;

        warningRoutine =
            null;
    }

    // =========================================================
    // Build Hit Timeline
    // =========================================================

    private bool TryBuildHitCues()
    {
        AutoFindReferences();

        if (songManager == null ||
            chartPlayer == null ||
            virusActor == null)
        {
            if (!waitingLogShown)
            {
                Debug.LogWarning(
                    "VirusAttackDirector: 필요한 Reference가 아직 준비되지 않았습니다. 자동 재시도합니다."
                );

                waitingLogShown = true;
            }

            return false;
        }

        ChartData chart =
            chartPlayer.CurrentChart;

        if (chart == null ||
            chart.notes == null)
        {
            if (!waitingLogShown)
            {
                Debug.Log(
                    "VirusAttackDirector: ChartData 준비 대기 중..."
                );

                waitingLogShown = true;
            }

            return false;
        }

        if (laneQ == null ||
            laneW == null ||
            laneO == null ||
            laneP == null)
        {
            if (!waitingLogShown)
            {
                Debug.LogWarning(
                    "VirusAttackDirector: Lane_Q/W/O/P Collider 준비 대기 중..."
                );

                waitingLogShown = true;
            }

            return false;
        }

        BuildHitCues(chart);

        initialized =
            true;

        waitingLogShown =
            false;

        return true;
    }

    private void BuildHitCues(
        ChartData chart
    )
    {
        hitCues.Clear();

        float chartTravelTime =
            chart.travelTime;

        if (chartTravelTime <= 0f)
        {
            chartTravelTime = 1.5f;
        }

        for (int i = 0;
             i < chart.notes.Count;
             i++)
        {
            ChartNoteData note =
                chart.notes[i];

            if (note == null)
                continue;

            if (note.type !=
                ChartNoteType.Hit)
            {
                continue;
            }

            BoxCollider2D laneArea =
                GetLaneArea(
                    note.lane
                );

            if (laneArea == null)
                continue;

            HitCue cue =
                new HitCue();

            cue.lane =
                note.lane;

            // ChartPlayer가 노트를 실제 생성하는 시점.
            cue.fireTime =
                note.hitTime -
                chartTravelTime;

            cue.laneX =
                laneArea.bounds.center.x;

            hitCues.Add(
                cue
            );
        }

        hitCues.Sort(
            (a, b) =>
                a.fireTime.CompareTo(
                    b.fireTime
                )
        );

        for (int i = 0;
             i < hitCues.Count;
             i++)
        {
            HitCue current =
                hitCues[i];

            if (i == 0)
            {
                current.sequenceStart =
                    true;
            }
            else
            {
                double previousGap =
                    current.fireTime -
                    hitCues[i - 1].fireTime;

                current.sequenceStart =
                    previousGap >
                    continuousGap;
            }

            if (i ==
                hitCues.Count - 1)
            {
                current.sequenceEnd =
                    true;
            }
            else
            {
                double nextGap =
                    hitCues[i + 1].fireTime -
                    current.fireTime;

                current.sequenceEnd =
                    nextGap >
                    continuousGap;
            }
        }

        Debug.Log(
            $"VirusAttackDirector: Hit Cue {hitCues.Count}개 로드 완료"
        );
    }

    // =========================================================
    // Timeline
    // =========================================================

    private void ProcessCue(
        HitCue cue,
        double songTime
    )
    {
        if (cue == null)
            return;

        // 새 공격 묶음 시작:
        // Hit Note 생성보다 먼저 해당 레인에서 등장.
        if (
            cue.sequenceStart &&
            !cue.appeared &&
            songTime >=
            cue.fireTime -
            appearLeadTime
        )
        {
            cue.appeared =
                true;

            virusActor.ShowAt(
                cue.laneX,
                appearDuration
            );
        }

        // 연속 공격 중 다음 Hit의 레인이 바뀌면
        // 발사 전에 그 레인으로 먼저 이동.
        if (
            !cue.moved &&
            songTime >=
            cue.fireTime -
            moveLeadTime
        )
        {
            cue.moved =
                true;

            if (!virusActor.IsVisible)
            {
                virusActor.ShowAt(
                    cue.laneX,
                    appearDuration
                );
            }
            else
            {
                virusActor.MoveToX(
                    cue.laneX,
                    moveDuration
                );
            }
        }

        // 발사 직전 짧은 경고 펄스.
        if (
            !cue.warned &&
            songTime >=
            cue.fireTime -
            warningLeadTime
        )
        {
            cue.warned =
                true;

            PlayWarning();
        }

        // Hit Note 실제 생성 순간과 동일한 시간에 발사.
        if (
            !cue.fired &&
            songTime >=
            cue.fireTime
        )
        {
            cue.fired =
                true;

            StopWarning();

            virusActor.PlayFire();

            Debug.Log(
                $"Virus Fire | Lane: {cue.lane} | Time: {cue.fireTime:F3}"
            );
        }

        // 연속 묶음의 마지막 Hit 이후에만 퇴장.
        if (
            cue.sequenceEnd &&
            !cue.retreated &&
            songTime >=
            cue.fireTime +
            retreatDelay
        )
        {
            cue.retreated =
                true;

            virusActor.Hide(
                retreatDuration
            );
        }
    }

    // =========================================================
    // Lane
    // =========================================================

    private BoxCollider2D GetLaneArea(
        NoteLane lane
    )
    {
        switch (lane)
        {
            case NoteLane.Q:
                return laneQ;

            case NoteLane.W:
                return laneW;

            case NoteLane.O:
                return laneO;

            case NoteLane.P:
                return laneP;

            default:
                return null;
        }
    }

    // =========================================================
    // Inspector Test / Rebuild
    // =========================================================

    [ContextMenu("Rebuild Hit Cues")]
    private void RebuildHitCues()
    {
        initialized =
            false;

        waitingLogShown =
            false;

        TryBuildHitCues();
    }
}
