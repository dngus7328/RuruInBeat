using UnityEngine;

public class HitNoteRuntime : MonoBehaviour
{
    private SongManager songManager;
    private Transform judgeLine;
    private RhythmManager rhythmManager;

    private double hitTime;
    private float travelTime;

    private float spawnY;
    private float judgeY;
    private float laneX;

    private bool resolved;
    private bool chartTimingMode;

    private float legacyMoveSpeed = 4f;
    private float legacyBottomY = -5f;
    private bool registeredAsExtraTarget;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer visualRenderer;
    [SerializeField] private bool forceVisibleOnSpawn = true;

    private void Awake()
    {
        RhythmNote normalNote =
            GetComponent<RhythmNote>();

        if (normalNote != null)
        {
            normalNote.enabled = false;
        }

        rhythmManager =
            FindFirstObjectByType<RhythmManager>();

        EnsureHitPhysics();
        EnsureVisibleVisual();
    }

    public void Initialize(
        RhythmManager manager,
        SongManager song,
        BoxCollider2D laneArea,
        Transform targetJudgeLine,
        float targetHitTime,
        float targetTravelTime
    )
    {
        rhythmManager = manager;
        songManager = song;
        judgeLine = targetJudgeLine;

        hitTime = targetHitTime;
        travelTime =
            Mathf.Max(
                0.01f,
                targetTravelTime
            );

        chartTimingMode = true;
        resolved = false;
        registeredAsExtraTarget = false;

        if (laneArea == null)
        {
            Debug.LogError(
                "HitNoteRuntime: Lane Area가 없습니다."
            );

            return;
        }

        if (judgeLine == null)
        {
            Debug.LogError(
                "HitNoteRuntime: Judge Line이 없습니다."
            );

            return;
        }

        laneX =
            laneArea.bounds.center.x;

        spawnY =
            laneArea.bounds.max.y;

        judgeY =
            judgeLine.position.y;

        Vector3 position =
            transform.position;

        position.x = laneX;
        position.y = spawnY;

        transform.position =
            position;

        EnsureHitPhysics();
        EnsureVisibleVisual();
    }

    public void Setup(
        float speed,
        float bottomY
    )
    {
        chartTimingMode = false;
        legacyMoveSpeed = speed;
        legacyBottomY = bottomY;
        resolved = false;

        RegisterAsExtraScoreTarget();
        EnsureHitPhysics();
    }

    private void Update()
    {
        if (resolved)
            return;

        if (chartTimingMode)
        {
            UpdateChartMovement();
        }
        else
        {
            UpdateLegacyMovement();
        }
    }

    private void UpdateChartMovement()
    {
        if (songManager == null)
            return;

        // 일반 RhythmNote와 완전히 동일한 시간 기반 이동식.
        double timeUntilHit =
            hitTime -
            songManager.SongTime;

        float speed =
            (spawnY - judgeY) /
            travelTime;

        float y =
            judgeY +
            (float)timeUntilHit *
            speed;

        Vector3 position =
            transform.position;

        position.x = laneX;
        position.y = y;

        transform.position =
            position;

        if (songManager.SongTime >= hitTime)
        {
            ResolveAvoid();
        }
    }

    private void UpdateLegacyMovement()
    {
        transform.position +=
            Vector3.down *
            legacyMoveSpeed *
            Time.deltaTime;

        float targetY =
            rhythmManager != null
                ? rhythmManager.JudgeY
                : legacyBottomY;

        if (transform.position.y <= targetY)
        {
            ResolveAvoid();
        }
    }

    private void OnTriggerEnter2D(
        Collider2D other
    )
    {
        if (resolved)
            return;

        PinoActor pino =
            other.GetComponentInParent<PinoActor>();

        if (pino == null)
            return;

        resolved = true;

        pino.TakeHit();

        if (rhythmManager != null)
        {
            rhythmManager.ResolveHitNoteHit();
        }

        Destroy(gameObject);
    }

    private void ResolveAvoid()
    {
        if (resolved)
            return;

        resolved = true;

        if (rhythmManager != null)
        {
            rhythmManager.ResolveHitNoteAvoid();
        }

        Destroy(gameObject);
    }

    private void EnsureVisibleVisual()
    {
        if (!forceVisibleOnSpawn)
            return;

        if (visualRenderer == null)
        {
            visualRenderer =
                GetComponentInChildren<SpriteRenderer>(
                    true
                );
        }

        if (visualRenderer == null)
        {
            Debug.LogError(
                "HitNoteRuntime: Hit Note Prefab 안에 SpriteRenderer가 없습니다.",
                this
            );

            return;
        }

        if (!visualRenderer.gameObject.activeSelf)
        {
            visualRenderer.gameObject.SetActive(
                true
            );
        }

        visualRenderer.enabled = true;

        Color color =
            visualRenderer.color;

        if (color.a <= 0.001f)
        {
            color.a = 1f;
            visualRenderer.color = color;
        }

        if (visualRenderer.sprite == null)
        {
            Debug.LogError(
                "HitNoteRuntime: Hit Note Prefab의 SpriteRenderer에 Sprite가 없습니다.",
                this
            );
        }
    }

    private void EnsureHitPhysics()
    {
        Collider2D col =
            GetComponent<Collider2D>();

        if (col == null)
        {
            col =
                gameObject.AddComponent<
                    BoxCollider2D
                >();
        }

        col.isTrigger = true;

        Rigidbody2D body =
            GetComponent<Rigidbody2D>();

        if (body == null)
        {
            body =
                gameObject.AddComponent<
                    Rigidbody2D
                >();
        }

        body.bodyType =
            RigidbodyType2D.Kinematic;

        body.gravityScale = 0f;
    }

    private void RegisterAsExtraScoreTarget()
    {
        if (registeredAsExtraTarget)
            return;

        if (rhythmManager == null)
        {
            rhythmManager =
                FindFirstObjectByType<RhythmManager>();
        }

        if (rhythmManager == null)
            return;

        registeredAsExtraTarget = true;

        rhythmManager.RegisterHitNoteTarget();
    }
}
