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

    [Header("탄막 외형: 작은 무광 탄환 (자동 생성)")]
    [SerializeField, Range(12f, 30f)] private float bulletSize = 20f;
    [SerializeField] private Color bulletColor = new Color(0.82f, 0.37f, 0.43f, 1f);
    [SerializeField] private Color bulletBorderColor = new Color(0.20f, 0.13f, 0.22f, 1f);
    [SerializeField] private Color warningColor = new Color(0.92f, 0.64f, 0.67f, 0.62f);

    [Header("리듬 연출: 착탄 시점은 채보 박자에 고정")]
    [SerializeField, Range(1.0f, 3.5f)] private float fallDurationBeats = 2.2f;
    [SerializeField, Range(0.5f, 2.2f)] private float warningLeadBeats = 1.4f;
    [SerializeField, Range(0.3f, 1.5f)] private float exitDurationBeats = 0.85f;

    [Header("피격")]
    [SerializeField, Range(4f, 32f)] private float pinoHitRadius = 9f;
    [SerializeField, Range(0.1f, 2.5f)] private float hitInvulnerabilitySeconds = 1.15f;

    [Header("BOSS 안정화 (선택)")]
    [SerializeField] private Image stabilityFill;
    [SerializeField, Range(1, 100)] private int damagePerHit = 12;
    [SerializeField] private GameObject failurePanel;

    [Header("패턴 검수")]
    [SerializeField] private bool showFairnessWarnings = true;
    [SerializeField] private bool showHitboxInEditor = false;

    public event Action<int> PinoHit;
    public event Action StabilityDepleted;

    public int Stability { get; private set; } = 100;
    public int HitCount { get; private set; }
    public int FinalScore
    {
        get
        {
            int baseScore = chartPlayer != null ? chartPlayer.Score : 0;
            return Mathf.Max(0, baseScore - HitCount * 20000);
        }
    }

    // Laser and burst are REAL chart events. They are stored in the existing
    // MouseModeChartAttack.patternId string so old charts and class definitions
    // remain compatible. Example:
    // laser|x=0.45|angle=15|width=0.055|warning=1.5|duration=1.5
    // burst|x=0.55|y=0.60|radius=0.32|count=12|warning=1.5|duration=2.0
    public enum AttackKind { Legacy, Laser, Burst }

    public struct AttackSpec
    {
        public AttackKind kind;
        public string legacyId;
        public float x, y, angle, width, radius, warning, duration;
        public int count;
    }

    public static AttackSpec ParseAttack(string value)
    {
        string[] parts = (value ?? "").Split('|');
        string id = parts[0].Trim().ToLowerInvariant();
        AttackSpec spec = new AttackSpec
        {
            kind = id == "laser" ? AttackKind.Laser :
                   id == "burst" ? AttackKind.Burst : AttackKind.Legacy,
            legacyId = id,
            x = 0.5f,
            y = 0.62f,
            angle = 0f,
            width = 0.055f,
            radius = 0.34f,
            warning = 1.5f,
            duration = 1.75f,
            count = 12
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
                case "warning": spec.warning = number; break;
                case "duration": spec.duration = number; break;
            }
        }
        spec.x = Mathf.Clamp01(spec.x);
        spec.y = Mathf.Clamp01(spec.y);
        spec.angle = Mathf.Clamp(spec.angle, -70f, 70f);
        spec.width = Mathf.Clamp(spec.width, 0.015f, 0.16f);
        spec.radius = Mathf.Clamp(spec.radius, 0.10f, 0.9f);
        spec.warning = Mathf.Clamp(spec.warning, 0.25f, 8f);
        spec.duration = Mathf.Clamp(spec.duration, 0.25f, 8f);
        spec.count = Mathf.Clamp(spec.count, 6, 24);
        return spec;
    }

    public static string EncodeAttack(AttackSpec spec)
    {
        var c = System.Globalization.CultureInfo.InvariantCulture;
        if (spec.kind == AttackKind.Legacy) return spec.legacyId ?? "basic";
        string s = spec.kind == AttackKind.Laser ? "laser" : "burst";
        s += "|x=" + spec.x.ToString("0.###", c);
        if (spec.kind == AttackKind.Laser)
        {
            s += "|angle=" + spec.angle.ToString("0.###", c);
            s += "|width=" + spec.width.ToString("0.###", c);
        }
        else
        {
            s += "|y=" + spec.y.ToString("0.###", c);
            s += "|radius=" + spec.radius.ToString("0.###", c);
            s += "|count=" + spec.count.ToString(c);
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
        public readonly List<RectTransform> pellets = new List<RectTransform>();
    }

    private readonly List<SpecialAttack> specialAttacks = new List<SpecialAttack>();
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
    }

    private readonly List<Shot> shots = new List<Shot>(96);
    private RectTransform bulletLayer;
    private RectTransform virusEmitter;
    private Texture2D shardTexture;
    private Sprite shardSprite;
    private MouseModeChartPlayer subscribedPlayer;
    private MouseModeChartAsset scheduledChart;
    private bool scheduleBuilt;
    private bool failed;
    private double lastSongSeconds = double.NaN;
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

    // 씬을 따로 설정하지 않아도 Mouse 씬의 탄막 레이어를 재생 시 자동 준비합니다.
    // Editor에서 Play를 종료하면 런타임 생성 오브젝트는 자동으로 정리됩니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapMouseScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
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

        // UI children render in sibling order. An opaque NoteLayer can hide
        // every attack if BulletLayer is placed behind it. Place bullets
        // immediately AFTER NoteLayer, while preserving later HUD layers.
        int noteOrder = noteRect.GetSiblingIndex();
        int bulletOrder = rect.GetSiblingIndex();
        int targetOrder = bulletOrder < noteOrder ? noteOrder : noteOrder + 1;
        rect.SetSiblingIndex(targetOrder);

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
                imported.songId == "BOOTBEAT_INTRUSION_EXPERT")
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
            if (name.Equals("BOOTBEAT_INTRUSION_EXPERT", StringComparison.OrdinalIgnoreCase)) rank = 100;
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
        // Old component values may still be serialized as oversized shards.
        // Keep this easier test consistent without requiring Inspector changes.
        bulletSize = 18f;
        fallDurationBeats = 2.25f;
        warningLeadBeats = 1.55f;
        exitDurationBeats = 0.8f;
        pinoHitRadius = 9f;
        damagePerHit = 15;
        hitInvulnerabilitySeconds = 0.90f;
        FindReferences();
        ResolvePlayableChart();
        CreateShardSprite();
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

        if (pino == null && transform.parent != null)
        {
            Transform moveArea = transform.parent.Find("PinoMoveArea");
            if (moveArea != null) pino = moveArea.Find("Pino") as RectTransform;
        }
        if (pinoAnimator == null && pino != null)
            pinoAnimator = pino.GetComponent<PinoSpriteAnimator>();

        if (virusEmitter == null && transform.parent != null)
        {
            Transform candidate = transform.parent.Find("VirusRoot");
            if (candidate == null) candidate = transform.parent.Find("Virus");
            virusEmitter = candidate as RectTransform;
        }
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
            subscribedPlayer.AttackTriggered -= OnAttackTriggered;
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
        double secondsPerBeat = 60.0 / chart.bpm;
        foreach (MouseModeChartAttack attack in chart.attacks)
        {
            if (attack == null) continue;
            AttackSpec spec = ParseAttack(attack.patternId);
            if (spec.kind != AttackKind.Legacy)
            {
                double hit = MouseModeChartTiming.BeatToSeconds(chart, attack.beat);
                specialAttacks.Add(new SpecialAttack
                {
                    spec = spec,
                    warnStart = hit - spec.warning * secondsPerBeat,
                    fireStart = hit,
                    fireEnd = hit + spec.duration * secondsPerBeat
                });
            }
            else AddPattern(spec.legacyId, attack.beat, chart, secondsPerBeat);
        }
        int lasers = 0;
        int bursts = 0;
        foreach (SpecialAttack special in specialAttacks)
        {
            if (special.spec.kind == AttackKind.Laser) lasers++;
            if (special.spec.kind == AttackKind.Burst) bursts++;
        }
        Debug.Log("[MouseBullet] ACTIVE CHART: " + current.name +
            " / songId=" + chart.songId +
            " / attacks=" + chart.attacks.Count +
            " / legacyShots=" + shots.Count +
            " / lasers=" + lasers + " / bursts=" + bursts +
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
        switch (id)
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
                Debug.LogWarning("[MouseBullet] Unknown pattern: " + id, this);
                break;
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
        float top = bulletLayer.rect.height * 0.5f + bulletSize;
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

    private void AddShot(MouseModeChartData chart, double beatSec, float beat,
        float originX, float targetX, bool aimed, float curve = 0f, bool telegraph = true)
    {
        double impact = MouseModeChartTiming.BeatToSeconds(chart, beat);
        double launch = impact - beatSec * fallDurationBeats;
        double warning = launch - beatSec * warningLeadBeats;
        double end = impact + beatSec * exitDurationBeats;
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
            originY = BossEmitterY(),
            aimAtWarning = aimed,
            telegraph = telegraph || aimed,
            curve = curve
        });
    }

    private void CreateShotVisuals(Shot shot)
    {
        if (shardSprite == null) CreateShardSprite();
        RectTransform bulletRt = MakeRect("VirusProjectile", bulletLayer);
        bulletRt.sizeDelta = new Vector2(bulletSize, bulletSize);
        Image bulletImage = bulletRt.gameObject.AddComponent<Image>();
        bulletImage.sprite = shardSprite;
        bulletImage.color = Color.white;
        bulletImage.raycastTarget = false;
        bulletRt.gameObject.SetActive(false);
        shot.body = bulletRt;

        if (!shot.telegraph) return;

        RectTransform line = MakeRect("AttackWarningLine", bulletLayer);
        Image lineImage = line.gameObject.AddComponent<Image>();
        lineImage.color = new Color(warningColor.r, warningColor.g, warningColor.b, 0.18f);
        lineImage.raycastTarget = false;
        line.gameObject.SetActive(false);
        shot.warningLine = line;
        shot.warningLineImage = lineImage;

        RectTransform mark = MakeRect("AttackWarningMark", bulletLayer);
        mark.sizeDelta = new Vector2(bulletSize * .65f, bulletSize * .65f);
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
        if (PlayerIsRunning()) BuildScheduleIfNeeded();
        double now = GetChartSeconds();
        if (double.IsNaN(now)) return;

        if (!double.IsNaN(lastSongSeconds) && now < lastSongSeconds - 0.5d)
        {
            ResetSession();
            BuildScheduleIfNeeded();
        }
        lastSongSeconds = now;
        // Do not hide attacks solely because the Unity Game view has lost focus.
        // The chart player owns pause behavior; timeScale and AudioListener pause
        // still prevent combat updates while actually paused.
        if (Time.timeScale <= 0f || AudioListener.pause) return;
        if (bulletLayer == null) return;

        Vector2 pinoLocal = PinoLocalCenter();
        float below = -bulletLayer.rect.height * 0.5f - bulletSize;
        // Fixed risk reference: never track Pino in Y.
        float impactY = -bulletLayer.rect.height * 0.22f;

        for (int i = shots.Count - 1; i >= 0; i--)
        {
            Shot shot = shots[i];
            if (now >= shot.endTime)
            {
                DestroyShot(shot);
                shots.RemoveAt(i);
                continue;
            }
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
                x = Mathf.Lerp(fromX, toX, t) +
                    Mathf.Sin(t * Mathf.PI) * shot.curve * bulletLayer.rect.width;
                y = Mathf.Lerp(shot.originY, impactY, t);
            }
            else
            {
                float t = Mathf.Clamp01((float)((now - shot.impactTime) /
                    Math.Max(.001d, shot.endTime - shot.impactTime)));
                x = toX;
                y = Mathf.Lerp(impactY, below, t);
            }
            shot.body.anchoredPosition = new Vector2(x, y);

            // Visual size is larger than hit radius so close dodges feel possible.
            float combined = pinoHitRadius + bulletSize * .20f;
            if (pino != null && now >= invulnerableUntil &&
                Vector2.Distance(shot.body.anchoredPosition, pinoLocal) < combined)
            {
                DestroyShot(shot);
                shots.RemoveAt(i);
                RegisterHit(now);
                if (failed) break;
            }
        }
        if (!failed) UpdateSpecialAttacks(now, pinoLocal);
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
        attack.root = MakeRect("Attack_" + attack.spec.kind, bulletLayer);
        if (attack.spec.kind == AttackKind.Laser)
        {
            // Matte outline / tinted body / thin bright core / charging trace.
            RectTransform outer = MakeImage("BeamOutline", attack.root, null,
                new Color(0.14f, 0.10f, 0.17f, 0.96f));
            RectTransform face = MakeImage("BeamBody", attack.root, null,
                new Color(0.82f, 0.30f, 0.41f, 0.88f));
            RectTransform core = MakeImage("BeamCore", attack.root, null,
                new Color(1.0f, 0.83f, 0.79f, 0.86f));
            RectTransform warning = MakeImage("FullLengthWarning", attack.root, null,
                new Color(0.92f, 0.60f, 0.67f, 0.42f));
            RectTransform source = MakeImage("Emitter", attack.root, shardSprite,
                new Color(1f, 1f, 1f, 0.9f));
            attack.outer = outer.GetComponent<Image>();
            attack.face = face.GetComponent<Image>();
            attack.core = core.GetComponent<Image>();
            attack.warning = warning.GetComponent<Image>();
            attack.source = source.GetComponent<Image>();
            source.sizeDelta = Vector2.one * (bulletSize * 1.65f);
        }
        else
        {
            EnsureAttackRingSprite();
            RectTransform ring = MakeImage("DetonationWarning", attack.root, attackRingSprite,
                new Color(0.88f, 0.39f, 0.45f, 0.2f));
            attack.ring = ring.GetComponent<Image>();
            RectTransform orb = MakeImage("IncomingBomb", attack.root, shardSprite,
                Color.white);
            attack.orb = orb.GetComponent<Image>();
            orb.sizeDelta = Vector2.one * (bulletSize * 1.5f);
            for (int i = 0; i < attack.spec.count; i++)
            {
                RectTransform pellet = MakeImage("ExplosionPellet_" + i,
                    attack.root, shardSprite, Color.white);
                pellet.sizeDelta = Vector2.one * (bulletSize * 0.83f);
                pellet.gameObject.SetActive(false);
                attack.pellets.Add(pellet);
            }
        }
    }

    private void UpdateSpecialAttacks(double now, Vector2 pinoLocal)
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
                UpdateLaser(a, firing, warningProgress, activeProgress, now, pinoLocal);
            else UpdateBurst(a, firing, warningProgress, activeProgress, now, pinoLocal);
            if (failed) break;
        }
    }

    private void UpdateLaser(SpecialAttack a, bool firing, float prep,
        float active, double now, Vector2 pinoLocal)
    {
        Vector2 start = LocalPoint(a.spec.x, 0.015f);
        Vector2 end = LaserEnd(start, a.spec.angle);
        float width = Mathf.Max(8f, a.spec.width * bulletLayer.rect.width);
        SetLine(a.outer.rectTransform, start, end, width * 1.23f);
        SetLine(a.face.rectTransform, start, end, width);
        SetLine(a.core.rectTransform, start, end, width * 0.28f);
        SetLine(a.warning.rectTransform, start, end,
            Mathf.Max(2.5f, width * 0.15f));
        a.outer.enabled = firing;
        a.face.enabled = firing;
        a.core.enabled = firing;
        a.warning.enabled = !firing;
        a.source.rectTransform.anchoredPosition = start;
        a.source.rectTransform.localScale = Vector3.one *
            (firing ? 1.0f : 0.85f + 0.15f * prep);
        if (firing)
        {
            Color body = new Color(0.82f, 0.30f, 0.41f, 0.84f);
            body.a *= Mathf.Clamp01((float)((a.fireEnd - now) / 0.13d));
            a.face.color = body;
            // Laser danger is the FULL rendered path, not only a moving head.
            if (pino != null && now >= invulnerableUntil &&
                DistanceToSegment(pinoLocal, start, end) < width * 0.50f + pinoHitRadius)
                RegisterHit(now);
        }
        else
        {
            float alpha = 0.18f + prep * 0.38f;
            a.warning.color = new Color(0.95f, 0.60f, 0.67f, alpha);
        }
    }

    private void UpdateBurst(SpecialAttack a, bool firing, float prep,
        float active, double now, Vector2 pinoLocal)
    {
        Vector2 target = LocalPoint(a.spec.x, a.spec.y);
        float radius = a.spec.radius * Mathf.Min(bulletLayer.rect.width,
            bulletLayer.rect.height);
        a.orb.enabled = !firing;
        a.ring.enabled = true;
        if (!firing)
        {
            Vector2 source = virusEmitter != null ?
                (Vector2)bulletLayer.InverseTransformPoint(virusEmitter.position) :
                LocalPoint(0.5f, 0f);
            a.orb.rectTransform.anchoredPosition = Vector2.Lerp(source, target, prep);
            // Warning ring is the FULL eventual projectile spread radius.
            float ringSize = Mathf.Max(16f, radius * 2f * (0.92f + prep * 0.08f));
            a.ring.rectTransform.anchoredPosition = target;
            a.ring.rectTransform.sizeDelta = Vector2.one * ringSize;
            a.ring.color = new Color(0.88f, 0.40f, 0.48f, 0.20f + prep * 0.20f);
        }
        else
        {
            // A quick flash at detonation, then a real expanding ring of bullets.
            float flash = Mathf.Clamp01(1f - active * 8f);
            a.ring.rectTransform.anchoredPosition = target;
            a.ring.rectTransform.sizeDelta = Vector2.one *
                Mathf.Max(16f, radius * 2f * Mathf.Min(1f, active * 3f));
            a.ring.color = new Color(0.96f, 0.65f, 0.61f, flash * 0.24f);
            float distance = radius * active;
            float prevDistance = Mathf.Max(0f, distance - radius *
                Time.unscaledDeltaTime / (float)Math.Max(0.001d, a.fireEnd - a.fireStart));
            for (int j = 0; j < a.pellets.Count; j++)
            {
                RectTransform pellet = a.pellets[j];
                if (!pellet.gameObject.activeSelf) pellet.gameObject.SetActive(true);
                float angle = (j + 0.5f) * 2f * Mathf.PI / a.pellets.Count;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 previous = target + direction * prevDistance;
                Vector2 next = target + direction * distance;
                pellet.anchoredPosition = next;
                if (pino != null && now >= invulnerableUntil &&
                    DistanceToSegment(pinoLocal, previous, next) <
                    pinoHitRadius + pellet.sizeDelta.x * 0.30f)
                    RegisterHit(now);
                if (failed) break;
            }
        }
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
        return bulletLayer.InverseTransformPoint(pino.TransformPoint(pino.rect.center));
    }

    private void RegisterHit(double now)
    {
        invulnerableUntil = now + hitInvulnerabilitySeconds;
        HitCount++;
        Stability = Mathf.Max(0, Stability - damagePerHit);
        UpdateStabilityVisual();
        if (pinoAnimator != null) pinoAnimator.PlayHit();
        PinoHit?.Invoke(Stability);
        if (Stability > 0) return;

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

    private void UpdateStabilityVisual()
    {
        if (stabilityFill != null && stabilityFill.type == Image.Type.Filled)
            stabilityFill.fillAmount = Stability / 100f;
    }

    private void ResetSession()
    {
        ClearShots();
        scheduleBuilt = false;
        scheduledChart = null;
        Stability = 100;
        HitCount = 0;
        invulnerableUntil = double.NegativeInfinity;
        failed = false;
        UpdateStabilityVisual();
    }

    private void DestroyShot(Shot shot)
    {
        if (shot.body != null) Destroy(shot.body.gameObject);
        if (shot.warningLine != null) Destroy(shot.warningLine.gameObject);
        if (shot.warningMark != null) Destroy(shot.warningMark.gameObject);
    }

    private void ClearShots()
    {
        foreach (Shot shot in shots) DestroyShot(shot);
        shots.Clear();
        foreach (SpecialAttack a in specialAttacks)
            if (a.root != null) Destroy(a.root.gameObject);
        specialAttacks.Clear();
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

    // Tiny matte circular bullet, generated locally; no additional PNG required.
    private void CreateShardSprite()
    {
        if (shardSprite != null) return;
        const int s = 64;
        shardTexture = new Texture2D(s, s, TextureFormat.RGBA32, false);
        shardTexture.filterMode = FilterMode.Bilinear;
        shardTexture.wrapMode = TextureWrapMode.Clamp;
        Color[] colors = new Color[s * s];
        Vector2 center = new Vector2(31.5f, 31.5f);
        const float radius = 28.5f;
        const float outlineWidth = 4.8f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(radius + 0.55f - distance);
                Color color = distance < radius - outlineWidth
                    ? bulletColor : bulletBorderColor;
                color.a *= alpha;
                colors[y * s + x] = color;
            }
        shardTexture.SetPixels(colors);
        shardTexture.Apply();
        shardSprite = Sprite.Create(shardTexture, new Rect(0, 0, s, s),
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
        if (shardSprite != null) Destroy(shardSprite);
        if (shardTexture != null) Destroy(shardTexture);
        if (attackRingSprite != null) Destroy(attackRingSprite);
        if (attackRingTexture != null) Destroy(attackRingTexture);
        if (temporaryChart != null) Destroy(temporaryChart);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showHitboxInEditor || pino == null) return;
        Gizmos.color = new Color(1f, 0.2f, 0.4f, 0.8f);
        Gizmos.DrawWireSphere(pino.TransformPoint(pino.rect.center), pinoHitRadius);
    }
#endif
}
