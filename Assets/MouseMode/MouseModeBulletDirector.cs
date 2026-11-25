using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif
using LuluInBeat.MouseMode.ChartSystem;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Mouse 씬 BOSS 탄막. 기존 MouseModeChartPlayer의 공격 이벤트를 사용합니다.
/// Chart Player / Pino 자동 탐색, JSON 직접 읽기, 임시 Chart Asset 자동 생성.
/// 별도 설치 스크립트 또는 Inspector의 Chart Asset 수동 연결 불필요.
/// </summary>
[DefaultExecutionOrder(2000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class MouseModeBulletDirector : MonoBehaviour
{
    [Header("채보/피노 (비어 있으면 자동 탐색)")]
    [SerializeField] private MouseModeChartPlayer chartPlayer;
    [SerializeField] private MouseModeChartAsset chartAsset;
    [SerializeField] private RectTransform pino;
    [SerializeField] private PinoSpriteAnimator pinoAnimator;

    [Header("탄막 외형: 읽기 쉬운 무광 탄환 (자동 생성)")]
    [SerializeField, Range(26f, 32f)] private float bulletSize = 30f;
    [SerializeField] private Color bulletColor = new Color(0.82f, 0.37f, 0.43f, 1f);
    [SerializeField] private Color bulletBorderColor = new Color(0.20f, 0.13f, 0.22f, 1f);
    [SerializeField] private Color warningColor = new Color(0.92f, 0.64f, 0.67f, 0.62f);

    [Header("고급 탄막 속도 (낮을수록 천천히 이동)")]
    [SerializeField, Range(0.07f, 0.30f)] private float advancedTravelPerBeat = 0.115f;

    [Header("리듬 연출: 착탄 시점은 채보 박자에 고정")]
    [SerializeField, Range(2.0f, 8.0f)] private float fallDurationBeats = 4.8f;
    [SerializeField, Range(0.5f, 2.2f)] private float warningLeadBeats = 1.4f;
    [SerializeField, Range(0.5f, 3.0f)] private float exitDurationBeats = 1.8f;

    [Header("피격")]
    [Header("피노 원형 피격 범위 (Collider2D 기준)")]
    [SerializeField] private CircleCollider2D pinoHitbox;
    [SerializeField, Range(0.04f, 0.25f)] private float defaultHitboxRadiusRatio = 0.12f;
    [SerializeField] private bool enableStabilityFailure = true;
    [Header("중간 강제 종료 방지")]
    [Tooltip("안정화 0에서 음악을 끊지 않고 마지막 결과에서 실패 판정")]
    [SerializeField] private bool stopImmediatelyAtZero = false;
    [SerializeField] private bool logPinoHits = true;
    // Old fallback for missing or temporarily disabled collider.
    [SerializeField, Range(4f, 32f)] private float pinoHitRadius = 9f;
    [SerializeField, Range(0.1f, 2.5f)] private float hitInvulnerabilitySeconds = 1.35f;

    [Header("탄막 회피 보상: GRAZE")]
    [Tooltip("탄환에 닿지 않고 가깝게 스치면 안정화가 회복됩니다.")]
    [SerializeField] private bool enableGrazeRewards = true;
    [SerializeField, Range(1, 6)] private int grazeRecovery = 2;
    [SerializeField, Range(0.2f, 0.8f)] private float grazeCooldownSeconds = 0.35f;

    [Header("BOSS 안정화 (선택)")]
    [SerializeField] private Image stabilityFill;
    private TMP_Text stabilityStatusLabel;
    private TMP_Text stabilityPercentLabel;
    [SerializeField, Range(1, 100)] private int damagePerHit = 12;
    [SerializeField] private GameObject failurePanel;

    [Header("리듬 판정과 안정화 규칙")]
    [SerializeField, Range(0, 10)] private int perfectRecovery = 1;
    [SerializeField, Range(0, 10)] private int greatRecovery = 0;
    [SerializeField, Range(0, 10)] private int goodPenalty = 0;
    [SerializeField, Range(0, 25)] private int missPenalty = 1;

    [Header("패턴 검수")]
    [SerializeField] private bool showFairnessWarnings = true;
    [SerializeField] private bool showHitboxInEditor = false;

    // A tutorial hit still shows its real feedback, but cannot abruptly
    // end the teaching scene or drain the entire gauge after a few misses.
    public void SetTutorialSafety(bool enabled)
    {
        if (!enabled) return;
        enableStabilityFailure = false;
        stopImmediatelyAtZero = false;
        missPenalty = 0;
        goodPenalty = 0;
        damagePerHit = Mathf.Min(damagePerHit, 6);
    }

    public event Action<int> PinoHit;
    public event Action StabilityDepleted;

    public int Stability { get; private set; } = 100;
    public int HitCount { get; private set; }
    public int GrazeCount { get; private set; }
    public bool ShouldFailAtSongEnd { get { return enableStabilityFailure && Stability <= 0; } }
    public int FinalScore
    {
        get
        {
            int baseScore = chartPlayer != null ? chartPlayer.Score : 0;
            return CalculateBattleScore(baseScore, HitCount, GrazeCount);
        }
    }

    // Grazing makes close dodges worth mastering instead of turning every
    // enemy attack into an obstacle that can only lower the final score.
    // It must never promote imperfect rhythm judgement to SS (1,000,000).
    public static int CalculateBattleScore(int baseScore, int hitCount, int grazeCount)
    {
        int clean = Mathf.Clamp(baseScore, 0, 1000000);
        int damage = Mathf.Max(0, hitCount) * 10000;
        int dodgeBonus = Mathf.Min(25000, Mathf.Max(0, grazeCount) * 1000);
        int adjusted = Mathf.Clamp(clean - damage + dodgeBonus, 0, 1000000);
        return clean == 1000000 && hitCount == 0
            ? 1000000 : Mathf.Min(999999, adjusted);
    }

    // Laser and burst are REAL chart events. They are stored in the existing
    // MouseModeChartAttack.patternId string so old charts and class definitions
    // remain compatible. Example:
    // laser|x=0.45|angle=15|width=0.055|warning=1.5|duration=1.5
    // burst|x=0.55|y=0.60|radius=0.32|count=12|warning=1.5|duration=2.0
    public enum AttackKind { Legacy, Laser, Burst, Rain }

    public struct AttackSpec
    {
        public AttackKind kind;
        public string legacyId;
        public float x, y, angle, width, radius, warning, duration;
        // Rain: a real barrage serialized with the chart attack, not just a preview.
        public float drift;
        public int rows, gap;
        public int rings; // Burst: number of staggered expansion rings (1-3).
        // 0=Arc Fan, 1=Cross Weave, 2=Snake Sweep, 3=Twin Pincer.
        public int style;
        public int count;
    }

    public static AttackSpec ParseAttack(string value)
    {
        string[] parts = (value ?? "").Split('|');
        string id = parts[0].Trim().ToLowerInvariant();
        AttackSpec spec = new AttackSpec
        {
            kind = id == "laser" ? AttackKind.Laser :
                   id == "burst" ? AttackKind.Burst :
                   id == "rain" ? AttackKind.Rain : AttackKind.Legacy,
            // Keep optional pattern parameters for reusable song variations.
            // The old parser silently stripped spread/spacing/density options.
            legacyId = id == "laser" || id == "burst" || id == "rain" ?
                id : (value ?? "").Trim().ToLowerInvariant(),
            x = 0.5f,
            y = 0.62f,
            angle = 0f,
            width = 0.055f,
            radius = 0.34f,
            warning = 1.5f,
            duration = 1.75f,
            count = 20,
            rings = 1,
            drift = 0f,
            rows = 7,
            gap = 3,
            style = 0
        };
        for (int i = 1; i < parts.Length; i++)
        {
            string[] pair = parts[i].Split('=');
            if (pair.Length != 2) continue;
            float number;
            if (!float.TryParse(pair[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out number)) continue;
            switch (pair[0].Trim().ToLowerInvariant())
            {
                case "x": spec.x = number; break;
                case "y": spec.y = number; break;
                case "angle": spec.angle = number; break;
                case "width": spec.width = number; break;
                case "radius": spec.radius = number; break;
                case "count": spec.count = Mathf.RoundToInt(number); break;
                case "rings": spec.rings = Mathf.RoundToInt(number); break;
                case "warning": spec.warning = number; break;
                case "duration": spec.duration = number; break;
                case "drift": spec.drift = number; break;
                case "rows": spec.rows = Mathf.RoundToInt(number); break;
                case "gap": spec.gap = Mathf.RoundToInt(number); break;
                case "style": spec.style = Mathf.RoundToInt(number); break;
            }
        }
        spec.x = Mathf.Clamp01(spec.x);
        spec.y = Mathf.Clamp01(spec.y);
        spec.angle = Mathf.Clamp(spec.angle, -70f, 70f);
        spec.width = Mathf.Clamp(spec.width, 0.015f, 0.16f);
        spec.radius = Mathf.Clamp(spec.radius, 0.10f, 0.9f);
        spec.warning = Mathf.Clamp(spec.warning, 0.25f, 8f);
        spec.duration = Mathf.Clamp(spec.duration, 0.25f, 8f);
        spec.count = Mathf.Clamp(spec.count, 6, 48);
        spec.rings = Mathf.Clamp(spec.rings, 1, 3);
        spec.drift = Mathf.Clamp(spec.drift, -0.40f, 0.40f);
        // 구 버전 9~12줄짜리 동시 난사를 의도적인 4~7줄 패턴으로 제한.
        spec.rows = Mathf.Clamp(spec.rows, 4, 7);
        // Chart-authored gap=7 is intentionally a wide dodge corridor.
        // Do not silently clamp it to 5 and crowd the falling bullets.
        spec.gap = Mathf.Clamp(spec.gap, 1, 7);
        spec.style = Mathf.Clamp(spec.style, 0, 3);
        if (spec.gap % 2 == 0) spec.gap += 1; // corridor always odd-width
        return spec;
    }

    public static string EncodeAttack(AttackSpec spec)
    {
        var c = System.Globalization.CultureInfo.InvariantCulture;
        if (spec.kind == AttackKind.Legacy) return spec.legacyId ?? "basic";
        string s = spec.kind == AttackKind.Laser ? "laser" :
            spec.kind == AttackKind.Burst ? "burst" : "rain";
        s += "|x=" + spec.x.ToString("0.###", c);
        if (spec.kind == AttackKind.Rain)
        {
            s += "|drift=" + spec.drift.ToString("0.###", c);
            s += "|rows=" + spec.rows.ToString(c);
            s += "|gap=" + spec.gap.ToString(c);
            s += "|style=" + spec.style.ToString(c);
        }
        else if (spec.kind == AttackKind.Laser)
        {
            s += "|angle=" + spec.angle.ToString("0.###", c);
            s += "|width=" + spec.width.ToString("0.###", c);
        }
        else
        {
            s += "|y=" + spec.y.ToString("0.###", c);
            s += "|radius=" + spec.radius.ToString("0.###", c);
            s += "|count=" + spec.count.ToString(c);
            s += "|rings=" + Mathf.Clamp(spec.rings, 1, 3).ToString(c);
        }
        return s + "|warning=" + spec.warning.ToString("0.###", c) +
            "|duration=" + spec.duration.ToString("0.###", c);
    }

    private sealed class SpecialAttack
    {
        public AttackSpec spec;
        public double warnStart, fireStart, fireEnd;
        public RectTransform root;
        public Image outer, face, core, warning, source, ring, orb;
        public Image chargeBand, tickA, tickB, bombTrail, reticleH, reticleV;
        public readonly List<RectTransform> pellets = new List<RectTransform>();
        public bool wasFiring;
    }

    private readonly List<SpecialAttack> specialAttacks = new List<SpecialAttack>();

    private sealed class RainCue
    {
        public AttackSpec spec;
        public float beat;
        public double visibleStart, visibleEnd, firstImpact, secondsPerBeat;
    }
    private readonly List<RainCue> rainCues = new List<RainCue>();
    // Keep only the small SAFE CORRIDOR brackets. The temporary red launchers
    // have been replaced by the animated Enemy01 sprites.
    private RectTransform safeGateLeft;
    private RectTransform safeGateRight;
    private Texture2D attackRingTexture;
    private Sprite attackRingSprite;

    private void EnsureAttackRingSprite()
    {
        if (attackRingSprite != null) return;
        const int size = 96;
        attackRingTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        attackRingTexture.filterMode = FilterMode.Bilinear;
        attackRingTexture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - size * .5f;
                float dy = y + 0.5f - size * .5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(46.5f - r) * Mathf.Clamp01(r - 43.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        attackRingTexture.SetPixels(pixels);
        attackRingTexture.Apply();
        attackRingSprite = Sprite.Create(attackRingTexture,
            new Rect(0f, 0f, size, size), new Vector2(.5f, .5f));
    }


    // Small 2-tone circular shot, generated at runtime. No neon or pre-rendered FX.
    private sealed class Shot
    {
        public RectTransform body;
        public RectTransform trail;
        public bool rain;
        public RectTransform warningLine;
        public RectTransform warningMark;
        public Image warningLineImage;
        public Image warningMarkImage;
        public double warningStart;
        public double launchTime;
        public double impactTime;
        public double endTime;
        public float fromX;
        public float toX;
        public bool aimAtWarning;
        public bool aimed;
        public bool telegraph;
        public float curve;
        public float originY;
        public Vector2 lastPosition;
        public bool hasLastPosition;
        public bool hitPino;
        public bool grazed;
    }

    [Header("Enemy01 early barrage warning")]
    [SerializeField] private bool showAnimatedEnemyBeforeAttacks = true;
    [SerializeField, Range(0.25f, 1.5f)] private float enemyLeadBeforeWarning = 0.95f;
    [SerializeField, Range(0.1f, 1f)] private float enemyLeaveAfterAttack = 0.40f;
    private MouseEnemy01TelegraphUI enemy01;

    private readonly List<Shot> shots = new List<Shot>(96);
    private readonly List<AdvancedShot> advancedShots = new List<AdvancedShot>(256);
    // Reuse UI Images during dense barrages. Destroy/Create every pellet
    // causes GC and frame-time spikes exactly when timing is hardest.
    private readonly Stack<RectTransform> advancedBulletVisualPool =
        new Stack<RectTransform>(256);
    private const int MaxPooledAdvancedBulletVisuals = 512;
    private RectTransform bulletLayer;
    private RectTransform virusEmitter;

    [Header("Chart projectile source (optional: VirusRoot / emitter transform)")]
    [SerializeField] private RectTransform projectileEmitter;
    private Texture2D shardTexture;
    private Sprite shardSprite;
    private Texture2D pulseTexture;
    private Sprite pulseSprite;
    private Texture2D diamondTexture;
    private Sprite diamondSprite;
    private Texture2D blastTexture;
    private Sprite blastSprite;

    private static readonly Color BurstTint = new Color(0.98f, 0.67f, 0.31f, 1f);
    private MouseModeChartPlayer subscribedPlayer;
    private MouseModeChartAsset scheduledChart;
    private bool scheduleBuilt;
    private bool failed;
    private bool pendingRhythmFailure;
    private double lastGrazeTime = double.NegativeInfinity;
    private TMP_Text grazeLabel;
    private float grazeLabelUntil;
    private bool fallbackGaugeCreated;
    private RectTransform runtimeGaugeRoot;
    private double lastSongSeconds = double.NaN;
    private Vector2 previousPinoLocal;
    private bool hasPreviousPinoLocal;
    private RectTransform dangerForeground;
    private double invulnerableUntil = double.NegativeInfinity;
    private static readonly FieldInfo PlayerStartDspField =
        typeof(MouseModeChartPlayer).GetField("startDsp", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo PlayerRunningField =
        typeof(MouseModeChartPlayer).GetField("running", BindingFlags.Instance | BindingFlags.NonPublic);

    // 현재 테스트 채보(사용자가 제공한 JSON)의 안전한 예비 사본.
    // Assets에 같은 이름의 JSON이 있으면 항상 그 파일을 우선합니다.
    private const string EmbeddedSampleJson = @"{""version"":1,""songId"":""sample_mouse_chart"",""bpm"":120.0,""beatZeroSeconds"":0.0,""approachSeconds"":1.2,""notes"":[{""id"":1,""kind"":0,""beat"":4.0,""position"":{""x"":0.3,""y"":0.35},""endBeat"":6.0,""path"":[]},{""id"":2,""kind"":1,""beat"":5.0,""position"":{""x"":0.7,""y"":0.37},""endBeat"":7.0,""path"":[]},{""id"":3,""kind"":0,""beat"":6.0,""position"":{""x"":0.42,""y"":0.5},""endBeat"":8.0,""path"":[]},{""id"":4,""kind"":1,""beat"":7.0,""position"":{""x"":0.65,""y"":0.54},""endBeat"":9.0,""path"":[]},{""id"":5,""kind"":0,""beat"":10.0,""position"":{""x"":0.46,""y"":0.35},""endBeat"":12.0,""path"":[]},{""id"":6,""kind"":1,""beat"":10.0,""position"":{""x"":0.56,""y"":0.35},""endBeat"":12.0,""path"":[]},{""id"":7,""kind"":2,""beat"":12.0,""position"":{""x"":0.3,""y"":0.44},""endBeat"":15.0,""path"":[{""x"":0.3,""y"":0.44},{""x"":0.5,""y"":0.58},{""x"":0.7,""y"":0.43}]}],""attacks"":[{""beat"":6.0,""patternId"":""basic""},{""beat"":10.0,""patternId"":""split""},{""beat"":12.0,""patternId"":""spread""}]}";

    private static readonly FieldInfo PlayerChartField =
        typeof(MouseModeChartPlayer).GetField("chartAsset",
            BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo PlayerNoteLayerField =
        typeof(MouseModeChartPlayer).GetField("noteLayer",
            BindingFlags.Instance | BindingFlags.NonPublic);
    private MouseModeChartAsset temporaryChart;
    private bool chartResolved;

    // The old AfterSceneLoad-only bootstrap runs once when Play Mode starts.
    // It misses Mouse when the player first visits Title -> SongSelect -> Mouse.
    // Register for *every* scene load, including gameplay launches from the
    // selected cartridge. This also handles re-entry after a completed song.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterMouseSceneLoader()
    {
        SceneManager.sceneLoaded -= OnMouseSceneLoaded;
        SceneManager.sceneLoaded += OnMouseSceneLoaded;
    }

    private static void OnMouseSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.IsValid() && scene.name == "Mouse")
            BootstrapMouseScene(scene);
    }

    // Also support starting Play Mode directly in Mouse.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapMouseScene()
    {
        BootstrapMouseScene(SceneManager.GetActiveScene());
    }

    // Editor Play Mode restores automatically created runtime objects.
    private static void BootstrapMouseScene(Scene activeScene)
    {
        if (!activeScene.IsValid() || activeScene.name != "Mouse") return;

        MouseModeChartPlayer player = null;
        foreach (MouseModeChartPlayer candidate in FindObjectsOfType<MouseModeChartPlayer>(true))
        {
            if (candidate != null && candidate.gameObject.scene == activeScene)
            {
                player = candidate;
                break;
            }
        }
        if (player == null)
        {
            Debug.LogError("[MouseBullet] Mouse 씬에서 MouseModeChartPlayer를 찾지 못했습니다. " +
                           "Systems/MouseNoteManager 컴포넌트를 확인하세요.");
            return;
        }

        RectTransform playArea = null;
        foreach (GameObject root in activeScene.GetRootGameObjects())
        {
            Transform canvas = FindNamedChild(root.transform, "GameplayCanvas");
            if (canvas == null) continue;
            playArea = canvas.Find("PlayArea") as RectTransform;
            if (playArea != null) break;
        }
        if (playArea == null)
        {
            Debug.LogError("[MouseBullet] GameplayCanvas/PlayArea를 찾지 못했습니다.");
            return;
        }

        // ChartPlayer.StartChart()는 NoteLayer가 없으면 이벤트 전체를 재생하지 않습니다.
        // 기존 레이어가 있다면 그대로 재사용하고, 비어 있는 참조만 자동으로 연결합니다.
        Transform noteObject = playArea.Find("NoteLayer");
        RectTransform noteRect = noteObject as RectTransform;
        if (noteObject != null && noteRect == null)
        {
            Debug.LogError("[MouseBullet] 기존 NoteLayer는 RectTransform이어야 합니다.");
            return;
        }
        if (noteRect == null)
        {
            GameObject createdNoteLayer = new GameObject("NoteLayer", typeof(RectTransform));
            noteRect = createdNoteLayer.GetComponent<RectTransform>();
            noteRect.SetParent(playArea, false);
            noteRect.anchorMin = Vector2.zero;
            noteRect.anchorMax = Vector2.one;
            noteRect.offsetMin = Vector2.zero;
            noteRect.offsetMax = Vector2.zero;
            Debug.Log("[MouseBullet] 테스트용 NoteLayer를 생성했습니다.");
        }
        if (PlayerNoteLayerField != null && PlayerNoteLayerField.GetValue(player) == null)
        {
            PlayerNoteLayerField.SetValue(player, noteRect);
            Debug.Log("[MouseBullet] MouseModeChartPlayer의 NoteLayer 자동 연결 성공.");
        }

        Transform foundLayer = playArea.Find("BulletLayer");
        RectTransform rect = foundLayer as RectTransform;
        if (foundLayer != null && rect == null)
        {
            Debug.LogError("[MouseBullet] BulletLayer는 RectTransform이어야 합니다.");
            return;
        }
        if (rect == null)
        {
            GameObject go = new GameObject("BulletLayer", typeof(RectTransform));
            rect = go.GetComponent<RectTransform>();
            rect.SetParent(playArea, false);
            Debug.Log("[MouseBullet] 테스트용 BulletLayer를 자동 생성했습니다.");
        }
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        // Put bullet layer directly BELOW the notes whenever NoteLayer is a
        // transparent container. This keeps rhythm symbols legible over attacks.
        // Preserve the older layering when NoteLayer has a graphic of its own.
        Image noteLayerImage = noteRect.GetComponent<Image>();
        if (noteLayerImage == null || !noteLayerImage.enabled ||
            noteLayerImage.color.a <= .001f)
        {
            int noteOrder = noteRect.GetSiblingIndex();
            int bulletOrder = rect.GetSiblingIndex();
            rect.SetSiblingIndex(bulletOrder < noteOrder ? noteOrder - 1 : noteOrder);
        }
        else
        {
            int noteOrder = noteRect.GetSiblingIndex();
            int bulletOrder = rect.GetSiblingIndex();
            rect.SetSiblingIndex(bulletOrder < noteOrder ? noteOrder : noteOrder + 1);
        }

        MouseModeBulletDirector director = rect.GetComponent<MouseModeBulletDirector>();
        if (director == null)
            director = rect.gameObject.AddComponent<MouseModeBulletDirector>();

        director.FindReferences();
        director.ResolvePlayableChart();
        director.Subscribe();
    }

    private static Transform FindNamedChild(Transform t, string target)
    {
        if (t.name == target) return t;
        foreach (Transform child in t)
        {
            Transform found = FindNamedChild(child, target);
            if (found != null) return found;
        }
        return null;
    }

    private MouseModeChartAsset CurrentPlayerChart()
    {
        if (chartPlayer == null || PlayerChartField == null) return null;
        return PlayerChartField.GetValue(chartPlayer) as MouseModeChartAsset;
    }

    private void ResolvePlayableChart()
    {
        if (chartPlayer == null || PlayerChartField == null) return;

        MouseModeChartAsset current = CurrentPlayerChart();
        // Don't replace a live chart repeatedly when the director runs Awake/OnEnable/Start.
        if (chartResolved && temporaryChart != null && current == temporaryChart)
            return;

        // An asset explicitly selected in the chart editor is authoritative.
        // Do not silently override it with some unrelated JSON in Assets.
        if (current != null && current.data != null && current != temporaryChart)
        {
            current.data.EnsureLists();
            chartAsset = current;
            chartResolved = true;
            return;
        }
        // Fallback JSON is used only if the player has NO valid chart asset.
        string sourceName;
        string json = FindChartJson(out sourceName);
        if (string.IsNullOrEmpty(json))
        {
            if (current != null && current.data != null)
            {
                current.data.EnsureLists();
                chartAsset = current;
                chartResolved = true;
                Debug.LogWarning("[MouseBullet] sample_mouse_chart.json not found in Assets. Playing existing chart asset: " + current.name, this);
                return;
            }
            json = EmbeddedSampleJson;
            sourceName = "fallback (temporary sample, not BOOTBEAT)";
            Debug.LogWarning("[MouseBullet] New BOOTBEAT chart JSON missing from Assets; using fallback. Import sample_mouse_chart.json.", this);
        }
        try
        {
            MouseModeChartData imported = JsonUtility.FromJson<MouseModeChartData>(json);
            if (imported == null || imported.version != 1 || imported.bpm <= 0f)
                throw new Exception("Invalid chart JSON (expected version 1 with positive BPM).");
            imported.EnsureLists();
            if (temporaryChart == null)
            {
                temporaryChart = ScriptableObject.CreateInstance<MouseModeChartAsset>();
                temporaryChart.hideFlags = HideFlags.DontSave;
            }
            temporaryChart.data = imported;
            // JSON stores timing, not AudioClip. Keep the connected song when possible.
            AudioClip existingClip = current != null ? current.song : null;
            if (existingClip == null && chartAsset != null) existingClip = chartAsset.song;
            // This specific chart uses BOOTBEAT. Do not accidentally play an older song.
            if (imported.songId == "BOOTBEAT_BOSS_EASY" ||
                imported.songId == "BOOTBEAT_INTRUSION_EXPERT" ||
                imported.songId == "BOOTBEAT_INTRUSION_EXPERT_READABLE" ||
                imported.songId == "BOOTBEAT_INTRUSION_EXPERT_FLOW_RAIN_V3" ||
                imported.songId == "BOOTBEAT_INTRUSION_EXPERT_CHOREOGRAPHY_V4" ||
                imported.songId == "BOOTBEAT_INTRUSION_EXPERT_PHASED_V6")
            {
                AudioClip bootbeat = FindBootbeatAudioClipInEditor();
                if (bootbeat != null) existingClip = bootbeat;
            }
            else if (existingClip == null) existingClip = FindBootbeatAudioClipInEditor();
            temporaryChart.song = existingClip;
            PlayerChartField.SetValue(chartPlayer, temporaryChart);
            chartAsset = temporaryChart;
            chartResolved = true;
            Debug.Log("[MouseBullet] Loaded chart from " + sourceName +
                " | " + imported.notes.Count + " notes, " + imported.attacks.Count +
                " attacks | audio=" + (existingClip != null ? existingClip.name : "NONE"), this);
        }
        catch (Exception e)
        {
            Debug.LogError("[MouseBullet] JSON load failed: " + e.Message, this);
        }
    }

    private static AudioClip FindBootbeatAudioClipInEditor()
    {
#if UNITY_EDITOR
        AudioClip backup = null;
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            if (!name.StartsWith("BOOTBEAT", StringComparison.OrdinalIgnoreCase)) continue;
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) continue;
            if (string.Equals(name, "BOOTBEAT(4)", StringComparison.OrdinalIgnoreCase)) return clip;
            if (backup == null) backup = clip;
        }
        return backup;
#else
        return null;
#endif
    }

    private static string FindChartJson(out string sourceName)
    {
        sourceName = null;
#if UNITY_EDITOR
        TextAsset best = null;
        int bestRank = -1;
        string bestPath = null;
        foreach (string guid in AssetDatabase.FindAssets("t:TextAsset"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
            string name = Path.GetFileNameWithoutExtension(path);
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset == null || string.IsNullOrEmpty(asset.text)) continue;

            int rank = -1;
            // This intentionally chooses the EXPERT chart while testing it.
            if (name.Equals("BOOTBEAT_INTRUSION_EXPERT_PHASED_V6", StringComparison.OrdinalIgnoreCase)) rank = 300;
            else if (name.Equals("BOOTBEAT_INTRUSION_EXPERT_PLAYFEEL_V5", StringComparison.OrdinalIgnoreCase)) rank = 240;
            else if (name.Equals("BOOTBEAT_INTRUSION_EXPERT_CHOREOGRAPHY_V4", StringComparison.OrdinalIgnoreCase)) rank = 200;
            else if (name.Equals("BOOTBEAT_INTRUSION_EXPERT_FLOW_RAIN_V3", StringComparison.OrdinalIgnoreCase)) rank = 140;
            else if (name.Equals("BOOTBEAT_INTRUSION_EXPERT_READABLE", StringComparison.OrdinalIgnoreCase)) rank = 120;
            else if (name.Equals("BOOTBEAT_INTRUSION_EXPERT", StringComparison.OrdinalIgnoreCase)) rank = 100;
            else if (name.Equals("sample_mouse_chart", StringComparison.OrdinalIgnoreCase) &&
                     asset.text.Contains("BOOTBEAT_INTRUSION_EXPERT")) rank = 95;
            else if (name.StartsWith("BOOTBEAT_INTRUSION_EXPERT", StringComparison.OrdinalIgnoreCase)) rank = 92;
            else if (name.StartsWith("BOOTBEAT_BOSS_EASY", StringComparison.OrdinalIgnoreCase)) rank = 30;
            else if (name.Equals("sample_mouse_chart", StringComparison.OrdinalIgnoreCase)) rank = 20;
            else if (name.StartsWith("sample_mouse_chart", StringComparison.OrdinalIgnoreCase)) rank = 10;

            if (rank > bestRank)
            {
                bestRank = rank;
                best = asset;
                bestPath = path;
            }
        }
        if (best != null) { sourceName = bestPath; return best.text; }
#endif
        return null;
    }

    private void Awake()
    {
        bulletLayer = GetComponent<RectTransform>();
        // 이전 스크립트는 모든 탄을 최소 50px로 강제했다.
        // 기존 씬에 저장된 큰 수치도 적정 크기 범위로 보정한다.
        bulletSize = Mathf.Clamp(bulletSize, 26f, 32f);
        // Inspector 설정을 매번 4.8/1.55/1.8 등으로 강제로 덮어쓰지 않습니다.
        fallDurationBeats = Mathf.Clamp(fallDurationBeats, 2f, 8f);
        advancedTravelPerBeat = Mathf.Clamp(advancedTravelPerBeat, .07f, .30f);
        warningLeadBeats = Mathf.Clamp(warningLeadBeats, .5f, 2.2f);
        exitDurationBeats = Mathf.Clamp(exitDurationBeats, .5f, 3f);
        damagePerHit = Mathf.Clamp(damagePerHit, 1, 100);
        hitInvulnerabilitySeconds = Mathf.Clamp(hitInvulnerabilitySeconds, .1f, 2.5f);
        FindReferences();
        ResolvePlayableChart();
        CreateShardSprite();
        // No placeholder emitter: the existing Virus art is the attack source.
        CreateRainGateMarkers();
        SetupEnemy01();
        // Remove a leftover placeholder from an earlier Play session, if present.
        if (bulletLayer != null)
        {
            Transform oldFallback = bulletLayer.Find("AttackEmitter_Fallback");
            if (oldFallback != null) Destroy(oldFallback.gameObject);
        }
        EnsureStabilityDisplay();
        UpdateStabilityVisual();
        if (failurePanel != null) failurePanel.SetActive(false);
    }

    private void OnEnable()
    {
        FindReferences();
        ResolvePlayableChart();
        Subscribe();
    }

    private void Start()
    {
        FindReferences();
        ResolvePlayableChart();
        Subscribe();
        if (chartPlayer == null)
            Debug.LogError("[MouseBullet] MouseModeChartPlayer가 없습니다.", this);
        if (PlayerStartDspField == null || PlayerRunningField == null || PlayerChartField == null)
            Debug.LogError("[MouseBullet] MouseModeChartPlayer의 실행 필드가 현재 스크립트 버전과 다릅니다. " +
                "startDsp / running / chartAsset 필드를 찾지 못했습니다.", this);
        if (pino == null)
            Debug.LogWarning("[MouseBullet] Pino를 찾지 못했습니다. 탄막은 나오지만 피격 판정은 할 수 없습니다.", this);
    }

    private void FindReferences()
    {
        if (chartPlayer == null)
        {
            GameObject manager = GameObject.Find("MouseNoteManager");
            if (manager != null) chartPlayer = manager.GetComponent<MouseModeChartPlayer>();
            if (chartPlayer == null)
            {
                foreach (MouseModeChartPlayer candidate in FindObjectsOfType<MouseModeChartPlayer>(true))
                {
                    if (candidate != null && candidate.gameObject.scene == gameObject.scene)
                    {
                        chartPlayer = candidate;
                        break;
                    }
                }
            }
        }

        // Always prefer the chart that the note player is actually playing.
        if (chartPlayer != null)
        {
            MouseModeChartAsset current = CurrentPlayerChart();
            if (current != null) chartAsset = current;
        }

        // Pino may be nested deeper than PlayArea/PinoMoveArea/Pino.
        // Search the scene hierarchy instead of assuming one fixed parent path.
        if (pino == null || !pino.gameObject.activeInHierarchy)
            ResolvePinoInScene();
        if (pinoAnimator == null && pino != null)
            pinoAnimator = pino.GetComponent<PinoSpriteAnimator>();
        EnsurePinoHitbox();

        if (virusEmitter == null && transform.parent != null)
        {
            Transform candidate = transform.parent.Find("VirusRoot");
            if (candidate == null) candidate = transform.parent.Find("Virus");
            virusEmitter = candidate as RectTransform;
        }
    }

    private float nextPinoSearchTime;
    private bool warnedPinoMissing;

    private void ResolvePinoInScene()
    {
        if (Time.unscaledTime < nextPinoSearchTime) return;
        nextPinoSearchTime = Time.unscaledTime + 0.35f;
        if (bulletLayer == null) bulletLayer = GetComponent<RectTransform>();
        // Only objects in THIS scene count. Do not attach to another scene's Pino.
        RectTransform best = null;
        int bestScore = int.MinValue;
        foreach (RectTransform candidate in FindObjectsOfType<RectTransform>(true))
        {
            if (candidate == null || candidate.gameObject.scene != gameObject.scene) continue;
            if (!candidate.gameObject.activeInHierarchy) continue;
            int score = 0;
            if (candidate.name == "Pino") score += 100;
            else if (candidate.name.StartsWith("Pino")) score += 30;
            else continue;
            if (candidate.GetComponent<PinoMouseCursorUI>() != null) score += 80;
            if (candidate.GetComponent<PinoSpriteAnimator>() != null) score += 40;
            if (candidate.parent != null && candidate.parent.name == "PinoMoveArea") score += 20;
            if (score > bestScore) { best = candidate; bestScore = score; }
        }
        if (best != null)
        {
            pino = best;
            pinoAnimator = best.GetComponent<PinoSpriteAnimator>();
            pinoHitbox = null;
            EnsurePinoHitbox();
            warnedPinoMissing = false;
            Debug.Log("[MouseBullet] Pino 피격 중심 자동 연결: " + GetHierarchyPath(best), this);
        }
        else if (!warnedPinoMissing)
        {
            warnedPinoMissing = true;
            Debug.LogWarning("[MouseBullet] 피노를 찾지 못했습니다. Pino 오브젝트에 RectTransform이 있어야 피격됩니다.", this);
        }
    }

    // The game uses RectTransform/UI bullets, so we read the CircleCollider2D
    // geometry and perform swept tests in BulletLayer local coordinates.
    // This does not require Rigidbody2D or physics trigger callbacks.
    private void EnsurePinoHitbox()
    {
        if (pino == null) return;
        if (pinoHitbox != null && !pinoHitbox.transform.IsChildOf(pino))
            pinoHitbox = null;
        if (pinoHitbox == null)
            pinoHitbox = pino.GetComponent<CircleCollider2D>();
        if (pinoHitbox == null)
            pinoHitbox = pino.GetComponentInChildren<CircleCollider2D>(true);
        if (pinoHitbox != null) return; // Never modify an existing artist-tuned hitbox.

        pinoHitbox = pino.gameObject.AddComponent<CircleCollider2D>();
        pinoHitbox.isTrigger = true;
        pinoHitbox.offset = pino.rect.center;
        float dimension = Mathf.Min(Mathf.Abs(pino.rect.width), Mathf.Abs(pino.rect.height));
        pinoHitbox.radius = dimension > 0.01f
            ? dimension * defaultHitboxRadiusRatio : pinoHitRadius;
        Debug.Log("[MouseBullet] 피노 CircleCollider2D 자동 생성. 반경=" +
            pinoHitbox.radius.ToString("0.##") + " (RectTransform 로컬 단위)", this);
    }

    private float PinoRadiusInBulletLayer()
    {
        if (pinoHitbox == null || !pinoHitbox.enabled || bulletLayer == null)
            return pinoHitRadius;
        Transform t = pinoHitbox.transform;
        Vector3 axisX = bulletLayer.InverseTransformVector(t.TransformVector(Vector3.right * pinoHitbox.radius));
        Vector3 axisY = bulletLayer.InverseTransformVector(t.TransformVector(Vector3.up * pinoHitbox.radius));
        // Take the smaller axis when UI is non-uniformly scaled, favoring generous dodges.
        return Mathf.Max(0.1f, Mathf.Min(((Vector2)axisX).magnitude, ((Vector2)axisY).magnitude));
    }

    private static string GetHierarchyPath(Transform target)
    {
        string path = target.name;
        for (Transform parent = target.parent; parent != null; parent = parent.parent)
            path = parent.name + "/" + path;
        return path;
    }

    private void Subscribe()
    {
        if (subscribedPlayer == chartPlayer) return;
        Unsubscribe();
        if (chartPlayer != null)
        {
            // Keep event compatibility. Patterns are scheduled up-front from the
            // same chart to show warnings BEFORE the event beat, without duplicates.
            chartPlayer.AttackTriggered += OnAttackTriggered;
            chartPlayer.ChartNoteJudged += OnRhythmNoteJudged;
            subscribedPlayer = chartPlayer;
        }
    }

    private void OnAttackTriggered(MouseModeChartAttack attack)
    {
        // Marker only. Projectiles are scheduled from the chart data in advance,
        // and use the SAME DSP time reference as MouseModeChartPlayer.
    }

    private void Unsubscribe()
    {
        if (subscribedPlayer != null)
        {
            subscribedPlayer.AttackTriggered -= OnAttackTriggered;
            subscribedPlayer.ChartNoteJudged -= OnRhythmNoteJudged;
        }
        subscribedPlayer = null;
    }

    private bool PlayerIsRunning()
    {
        return chartPlayer != null && PlayerRunningField != null &&
            (bool)PlayerRunningField.GetValue(chartPlayer);
    }

    private double GetChartSeconds()
    {
        if (!PlayerIsRunning() || PlayerStartDspField == null) return double.NaN;
        // Matching MouseModeChartPlayer.Update() exactly. This also works for
        // a silent test chart; AudioSource.timeSamples does NOT work in that case.
        double start = (double)PlayerStartDspField.GetValue(chartPlayer);
        return AudioSettings.dspTime - start;
    }

    private void BuildScheduleIfNeeded()
    {
        if (chartPlayer == null) return;
        MouseModeChartAsset current = CurrentPlayerChart();
        if (current == null || current.data == null) return;
        if (scheduleBuilt && current == scheduledChart) return;

        ClearShots();
        chartAsset = current;
        scheduledChart = current;
        scheduleBuilt = true;
        MouseModeChartData chart = current.data;
        chart.EnsureLists();
        if (chart.bpm <= 0f) return;
        SetupEnemy01();
        List<MouseEnemy01TelegraphUI.Cue> enemyCues =
            new List<MouseEnemy01TelegraphUI.Cue>();
        foreach (MouseModeChartAttack attack in chart.attacks)
        {
            if (attack == null) continue;
            int shotStart = shots.Count;
            int advancedStart = advancedShots.Count;
            int specialStart = specialAttacks.Count;
            int rainStart = rainCues.Count;
            double secondsPerBeat = MouseModeChartTiming.SecondsPerBeatAt(chart, attack.beat);
            AttackSpec spec = ParseAttack(attack.patternId);
            if (spec.kind == AttackKind.Rain)
            {
                AddRainPattern(spec, attack.beat, chart, secondsPerBeat);
            }
            else if (spec.kind != AttackKind.Legacy)
            {
                double hit = MouseModeChartTiming.BeatToSeconds(chart, attack.beat);
                specialAttacks.Add(new SpecialAttack
                {
                    spec = spec,
                    warnStart = EnsureTelegraphLead(
                        MouseModeChartTiming.BeatToSeconds(chart, attack.beat - spec.warning),
                        hit, 0.65d),
                    fireStart = hit,
                    fireEnd = MouseModeChartTiming.BeatToSeconds(chart, attack.beat + spec.duration)
                });
                if (spec.kind == AttackKind.Burst)
                    QueueBurstProjectiles(spec, hit, secondsPerBeat);
            }
            else AddPattern(spec.legacyId, attack.beat, chart, secondsPerBeat);

            // Derive the appearance from the same scheduled actual shots.
            // This covers rain, advanced patterns, lasers and explosions without
            // having to guess timing from the visible beat alone.
            QueueEnemy01Cue(enemyCues, spec, shotStart, advancedStart,
                specialStart, rainStart);
        }
        if (enemy01 != null)
            enemy01.SetCues(enemyCues);
        int lasers = 0;
        int bursts = 0;
        int rainEvents = 0;
        foreach (MouseModeChartAttack attack in chart.attacks)
            if (attack != null && ParseAttack(attack.patternId).kind == AttackKind.Rain) rainEvents++;
        foreach (SpecialAttack special in specialAttacks)
        {
            if (special.spec.kind == AttackKind.Laser) lasers++;
            if (special.spec.kind == AttackKind.Burst) bursts++;
        }
        Debug.Log("[MouseBullet] ACTIVE CHART: " + current.name +
            " / songId=" + chart.songId +
            " / attacks=" + chart.attacks.Count +
            " / fallingShots=" + shots.Count +
            " / lasers=" + lasers + " / bursts=" + bursts +
            " / rainEvents=" + rainEvents +
            " / layer=" + (bulletLayer != null ? bulletLayer.rect.size.ToString() : "MISSING"), this);
        if (chart.attacks.Count == 0)
            Debug.LogWarning("[MouseBullet] 현재 플레이 중인 채보의 attacks가 0개입니다. " +
                "RHYTHM용 채보 또는 JSON 미적용 상태라면 공격은 표시되지 않습니다. " +
                "채보 에디터의 JSON 가져오기로 INTRUSION용 채보를 적용하세요.", this);
        if (bulletLayer != null && (bulletLayer.rect.width < 10f || bulletLayer.rect.height < 10f))
            Debug.LogError("[MouseBullet] BulletLayer 크기가 0에 가깝습니다. PlayArea와 RectTransform을 확인하세요.", this);
    }

    // The attack beat now means the first IMPACT beat, not the beginning of
    // several unrelated seconds of falling. A pattern spans 2-4 musical beats.
    // BOOTBEAT EASY: small, readable waves. Only a handful of shots per attack.
    // The beat of each event is the first shot's crossing time, not an arbitrary delay.
    // No full-screen bullet walls or sudden homing movement.
    // Legacy ID support is retained; EXPERT IDs are entirely separate.
    // The chart beat denotes the first projectile crossing the fixed risk line.
    // Each shot is visually scheduled from the same DSP clock as the notes.
    private void AddPattern(string id, float baseBeat, MouseModeChartData chart, double beatSec)
    {
        // Match built-in names by base ID but keep the original encoded
        // pattern for GenerateForChart. This makes chart-authored variation
        // (spread / spacing / density) work in the actual game.
        string patternKey = (id ?? "").Split('|')[0];
        switch (patternKey)
        {
            case "basic":
                AddRow(chart, beatSec, baseBeat, 0.29f);
                AddRow(chart, beatSec, baseBeat + 2f, 0.71f);
                break;
            case "left":
                AddRow(chart, beatSec, baseBeat, 0.27f);
                AddRow(chart, beatSec, baseBeat + 2f, 0.40f);
                break;
            case "right":
                AddRow(chart, beatSec, baseBeat, 0.73f);
                AddRow(chart, beatSec, baseBeat + 2f, 0.60f);
                break;
            case "split":
                AddRow(chart, beatSec, baseBeat, 0.28f, 0.72f);
                AddRow(chart, beatSec, baseBeat + 2f, 0.36f, 0.64f);
                break;
            case "spread":
            case "fan":
                AddFan(chart, beatSec, baseBeat, 0.22f, 0.78f);
                AddFan(chart, beatSec, baseBeat + 2f, 0.49f);
                break;
            case "rain":
                AddRow(chart, beatSec, baseBeat, 0.28f);
                AddRow(chart, beatSec, baseBeat + 1.5f, 0.68f);
                AddRow(chart, beatSec, baseBeat + 3f, 0.44f);
                break;
            case "aim":
                AddShot(chart, beatSec, baseBeat, BossEmitterX(baseBeat), 0.50f, true);
                break;

            // EXPERT: repeated fans shift between strong beats; safe positions move.
            case "ex_fan":
                for (int wave = 0; wave < 3; wave++)
                {
                    float beat = baseBeat + wave * 1.15f;
                    for (int i = 0; i < 5; i++)
                    {
                        float destination = 0.14f + i * 0.18f + (wave == 1 ? 0.025f : 0f);
                        float curve = wave == 1 ? (i < 2 ? -0.07f : 0.07f) : 0f;
                        AddShot(chart, beatSec, beat, BossEmitterX(beat), destination,
                            false, curve, i == 0 || i == 4);
                    }
                }
                break;

            // EXPERT: changing pairs; alternating side passage instead of idle dodging.
            case "ex_weave":
                for (int wave = 0; wave < 4; wave++)
                {
                    float beat = baseBeat + wave * 0.9f;
                    float side = wave % 2 == 0 ? 0.0f : 0.45f;
                    for (int i = 0; i < 3; i++)
                        AddShot(chart, beatSec, beat, BossEmitterX(beat),
                            0.12f + side + i * 0.14f, false, 0f, i == 0);
                }
                break;

            // EXPERT: mirrored diagonals from opposite flanks, crossing in the middle.
            case "ex_cross":
                for (int wave = 0; wave < 4; wave++)
                {
                    float beat = baseBeat + wave * 0.85f;
                    float displacement = wave * 0.10f;
                    AddShot(chart, beatSec, beat, 0.13f,
                        Mathf.Clamp01(0.84f - displacement), false, 0f, true);
                    AddShot(chart, beatSec, beat, 0.87f,
                        Mathf.Clamp01(0.16f + displacement), false, 0f, true);
                }
                break;

            // EXPERT: dense walls with a deliberately open, changing escape channel.
            case "ex_gate":
                for (int wave = 0; wave < 2; wave++)
                {
                    float beat = baseBeat + wave * 1.8f;
                    int gapIndex = ((Mathf.FloorToInt(baseBeat / 8f) + wave * 2) % 5 + 5) % 5;
                    for (int i = 0; i < 5; i++)
                    {
                        if (i == gapIndex) continue;
                        float laneX = 0.13f + i * 0.185f;
                        AddShot(chart, beatSec, beat, BossEmitterX(beat), laneX - .036f,
                            false, 0f, i == 0);
                        AddShot(chart, beatSec, beat, BossEmitterX(beat), laneX + .036f,
                            false, 0f, i == 4);
                    }
                }
                break;

            // EXPERT: horizontal chase-wave, then reverse on the next accented pulse.
            case "ex_sweep":
                for (int i = 0; i < 6; i++)
                {
                    float beat = baseBeat + i * 0.48f;
                    float x = 0.12f + i * 0.145f;
                    AddShot(chart, beatSec, beat, BossEmitterX(beat), x,
                        false, 0f, i == 0);
                    AddShot(chart, beatSec, beat, BossEmitterX(beat),
                        Mathf.Clamp01(1f - x), false, 0f, i == 0);
                }
                break;

            // EXPERT: two gently curved rotating curtains (arcs are not aimed shots).
            case "ex_spiral":
                for (int wave = 0; wave < 2; wave++)
                {
                    float beat = baseBeat + wave * 1.65f;
                    for (int i = 0; i < 5; i++)
                    {
                        float x = 0.16f + i * 0.17f;
                        float bend = (i - 2f) * (wave == 0 ? 0.035f : -0.035f);
                        AddShot(chart, beatSec, beat, BossEmitterX(beat), x,
                            false, bend, i == 0 || i == 4);
                    }
                }
                break;

            // EXPERT: outer bullets close toward the centre; escape route changes.
            case "ex_pincer":
                for (int wave = 0; wave < 3; wave++)
                {
                    float beat = baseBeat + wave * 1.1f;
                    float left = 0.14f + wave * 0.10f;
                    float right = 0.86f - wave * 0.10f;
                    foreach (float x in new float[] {left - 0.035f, left + 0.035f,
                                                     right - 0.035f, right + 0.035f})
                        AddShot(chart, beatSec, beat, BossEmitterX(beat), x,
                            false, 0f, Mathf.Abs(x - left) < .04f);
                }
                break;

            // EXPERT: three location-locked warnings, each with a real dodge window.
            case "ex_aim":
                for (int i = 0; i < 3; i++)
                {
                    float beat = baseBeat + i * 1.3f;
                    AddShot(chart, beatSec, beat, BossEmitterX(beat), 0.50f,
                        true, 0f, true);
                }
                break;

            // EXPERT: syncopated clusters that span multiple quarter-beats.
            case "ex_burst":
                for (int wave = 0; wave < 3; wave++)
                {
                    float beat = baseBeat + wave * 1.15f;
                    float shift = wave % 2 == 0 ? 0f : 0.06f;
                    for (int i = 0; i < 5; i++)
                    {
                        float destination = Mathf.Clamp01(.14f + i * .18f + shift);
                        AddShot(chart, beatSec, beat, BossEmitterX(beat), destination,
                            false, 0f, i == 0 || i == 4);
                    }
                }
                break;
            default:
                if (AddAdvancedPattern(id, baseBeat, chart, beatSec)) break;
                Debug.LogWarning("[MouseBullet] Unknown pattern: " + id, this);
                break;
        }
    }

    // Advanced patterns are also Legacy IDs in the existing chart format.
    // No change to saved JSON, ChartData, or the note player is required.
    private sealed class AdvancedShot
    {
        public double launch, end;
        public Vector2 start, direction;
        public float speed; // Pixels/second, calculated using stage height.
        public bool aimedAtLaunch;
        public bool initialized;
        public RectTransform body;
        public Image image;
        public Vector2 lastPosition;
        public bool hasLastPosition;
        // 화면 밖에서 시작한 탄환이 진입하기 전에 삭제되는 것을 방지합니다.
        public bool hasEnteredStage;
        public int colorIndex;
        public bool proceduralMotion;
        public MouseModeBulletPatternLibrary.Pellet motion;
        public float secondsPerBeat;
        public MouseModeChartData musicalChart;
        public float musicalLaunchBeat;
        public bool hitPino;
        public bool grazed;
        public bool burstFragment;
    }

    public static readonly string[] AdvancedPatternIds =
        { "adv_stream", "adv_spiral", "adv_ring", "adv_split", "adv_sweep", "adv_braid",
          "adv_softfan", "adv_turnfan", "adv_seedburst", "adv_edgecross" };
    public static readonly string[] AdvancedPatternLabels =
        { "조준 연속탄 (스트리밍)", "다중 회전 나선", "안전 틈새 링", "분열 방사탄", "좌우 교대 부채꼴", "양측 교차 편대",
          "저밀도 부채꼴", "회전 부채꼴", "이동 후 분열탄", "화면 횡단탄" };

    // The actual emitter is authoritative. Never conjure beginner-chart bullets
    // at unrelated screen positions; they start from a single physical source.
    private Vector2 SongPatternOrigin()
    {
        if (bulletLayer == null) bulletLayer = GetComponent<RectTransform>();
        RectTransform source = projectileEmitter != null ? projectileEmitter : virusEmitter;
        if (source != null)
            return bulletLayer.InverseTransformPoint(source.position);
        // Fallback for scenes without VirusRoot: top-centre of the play area.
        return LocalPoint(0.5f, 0.07f);
    }

    private void QueueAdvanced(double launch, double duration, Vector2 origin,
        float angleDegrees, float pixelsPerSecond, int colorIndex = 0,
        bool aimedAtLaunch = false, bool burstFragment = false)
    {
        float r = angleDegrees * Mathf.Deg2Rad;
        advancedShots.Add(new AdvancedShot
        {
            launch = launch,
            end = launch + duration,
            start = origin,
            direction = new Vector2(Mathf.Cos(r), Mathf.Sin(r)),
            speed = pixelsPerSecond,
            colorIndex = colorIndex,
            aimedAtLaunch = aimedAtLaunch,
            burstFragment = burstFragment
        });
    }

    // Spawn explosion fragments as independent projectiles. The burst warning
    // and detonation flash can end, but its fragments remain in flight until
    // their sprites are fully outside the play area.
    private void QueueBurstProjectiles(AttackSpec spec, double fireStart, double beatSec)
    {
        if (bulletLayer == null) bulletLayer = GetComponent<RectTransform>();
        float stageHeight = Mathf.Max(1f, bulletLayer.rect.height);
        Vector2 blastCenter = LocalPoint(spec.x, spec.y);
        int countPerRing = Mathf.Clamp(spec.count, 6, 48);
        int ringTotal = Mathf.Clamp(spec.rings, 1, 3);
        for (int ringIndex = 0; ringIndex < ringTotal; ringIndex++)
        {
            double launch = fireStart + ringIndex * .5d * beatSec;
            // The editor uses the same stage-heights-per-beat speed.
            float velocity = stageHeight *
                (.075f + spec.radius * .05f) * (1f + ringIndex * .08f) /
                Mathf.Max(.001f, (float)beatSec);
            for (int pelletIndex = 0; pelletIndex < countPerRing; pelletIndex++)
            {
                float degrees = (pelletIndex + (ringIndex % 2) * .5f) *
                    360f / countPerRing;
                // Maintain the same downward escape opening as editor preview.
                if (Mathf.Abs(Mathf.DeltaAngle(degrees, -90f)) < 18f)
                    continue;
                QueueAdvanced(launch, 30d * beatSec, blastCenter,
                    degrees, velocity, 2, false, true);
            }
        }
    }

    public static int BootbeatAimedVolleysForLevel(int level)
    {
        return level <= 6 ? 1 : 2;
    }

    private void QueueBootbeatTrackingVolleys(
        MouseModeChartData chart, float attackBeat, double beatSeconds)
    {
        if (bulletLayer == null || chart == null) return;
        float height = Mathf.Max(1f, bulletLayer.rect.height);
        float travelSpeed = height * .185f / Mathf.Max(.001f, (float)beatSeconds);
        Vector2 origin = SongPatternOrigin();

        // One locked shot at a time. The old three-by-three aimed volley
        // made a tight 9-shot stack directly on Pino during every attack.
        // This keeps the center unsafe to camp in, with clear room to dodge.
        // Beginner songs should introduce one aimed dodge at a time;
        // keep two volleys only for the harder experimental chart.
        int volleyCount = BootbeatAimedVolleysForLevel(chart.level);
        for (int volley = 0; volley < volleyCount; volley++)
        {
            float onBeat = attackBeat + 2.0f + volley * 2.5f;
            double launch = MouseModeChartTiming.BeatToSeconds(chart, onBeat);
            QueueAdvanced(launch, 10.0 * beatSeconds, origin,
                0f, travelSpeed, volley % 2,
                aimedAtLaunch: true);
        }
    }

    private bool AddAdvancedPattern(string id, float beat, MouseModeChartData chart, double beatSec)
    {
        // Use one shared source of trajectories for the chart editor and game.
        // This also enables all Pop'n Dash pop_* event IDs and the adv_*
        // library patterns that previously existed only in editor previews.
        if (MouseModeBulletPatternLibrary.IsSupported(id))
        {
            double patternStartSeconds = MouseModeChartTiming.BeatToSeconds(chart, beat);
            // Built-in musical motifs begin before their attack marker so
            // their FIRST lower-field danger reaches Pino on that marker's
            // beat. Previously the marker triggered the off-screen launch,
            // postponing the real dodge several beats after the music.
            // The same computed first-impact offset also drives FH
            // patterns, ensuring their danger reaches the musical cue
            // rather than relying on a fixed approximate 4.6-beat lead.
            // Pop and special patterns retain their existing launch design.
            bool alignDanger = id.StartsWith("fh_", StringComparison.Ordinal) ||
                id.StartsWith("mix_", StringComparison.Ordinal) ||
                id.StartsWith("boot_", StringComparison.Ordinal);
            float lead = alignDanger
                ? MouseModeBulletPatternLibrary.FirstRiskCrossingBeat(id)
                : 0f;
            float attackLead = Mathf.Min(lead, Mathf.Max(0f, beat));
            foreach (MouseModeBulletPatternLibrary.Pellet pellet
                in MouseModeBulletPatternLibrary.GenerateForChart(id, beat, chart))
            {
                float r = pellet.angle * Mathf.Deg2Rad;
                float launchBeat = beat + pellet.atBeat - attackLead;
                advancedShots.Add(new AdvancedShot
                {
                    launch = MouseModeChartTiming.BeatToSeconds(chart, launchBeat),
                    end = MouseModeChartTiming.BeatToSeconds(chart, launchBeat + pellet.lifeBeats),
                    proceduralMotion = true,
                    motion = pellet,
                    secondsPerBeat = (float)beatSec,
                    musicalChart = chart,
                    musicalLaunchBeat = launchBeat,
                    start = LocalPoint(pellet.origin.x, pellet.origin.y),
                    direction = new Vector2(Mathf.Cos(r), Mathf.Sin(r)),
                    colorIndex = pellet.color
                });
            }

            // BOOTBEAT's early fixed patterns allowed camping in a lane
            // indefinitely. Add a few *locked at launch* aimed volleys:
            // standing still is unsafe, but moving after a volley fires
            // reliably avoids it. These are never homing projectiles.
            if (id.StartsWith("boot_", StringComparison.Ordinal) &&
                id != "boot_gate")
                QueueBootbeatTrackingVolleys(chart, beat, beatSec);

            // Full-Band's fixed motifs leave long stationary pockets. An
            // authored reply locks onto the current Pino only at its musical
            // launch accent, then travels straight. Gate/wheel corridors are
            // untouched; only explicitly marked phrases get one reply.
            float aimedReply = MouseModeBulletPatternLibrary.GetParameter(id, "aimreply", -1f);
            if (id.StartsWith("fh_", StringComparison.Ordinal) &&
                aimedReply >= 0f && aimedReply <= 8f && bulletLayer != null)
            {
                float replyBeat = beat + aimedReply;
                double launch = MouseModeChartTiming.BeatToSeconds(chart, replyBeat);
                double replyBeatSeconds = MouseModeChartTiming.SecondsPerBeatAt(chart, replyBeat);
                float travelSpeed = Mathf.Max(1f, bulletLayer.rect.height) * .185f /
                    Mathf.Max(.001f, (float)replyBeatSeconds);
                QueueAdvanced(launch, 10.0 * replyBeatSeconds, SongPatternOrigin(),
                    0f, travelSpeed, 0, aimedAtLaunch: true);
            }
            return true;
        }
        if (id != "adv_stream" && id != "adv_spiral" &&
            id != "adv_ring" && id != "adv_split" &&
            id != "adv_sweep" && id != "adv_braid" &&
            id != "adv_softfan" && id != "adv_turnfan" &&
            id != "adv_seedburst" && id != "adv_edgecross") return false;
        if (bulletLayer == null) bulletLayer = GetComponent<RectTransform>();
        float h = Mathf.Max(1f, bulletLayer.rect.height);
        // Velocity is tied to musical time: the same pattern has the same
        // beat-relative travel distance at any BPM.
        float speed = h * advancedTravelPerBeat / (float)beatSec;
        double t0 = MouseModeChartTiming.BeatToSeconds(chart, beat);
        double duration = 18.0 * beatSec;
        if (id == "adv_softfan")
        {
            // Three sparse musical accents, emitted at the event's actual beat.
            // Five visible fan pellets, no arbitrary impact-time offset.
            for (int volley = 0; volley < 3; volley++)
            {
                double fire = t0 + volley * 1.75 * beatSec;
                Vector2 origin = SongPatternOrigin();
                // Deliberately send shots BETWEEN the three input positions,
                // not through their centres. The gap is a playable route.
                for (int j = -2; j <= 2; j++)
                {
                    if (j == 0) continue;
                    float wing = Mathf.Sign(j) * (22f + (Mathf.Abs(j) - 1) * 10f);
                    QueueAdvanced(fire, duration, origin,
                        -90f + wing + (volley - 1) * 2f, speed * 1.13f, 0);
                }
            }
        }
        else if (id == "adv_turnfan")
        {
            // Each burst comes from the same source while its aim direction
            // rotates between phrases; bullets themselves keep straight paths.
            for (int volley = 0; volley < 4; volley++)
            {
                double fire = t0 + volley * 1.25 * beatSec;
                Vector2 origin = SongPatternOrigin();
                for (int j = -2; j <= 2; j++)
                {
                    if (j == 0) continue;
                    float wing = Mathf.Sign(j) * (20f + (Mathf.Abs(j) - 1) * 11f);
                    float turn = (volley - 1.5f) * Mathf.Sign(j) * 2.0f;
                    QueueAdvanced(fire, duration, origin,
                        -90f + wing + turn, speed * 1.05f, 1);
                }
            }
        }
        else if (id == "adv_seedburst")
        {
            // A travelling seed splits into five daughter shots. The seed
            // disappears at the split intentionally; children travel offscreen.
            for (int seed = 0; seed < 2; seed++)
            {
                double fire = t0 + seed * 2.25 * beatSec;
                Vector2 origin = SongPatternOrigin();
                float seedSpeed = speed * 0.79f;
                QueueAdvanced(fire, 2.25 * beatSec, origin, -90f, seedSpeed, 3);
                Vector2 split = origin + Vector2.down * seedSpeed * (float)(2.25 * beatSec);
                for (int j = -2; j <= 2; j++)
                {
                    if (j == 0) continue; // seed divides into two readable wings
                    QueueAdvanced(fire + 2.25 * beatSec, duration,
                        split, -90f + Mathf.Sign(j) *
                        (38f + (Mathf.Abs(j) - 1f) * 14f), speed * 1.06f, 3);
                }
            }
        }
        else if (id == "adv_edgecross")
        {
            // 화면 밖에서 시작해 플레이 화면을 가로지른 뒤 반대편으로 나갑니다.
            // 기존 채보에는 자동으로 적용되지 않는 선택형 기믹입니다.
            float width = Mathf.Max(1f, bulletLayer.rect.width);
            for (int volley = 0; volley < 10; volley++)
            {
                bool fromLeft = volley % 2 == 0;
                float originX = (fromLeft ? -1f : 1f) *
                    (width * .5f + VisibleBulletSize() * 1.5f);
                float originY = bulletLayer.rect.height *
                    (-.20f + (volley % 4) * .11f);
                float angle = fromLeft ? -7f : 187f;
                for (int pellet = 0; pellet < 2; pellet++)
                    QueueAdvanced(t0 + (volley * .75f + pellet * .14f) * beatSec,
                        duration, new Vector2(originX, originY + pellet * 28f),
                        angle, speed * 2.1f, 2);
            }
        }
        else if (id == "adv_stream")
        {
            // Eight aimed volleys over four beats. Aim is LOCKED when each
            // volley is fired, allowing the player to stream in one direction.
            for (int volley = 0; volley < 16; volley++)
            {
                double t = t0 + volley * 0.5 * beatSec;
                Vector2 origin = LocalPoint(0.5f + 0.24f * Mathf.Sin(volley * 0.47f), 0.06f);
                for (int j = -4; j <= 4; j++)
                    QueueAdvanced(t, duration, origin, j * 6f, speed * (j == 0 ? 1f : .92f),
                        0, true);
            }
        }
        else if (id == "adv_spiral")
        {
            // Three rotating arms: each pellet flies straight after emission.
            // The staggered EMISSION angle creates a true spiral curtain.
            Vector2 origin = LocalPoint(.5f, .19f);
            for (int step = 0; step < 64; step++)
                for (int arm = 0; arm < 3; arm++)
                    QueueAdvanced(t0 + step * .15625 * beatSec, duration,
                        origin, -90f + step * 12f + arm * 120f, speed * .73f, 1);
        }
        else if (id == "adv_ring")
        {
            // Three rings, each with a genuine angular opening toward the
            // lower play area. Ring rotation is incremental, not random.
            Vector2 origin = LocalPoint(.5f, .17f);
            for (int ring = 0; ring < 6; ring++)
            {
                float gapCenter = -90f + (ring - 1.5f) * 14f;
                for (int i = 0; i < 36; i++)
                {
                    float angle = i * 10f + ring * 5f;
                    if (Mathf.Abs(Mathf.DeltaAngle(angle, gapCenter)) < 33f) continue;
                    QueueAdvanced(t0 + ring * 1.75 * beatSec, duration,
                        origin, angle, speed * .65f, 2);
                }
            }
        }
        else if (id == "adv_sweep")
        {
            // Alternating left/right downbeat fans: the source and spread
            // are visible before each following response, not random rain.
            for (int volley = 0; volley < 16; volley++)
            {
                bool left = volley % 2 == 0;
                Vector2 origin = LocalPoint(left ? .16f : .84f, .09f);
                float baseAngle = left ? -66f : -114f;
                for (int j = -3; j <= 3; j++)
                    QueueAdvanced(t0 + volley * .5 * beatSec, duration,
                        origin, baseAngle + j * 8f, speed * .87f, 0);
            }
        }
        else if (id == "adv_braid")
        {
            // Two visibly opposed emitters weave diagonal streams.
            // The sources alternate half-beat accents, creating a readable
            // cross rather than scatter over the entire screen.
            for (int volley = 0; volley < 16; volley++)
            {
                bool left = volley % 2 == 0;
                Vector2 origin = LocalPoint(left ? .21f : .79f, .07f);
                float baseAngle = left ? -68f : -112f;
                for (int j = -3; j <= 3; j++)
                    QueueAdvanced(t0 + volley * .5 * beatSec, duration,
                        origin, baseAngle + j * 7f, speed * .79f, 1);
            }
        }
        else // adv_split
        {
            // Four seeds travel downward; at the 2-beat mark their five
            // daughter bullets continue from their actual split positions.
            for (int seed = 0; seed < 7; seed++)
            {
                double startTime = t0 + seed * .75 * beatSec;
                Vector2 origin = LocalPoint(.14f + seed * .12f, .055f);
                float primarySpeed = speed * .52f;
                QueueAdvanced(startTime, 2 * beatSec, origin, -90f, primarySpeed, 3);
                Vector2 splitPoint = origin + Vector2.down * primarySpeed * (float)(2 * beatSec);
                for (int child = -3; child <= 3; child++)
                    QueueAdvanced(startTime + 2 * beatSec, 8.0 * beatSec,
                        splitPoint, -90f + child * 18f, speed * .88f, 3);
            }
        }
        return true;
    }

    // Use the chart's entire tempo map, rather than freezing projectile
    // movement at the BPM on the first attack beat. This keeps choreographed
    // volleys in musical sync across BPM changes without moving audio or notes.
    public static float ProjectileBeatAtTime(
        MouseModeChartData chart, float launchBeat, double nowSeconds,
        double launchSeconds, float fallbackSecondsPerBeat)
    {
        if (chart != null)
            return Mathf.Max(0f, (float)(
                MouseModeChartTiming.SecondsToBeat(chart, nowSeconds) -
                launchBeat));
        return Mathf.Max(0f, (float)(
            (nowSeconds - launchSeconds) /
            Math.Max(.0001f, fallbackSecondsPerBeat)));
    }

    private void UpdateAdvancedShots(double now, Vector2 pinoLocal,
        Vector2 previousPinoFrame)
    {
        float size = VisibleBulletSize() * .77f;
        float r = PinoRadiusInBulletLayer() + size * .24f;
        for (int i = advancedShots.Count - 1; i >= 0; i--)
        {
            AdvancedShot shot = advancedShots[i];
            // An on-screen pellet never expires by lifetime. Lifetime is used
            // only to discard shots that started offscreen and never entered.
            // Seeds continue moving even after releasing their child pellets.
            if (now >= shot.end && !shot.hasEnteredStage)
            {
                RecycleAdvancedBulletVisual(shot);
                advancedShots.RemoveAt(i);
                continue;
            }
            if (now < shot.launch) continue;
            if (!shot.initialized)
            {
                shot.initialized = true;
                if (shot.aimedAtLaunch)
                {
                    Vector2 target = pinoLocal - shot.start;
                    if (target.sqrMagnitude < 1f) target = Vector2.down;
                    float aim = Mathf.Atan2(target.y, target.x) * Mathf.Rad2Deg;
                    // The queued angle describes the fan offset from aim.
                    float offset = Mathf.Atan2(shot.direction.y, shot.direction.x) * Mathf.Rad2Deg;
                    float angle = (aim + offset) * Mathf.Deg2Rad;
                    shot.direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                }
                RectTransform rt = AcquireAdvancedBulletVisual();
                rt.sizeDelta = Vector2.one * size;
                Image img = rt.GetComponent<Image>();
                img.sprite = GetProjectileSprite(
                    shot.colorIndex, shot.burstFragment);
                img.raycastTarget = false;
                Color tint = shot.colorIndex == 1 ? new Color(.70f, .58f, .90f, 1f) :
                    shot.colorIndex == 2 ? new Color(.97f, .72f, .43f, 1f) :
                    shot.colorIndex == 3 ? new Color(.53f, .80f, .89f, 1f) : bulletColor;
                img.color = tint;
                shot.body = rt;
                shot.image = img;
                shot.body.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(shot.direction.y, shot.direction.x) * Mathf.Rad2Deg + 90f);
            }
            float elapsed = Mathf.Max(0f, (float)(now - shot.launch));
            float motionBeat = shot.proceduralMotion
                ? ProjectileBeatAtTime(shot.musicalChart,
                    shot.musicalLaunchBeat, now, shot.launch, shot.secondsPerBeat)
                : 0f;
            Vector2 current = shot.proceduralMotion
                ? MouseModeBulletPatternLibrary.Position(shot.motion,
                    motionBeat, bulletLayer.rect.width, bulletLayer.rect.height)
                : shot.start + shot.direction * shot.speed * elapsed;
            SetProjectileVisualLayer(shot.body, current);
            shot.body.anchoredPosition = current;
            if (shot.proceduralMotion)
            {
                float prevBeat = Mathf.Max(0f, motionBeat - .02f);
                Vector2 previousPosition = MouseModeBulletPatternLibrary.Position(
                    shot.motion, prevBeat, bulletLayer.rect.width, bulletLayer.rect.height);
                Vector2 velocity = current - previousPosition;
                if (velocity.sqrMagnitude > .001f)
                    shot.body.localRotation = Quaternion.Euler(0f, 0f,
                        Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg + 90f);
            }
            // Test the swept segment so fast pellets cannot tunnel through Pino.
            Vector2 previous = shot.hasLastPosition ? shot.lastPosition : current;
            float proximity = SweptRelativeDistance(
                shot.hasLastPosition ? previousPinoFrame : pinoLocal,
                pinoLocal, previous, current);
            if (!shot.hitPino && pino != null && now >= invulnerableUntil &&
                proximity <= r)
            {
                shot.hitPino = true;
                RegisterHit(now);
                if (failed) return;
                // A hit does not erase the projectile: keep flying.
            }
            else if (!shot.hitPino && !shot.grazed && pino != null &&
                InGrazeBand(proximity, r, size * .60f) &&
                RegisterGraze(now))
                shot.grazed = true;
            shot.lastPosition = current;
            shot.hasLastPosition = true;
            // 진입 전의 화면 밖 위치를 "퇴장"으로 오인해 바로 삭제하지 않습니다.
            // Remove only after the entire rendered sprite leaves camera view.
            float w = bulletLayer.rect.width * .5f + size * .5f;
            float h = bulletLayer.rect.height * .5f + size * .5f;
            bool inside = Mathf.Abs(current.x) <= w && Mathf.Abs(current.y) <= h;
            if (inside) shot.hasEnteredStage = true;
            if (!inside && shot.hasEnteredStage)
            {
                RecycleAdvancedBulletVisual(shot);
                advancedShots.RemoveAt(i);
            }
        }
    }

    // Rhythm art remains in front of upper-field shots. In the dodge half
    // only, the *existing projectile sprite* is foregrounded, never duplicated.
    // Identical RectTransform geometry keeps the collision coordinates intact.
    private void SetProjectileVisualLayer(RectTransform visual, Vector2 local)
    {
        if (visual == null || bulletLayer == null) return;
        Transform board = bulletLayer.parent;
        if (board == null) return;
        Transform note = board.Find("NoteLayer");
        if (note == null) return;
        Transform pinoArea = board.Find("PinoMoveArea");
        if (dangerForeground == null)
        {
            dangerForeground = MakeRect("DodgeProjectileForeground", board);
            dangerForeground.anchorMin = bulletLayer.anchorMin;
            dangerForeground.anchorMax = bulletLayer.anchorMax;
            dangerForeground.pivot = bulletLayer.pivot;
            dangerForeground.offsetMin = bulletLayer.offsetMin;
            dangerForeground.offsetMax = bulletLayer.offsetMax;
        }
        // Put the projectile overlay immediately above notes and under Pino.
        int noteIndex = note.GetSiblingIndex();
        int overlayIndex = dangerForeground.GetSiblingIndex();
        if (overlayIndex != noteIndex + 1)
            dangerForeground.SetSiblingIndex(overlayIndex < noteIndex ? noteIndex : noteIndex + 1);
        if (pinoArea != null &&
            pinoArea.GetSiblingIndex() < dangerForeground.GetSiblingIndex())
            pinoArea.SetSiblingIndex(dangerForeground.GetSiblingIndex());
        // Include the top rim of a projectile entering the dodge area so it
        // cannot disappear just as the player needs to react to it.
        float dangerTop = 0f;
        RectTransform moveRect = pinoArea as RectTransform;
        if (moveRect != null)
            dangerTop = bulletLayer.InverseTransformPoint(moveRect.TransformPoint(
                new Vector3(moveRect.rect.center.x, moveRect.rect.yMax, 0f))).y;
        Transform target = local.y <= dangerTop + visual.rect.height * .5f
            ? dangerForeground : bulletLayer;
        if (visual.parent != target) visual.SetParent(target, false);
    }

    private RectTransform AcquireAdvancedBulletVisual()
    {
        while (advancedBulletVisualPool.Count > 0)
        {
            RectTransform cached = advancedBulletVisualPool.Pop();
            if (cached == null) continue;
            cached.SetParent(bulletLayer, false);
            cached.gameObject.SetActive(true);
            cached.SetAsLastSibling();
            cached.localRotation = Quaternion.identity;
            return cached;
        }

        RectTransform created = MakeRect("AdvancedBullet", bulletLayer);
        Image image = created.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        return created;
    }

    private void RecycleAdvancedBulletVisual(AdvancedShot shot)
    {
        if (shot.body == null) return;
        RectTransform visual = shot.body;
        shot.body = null;
        shot.image = null;
        if (bulletLayer != null) visual.SetParent(bulletLayer, false);
        visual.gameObject.SetActive(false);
        if (advancedBulletVisualPool.Count < MaxPooledAdvancedBulletVisuals &&
            bulletLayer != null)
            advancedBulletVisualPool.Push(visual);
        else Destroy(visual.gameObject);
    }

    // Music-choreographed barrages. The previous 11 fixed lanes and their vertical
    // warning rails are gone. Each style produces a distinct, curved entrance,
    // still leaving a continuously moving safe route at the risk line.
    public struct WaveShot
    {
        public float startX;
        public float targetX;
        public float curve;
    }

    // Shared with the chart editor: the preview and runtime derive the EXACT
    // same shot positions from the serialized patternId parameters.
    // Four attack motifs, each with a different entrance trajectory and a single
    // continuous, deliberately open corridor at the Pino crossing line.
    // Keep in sync with the editor: the editor calls this exact function.
    public static List<WaveShot> GetWaveShots(AttackSpec spec, int row)
    {
        List<WaveShot> result = new List<WaveShot>(16);
        float t = spec.rows <= 1 ? 0f : row / (float)(spec.rows - 1);
        float corridor = Mathf.Clamp(spec.x + spec.drift * t, 0.22f, 0.78f);
        float openHalfWidth = spec.gap >= 7 ? 0.20f : spec.gap >= 5 ? 0.15f : 0.125f;
        int count = spec.style == 1 || spec.style == 3 ? 12 : 11;

        for (int i = 0; i < count; i++)
        {
            float u = count <= 1 ? 0.5f : i / (float)(count - 1);
            float from, target, curve;
            switch (spec.style)
            {
                // 1. Opening/closing fan from one enemy source. Five consecutive
                // volleys form one recognisable fan, not an unrelated rain grid.
                case 0:
                    float spread = 0.36f + 0.11f * Mathf.Sin(t * Mathf.PI);
                    from = spec.x + (u - 0.5f) * 0.065f;
                    target = 0.50f + (u - 0.5f) * (spread * 2.05f) +
                        (t - 0.5f) * 0.085f;
                    curve = (u - .5f) * .12f;
                    break;
                // 2. Opposite launchers, alternating diagonals that cross in
                // the upper half. Bottom gaps remain clearly reachable.
                case 1:
                    bool fireFromLeft = (i + row) % 2 == 0;
                    from = fireFromLeft ? 0.075f : 0.925f;
                    target = 0.09f + u * 0.82f +
                        (fireFromLeft ? 0.024f : -0.024f) * Mathf.Sin(t * Mathf.PI);
                    curve = fireFromLeft ? -0.055f : 0.055f;
                    break;
                // 3. Serpentine source: the whole fan rotates as the source
                // moves horizontally. The corridor drifts smoothly per volley.
                case 2:
                    float sourceX = 0.50f + 0.24f * Mathf.Sin(t * Mathf.PI * 1.55f - .65f);
                    from = sourceX + (u - .5f) * .07f;
                    target = .10f + u * .80f +
                        .07f * Mathf.Sin(t * Mathf.PI * 1.6f + u * .65f);
                    curve = .10f * Mathf.Sin(t * Mathf.PI + u * 1.8f);
                    break;
                // 4. Pincer: left and right wings approach the chosen gate,
                // then return outward. They NEVER fill the announced gate.
                default:
                    bool left = i < count / 2;
                    float side = left ? i / (float)(count / 2 - 1) :
                        (i - count / 2) / (float)(count / 2 - 1);
                    float close = .11f * Mathf.Sin(t * Mathf.PI);
                    from = left ? .07f : .93f;
                    target = left ? .075f + side * (.40f + close) :
                                    .525f + side * (.40f - close);
                    curve = left ? .048f : -.048f;
                    break;
            }
            from = Mathf.Clamp(from, .03f, .97f);
            target = Mathf.Clamp(target, .055f, .945f);
            if (Mathf.Abs(target - corridor) < openHalfWidth)
                continue;
            result.Add(new WaveShot { startX = from, targetX = target, curve = curve });
        }
        return result;
    }

    private void AddRainPattern(AttackSpec spec, float baseBeat,
        MouseModeChartData chart, double secondsPerBeat)
    {
        double firstImpact = MouseModeChartTiming.BeatToSeconds(chart, baseBeat);
        rainCues.Add(new RainCue
        {
            spec = spec,
            beat = baseBeat,
            firstImpact = firstImpact,
            secondsPerBeat = secondsPerBeat,
            visibleStart = MouseModeChartTiming.BeatToSeconds(chart,
                baseBeat - fallDurationBeats - spec.warning),
            visibleEnd = MouseModeChartTiming.BeatToSeconds(chart,
                baseBeat + spec.duration + exitDurationBeats)
        });
        for (int row = 0; row < spec.rows; row++)
        {
            float t = spec.rows <= 1 ? 0f : row / (float)(spec.rows - 1);
            float beat = baseBeat + t * Mathf.Max(.25f, spec.duration - .55f);
            List<WaveShot> pattern = GetWaveShots(spec, row);
            foreach (WaveShot shot in pattern)
            {
                // No dozens of full-screen hairline warnings. The projectiles
                // originate visibly at the top and pass through the full area.
                AddShot(chart, secondsPerBeat, beat,
                    shot.startX, shot.targetX, false, shot.curve,
                    false, true, spec.warning);
            }
        }
    }

    private float BossEmitterX(float beat)
    {
        if (virusEmitter != null && bulletLayer != null && bulletLayer.rect.width > 1f)
        {
            Vector3 pos = bulletLayer.InverseTransformPoint(virusEmitter.position);
            return Mathf.Clamp(pos.x / bulletLayer.rect.width + 0.5f, 0.10f, 0.90f);
        }
        return Mathf.Clamp(0.50f + 0.10f * Mathf.Sin(beat * 0.12f), 0.40f, 0.60f);
    }

    private float BossEmitterY()
    {
        float top = bulletLayer.rect.height * 0.5f + VisibleBulletSize();
        if (virusEmitter == null) return top;
        float candidate = bulletLayer.InverseTransformPoint(virusEmitter.position).y;
        // Don't launch below the crossing line if Virus is placed elsewhere.
        return Mathf.Clamp(candidate, bulletLayer.rect.height * 0.05f, top);
    }

    private void AddRow(MouseModeChartData chart, double beatSec, float atBeat,
        params float[] positions)
    {
        float emitter = BossEmitterX(atBeat);
        foreach (float x in positions)
            AddShot(chart, beatSec, atBeat, emitter, x, false);
    }

    private void AddFan(MouseModeChartData chart, double beatSec, float atBeat,
        params float[] targets)
    {
        AddRow(chart, beatSec, atBeat, targets);
    }

    // High-BPM beat maps must not make dangerous attacks appear with
    // near-zero reaction time. Keep the authored impact/launch seconds intact.
    public static double EnsureTelegraphLead(double proposedStart,
        double dangerousTime, double minimumLeadSeconds)
    {
        return Math.Min(proposedStart,
            dangerousTime - Math.Max(0d, minimumLeadSeconds));
    }

    private void AddShot(MouseModeChartData chart, double beatSec, float beat,
        float originX, float targetX, bool aimed, float curve = 0f, bool telegraph = true,
        bool rain = false, float warningOverride = -1f)
    {
        double impact = MouseModeChartTiming.BeatToSeconds(chart, beat);
        double launch = MouseModeChartTiming.BeatToSeconds(chart, beat - fallDurationBeats);
        double warning = MouseModeChartTiming.BeatToSeconds(chart,
            beat - fallDurationBeats - (warningOverride > 0f ? warningOverride : warningLeadBeats));
        warning = EnsureTelegraphLead(warning, launch, 0.55d);
        double end = MouseModeChartTiming.BeatToSeconds(chart, beat + exitDurationBeats);
        if (bulletLayer == null) bulletLayer = GetComponent<RectTransform>();

        // Scheduling is cheap: create UI elements only when the cue or shot
        // approaches the visible play window, not all at chart start.
        shots.Add(new Shot
        {
            warningStart = (telegraph || aimed) ? warning : launch,
            launchTime = launch,
            impactTime = impact,
            endTime = end,
            fromX = originX,
            toX = targetX,
            // 연속 낙하는 상단 발사구에서 시작하고 하단까지 이어진다.
            originY = rain ? bulletLayer.rect.height * 0.52f + VisibleBulletSize() * 0.6f : BossEmitterY(),
            aimAtWarning = aimed,
            telegraph = telegraph || aimed,
            curve = curve,
            rain = rain
        });
    }

    private void CreateShotVisuals(Shot shot)
    {
        if (shardSprite == null) CreateShardSprite();
        RectTransform bulletRt = MakeRect("VirusProjectile", bulletLayer);
        float size = VisibleBulletSize();
        bulletRt.sizeDelta = new Vector2(size, size);
        Image bulletImage = bulletRt.gameObject.AddComponent<Image>();
        bulletImage.sprite = shardSprite;
        bulletImage.color = Color.white;
        bulletImage.raycastTarget = false;
        bulletRt.gameObject.SetActive(false);
        shot.body = bulletRt;

        // Cytus식 시각 정보 우선순위를 참고해 밀집 탄막의 잔상을 제거한다.
        // 탄환 본체와 실제 이동/충돌은 그대로 두고 중복되는 줄무늬만 숨긴다.
        // 공격 예고와 보스 발사부는 계속 유지한다.
        shot.trail = null;

        if (!shot.telegraph) return;

        RectTransform line = MakeRect("AttackWarningLine", bulletLayer);
        Image lineImage = line.gameObject.AddComponent<Image>();
        lineImage.color = new Color(warningColor.r, warningColor.g, warningColor.b, 0.18f);
        lineImage.raycastTarget = false;
        line.gameObject.SetActive(false);
        shot.warningLine = line;
        shot.warningLineImage = lineImage;

        RectTransform mark = MakeRect("AttackWarningMark", bulletLayer);
        mark.sizeDelta = Vector2.one * (VisibleBulletSize() * 0.80f);
        Image markImage = mark.gameObject.AddComponent<Image>();
        markImage.sprite = shardSprite;
        markImage.color = new Color(warningColor.r, warningColor.g, warningColor.b, .50f);
        markImage.raycastTarget = false;
        mark.gameObject.SetActive(false);
        shot.warningMark = mark;
        shot.warningMarkImage = markImage;
    }

    private void Update()
    {
        if (failed || chartPlayer == null) return;

        // On resume the rhythm player shifts its DSP origin. Do not advance
        // the bullet scheduler's prior timestamp while globally paused,
        // otherwise every tutorial checkpoint appears to rewind the chart.
        if (Time.timeScale <= 0f || AudioListener.pause)
            return;

        // Never stop the chart from inside a Judged callback: the player may
        // still be enumerating notes and destroying visuals that frame.
        if (pendingRhythmFailure)
        {
            pendingRhythmFailure = false;
            if (stopImmediatelyAtZero)
            {
                TryDepleteStability();
                return;
            }
            // A critical gauge must not cut off the music mid-song.
            // PERFECT and near-miss grazes can recover before results.
        }
        UpdateGrazeLabel();
        if (pino == null || !pino.gameObject.activeInHierarchy) ResolvePinoInScene();
        if (PlayerIsRunning()) BuildScheduleIfNeeded();
        double now = GetChartSeconds();
        if (double.IsNaN(now)) return;

        if (!double.IsNaN(lastSongSeconds) && now < lastSongSeconds - 0.5d)
        {
            ResetSession();
            BuildScheduleIfNeeded();
        }
        lastSongSeconds = now;
        // Match the rhythm note player: the stage stays clear throughout
        // READY / 3 / 2 / 1. Enemy warnings, pellets and lasers start only
        // once the actual scheduled music begins at song time zero.
        if (now < 0d)
            return;

        // Do not hide attacks solely because the Unity Game view has lost focus.
        // The chart player owns pause behavior; timeScale and AudioListener pause
        // still prevent combat updates while actually paused.
        if (Time.timeScale <= 0f || AudioListener.pause) return;
        if (bulletLayer == null) return;
        if (enemy01 != null) enemy01.Tick(now);

        Vector2 pinoLocal = PinoLocalCenter();
        // Sample AFTER PinoMouseCursorUI.Update (director execution order 2000).
        // Both Pino and shots can move during a frame: collision must use
        // their relative sweep, or fast horizontal dodges tunnel through.
        Vector2 pinoPrevious = hasPreviousPinoLocal
            ? previousPinoLocal : pinoLocal;
        previousPinoLocal = pinoLocal;
        hasPreviousPinoLocal = true;
        float below = -bulletLayer.rect.height * 0.5f - VisibleBulletSize();
        // Fixed risk reference: never track Pino in Y.
        float impactY = -bulletLayer.rect.height * 0.22f;

        for (int i = shots.Count - 1; i >= 0; i--)
        {
            Shot shot = shots[i];
            // The end time defines flight speed after impact, not deletion.
            // Only complete exit from BulletLayer may remove the projectile.
            if (now < shot.warningStart) continue;
            if (shot.body == null) CreateShotVisuals(shot);

            if (shot.aimAtWarning && !shot.aimed)
            {
                shot.toX = GetPinoNormalizedX();
                shot.aimed = true; // Fixed target for this one shot only.
            }

            float fromX = XLocal(shot.fromX);
            float toX = XLocal(shot.toX);

            if (now < shot.launchTime)
            {
                if (shot.telegraph && shot.warningLine != null)
                {
                    shot.warningLine.gameObject.SetActive(true);
                    shot.warningMark.gameObject.SetActive(true);
                    Vector2 pathStart = new Vector2(fromX, shot.originY);
                    Vector2 pathEnd = new Vector2(toX, impactY);
                    Vector2 diff = pathEnd - pathStart;
                    shot.warningLine.sizeDelta = new Vector2(1.5f, diff.magnitude);
                    shot.warningLine.anchoredPosition = (pathStart + pathEnd) * .5f;
                    shot.warningLine.localRotation = Quaternion.Euler(0f, 0f,
                        Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg - 90f);
                    shot.warningMark.anchoredPosition = pathEnd;
                }
                continue;
            }

            if (shot.telegraph && shot.warningLine != null &&
                shot.warningLine.gameObject.activeSelf)
            {
                shot.warningLine.gameObject.SetActive(false);
                shot.warningMark.gameObject.SetActive(false);
            }
            if (!shot.body.gameObject.activeSelf) shot.body.gameObject.SetActive(true);

            float x, y;
            if (now <= shot.impactTime)
            {
                float t = Mathf.Clamp01((float)((now - shot.launchTime) /
                    Math.Max(.001d, shot.impactTime - shot.launchTime)));
                // 화면 위쪽에서 사선/곡선 전개가 끝난 뒤 하단 회피 영역에서는
                // 탄환이 갑자기 옆으로 휘지 않는다. 안전 통로가 읽히도록 한다.
                float horizontalT = Mathf.Clamp01(t / 0.64f);
                x = Mathf.Lerp(fromX, toX, horizontalT) +
                    Mathf.Sin(horizontalT * Mathf.PI) * shot.curve * bulletLayer.rect.width;
                y = Mathf.Lerp(shot.originY, impactY, t);
            }
            else
            {
                float t = Mathf.Max(0f, (float)((now - shot.impactTime) /
                    Math.Max(.001d, shot.endTime - shot.impactTime)));
                x = toX;
                y = Mathf.LerpUnclamped(impactY, below, t);
            }
            Vector2 bulletPosition = new Vector2(x, y);
            bool hadBulletPosition = shot.hasLastPosition;
            Vector2 bulletPrevious = hadBulletPosition
                ? shot.lastPosition : bulletPosition;
            SetProjectileVisualLayer(shot.body, bulletPosition);
            shot.body.anchoredPosition = bulletPosition;
            shot.lastPosition = bulletPosition;
            shot.hasLastPosition = true;
            // Short diagonal shots point along their own flight trajectory.
            float horizontal = (toX - fromX);
            shot.body.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Clamp(-horizontal / Mathf.Max(1f, bulletLayer.rect.height) * 110f, -35f, 35f));
            if (shot.trail != null)
            {
                shot.trail.gameObject.SetActive(true);
                shot.trail.anchoredPosition = new Vector2(x, y + VisibleBulletSize() * .72f);
            }

            // Visual size is larger than hit radius so close dodges feel possible.
            // 그림 전체가 피격 범위는 아니다. 시각 크기보다 작은 판정으로 회피 여유 확보.
            float combined = PinoRadiusInBulletLayer() + VisibleBulletSize() * .24f;
            float proximity = SweptRelativeDistance(
                hadBulletPosition ? pinoPrevious : pinoLocal,
                pinoLocal, bulletPrevious, bulletPosition);
            if (!shot.hitPino && pino != null && now >= invulnerableUntil &&
                proximity < combined)
            {
                shot.hitPino = true;
                RegisterHit(now);
                if (failed) break;
            }
            else if (!shot.hitPino && !shot.grazed && pino != null &&
                InGrazeBand(proximity, combined, VisibleBulletSize() * .60f) &&
                RegisterGraze(now))
                shot.grazed = true;
            if (y < -bulletLayer.rect.height * .5f - VisibleBulletSize() * .5f)
            {
                DestroyShot(shot);
                shots.RemoveAt(i);
            }
        }
        if (!failed) UpdateAdvancedShots(now, pinoLocal, pinoPrevious);
        if (!failed) UpdateSpecialAttacks(now, pinoLocal, pinoPrevious);
        UpdateRainGateMarkers(now);
        // Enemy01 has replaced the old red launcher/placeholder indicator.
    }

    private Vector2 LocalPoint(float x, float y)
    {
        return new Vector2((x - 0.5f) * bulletLayer.rect.width,
            (0.5f - y) * bulletLayer.rect.height);
    }

    // Exact ray-to-viewport intersection: the laser NEVER ends halfway down.
    private Vector2 LaserEnd(Vector2 origin, float angle)
    {
        float rad = angle * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad));
        float halfW = bulletLayer.rect.width * 0.5f;
        float halfH = bulletLayer.rect.height * 0.5f;
        float travel = (-halfH - origin.y) / Mathf.Min(-0.01f, direction.y);
        if (direction.x > 0.001f)
            travel = Mathf.Min(travel, (halfW - origin.x) / direction.x);
        else if (direction.x < -0.001f)
            travel = Mathf.Min(travel, (-halfW - origin.x) / direction.x);
        return origin + direction * Mathf.Max(0f, travel);
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 delta = b - a;
        float lengthSqr = delta.sqrMagnitude;
        float t = lengthSqr > 0.0001f ?
            Mathf.Clamp01(Vector2.Dot(p - a, delta) / lengthSqr) : 0f;
        return Vector2.Distance(p, a + delta * t);
    }

    // Relative movement gives the exact closest approach of two objects
    // linearly moving during one frame; previous-frame stationary hit tests
    // miss crossings when the cursor itself moves quickly.
    public static float SweptRelativeDistance(
        Vector2 playerBefore, Vector2 playerNow,
        Vector2 shotBefore, Vector2 shotNow)
    {
        return DistanceToSegment(Vector2.zero,
            shotBefore - playerBefore, shotNow - playerNow);
    }

    private static float Cross2D(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    // Needed for a moving Pino crossing a stationary live laser.
    // Endpoint-only tests miss a full-width beam crossed in one frame.
    public static float DistanceBetweenSegments(
        Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1)
    {
        Vector2 da = a1 - a0;
        Vector2 db = b1 - b0;
        float denom = Cross2D(da, db);
        if (Mathf.Abs(denom) > .000001f)
        {
            float u = Cross2D(b0 - a0, db) / denom;
            float v = Cross2D(b0 - a0, da) / denom;
            if (u >= 0f && u <= 1f && v >= 0f && v <= 1f)
                return 0f;
        }
        return Mathf.Min(
            Mathf.Min(DistanceToSegment(a0, b0, b1),
                DistanceToSegment(a1, b0, b1)),
            Mathf.Min(DistanceToSegment(b0, a0, a1),
                DistanceToSegment(b1, a0, a1)));
    }

    private static RectTransform MakeImage(string name, Transform parent,
        Sprite sprite, Color color)
    {
        RectTransform t = MakeRect(name, parent);
        Image image = t.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return t;
    }

    private static void SetImageColor(RectTransform t, Color color)
    {
        if (t != null)
        {
            Image im = t.GetComponent<Image>();
            if (im != null) im.color = color;
        }
    }

    private void SetLine(RectTransform line, Vector2 start, Vector2 end, float width)
    {
        if (line == null) return;
        Vector2 v = end - start;
        line.anchoredPosition = (start + end) * 0.5f;
        line.sizeDelta = new Vector2(width, v.magnitude + 2f);
        line.localRotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg - 90f);
    }

    private void CreateSpecialVisual(SpecialAttack attack)
    {
        if (shardSprite == null) CreateShardSprite();
        if (attack.spec.kind == AttackKind.Burst) EnsureBlastSprite();
        else EnsureAttackRingSprite();
        attack.root = MakeRect("Attack_" + attack.spec.kind, bulletLayer);
        if (attack.spec.kind == AttackKind.Laser)
        {
            // Matte outline / tinted body / thin bright core / charging trace.
            RectTransform outer = MakeImage("BeamOutline", attack.root, null,
                new Color(0.14f, 0.10f, 0.17f, 0.96f));
            RectTransform face = MakeImage("BeamBody", attack.root, null,
                new Color(0.78f, 0.45f, 0.83f, 0.88f));
            RectTransform core = MakeImage("BeamCore", attack.root, null,
                new Color(1.0f, 0.90f, 1.0f, 0.86f));
            RectTransform warning = MakeImage("FullLengthWarning", attack.root, null,
                new Color(0.87f, 0.69f, 0.95f, 0.42f));
            // A hollow emitter ring distinguishes lasers from falling shots.
            RectTransform source = MakeImage("LaserEmitterRing", attack.root, attackRingSprite,
                new Color(0.94f, 0.76f, 0.98f, 0.94f));
            attack.outer = outer.GetComponent<Image>();
            attack.face = face.GetComponent<Image>();
            attack.core = core.GetComponent<Image>();
            attack.warning = warning.GetComponent<Image>();
            attack.source = source.GetComponent<Image>();
            source.sizeDelta = Vector2.one * (VisibleBulletSize() * 1.50f);
            RectTransform band = MakeImage("FullDangerWidthPreview", attack.root, null,
                new Color(.79f, .50f, .90f, .09f));
            attack.chargeBand = band.GetComponent<Image>();
            RectTransform tick1 = MakeImage("ChargeNotchA", attack.root, null,
                new Color(.96f, .83f, 1f, .86f));
            RectTransform tick2 = MakeImage("ChargeNotchB", attack.root, null,
                new Color(.96f, .83f, 1f, .86f));
            attack.tickA = tick1.GetComponent<Image>();
            attack.tickB = tick2.GetComponent<Image>();
        }
        else
        {
            EnsureAttackRingSprite();
            RectTransform ring = MakeImage("DetonationWarning", attack.root, attackRingSprite,
                new Color(0.98f, 0.67f, 0.31f, 0.26f));
            attack.ring = ring.GetComponent<Image>();
            // A six-sided bomb icon, not a pointed ordinary bullet.
            RectTransform orb = MakeImage("IncomingBomb_Hex", attack.root, blastSprite,
                BurstTint);
            attack.orb = orb.GetComponent<Image>();
            // Smaller incoming bomb: the larger burst occurs only after impact.
            orb.sizeDelta = Vector2.one * VisibleBulletSize();
            RectTransform flight = MakeImage("BombFlightGuide", attack.root, null,
                new Color(1.0f, .75f, .39f, .36f));
            attack.bombTrail = flight.GetComponent<Image>();
            RectTransform horizontal = MakeImage("TargetHorizontal", attack.root, null,
                new Color(1.0f, .82f, .46f, .74f));
            RectTransform vertical = MakeImage("TargetVertical", attack.root, null,
                new Color(1.0f, .82f, .46f, .74f));
            attack.reticleH = horizontal.GetComponent<Image>();
            attack.reticleV = vertical.GetComponent<Image>();
            // Fragment sprites are spawned by QueueBurstProjectiles on
            // BulletLayer, not parented to this short-lived warning visual.
        }
    }

    // Visible source ports, not a fake full-width 11-lane rail. The small
    // launchers correspond to the source side used by the active choreography.

    private void SetupEnemy01()
    {
        if (!showAnimatedEnemyBeforeAttacks || bulletLayer == null)
            return;
        if (enemy01 == null)
        {
            enemy01 = GetComponent<MouseEnemy01TelegraphUI>();
            if (enemy01 == null)
                enemy01 = gameObject.AddComponent<MouseEnemy01TelegraphUI>();
        }
        enemy01.Setup(bulletLayer);
    }

    private void QueueEnemy01Cue(
        List<MouseEnemy01TelegraphUI.Cue> output,
        AttackSpec spec, int shotStart, int advancedStart,
        int specialStart, int rainStart)
    {
        if (!showAnimatedEnemyBeforeAttacks) return;

        double warningAt = double.PositiveInfinity;
        double firstFire = double.PositiveInfinity;
        double lastFire = double.NegativeInfinity;

        // Warning time comes from the existing bullet schedule.
        // Exit uses LAST RELEASE, not the shot lifetime: bullets can remain
        // on screen after their emitter has disappeared.
        for (int i = shotStart; i < shots.Count; i++)
        {
            Shot shot = shots[i];
            warningAt = Math.Min(warningAt,
                Math.Min(shot.warningStart, shot.launchTime));
            firstFire = Math.Min(firstFire, shot.launchTime);
            lastFire = Math.Max(lastFire, shot.launchTime);
        }
        for (int i = advancedStart; i < advancedShots.Count; i++)
        {
            AdvancedShot shot = advancedShots[i];
            warningAt = Math.Min(warningAt, shot.launch);
            firstFire = Math.Min(firstFire, shot.launch);
            lastFire = Math.Max(lastFire, shot.launch);
        }
        for (int i = specialStart; i < specialAttacks.Count; i++)
        {
            SpecialAttack attack = specialAttacks[i];
            warningAt = Math.Min(warningAt, attack.warnStart);
            firstFire = Math.Min(firstFire, attack.fireStart);
            // A laser fires continuously until fireEnd. Burst children are
            // accounted for by advancedShots above.
            double last = attack.spec.kind == AttackKind.Laser ?
                attack.fireEnd : attack.fireStart;
            lastFire = Math.Max(lastFire, last);
        }
        for (int i = rainStart; i < rainCues.Count; i++)
        {
            RainCue attack = rainCues[i];
            warningAt = Math.Min(warningAt, attack.visibleStart);
            double first = attack.firstImpact -
                fallDurationBeats * attack.secondsPerBeat;
            firstFire = Math.Min(firstFire, first);
            double last = first + Mathf.Max(.25f,
                attack.spec.duration - .55f) * attack.secondsPerBeat;
            lastFire = Math.Max(lastFire, last);
        }

        if (double.IsInfinity(warningAt) ||
            double.IsInfinity(firstFire) || double.IsInfinity(lastFire))
            return;

        output.Add(new MouseEnemy01TelegraphUI.Cue
        {
            appearAt = Math.Max(0d, warningAt - enemyLeadBeforeWarning),
            fireAt = firstFire,
            lastShotAt = lastFire,
            disappearAt = Math.Max(0.01d, lastFire + enemyLeaveAfterAttack),
            sourceX = spec.kind == AttackKind.Legacy
                ? .5f : Mathf.Clamp(spec.x, .14f, .86f)
        });
    }

    private void CreateRainGateMarkers()
    {
        if (safeGateLeft != null || bulletLayer == null)
            return;

        // The old WaveEmitter_A / WaveEmitter_B red source bars are removed.
        // Preserve only small brackets showing the safe rain exit corridor.
        safeGateLeft = MakeImage("OpenCorridor_Left", bulletLayer, null,
            new Color(.97f, .76f, .72f, .66f));
        safeGateLeft.sizeDelta = new Vector2(5f, 24f);
        safeGateRight = MakeImage("OpenCorridor_Right", bulletLayer, null,
            new Color(.97f, .76f, .72f, .66f));
        safeGateRight.sizeDelta = safeGateLeft.sizeDelta;
        safeGateLeft.gameObject.SetActive(false);
        safeGateRight.gameObject.SetActive(false);
    }

    private void UpdateRainGateMarkers(double now)
    {
        if (safeGateLeft == null || safeGateRight == null)
            CreateRainGateMarkers();
        if (safeGateLeft == null || safeGateRight == null) return;

        // Gates with an imminent crossing take precedence over distant rain
        // markers. Otherwise an unrelated long rain preview could hide the
        // much more urgent passage hint during a mixed barrage.
        if (UpdateFullBandHitGateCue(now) || UpdateSharedGateCue(now))
            return;

        RainCue cue = null;
        foreach (RainCue candidate in rainCues)
        {
            if (now < candidate.visibleStart ||
                now > candidate.visibleEnd)
                continue;
            if (cue == null || candidate.firstImpact > cue.firstImpact)
                cue = candidate;
        }

        if (cue == null)
        {
            safeGateLeft.gameObject.SetActive(false);
            safeGateRight.gameObject.SetActive(false);
            return;
        }

        float activeT = Mathf.Clamp01((float)
            ((now - cue.firstImpact) /
            Math.Max(.05d,
                (cue.spec.duration - .55f) * cue.secondsPerBeat)));
        float center = Mathf.Clamp(
            cue.spec.x + cue.spec.drift * activeT, .22f, .78f);
        float half = cue.spec.gap >= 7 ? .20f :
            cue.spec.gap >= 5 ? .15f : .125f;

        float y = -bulletLayer.rect.height * .22f;
        safeGateLeft.anchoredPosition =
            new Vector2(XLocal(center - half), y);
        safeGateRight.anchoredPosition =
            new Vector2(XLocal(center + half), y);
        bool show = now < cue.firstImpact + cue.secondsPerBeat * .5f;
        safeGateLeft.gameObject.SetActive(show);
        safeGateRight.gameObject.SetActive(show);
    }

    private bool UpdateFullBandHitGateCue(double now)
    {
        MouseModeChartData chart = scheduledChart != null
            ? scheduledChart.data : null;
        if (chart == null || chart.songId != "FullBandHit_MOUSE" ||
            chart.attacks == null || bulletLayer == null)
            return false;

        // Gate brackets use the same exact first-risk lead as the shots.
        // The straight gate rows cross the lower y=0.72 line after
        // 0.775 stage heights of travel; avoid a fixed 4.6-beat guess.
        const double warningLeadSeconds = 0.68d;
        const double hideAfterImpactSeconds = 0.10d;
        double upcomingImpact = double.PositiveInfinity;
        float upcomingLeft = 0f;
        float upcomingRight = 0f;

        foreach (MouseModeChartAttack attack in chart.attacks)
        {
            if (attack == null || string.IsNullOrEmpty(attack.patternId) ||
                !attack.patternId.StartsWith("fh_gate",
                    StringComparison.Ordinal))
                continue;

            string encoded = attack.patternId;
            float spacing = Mathf.Clamp(
                MouseModeBulletPatternLibrary.GetParameter(
                    encoded, "spacing", 1f), .55f, 1.8f);
            bool mirrored =
                MouseModeBulletPatternLibrary.GetParameter(
                    encoded, "mirror", 0f) > .5f;
            int openLanes =
                MouseModeBulletPatternLibrary.FullBandHitGateOpenLanes(encoded);

            float speed = Mathf.Clamp(
                MouseModeBulletPatternLibrary.GetParameter(
                    encoded, "speed", 1f), .40f, 1.6f);
            float flight = .775f / (.171f * speed);
            float attackLead = Mathf.Min(
                MouseModeBulletPatternLibrary.FirstRiskCrossingBeat(encoded),
                Mathf.Max(0f, attack.beat));
            for (int row = 0; row < 3; row++)
            {
                float impactBeat = attack.beat +
                    row * 2.50f * spacing + flight - attackLead;
                double impact = MouseModeChartTiming.BeatToSeconds(
                    chart, impactBeat);
                if (now < impact - warningLeadSeconds ||
                    now > impact + hideAfterImpactSeconds ||
                    impact >= upcomingImpact)
                    continue;

                int safeStart =
                    MouseModeBulletPatternLibrary.FullBandHitGateSafeStart(
                        row, mirrored, openLanes);
                // Inner boundaries of the two/three safe lanes. These are
                // orientation hints, not a promise of an exact hitbox edge.
                upcomingLeft = .11f + (safeStart - .24f) * .13f;
                upcomingRight = .11f +
                    (safeStart + openLanes - .76f) * .13f;
                upcomingImpact = impact;
            }
        }

        if (double.IsInfinity(upcomingImpact)) return false;
        float y = -bulletLayer.rect.height * .22f;
        safeGateLeft.anchoredPosition =
            new Vector2(XLocal(upcomingLeft), y);
        safeGateRight.anchoredPosition =
            new Vector2(XLocal(upcomingRight), y);
        safeGateLeft.gameObject.SetActive(true);
        safeGateRight.gameObject.SetActive(true);
        return true;
    }

    // The other songs' gate patterns also need their NEXT opening announced.
    // Both layout and preview use SharedGateSafeStart, avoiding hints that
    // disagree with the actual projectile positions.
    private bool UpdateSharedGateCue(double now)
    {
        MouseModeChartData chart = scheduledChart != null
            ? scheduledChart.data : null;
        if (chart == null || chart.attacks == null || bulletLayer == null)
            return false;

        double nextImpact = double.PositiveInfinity;
        float left = 0f;
        float right = 0f;
        foreach (MouseModeChartAttack attack in chart.attacks)
        {
            if (attack == null || string.IsNullOrEmpty(attack.patternId))
                continue;
            string pattern = attack.patternId.Split('|')[0];
            if (pattern != "mix_gate" && pattern != "boot_gate")
                continue;

            float spacing = Mathf.Clamp(
                MouseModeBulletPatternLibrary.GetParameter(
                    attack.patternId, "spacing", 1f), .55f, 1.8f);
            float speed = Mathf.Clamp(
                MouseModeBulletPatternLibrary.GetParameter(
                    attack.patternId, "speed", 1f), .40f, 1.6f);
            float firstFlight = pattern == "boot_gate"
                ? .675f / (.112f * 1.12f * speed)
                : .675f / (.112f * 1.06f * speed);
            float attackLead = Mathf.Min(
                MouseModeBulletPatternLibrary.FirstRiskCrossingBeat(
                    attack.patternId), Mathf.Max(0f, attack.beat));
            float rowBeatStep = 1.75f;
            float firstX = pattern == "boot_gate" ? .10f : .11f;
            float laneStep = pattern == "boot_gate" ? .114f : .13f;
            float spread = Mathf.Clamp(
                MouseModeBulletPatternLibrary.GetParameter(
                    attack.patternId, "spread", 1f), .7f, 1.7f);
            float center = Mathf.Clamp(
                MouseModeBulletPatternLibrary.GetParameter(
                    attack.patternId, "x", .5f), .2f, .8f);

            for (int row = 0; row < 3; row++)
            {
                float impactBeat = attack.beat +
                    row * rowBeatStep * spacing + firstFlight - attackLead;
                double impact = MouseModeChartTiming.BeatToSeconds(
                    chart, impactBeat);
                if (impact < now - .10d || impact > now + .75d ||
                    impact >= nextImpact)
                    continue;

                int startLane =
                    MouseModeBulletPatternLibrary.SharedGateSafeStart(
                        pattern, row);
                if (startLane < 0) continue;

                // The markers indicate the authored lane opening, not
                // guaranteed immunity from unrelated overlapping bullets.
                float innerLeft = firstX +
                    (startLane - .25f) * laneStep;
                float innerRight = firstX +
                    (startLane + 1.25f) * laneStep;
                left = Mathf.Clamp(center + (innerLeft - .5f) * spread,
                    .035f, .965f);
                right = Mathf.Clamp(center + (innerRight - .5f) * spread,
                    .035f, .965f);
                nextImpact = impact;
            }
        }

        if (double.IsInfinity(nextImpact)) return false;
        float y = -bulletLayer.rect.height * .22f;
        safeGateLeft.anchoredPosition = new Vector2(XLocal(left), y);
        safeGateRight.anchoredPosition = new Vector2(XLocal(right), y);
        safeGateLeft.gameObject.SetActive(true);
        safeGateRight.gameObject.SetActive(true);
        return true;
    }

    private void UpdateSpecialAttacks(double now, Vector2 pinoLocal,
        Vector2 previousPinoFrame)
    {
        for (int i = specialAttacks.Count - 1; i >= 0; i--)
        {
            SpecialAttack a = specialAttacks[i];
            if (now >= a.fireEnd)
            {
                if (a.root != null) Destroy(a.root.gameObject);
                specialAttacks.RemoveAt(i);
                continue;
            }
            if (now < a.warnStart) continue;
            if (a.root == null) CreateSpecialVisual(a);
            bool firing = now >= a.fireStart;
            float warningProgress = Mathf.Clamp01((float)((now - a.warnStart) /
                Math.Max(0.001d, a.fireStart - a.warnStart)));
            float activeProgress = Mathf.Clamp01((float)((now - a.fireStart) /
                Math.Max(0.001d, a.fireEnd - a.fireStart)));
            if (a.spec.kind == AttackKind.Laser)
                UpdateLaser(a, firing, warningProgress, activeProgress,
                    now, pinoLocal, previousPinoFrame);
            else UpdateBurst(a, firing, warningProgress, activeProgress, now, pinoLocal);
            if (failed) break;
        }
    }

    private void UpdateLaser(SpecialAttack a, bool firing, float prep,
        float active, double now, Vector2 pinoLocal,
        Vector2 previousPinoLocalFrame)
    {
        Vector2 start = LocalPoint(a.spec.x, 0.015f);
        Vector2 end = LaserEnd(start, a.spec.angle);
        float width = Mathf.Max(30f, a.spec.width * bulletLayer.rect.width * 1.15f);
        SetLine(a.outer.rectTransform, start, end, width * 1.23f);
        SetLine(a.face.rectTransform, start, end, width);
        SetLine(a.core.rectTransform, start, end, width * 0.28f);
        SetLine(a.warning.rectTransform, start, end,
            Mathf.Max(2.5f, width * 0.15f));
        SetLine(a.chargeBand.rectTransform, start, end, width);
        Vector2 vec = (end - start).normalized;
        Vector2 across = new Vector2(-vec.y, vec.x);
        float notchWidth = Mathf.Max(11f, width * 1.20f);
        Vector2 anchorA = Vector2.Lerp(start, end, .33f);
        Vector2 anchorB = Vector2.Lerp(start, end, .67f);
        SetLine(a.tickA.rectTransform, anchorA - across * notchWidth * .5f,
            anchorA + across * notchWidth * .5f, 3.0f);
        SetLine(a.tickB.rectTransform, anchorB - across * notchWidth * .5f,
            anchorB + across * notchWidth * .5f, 3.0f);
        a.chargeBand.enabled = !firing;
        a.tickA.enabled = !firing;
        a.tickB.enabled = !firing;
        a.outer.enabled = firing;
        a.face.enabled = firing;
        a.core.enabled = firing;
        a.warning.enabled = !firing;
        a.source.rectTransform.anchoredPosition = start;
        a.source.rectTransform.localScale = Vector3.one *
            (firing ? 1.0f : 0.85f + 0.15f * prep);
        if (firing)
        {
            Color body = new Color(0.79f, 0.43f, 0.84f, 0.84f);
            body.a *= Mathf.Clamp01((float)((a.fireEnd - now) / 0.13d));
            a.face.color = body;
            float tail = Mathf.Clamp01((float)((a.fireEnd - now) / .12d));
            a.outer.color = new Color(.14f, .10f, .17f, .96f * tail);
            a.core.color = new Color(1f, .91f, .99f, .86f * tail);
            // Laser danger is the FULL rendered path, not only a moving head.
            Vector2 fromPino = a.wasFiring && hasPreviousPinoLocal
                ? previousPinoLocalFrame : pinoLocal;
            if (pino != null && now >= invulnerableUntil &&
                DistanceBetweenSegments(fromPino, pinoLocal, start, end)
                    < width * 0.50f + PinoRadiusInBulletLayer())
                RegisterHit(now);
            a.wasFiring = true;
        }
        else
        {
            a.wasFiring = false;
            float alpha = .18f + prep * .38f;
            a.warning.color = new Color(.89f, .67f, .96f, alpha);
            a.chargeBand.color = new Color(.73f, .45f, .85f, .05f + .15f * prep);
            float blink = (Mathf.Sin(Time.unscaledTime * (5f + prep * 7f)) + 1f) * .5f;
            a.tickA.color = new Color(1f, .87f, .99f, .42f + blink * .46f);
            a.tickB.color = new Color(1f, .87f, .99f, .42f + (1f - blink) * .46f);
        }
    }

    private void UpdateBurst(SpecialAttack a, bool firing, float prep,
        float active, double now, Vector2 pinoLocal)
    {
        Vector2 target = LocalPoint(a.spec.x, a.spec.y);
        float radius = a.spec.radius * Mathf.Min(bulletLayer.rect.width,
            bulletLayer.rect.height);
        // The ring is a compact timing cue, not the full flying shrapnel.
        float cueRadius = Mathf.Min(radius * .55f, VisibleBulletSize() * 3.5f);
        a.orb.enabled = !firing;
        a.ring.enabled = true;
        a.bombTrail.enabled = !firing;
        a.reticleH.enabled = !firing;
        a.reticleV.enabled = !firing;
        float targetWidth = Mathf.Max(24f, VisibleBulletSize() * 1.55f);
        SetLine(a.reticleH.rectTransform,
            target - new Vector2(targetWidth, 0),
            target + new Vector2(targetWidth, 0), 2.2f);
        SetLine(a.reticleV.rectTransform,
            target - new Vector2(0, targetWidth),
            target + new Vector2(0, targetWidth), 2.2f);
        if (!firing)
        {
            Vector2 source = virusEmitter != null ?
                (Vector2)bulletLayer.InverseTransformPoint(virusEmitter.position) :
                LocalPoint(0.5f, 0f);
            Vector2 orbPos = Vector2.Lerp(source, target, prep);
            a.orb.rectTransform.anchoredPosition = orbPos;
            a.orb.rectTransform.localRotation = Quaternion.Euler(0, 0, prep * 360f);
            a.orb.color = new Color(1f, .76f, .38f, .80f + .20f * prep);
            SetLine(a.bombTrail.rectTransform, source, orbPos,
                Mathf.Max(3f, VisibleBulletSize() * .12f));
            a.bombTrail.color = new Color(.99f, .68f, .31f, .20f + .26f * prep);
            // Compact warning ring; departing shrapnel travels beyond it.
            float ringSize = Mathf.Max(16f, cueRadius * 2f * (0.92f + prep * 0.08f));
            a.ring.rectTransform.anchoredPosition = target;
            a.ring.rectTransform.sizeDelta = Vector2.one * ringSize;
            a.ring.color = new Color(0.99f, 0.70f, 0.34f, 0.25f + prep * 0.25f);
        }
        else
        {
            // A quick flash at detonation, then a real expanding ring of bullets.
            float flash = Mathf.Clamp01(1f - active * 8f);
            a.ring.rectTransform.anchoredPosition = target;
            a.ring.rectTransform.sizeDelta = Vector2.one *
                Mathf.Max(16f, cueRadius * 2f * Mathf.Min(1f, active * 3f));
            a.ring.color = new Color(1f, 0.79f, 0.40f, flash * 0.27f);
            // Independent explosion pellets were already scheduled at chart
            // load. Do not clamp, fade, or delete them when this flash ends.
        }
    }

    private float VisibleBulletSize()
    {
        // 900x450 기준 약 26~32px. 큰 해상도에서도 과도하게 비대해지지 않게 한다.
        // 충돌 범위 역시 이 값에서 계산하므로 실제 피격과 외형 크기가 함께 조정된다.
        float h = bulletLayer != null ? bulletLayer.rect.height : 450f;
        float heightScale = Mathf.Clamp(h / 450f, 0.82f, 1.15f);
        return Mathf.Clamp(bulletSize * heightScale, 24f, 38f);
    }

    private float XLocal(float n) => (Mathf.Clamp01(n) - 0.5f) * bulletLayer.rect.width;

    private float GetPinoNormalizedX()
    {
        if (pino == null || bulletLayer.rect.width <= 0f) return 0.5f;
        return Mathf.Clamp01(PinoLocalCenter().x / bulletLayer.rect.width + 0.5f);
    }

    private Vector2 PinoLocalCenter()
    {
        if (pino == null) return new Vector2(100000f, 100000f);
        if (pinoHitbox != null && pinoHitbox.enabled)
            return bulletLayer.InverseTransformPoint(
                pinoHitbox.transform.TransformPoint(pinoHitbox.offset));
        return bulletLayer.InverseTransformPoint(pino.TransformPoint(pino.rect.center));
    }

    private void OnRhythmNoteJudged(string judgement)
    {
        if (failed || chartPlayer == null) return;
        // Only real chart notes trigger this event; empty-click UI MISS is
        // intentionally excluded so an input cannot drain stability twice.
        int delta = JudgementStabilityDelta(
            judgement, true, perfectRecovery,
            greatRecovery, goodPenalty, missPenalty);
        if (delta == 0) return;
        Stability = Mathf.Clamp(Stability + delta, 0, 100);
        UpdateStabilityVisual();
        if (Stability == 0 && enableStabilityFailure && stopImmediatelyAtZero)
            pendingRhythmFailure = true;
    }

    // Pure calculation shared with EditMode tests. Rules remain adjustable
    // in the Inspector; a MISS from an empty click is not a chart-note MISS.
    public static int JudgementStabilityDelta(string judgement,
        bool isRealNoteMiss, int perfect, int great, int good, int miss)
    {
        switch (judgement)
        {
            case "PERFECT": return Mathf.Max(0, perfect);
            case "GREAT": return Mathf.Max(0, great);
            case "GOOD": return -Mathf.Max(0, good);
            case "MISS": return isRealNoteMiss ? -Mathf.Max(0, miss) : 0;
            default: return 0;
        }
    }

    private void RegisterHit(double now)
    {
        invulnerableUntil = now + hitInvulnerabilitySeconds;
        HitCount++;
        if (logPinoHits)
            Debug.Log("[MouseBullet] Pino HIT #" + HitCount + " (무적 " +
                hitInvulnerabilitySeconds.ToString("0.##") + "초)", this);

        Stability = Mathf.Clamp(Stability - damagePerHit, 0, 100);
        UpdateStabilityVisual();
        if (pinoAnimator != null) pinoAnimator.PlayHit();

        PinoHitFeedback feedback = GetComponent<PinoHitFeedback>();
        if (feedback == null) feedback = gameObject.AddComponent<PinoHitFeedback>();
        feedback.Play(pino);
        PinoHit?.Invoke(Stability);

        // Damage can occur while the projectile lists are being iterated.
        // Finish on the next director Update rather than clearing lists here.
        if (Stability == 0 && enableStabilityFailure && stopImmediatelyAtZero)
            pendingRhythmFailure = true;
    }

    private void TryDepleteStability()
    {
        pendingRhythmFailure = false;
        if (failed || !enableStabilityFailure || Stability > 0)
            return;

        failed = true;
        StabilityDepleted?.Invoke();
        if (chartPlayer != null) chartPlayer.StopChart();
        PinoMouseCursorUI cursor = pino != null ? pino.GetComponent<PinoMouseCursorUI>() : null;
        if (cursor != null) cursor.ExitGameplay();
        ClearShots();
        if (failurePanel != null)
        {
            failurePanel.SetActive(true);
            failurePanel.transform.SetAsLastSibling();
        }
    }

    private void EnsureStabilityDisplay()
    {
        if (stabilityFill != null ||
            (gameObject.scene.name != "Mouse" && gameObject.scene.name != "Tutorial"))
            return;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        Transform hud = canvas.transform.Find("HUDLayer");
        if (hud == null) return;
        RectTransform strip = MouseGameplayScoreComboHUD.GetOrCreateReadoutRoot(hud);
        RectTransform root = RuruConsoleUI.Region(strip,"Stability_Runtime",.340f,.12f,.609f,.89f);
        stabilityStatusLabel = RuruConsoleUI.Text(root,"StabilityStatus_Runtime","안정화",0,.59f,.55f,.97f,26,RuruConsoleUI.Muted);
        stabilityPercentLabel = RuruConsoleUI.Text(root,"StabilityPercent","100%",.58f,.50f,1,1,38,RuruConsoleUI.Paper,TextAlignmentOptions.MidlineRight);
        var border = RuruConsoleUI.Strip(root,"Stability_Frame",0,.20f,1,.43f,new Color32(133,111,159,255));
        var track = RuruConsoleUI.Strip(border.transform,"Stability_Track",.006f,.12f,.994f,.88f,new Color32(28,23,39,255));
        stabilityFill = RuruConsoleUI.Strip(track.transform,"Stability_Fill",0,0,1,1,RuruConsoleUI.Lilac);
        stabilityFill.sprite=Resources.Load<Sprite>("RuruArt/UI/Stability_Fill");
        if(stabilityFill.sprite != null)
        {
            stabilityFill.type=Image.Type.Filled;
            stabilityFill.fillMethod=Image.FillMethod.Horizontal;
            stabilityFill.fillOrigin=(int)Image.OriginHorizontal.Left;
            fallbackGaugeCreated=false;
        }
        else fallbackGaugeCreated=true;
        runtimeGaugeRoot=root;
        EnsureGrazeLabel();
        UpdateStabilityVisual();
    }

    private static Image CreateStabilityArtImage(string name, RectTransform root,
        Sprite sprite, Vector2 min, Vector2 max, Image.Type type)
    {
        GameObject go = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(root, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = min;
        rt.offsetMax = max;
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = type;
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private void UpdateStabilityVisual()
    {
        if (stabilityFill == null) return;
        if (stabilityStatusLabel != null)
        {
            string condition = Stability <= 25 ? "위험" :
                Stability <= 50 ? "주의" : "정상";
            stabilityStatusLabel.text = Stability <= 50 ? "안정화 · " + condition : "안정화";
            if (stabilityPercentLabel != null)
            {
                stabilityPercentLabel.text = Stability.ToString() + "%";
                stabilityPercentLabel.color = Stability <= 25 ? RuruConsoleUI.Rose : RuruConsoleUI.Paper;
            }
            stabilityStatusLabel.color = Stability <= 25
                ? new Color(1f,.65f,.75f,1f)
                : new Color(.96f,.92f,1f,1f);
        }
        float amount = Mathf.Clamp01(Stability / 100f);
        if (fallbackGaugeCreated)
        {
            RectTransform fillRect = stabilityFill.rectTransform;
            // Fill left-to-right, keeping the inset proportional to the
            // width so the bar does not invert at very low stability.
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(amount, 1f);
            fillRect.offsetMin = new Vector2(4f, 4f);
            fillRect.offsetMax = new Vector2(-4f * amount, -4f);
            stabilityFill.enabled = amount > 0f;
            stabilityFill.color = Stability <= 25
                ? new Color(.95f, .41f, .42f, 1f)
                : Stability <= 50
                    ? new Color(.97f, .73f, .43f, 1f)
                    : new Color(.70f, .64f, .88f, 1f);
        }
        else if (stabilityFill.type == Image.Type.Filled)
        {
            stabilityFill.fillAmount = amount;
            stabilityFill.enabled = amount > 0f;
            stabilityFill.color = Stability <= 25
                ? new Color(1f, .67f, .76f, 1f)
                : Stability <= 50
                    ? new Color(1f, .88f, .87f, 1f)
                    : Color.white;
        }
    }

    // Only safe close passes count. Awards are also rate-limited across shots.
    public static bool InGrazeBand(float distance, float hitRadius,
        float outerBonus)
    {
        return distance > hitRadius + 2f &&
               distance <= hitRadius + Math.Max(4f, outerBonus);
    }

    private bool RegisterGraze(double now)
    {
        if (!enableGrazeRewards || now < invulnerableUntil ||
            now - lastGrazeTime < grazeCooldownSeconds)
            return false;

        lastGrazeTime = now;
        GrazeCount++;
        Stability = Mathf.Clamp(Stability + grazeRecovery, 0, 100);
        UpdateStabilityVisual();
        EnsureGrazeLabel();
        if (grazeLabel != null)
        {
            grazeLabel.text = "GRAZE  +" + grazeRecovery;
            grazeLabel.color = new Color(.77f, .71f, .91f, 1f);
            grazeLabel.gameObject.SetActive(true);
            grazeLabelUntil = Time.unscaledTime + .62f;
        }
        return true;
    }

    private void EnsureGrazeLabel()
    {
        if (grazeLabel != null || runtimeGaugeRoot == null) return;
        grazeLabel=RuruConsoleUI.Text(runtimeGaugeRoot,"GrazeFeedback","",.45f,0,1,.22f,15,RuruConsoleUI.Mint,TextAlignmentOptions.MidlineRight);
        grazeLabel.gameObject.SetActive(false);
    }

    private void UpdateGrazeLabel()
    {
        if (grazeLabel == null || !grazeLabel.gameObject.activeSelf) return;
        float remaining = grazeLabelUntil - Time.unscaledTime;
        if (remaining <= 0f)
        {
            grazeLabel.gameObject.SetActive(false);
            return;
        }
        Color color = grazeLabel.color;
        color.a = Mathf.Clamp01(remaining / .32f);
        grazeLabel.color = color;
    }

    private void ResetSession()
    {
        ClearShots();
        scheduleBuilt = false;
        scheduledChart = null;
        Stability = 100;
        HitCount = 0;
        GrazeCount = 0;
        lastGrazeTime = double.NegativeInfinity;
        grazeLabelUntil = 0f;
        if (grazeLabel != null) grazeLabel.gameObject.SetActive(false);
        invulnerableUntil = double.NegativeInfinity;
        hasPreviousPinoLocal = false;
        pendingRhythmFailure = false;
        failed = false;
        UpdateStabilityVisual();
    }

    private void DestroyShot(Shot shot)
    {
        if (shot.body != null) Destroy(shot.body.gameObject);
        if (shot.trail != null) Destroy(shot.trail.gameObject);
        if (shot.warningLine != null) Destroy(shot.warningLine.gameObject);
        if (shot.warningMark != null) Destroy(shot.warningMark.gameObject);
    }

    private void ClearShots()
    {
        hasPreviousPinoLocal = false;
        foreach (Shot shot in shots) DestroyShot(shot);
        shots.Clear();
        foreach (AdvancedShot a in advancedShots)
            RecycleAdvancedBulletVisual(a);
        advancedShots.Clear();
        foreach (SpecialAttack a in specialAttacks)
            if (a.root != null) Destroy(a.root.gameObject);
        specialAttacks.Clear();
        rainCues.Clear();
        if (safeGateLeft != null) safeGateLeft.gameObject.SetActive(false);
        if (safeGateRight != null) safeGateRight.gameObject.SetActive(false);
        if (enemy01 != null) enemy01.SetCues(null);
    }

    private static RectTransform MakeRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        return rt;
    }

    // Matte enemy projectile, recognisable without bloom. Not a rhythm note:
    // tapered tail, broad shoulders, dark outer stroke and small cut-out notch.
    // Four unmistakable matte silhouettes, not four tints of the same shard.
    // The bullet hit radius remains unchanged across every appearance.
    public static int ProjectileSilhouetteKey(int colorIndex, bool burstFragment)
    {
        if (burstFragment || colorIndex == 2) return 2; // solid notched hex
        if (colorIndex == 1) return 1; // compact round pulse
        if (colorIndex == 3) return 3; // angular diamond
        return 0; // directional shard
    }

    private Sprite GetProjectileSprite(int colorIndex, bool burstFragment)
    {
        switch (ProjectileSilhouetteKey(colorIndex, burstFragment))
        {
            case 1:
                if (pulseSprite == null) CreatePulseSprite();
                return pulseSprite;
            case 2:
                if (blastSprite == null) EnsureBlastSprite();
                return blastSprite;
            case 3:
                if (diamondSprite == null) CreateDiamondSprite();
                return diamondSprite;
            default:
                if (shardSprite == null) CreateShardSprite();
                return shardSprite;
        }
    }

    private void CreatePulseSprite()
    {
        if (pulseSprite != null) return;
        const int size = 64;
        pulseTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        pulseTexture.filterMode = FilterMode.Bilinear;
        pulseTexture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x + .5f - size * .5f) / (size * .5f);
                float ny = (y + .5f - size * .5f) / (size * .5f);
                float radius = Mathf.Sqrt(nx * nx + ny * ny);
                float alpha = Mathf.Clamp01((.84f - radius) * 32f);
                // Opaque readable disk, one dark rim and a plain inset.
                Color c = radius > .63f
                    ? new Color(.19f, .13f, .23f, 1f)
                    : radius < .32f
                        ? new Color(1f, .98f, .98f, 1f)
                        : new Color(.86f, .77f, .91f, 1f);
                c.a = alpha;
                pixels[y * size + x] = c;
            }
        pulseTexture.SetPixels(pixels);
        pulseTexture.Apply();
        pulseSprite = Sprite.Create(pulseTexture,
            new Rect(0f, 0f, size, size), new Vector2(.5f, .5f));
    }

    private void CreateDiamondSprite()
    {
        if (diamondSprite != null) return;
        const int size = 64;
        diamondTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        diamondTexture.filterMode = FilterMode.Bilinear;
        diamondTexture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x + .5f - size * .5f) / (size * .5f);
                float ny = (y + .5f - size * .5f) / (size * .5f);
                float shape = Mathf.Abs(nx) / .70f +
                    Mathf.Abs(ny) / .88f;
                float alpha = Mathf.Clamp01((1f - shape) * 24f);
                Color c = shape > .77f
                    ? new Color(.17f, .15f, .23f, 1f)
                    : new Color(.93f, .94f, .98f, 1f);
                // A short flat seam improves identification at 26-32px.
                if (Mathf.Abs(ny) < .09f && shape < .58f)
                    c = new Color(.36f, .35f, .44f, 1f);
                c.a = alpha;
                pixels[y * size + x] = c;
            }
        diamondTexture.SetPixels(pixels);
        diamondTexture.Apply();
        diamondSprite = Sprite.Create(diamondTexture,
            new Rect(0f, 0f, size, size), new Vector2(.5f, .5f));
    }

    private void CreateShardSprite()
    {
        if (shardSprite != null) return;
        const int size = 72;
        shardTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        shardTexture.filterMode = FilterMode.Bilinear;
        shardTexture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x + .5f - size * .5f) / (size * .5f);
                float ny = (y + .5f - size * .5f) / (size * .5f);
                // Broad, rounded head, tapering into one downward spear point.
                float width = ny > .15f ? .62f - (.30f * (ny - .15f)) :
                    .62f + .56f * ny;
                width = Mathf.Max(.035f, width);
                float edge = Mathf.Abs(nx) / width;
                bool inside = ny > -.94f && ny < .79f && edge < 1f;
                float aa = Mathf.Clamp01((1f - edge) * 15f) *
                    Mathf.Clamp01((ny + .94f) * 30f) * Mathf.Clamp01((.79f - ny) * 30f);
                Color c = Color.clear;
                if (inside)
                {
                    c = edge > .80f || ny < -.80f || ny > .69f ?
                        bulletBorderColor : bulletColor;
                    // Two clean facets, no gradient/glow.
                    if (edge < .5f && ny > .14f && ny < .36f)
                        c = new Color(.97f, .78f, .75f, 1f);
                    if (Mathf.Abs(nx) < .10f && ny > -.10f && ny < .08f)
                        c = new Color(.30f, .14f, .24f, 1f);
                    c.a *= aa;
                }
                pixels[y * size + x] = c;
            }
        shardTexture.SetPixels(pixels);
        shardTexture.Apply();
        shardSprite = Sprite.Create(shardTexture, new Rect(0, 0, size, size),
            new Vector2(.5f, .5f));
    }

    // Runtime-made hexagon with a central notch. Unlike the pointed falling bullet,
    // this icon remains distinct even with color filters / grayscale rendering.
    private void EnsureBlastSprite()
    {
        if (blastSprite != null) return;
        const int size = 72;
        blastTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        blastTexture.filterMode = FilterMode.Bilinear;
        blastTexture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f - size * 0.5f) / (size * 0.5f);
                float ny = (y + 0.5f - size * 0.5f) / (size * 0.5f);
                float edge = Mathf.Max(Mathf.Abs(nx) / 0.82f,
                    Mathf.Max(Mathf.Abs(ny) / 0.84f,
                        (Mathf.Abs(nx) + 0.54f * Mathf.Abs(ny)) / 0.94f));
                float alpha = Mathf.Clamp01((1.0f - edge) * 25f);
                // Dark central stamp, not a transparent safe spot: the full hex is dangerous.
                float center = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny));
                Color fill = center < 0.15f
                    ? new Color(0.22f, 0.18f, 0.20f, alpha)
                    : new Color(1f, 1f, 1f, alpha);
                pixels[y * size + x] = fill;
            }
        blastTexture.SetPixels(pixels);
        blastTexture.Apply();
        blastSprite = Sprite.Create(blastTexture, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f));
    }

    private static bool InsidePoly(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            Vector2 a = poly[i], b = poly[j];
            bool cross = ((a.y > p.y) != (b.y > p.y)) &&
                         p.x < ((b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x);
            if (cross) inside = !inside;
        }
        return inside;
    }

    private void OnDisable()
    {
        Unsubscribe();
        ClearShots();
        scheduleBuilt = false;
        scheduledChart = null;
    }

    private void OnDestroy()
    {
        Unsubscribe();
        ClearShots();
        while (advancedBulletVisualPool.Count > 0)
        {
            RectTransform cached = advancedBulletVisualPool.Pop();
            if (cached != null) Destroy(cached.gameObject);
        }
        if (shardSprite != null) Destroy(shardSprite);
        if (shardTexture != null) Destroy(shardTexture);
        if (pulseSprite != null) Destroy(pulseSprite);
        if (pulseTexture != null) Destroy(pulseTexture);
        if (diamondSprite != null) Destroy(diamondSprite);
        if (diamondTexture != null) Destroy(diamondTexture);
        if (blastSprite != null) Destroy(blastSprite);
        if (blastTexture != null) Destroy(blastTexture);
        if (attackRingSprite != null) Destroy(attackRingSprite);
        if (attackRingTexture != null) Destroy(attackRingTexture);
        if (temporaryChart != null) Destroy(temporaryChart);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showHitboxInEditor || pino == null) return;
        Gizmos.color = new Color(1f, 0.2f, 0.4f, 0.8f);
        CircleCollider2D circle = pinoHitbox != null ? pinoHitbox : pino.GetComponent<CircleCollider2D>();
        if (circle != null)
        {
            Vector3 center = circle.transform.TransformPoint(circle.offset);
            float r = circle.radius * Mathf.Min(
                circle.transform.lossyScale.x, circle.transform.lossyScale.y);
            Gizmos.DrawWireSphere(center, Mathf.Abs(r));
        }
        else
            Gizmos.DrawWireSphere(pino.TransformPoint(pino.rect.center), pinoHitRadius);
    }
#endif
}
