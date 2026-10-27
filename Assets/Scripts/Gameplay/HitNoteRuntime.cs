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
        // 피격 노트를 일반 노트 Prefab에서 복제했더라도
        // Q/W/O/P 입력으로 판정되는 일반 노트 로직은 절대 실행하지 않는다.
        RhythmNote normalNote =
            GetComponent<RhythmNote>();

        if (normalNote != null)
        {
            normalNote.enabled = false;
        }

        rhythmManager =
            FindObjectOfType<RhythmManager>();

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
        travelTime = Mathf.Max(0.01f, targetTravelTime);

        chartTimingMode = true;
        resolved = false;
        registeredAsExtraTarget = false;

        if (laneArea == null)
        {
            Debug.LogError("HitNoteRuntime: Lane Area가 없습니다.");
            return;
        }

        if (judgeLine == null)
        {
            Debug.LogError("HitNoteRuntime: Judge Line이 없습니다.");
            return;
        }

        laneX = laneArea.bounds.center.x;
        spawnY = laneArea.bounds.max.y;
        judgeY = judgeLine.position.y;

        Vector3 p = transform.position;
        p.x = laneX;
        p.y = spawnY;
        transform.position = p;

        EnsureHitPhysics();
        EnsureVisibleVisual();
    }

    // 기존 테스트 스포너 호환
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
            UpdateChartMovement();
        else
            UpdateLegacyMovement();
    }

    private void UpdateChartMovement()
    {
        if (songManager == null)
            return;

        double songTime = songManager.SongTime;
        double spawnTime = hitTime - travelTime;

        float progress =
            Mathf.Clamp01(
                (float)(
                    (songTime - spawnTime) /
                    travelTime
                )
            );

        Vector3 p = transform.position;
        p.x = laneX;
        p.y = Mathf.Lerp(spawnY, judgeY, progress);
        transform.position = p;

        // 피노와 부딪히지 않고 도착하면 회피 성공.
        if (songTime >= hitTime)
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

        // 피격노트는 키 입력 대상이 아니라
        // 피노와 닿았을 때만 HIT 처리한다.
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

        Color c =
            visualRenderer.color;

        if (c.a <= 0.001f)
        {
            c.a = 1f;
            visualRenderer.color = c;
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
                gameObject.AddComponent<BoxCollider2D>();
        }

        col.isTrigger = true;

        Rigidbody2D body =
            GetComponent<Rigidbody2D>();

        if (body == null)
        {
            body =
                gameObject.AddComponent<Rigidbody2D>();
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
                FindObjectOfType<RhythmManager>();
        }

        if (rhythmManager == null)
            return;

        registeredAsExtraTarget = true;
        rhythmManager.RegisterHitNoteTarget();
    }
}
