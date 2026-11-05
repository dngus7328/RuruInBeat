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

    [Header("Note Speed Setting")]
    [SerializeField] private float noteSpeedSetting = 5.0f;
    [SerializeField] private float minNoteSpeed = 1.0f;
    [SerializeField] private float maxNoteSpeed = 10.0f;
    [SerializeField] private float defaultNoteSpeed = 5.0f;

    [Header("Long Note")]
    [Tooltip("현재는 홀드 체크용 임시 Tick 간격입니다.")]
    [SerializeField] private float longTickInterval = 0.25f;

    private HitNoteRuntime sceneHitNoteTemplate;
    private int nextNoteIndex;

    // 채보에 원래 들어 있던 travelTime.
    // 사용자 노트 속도 설정은 이 값을 기준으로 배율만 적용한다.
    private float baseTravelTime = 1.5f;

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

                // Tap / Error / Hit / Long 시작 판정 = 각각 1단위
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

    public float CurrentNoteSpeed => noteSpeedSetting;
    public float CurrentTravelTime => travelTime;

    public ChartData CurrentChart
    {
        get
        {
            return chartData;
        }
    }

    public void SetNoteSpeed(float value)
    {
        noteSpeedSetting =
            RoundToOneDecimal(
                Mathf.Clamp(
                    value,
                    minNoteSpeed,
                    maxNoteSpeed
                )
            );

        ApplyNoteSpeed();
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

        // 채보가 가진 원래 낙하 시간을 먼저 보존한다.
        if (chartData.travelTime > 0f)
        {
            baseTravelTime =
                chartData.travelTime;
        }
        else
        {
            baseTravelTime =
                Mathf.Max(
                    0.01f,
                    travelTime
                );
        }

        // 저장값이 있으면 사용자 노트 속도를 불러온다.
        if (SaveManager.Instance != null &&
            SaveManager.Instance.Data != null)
        {
            float savedSpeed =
                SaveManager.Instance.Data.noteSpeed;

            if (savedSpeed > 0f)
            {
                noteSpeedSetting =
                    savedSpeed;
            }
        }

        SetNoteSpeed(noteSpeedSetting);

        Debug.Log(
            $"Chart Loaded | " +
            $"Notes: {TotalNoteCount} | " +
            $"BPM: {chartData.bpm} | " +
            $"Note Speed: {noteSpeedSetting:F1} | " +
            $"Travel Time: {travelTime:F3}s"
        );
    }

    private void Update()
    {
        SpawnUpcomingNotes();
    }

    // =========================================================
    // Note Speed
    // =========================================================

    private void ApplyNoteSpeed()
    {
        /*
         * 5.0 = 채보 원래 travelTime 그대로
         * 10.0 = travelTime 절반 -> 2배 빠르게 보임
         * 2.5 = travelTime 2배 -> 절반 속도로 보임
         *
         * 판정 시각(hitTime)은 건드리지 않고,
         * 화면에서 판정선까지 접근하는 시간만 바꾼다.
         */

        float safeDefaultSpeed =
            Mathf.Max(
                0.1f,
                defaultNoteSpeed
            );

        float safeSpeed =
            Mathf.Max(
                0.1f,
                noteSpeedSetting
            );

        travelTime =
            baseTravelTime *
            (safeDefaultSpeed / safeSpeed);

        travelTime =
            Mathf.Max(
                0.01f,
                travelTime
            );
    }

    private float RoundToOneDecimal(float value)
    {
        return
            Mathf.Round(value * 10f) /
            10f;
    }

    // =========================================================
    // Long Tick Count
    // =========================================================

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
         * 씬에 남아 있는 HitNote_Test는 실제 플레이용 노트가 아니라
         * 복제용/테스트용 원본이다.
         *
         * Hit Note Prefab이 Inspector에 연결되어 있더라도
         * 이 원본이 Active 상태면 HitNoteRuntime.Update()가 돌아가면서
         * 판정선 도달 시 ResolveAvoid()를 실행하고 콤보를 올릴 수 있다.
         *
         * 따라서 Prefab 연결 여부를 확인하기 전에
         * 먼저 씬의 HitNote_Test를 무조건 비활성화한다.
         */

        HitNoteRuntime[] candidates = null;

        if (notesParent != null)
        {
            candidates =
                notesParent.GetComponentsInChildren<
                    HitNoteRuntime
                >(
                    true
                );

            if (candidates != null)
            {
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
                        candidate.gameObject.SetActive(
                            false
                        );
                    }
                }
            }
        }

        // 1순위는 Inspector에 연결된 실제 Hit Note Prefab.
        // 이 경우 씬의 HitNote_Test는 위에서 이미 꺼져 있다.
        if (hitNotePrefab != null)
        {
            sceneHitNoteTemplate =
                null;

            return;
        }

        if (notesParent == null)
            return;

        if (candidates == null ||
            candidates.Length == 0)
        {
            return;
        }

        // Prefab이 없을 때만 씬의 HitNote_Test를 복제 템플릿으로 사용한다.
        // 비활성화 상태여도 Instantiate 후 SpawnHitNote에서 다시 활성화된다.
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

        // 이름이 달라도 Notes 아래 HitNoteRuntime이 하나라면 템플릿으로 사용한다.
        if (candidates.Length == 1)
        {
            sceneHitNoteTemplate =
                candidates[0];

            if (sceneHitNoteTemplate != null)
            {
                sceneHitNoteTemplate.gameObject.SetActive(
                    false
                );
            }

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
