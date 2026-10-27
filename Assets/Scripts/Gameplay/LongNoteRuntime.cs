using UnityEngine;

public class LongNoteRuntime : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField]
    private LongNoteVisual visual;

    private RhythmManager rhythmManager;
    private SongManager songManager;

    private NoteLane lane;

    private double hitTime;
    private double endTime;

    private float duration;
    private float travelTime;

    private float spawnY;
    private float judgeY;

    private float noteSpeed;
    private float holdWorldLength;

    private float tickInterval;
    private double nextTickTime;

    private bool started;
    private bool resolved;
    private bool failedStart;

    // =========================================================
    // Public
    // =========================================================

    public NoteLane Lane
    {
        get
        {
            return lane;
        }
    }

    public bool IsStarted
    {
        get
        {
            return started;
        }
    }

    public bool IsResolved
    {
        get
        {
            return resolved;
        }
    }

    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        // LongNote 프리팹을 일반 노트에서 복제했을 때
        // RhythmNote가 남아 있으면 일반 노트 로직까지 같이 실행된다.
        // LongNote에서는 반드시 비활성화한다.
        RhythmNote normalNote =
            GetComponent<RhythmNote>();

        if (normalNote != null)
        {
            normalNote.enabled = false;
        }

        if (visual == null)
        {
            visual =
                GetComponent<LongNoteVisual>();
        }
    }

    private void Update()
    {
        if (resolved)
            return;

        if (songManager == null)
            return;

        if (!started)
        {
            UpdateApproach();
        }
        else
        {
            UpdateHold();
        }
    }

    // =========================================================
    // Initialize
    // =========================================================

    public void Initialize(
        RhythmManager rhythmManager,
        SongManager songManager,
        BoxCollider2D laneArea,
        Transform judgeLine,
        NoteLane lane,
        double hitTime,
        float duration,
        float travelTime,
        float tickInterval
    )
    {
        this.rhythmManager =
            rhythmManager;

        this.songManager =
            songManager;

        this.lane =
            lane;

        this.hitTime =
            hitTime;

        this.duration =
            Mathf.Max(
                0.05f,
                duration
            );

        endTime =
            hitTime +
            this.duration;

        this.travelTime =
            Mathf.Max(
                0.01f,
                travelTime
            );

        this.tickInterval =
            Mathf.Max(
                0.05f,
                tickInterval
            );

        // =====================================================
        // Travel Speed
        // =====================================================

        spawnY =
            laneArea.bounds.max.y;

        judgeY =
            judgeLine.position.y;

        float travelDistance =
            Mathf.Abs(
                spawnY -
                judgeY
            );

        /*
         * �Ϲ� ��Ʈ�� ���� �̵� �ӵ�
         */
        noteSpeed =
            travelDistance /
            this.travelTime;

        /*
         * duration ���� Stream�� �̵��� �Ÿ�.
         *
         * �� �Ÿ��� �� �ճ�Ʈ ���̰� �ȴ�.
         */
        holdWorldLength =
            noteSpeed *
            this.duration;

        // =====================================================
        // Spawn Position
        // =====================================================

        transform.position =
            new Vector3(
                laneArea.bounds.center.x,
                spawnY,
                transform.position.z
            );

        // =====================================================
        // Build
        // =====================================================

        if (visual != null)
        {
            visual.BuildForWorldLength(
                holdWorldLength
            );
        }
        else
        {
            Debug.LogError(
                "LongNoteRuntime: LongNoteVisual�� ������� �ʾҽ��ϴ�."
            );
        }

        // =====================================================
        // Register
        // =====================================================

        if (rhythmManager != null)
        {
            rhythmManager.RegisterLongNote(
                this
            );
        }
    }

    // =========================================================
    // Approach
    // =========================================================

    private void UpdateApproach()
    {
        double songTime =
            songManager.SongTime;

        Vector3 position =
            transform.position;

        /*
         * 일반 노트와 동일한 시간 기반 위치식.
         *
         * y = judgeY + (hitTime - songTime) * speed
         *
         * Long Note Head도 같은 식으로 계산해서
         * 일반 노트와 같은 속도로 판정선에 도착한다.
         */
        float signedSpeed =
            (spawnY - judgeY) /
            travelTime;

        float targetY =
            judgeY +
            (float)(
                hitTime -
                songTime
            ) *
            signedSpeed;

        if (spawnY >= judgeY)
        {
            targetY =
                Mathf.Clamp(
                    targetY,
                    judgeY,
                    spawnY
                );
        }
        else
        {
            targetY =
                Mathf.Clamp(
                    targetY,
                    spawnY,
                    judgeY
                );
        }

        position.y =
            targetY;

        transform.position =
            position;
    }

    // =========================================================
    // Begin Hold
    // =========================================================

    public void BeginHold()
    {
        if (started ||
            resolved)
        {
            return;
        }

        started =
            true;

        failedStart =
            false;

        nextTickTime =
            hitTime +
            tickInterval;

        /*
         * ���⼭���� LongNote�� Root��
         * �� �̻� �������� �ʴ´�.
         *
         * �� Head�� �������� �����ȴ�.
         */
        Vector3 position =
            transform.position;

        position.y =
            judgeY;

        transform.position =
            position;

        if (visual != null)
        {
            visual.BeginHoldVisual();
        }
    }

    // =========================================================
    // Hold
    // =========================================================

    private void UpdateHold()
    {
        double songTime =
            songManager.SongTime;

        // =====================================================
        // Head Lock
        // =====================================================

        Vector3 position =
            transform.position;

        position.y =
            judgeY;

        transform.position =
            position;

        // =====================================================
        // Stream Movement
        // =====================================================

        /*
         * hitTime ���� ��� �ð�.
         *
         * ���� �Է� �ð��� �ƴ϶�
         * ���� �ð����� ����Ѵ�.
         */
        float elapsed =
            Mathf.Max(
                0f,
                (float)(
                    songTime -
                    hitTime
                )
            );

        /*
         * �Ϲ� ��Ʈ�� �Ȱ��� �ӵ���
         * Body + Tail Stream�� ��� �Ʒ��� �̵��Ѵ�.
         */
        float streamDistance =
            noteSpeed *
            elapsed;

        streamDistance =
            Mathf.Clamp(
                streamDistance,
                0f,
                holdWorldLength
            );

        if (visual != null)
        {
            visual.SetStreamTravelDistance(
                streamDistance
            );
        }

        // =====================================================
        // Ticks
        // =====================================================

        ProcessTicks(
            songTime
        );

        // =====================================================
        // Finish
        // =====================================================

        if (songTime >=
            endTime)
        {
            Complete();
        }
    }

    // =========================================================
    // Timing Error
    // =========================================================

    public float GetTimingErrorSeconds()
    {
        if (songManager == null)
        {
            return
                float.MaxValue;
        }

        return
            (float)(
                hitTime -
                songManager.SongTime
            );
    }

    // =========================================================
    // Ticks
    // =========================================================

    private void ProcessTicks(
        double songTime
    )
    {
        while (
            nextTickTime <
            endTime &&
            songTime >=
            nextTickTime
        )
        {
            bool held =
                !failedStart &&
                rhythmManager != null &&
                rhythmManager.IsLaneHeld(
                    lane
                );

            if (rhythmManager != null)
            {
                if (held)
                {
                    rhythmManager.ResolveLongTickHit(
                        lane
                    );
                }
                else
                {
                    rhythmManager.ResolveLongTickMiss(
                        lane
                    );
                }
            }

            nextTickTime +=
                tickInterval;
        }
    }

    // =========================================================
    // Start Miss
    // =========================================================

    public void FailStart()
    {
        if (resolved ||
            started)
        {
            return;
        }

        /*
         * 시작 판정을 놓쳐도 롱노트를 즉시 삭제하지 않는다.
         * 정상적으로 잡았을 때와 똑같이 Head를 판정선에 고정하고
         * Body/Tail을 끝까지 흘려보낸다.
         *
         * 단, failedStart 상태이므로 이후 모든 Tick은 MISS 처리된다.
         */
        started =
            true;

        failedStart =
            true;

        nextTickTime =
            hitTime +
            tickInterval;

        Vector3 position =
            transform.position;

        position.y =
            judgeY;

        transform.position =
            position;

        if (visual != null)
        {
            visual.EndHoldVisual();
        }
    }

    // =========================================================
    // Complete
    // =========================================================

    private void Complete()
    {
        if (resolved)
            return;

        resolved =
            true;

        /*
         * ��Ȯ�� ������ ��ġ���� Stream�� �̵�.
         */
        if (visual != null)
        {
            visual.SetStreamTravelDistance(
                holdWorldLength
            );

            visual.EndHoldVisual();
            visual.HideAll();
        }

        if (rhythmManager != null)
        {
            rhythmManager.UnregisterLongNote(
                this
            );
        }

        Destroy(
            gameObject
        );
    }

    // =========================================================
    // Destroy
    // =========================================================

    private void OnDestroy()
    {
        if (rhythmManager != null)
        {
            rhythmManager.UnregisterLongNote(
                this
            );
        }
    }
}