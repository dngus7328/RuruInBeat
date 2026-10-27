using UnityEngine;

public class RhythmNote : MonoBehaviour
{
    private RhythmManager rhythmManager;
    private SongManager songManager;

    private BoxCollider2D laneArea;
    private Transform judgeLine;

    private NoteLane lane;

    private double hitTime;
    private float travelTime;

    private float spawnY;
    private float judgeY;

    private bool initialized;
    private bool isJudged;

    public NoteLane Lane => lane;
    public bool IsJudged => isJudged;

    public void Initialize(
        RhythmManager newRhythmManager,
        SongManager newSongManager,
        BoxCollider2D newLaneArea,
        Transform newJudgeLine,
        NoteLane newLane,
        double newHitTime,
        float newTravelTime
    )
    {
        rhythmManager = newRhythmManager;
        songManager = newSongManager;

        laneArea = newLaneArea;
        judgeLine = newJudgeLine;

        lane = newLane;

        hitTime = newHitTime;
        travelTime = newTravelTime;

        if (laneArea == null)
        {
            Debug.LogError(
                $"{name}: Lane Area가 없습니다."
            );

            return;
        }

        if (judgeLine == null)
        {
            Debug.LogError(
                $"{name}: JudgeLine이 없습니다."
            );

            return;
        }

        spawnY =
            laneArea.bounds.max.y;

        judgeY =
            judgeLine.position.y;

        transform.position =
            new Vector3(
                laneArea.bounds.center.x,
                spawnY,
                transform.position.z
            );

        initialized = true;

        if (rhythmManager != null)
        {
            rhythmManager.RegisterNote(this);
        }
    }

    private void Update()
    {
        if (!initialized)
            return;

        if (isJudged)
            return;

        if (songManager == null)
            return;

        double timeUntilHit =
            hitTime -
            songManager.SongTime;

        float speed =
            (spawnY - judgeY)
            / travelTime;

        float y =
            judgeY +
            (float)timeUntilHit *
            speed;

        Vector3 position =
            transform.position;

        position.y = y;

        transform.position = position;
    }

    public float GetTimingErrorSeconds()
    {
        if (!initialized)
            return 999f;

        if (songManager == null)
            return 999f;

        return (float)(
            hitTime -
            songManager.SongTime
        );
    }

    public void Resolve(
        NoteJudgement judgement
    )
    {
        if (isJudged)
            return;

        isJudged = true;

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (rhythmManager != null)
        {
            rhythmManager.UnregisterNote(this);
        }
    }
}