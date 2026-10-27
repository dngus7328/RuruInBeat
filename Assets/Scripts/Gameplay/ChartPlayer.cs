using UnityEngine;

public class ChartPlayer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SongManager songManager;
    [SerializeField] private RhythmManager rhythmManager;

    [SerializeField] private ChartData chartData;

    [Header("Prefabs")]
    [SerializeField] private RhythmNote notePrefab;
    [SerializeField] private LongNoteRuntime longNotePrefab;

    [Tooltip("비워두면 임시 빨간 사각형 Hit Note를 런타임에 자동 생성합니다.")]
    [SerializeField] private HitNoteRuntime hitNotePrefab;

    [SerializeField] private Transform notesParent;

    [SerializeField] private Transform judgeLine;

    [Header("Lanes")]
    [SerializeField] private BoxCollider2D laneQ;
    [SerializeField] private BoxCollider2D laneW;
    [SerializeField] private BoxCollider2D laneO;
    [SerializeField] private BoxCollider2D laneP;

    [Header("Note")]
    [SerializeField] private float travelTime = 1.5f;

    [Header("Long Note")]
    [Tooltip("현재는 홀드 체크용 임시 Tick 간격입니다.")]
    [SerializeField] private float longTickInterval = 0.25f;
    private HitNoteRuntime sceneHitNoteTemplate;

    private int nextNoteIndex;

    // =========================================================
    // Public
    // =========================================================

    public int TotalNoteCount
    {
        get
        {
            if (chartData == null ||
                chartData.notes == null)
            {
                return 0;
            }

            // Hit Note도 채보에 포함되어 있으므로
            // 곡 시작 시점부터 전체 점수 분모에 포함됩니다.
            return chartData.notes.Count;
        }
    }

    public int TotalScoreUnitCount
    {
        get
        {
            if (chartData == null ||
                chartData.notes == null)
            {
                return 0;
            }

            int total = 0;

            for (int i = 0;
                 i < chartData.notes.Count;
                 i++)
            {
                ChartNoteData note =
                    chartData.notes[i];

                if (note == null)
                    continue;

                // Tap / Error / Hit / Long 시작 판정 = 각 1단위
                total++;

                if (note.type ==
                    ChartNoteType.Long)
                {
                    total +=
                        CountLongTicks(
                            note.duration
                        );
                }
            }

            return total;
        }
    }

    private int CountLongTicks(
        float duration
    )
    {
        float interval =
            Mathf.Max(
                0.05f,
                longTickInterval
            );

        double end =
            Mathf.Max(
                0.05f,
                duration
            );

        double tick =
            interval;

        int count = 0;

        // LongNoteRuntime과 동일하게 endTime "미만"의 Tick만 계산.
        while (tick < end)
        {
            count++;
            tick += interval;
        }

        return count;
    }

    public ChartData CurrentChart
    {
        get
        {
            return chartData;
        }
    }

    // =========================================================
    // Unity
    // =========================================================

    private void Start()
    {
        nextNoteIndex = 0;

        if (chartData == null)
        {
            Debug.LogError(
                "ChartPlayer: Chart Data가 연결되지 않았습니다."
            );

            enabled = false;
            return;
        }

        if (songManager == null)
        {
            Debug.LogError(
                "ChartPlayer: SongManager가 연결되지 않았습니다."
            );

            enabled = false;
            return;
        }

        if (rhythmManager == null)
        {
            Debug.LogError(
                "ChartPlayer: RhythmManager가 연결되지 않았습니다."
            );

            enabled = false;
            return;
        }

        if (notePrefab == null)
        {
            Debug.LogError(
                "ChartPlayer: Note Prefab이 연결되지 않았습니다."
            );

            enabled = false;
            return;
        }

        if (longNotePrefab == null)
        {
            Debug.LogWarning(
                "ChartPlayer: Long Note Prefab이 연결되지 않았습니다."
            );
        }

        if (judgeLine == null)
        {
            Debug.LogError(
                "ChartPlayer: Judge Line이 연결되지 않았습니다."
            );

            enabled = false;
            return;
        }

        if (notesParent == null)
        {
            Debug.LogError(
                "ChartPlayer: Notes Parent가 연결되지 않았습니다."
            );

            enabled = false;
            return;
        }

        ResolveHitNoteTemplate();

        if (chartData.travelTime > 0f)
        {
            travelTime =
                chartData.travelTime;
        }

        Debug.Log(
            $"Chart Loaded | " +
            $"Notes: {TotalNoteCount} | " +
            $"BPM: {chartData.bpm} | " +
            $"Travel Time: {travelTime}"
        );
    }

    private void Update()
    {
        SpawnUpcomingNotes();
    }

    // =========================================================
    // Spawn
    // =========================================================

    private void SpawnUpcomingNotes()
    {
        if (chartData == null ||
            chartData.notes == null)
        {
            return;
        }

        while (
            nextNoteIndex <
            chartData.notes.Count
        )
        {
            ChartNoteData noteData =
                chartData.notes[
                    nextNoteIndex
                ];

            double spawnTime =
                noteData.hitTime -
                travelTime;

            if (
                songManager.SongTime <
                spawnTime
            )
            {
                break;
            }

            SpawnNote(
                noteData
            );

            nextNoteIndex++;
        }
    }

    private void SpawnNote(
        ChartNoteData noteData
    )
    {
        BoxCollider2D laneArea =
            GetLaneArea(
                noteData.lane
            );

        if (laneArea == null)
        {
            Debug.LogError(
                $"ChartPlayer: " +
                $"{noteData.lane} Lane이 없습니다."
            );

            return;
        }

        if (noteData.type ==
            ChartNoteType.Long)
        {
            SpawnLongNote(
                noteData,
                laneArea
            );

            return;
        }

        if (noteData.type ==
            ChartNoteType.Hit)
        {
            SpawnHitNote(
                noteData,
                laneArea
            );

            return;
        }

        // Tap / Error는 기존 RhythmNote 경로 그대로 유지
        SpawnNormalNote(
            noteData,
            laneArea
        );
    }

    // =========================================================
    // Normal / Error
    // =========================================================

    private void SpawnNormalNote(
        ChartNoteData noteData,
        BoxCollider2D laneArea
    )
    {
        RhythmNote note =
            Instantiate(
                notePrefab,
                Vector3.zero,
                Quaternion.identity,
                notesParent
            );

        note.name =
            $"Note_" +
            $"{noteData.type}_" +
            $"{noteData.lane}_" +
            $"{noteData.hitTime:F3}";

        note.Initialize(
            rhythmManager,
            songManager,
            laneArea,
            judgeLine,
            noteData.lane,
            noteData.hitTime,
            travelTime
        );
    }

    // =========================================================
    // Long
    // =========================================================

    private void SpawnLongNote(
        ChartNoteData noteData,
        BoxCollider2D laneArea
    )
    {
        if (longNotePrefab == null)
        {
            Debug.LogError(
                "ChartPlayer: Long Note Prefab이 없습니다."
            );

            return;
        }

        LongNoteRuntime note =
            Instantiate(
                longNotePrefab,
                Vector3.zero,
                Quaternion.identity,
                notesParent
            );

        note.name =
            $"LongNote_" +
            $"{noteData.lane}_" +
            $"{noteData.hitTime:F3}";

        note.Initialize(
            rhythmManager,
            songManager,
            laneArea,
            judgeLine,
            noteData.lane,
            noteData.hitTime,
            noteData.duration,
            travelTime,
            longTickInterval
        );
    }

    // =========================================================
    // Hit
    // =========================================================

    private void SpawnHitNote(
        ChartNoteData noteData,
        BoxCollider2D laneArea
    )
    {
        HitNoteRuntime template =
            hitNotePrefab != null
                ? hitNotePrefab
                : sceneHitNoteTemplate;

        if (template == null)
        {
            /*
             * 빨간 임시 사각형을 만들지 않는다.
             * 실제 Hit 이미지가 없는 상태를 가짜 이미지로 숨기지 않는다.
             */
            Debug.LogError(
                "ChartPlayer: 실제 Hit Note Prefab/Template을 찾지 못했습니다. " +
                "빨간 fallback은 생성하지 않습니다.",
                this
            );

            return;
        }

        HitNoteRuntime note =
            Instantiate(
                template,
                Vector3.zero,
                Quaternion.identity,
                notesParent
            );

        if (note == null)
        {
            Debug.LogError(
                "ChartPlayer: Hit Note 인스턴스 생성에 실패했습니다.",
                this
            );

            return;
        }

        note.gameObject.SetActive(
            true
        );

        note.enabled =
            true;

        note.name =
            $"HitNote_" +
            $"{noteData.lane}_" +
            $"{noteData.hitTime:F3}";

        note.Initialize(
            rhythmManager,
            songManager,
            laneArea,
            judgeLine,
            noteData.hitTime,
            travelTime
        );
    }

    private void ResolveHitNoteTemplate()
    {
        /*
         * 1순위는 Inspector에 연결된 hitNotePrefab.
         * 그 값이 살아 있으면 아무것도 바꾸지 않는다.
         */
        if (hitNotePrefab != null)
        {
            sceneHitNoteTemplate =
                null;

            return;
        }

        if (notesParent == null)
            return;

        HitNoteRuntime[] candidates =
            notesParent.GetComponentsInChildren<
                HitNoteRuntime
            >(
                true
            );

        if (candidates == null ||
            candidates.Length == 0)
        {
            return;
        }

        /*
         * 현재 씬에 이미 만들어 둔 HitNote_Test를 우선 사용한다.
         * 사용자 프리팹/이미지를 그대로 복제하기 위한 복구 경로다.
         */
        for (int i = 0;
             i < candidates.Length;
             i++)
        {
            HitNoteRuntime candidate =
                candidates[i];

            if (candidate == null)
                continue;

            if (candidate.name ==
                "HitNote_Test")
            {
                sceneHitNoteTemplate =
                    candidate;

                Debug.Log(
                    "ChartPlayer: HitNote_Test를 Hit Note 표시 템플릿으로 사용합니다.",
                    candidate
                );

                return;
            }
        }

        /*
         * 이름이 달라도 Notes 아래에 HitNoteRuntime이 하나라면
         * 그 실제 오브젝트를 사용한다.
         */
        if (candidates.Length == 1)
        {
            sceneHitNoteTemplate =
                candidates[0];

            Debug.Log(
                "ChartPlayer: 씬의 HitNoteRuntime을 Hit Note 표시 템플릿으로 사용합니다.",
                sceneHitNoteTemplate
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
}
