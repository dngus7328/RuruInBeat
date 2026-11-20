
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace LuluInBeat.MouseMode.ChartSystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public class MouseModeChartPlayer : MonoBehaviour
    {
        [Header("Mouse 모드 채보")]
        [SerializeField] private MouseModeChartAsset chartAsset;
        [SerializeField] private RectTransform noteLayer;

        [Header("노트 표시")]
        [SerializeField, Range(64f, 160f)]
        private float noteSize = 104f;

        [SerializeField]
        private Color leftColor =
            new Color(0.67f, 0.47f, 0.84f);

        [SerializeField]
        private Color rightColor =
            new Color(0.34f, 0.76f, 0.78f);

        [SerializeField]
        private Color wheelColor =
            new Color(0.94f, 0.70f, 0.32f);

        [Header("노트 시각 구분")]
        [Tooltip("기존 Inspector에 저장된 색 대신 L/R/W 기본 구분색을 사용합니다.")]
        [SerializeField] private bool useDistinctNotePalette = true;

        [Header("노트 크기와 읽기 속도")]
        [Tooltip("좁은 화면에서도 다음 타이밍 원을 읽을 수 있도록 노트 크기를 조절합니다.")]
        [SerializeField, Range(0.85f, 1.45f)]
        private float noteVisualScale = 1.0f;

        [Header("접근 원 이동 시간 (Inspector에서 조절)")]
        [Tooltip("모든 노트의 접근 원이 처음 나타나 판정 원까지 줄어드는 시간(초). 값이 클수록 천천히 줄어듭니다. 노트 판정 시각과 음악 속도는 바뀌지 않습니다.")]
        [SerializeField, Range(0.70f, 2.20f)]
        private float approachTravelSeconds = 1.40f;

        [Tooltip("휠 노트의 끝점에 END 표시")]
        [SerializeField]
        private bool showWheelEndMarker = true;

        [Header("접근 원 크기 (Inspector에서 조절)")]
        [Tooltip("설정한 시작 배율을 그대로 사용합니다. 코드는 원을 임의로 작게 제한하지 않습니다. 판정 시점에는 1배가 됩니다.")]
        [SerializeField, Range(1.0f, 4.0f)]
        private float approachStartScale = 1.8f;

        [SerializeField, Range(0.2f, 1f)]
        private float approachOpacity = 0.84f;

        [Header("휠 롱노트 - 두 줄 레일")]
        [SerializeField, Range(24f, 90f)]
        private float wheelRailGap = 50f;

        [SerializeField, Range(2f, 12f)]
        private float wheelRailThickness = 5f;

        [SerializeField, Range(0.2f, 1f)]
        private float wheelPathOpacity = 0.85f;

        [Header("시작 안내")]
        [SerializeField] private bool showCountdown = true;

        [SerializeField, Range(0f, 3f)]
        private float readySeconds = 0.6f;

        [SerializeField] private Font uiFont;

        [Header("노트 아래 판정")]
        [SerializeField, Range(0.1f, 2f)]
        private float feedbackDuration = 0.65f;

        [SerializeField, Range(14, 48)]
        private int feedbackFontSize = 25;

        [SerializeField, Range(0f, 100f)]
        private float feedbackBelowNoteGap = 18f;

        [Header("타격 확인: 노트 본체 확대")]
        [Tooltip("성공 판정 시 노트 본체가 잠깐 커져 입력 성공을 알려줍니다.")]
        [SerializeField] private bool showHitPop = true;

        [Tooltip("타격 순간 노트 본체 확대 배율. 1.16은 16% 확대입니다.")]
        [SerializeField, Range(1.05f, 1.30f)]
        private float hitPopScale = 1.16f;

        [Tooltip("노트 확대 효과 지속시간(초). 판정 시간에는 영향이 없습니다.")]
        [SerializeField, Range(0.08f, 0.30f)]
        private float hitPopDuration = 0.16f;

        [Tooltip("추가 시각 타격 강도입니다. 기존 확대 배율과 곱해 적용하며, 소리나 판정 시간은 변경하지 않습니다.")]
        [SerializeField, Range(1.00f, 1.15f)]
        private float hitVisualImpactMultiplier = 1.07f;

        [Tooltip("기존의 바깥쪽 타격 링도 함께 보일지 선택합니다. 기본값은 끔.")]
        [SerializeField] private bool showOldTapRing = false;

        [Header("휠 롱노트 종료")]
        [Tooltip("끝지점 직전 휠 입력을 유지 중이라고 인정할 시간")]
        [SerializeField, Range(0.02f, 0.30f)]
        private float wheelEndScrollBufferSeconds = 0.15f;

        [Header("휠 롱노트: 중간 입력 반응")]
        [Tooltip("롱노트 중간 휠 입력을 확인합니다. 끄면 기존 시작/끝 판정만 사용합니다.")]
        [SerializeField] private bool requireWheelMiddleInput = true;

        [Tooltip("롱노트 중간 입력을 서로 다른 구간으로 구분할 간격(초).")]
        [SerializeField, Range(0.35f, 0.90f)]
        private float wheelMiddleSegmentSeconds = 0.60f;

        [Tooltip("중간 휠 입력을 요구하는 구간의 최소 비율. EASY에서도 지나치게 엄격하지 않도록 기본값은 0.40.")]
        [SerializeField, Range(0.20f, 0.85f)]
        private float wheelMiddleRequiredRatio = 0.40f;

        [Tooltip("중간 입력이 부족하면 종료 판정을 GOOD 이하로 조절합니다. 강제 MISS는 사용하지 않습니다.")]
        [SerializeField] private bool softenWheelEndWhenUnderplayed = true;

        [Header("타격 연출: 코드 생성")]
        [Tooltip("이미지 에셋 없이 성공한 노트 주위에 짧은 무광 타격 원을 생성합니다.")]
        [SerializeField] private bool showCompactHitPulse = true;

        [Header("판정별 이펙트 (사운드 미포함)")]
        [Tooltip("PERFECT/GREAT/GOOD/MISS에 서로 다른 무광 UI 연출을 표시합니다. 기존 노트 이미지와 채보에는 영향을 주지 않습니다.")]
        [SerializeField] private bool useDistinctJudgementEffects = true;

        [Header("판정 시간 (초)")]
        [SerializeField] private float perfectWindow = 0.05f;
        [SerializeField] private float greatWindow = 0.09f;
        [SerializeField] private float goodWindow = 0.13f;
        [SerializeField] private float judgeOffsetSeconds = 0f;

        [Header("입력 정확도 / 렉 대응")]
        [Tooltip("노트가 없는 곳에서 L/R 연타 시 콤보를 끊고 점수를 차감합니다. 노트 위치 판정은 추가하지 않습니다.")]
        [SerializeField] private bool penalizeEmptyClicks = true;

        [Tooltip("빈 클릭 한 번당 일반 노트 점수 가중치의 차감량입니다. 0.35 = PERFECT 한 개 점수의 35%.")]
        [SerializeField, Range(0f, 1f)] private float emptyClickPenalty = 0.35f;

        // 정확한 노트 입력을 제한하거나 미리 MISS 처리하는 연타 잠금은 제거합니다.
        // 실제 노트를 맞히지 못한 빈 클릭의 콤보/점수 불이익만 적용합니다.

        [Tooltip("구형 입력 시스템 사용 시 클릭이 프레임 사이에 발생한 것으로 추정하여 지연을 보정합니다. 신형 Input System에서는 실제 입력 이벤트 시각을 사용합니다.")]
        [SerializeField] private bool compensateLegacyInputFrameDelay = true;

        [Tooltip("판정마다 Console 로그를 기록하면 저사양 기기에서 프레임 지연이 커질 수 있습니다.")]
        [SerializeField] private bool logEveryJudgement = false;

        public event Action<MouseModeChartAttack> AttackTriggered;
        public event Action<string, int, int> Judged;

        private class ActiveNote
        {
            public MouseModeChartNote data;

            public double hitTime;
            public double endTime;

            public RectTransform root;
            public CanvasGroup visualPriorityGroup;
            public RectTransform approach;
            public Image approachImage;
            // Fixed landing reference: the shrinking ring meets this rim at hit time.
            public RectTransform hitTarget;
            public Image hitTargetImage;
            public RectTransform rollingMark;
            public Image wheelInputRing;
            public float wheelInputFlash;
            public int wheelMiddleMask;
            public int wheelMiddleCount;

            public readonly List<GameObject> pathParts =
                new List<GameObject>();

            public readonly List<RailPiece> rails =
                new List<RailPiece>();

            public List<Vector2> uiPath;

            public float rollAngle;
            public bool created;
            public bool started;
            public bool finished;

            // 종료 휠 입력은 시작 판정과 무관하게 기록.
            public double lastEndScrollTime =
                double.NegativeInfinity;
        }

        private class RailPiece
        {
            public Image image;
            public float endProgress;
            public Color normalColor;
        }

        private class Feedback
        {
            public RectTransform rect;
            public Text text;
            public Color baseColor;
            public float elapsed;
            public Vector2 startPosition;
            public string result;
        }

        // 결과별 연출 파츠를 재사용하여 빠른 연타 중 UI 생성/파괴 부담을 줄입니다.
        private class JudgementVfxPart
        {
            public RectTransform rect;
            public Image image;
            public Vector2 origin;
            public Vector2 startSize;
            public Vector2 endSize;
            public Vector2 movement;
            public Color color;
            public float elapsed;
            public float duration;
        }

        private readonly List<JudgementVfxPart> judgementVfxs =
            new List<JudgementVfxPart>(48);
        private readonly Stack<RectTransform> judgementVfxPool =
            new Stack<RectTransform>(48);
        private RectTransform judgementVfxLayer;

        private class TapPulse
        {
            public RectTransform rect;
            public Image image;
            public Color color;
            public float elapsed;
            public float initialSize;
        }

        // 성공 판정 노트의 본체를 짧게 확대합니다. 일반 노트와 휠 종료는
        // 잔상 오브젝트를 별도로 만들고, 휠 시작은 이동 중인 본체를 키웠다 되돌립니다.
        private class HitPop
        {
            public RectTransform rect;
            public CanvasGroup group;
            public Vector3 baseScale;
            public float elapsed;
            public bool destroyAtEnd;
            public string result;
        }

        private readonly List<HitPop> hitPops = new List<HitPop>();

        private struct QueuedInput
        {
            public MouseModeNoteKind kind;
            public double dspTime;
            public float scrollDirection;
        }

        private readonly List<QueuedInput> queuedInputs = new List<QueuedInput>(16);
        private float currentScrollDirection = 1f;
        private float emptyClickPenaltyTotal;
        private int emptyClickCount;
        private bool visualOrderDirty;

#if ENABLE_INPUT_SYSTEM
        // InputAction 이벤트 시각(context.time)을 사용하면 Update가 늦게 실행되어도
        // 마우스 입력 시간을 프레임 끝 시각으로 잘못 취급하지 않습니다.
        private InputAction leftClickAction;
        private InputAction rightClickAction;
        private InputAction wheelScrollAction;
#endif

        private readonly List<ActiveNote> notes =
            new List<ActiveNote>();

        private readonly List<MouseModeChartAttack> attacks =
            new List<MouseModeChartAttack>();

        private readonly List<Feedback> feedbacks =
            new List<Feedback>();

        private readonly List<TapPulse> tapPulses =
            new List<TapPulse>();

        private int nextSpawn;
        private int nextAttack;
        private int combo;
        private int totalJudgeCount;
        private int judgedCount;
        private double lastJudgementTime;
        private bool songCompleted;

        public event Action SongCompleted;
        public int PerfectCount { get; private set; }
        public int GreatCount { get; private set; }
        public int GoodCount { get; private set; }
        public int MissCount { get; private set; }
        public bool IsChartPlaying { get { return running; } }
        public float SongElapsedSeconds
        {
            get { return (float)Math.Max(0d, AudioSettings.dspTime - startDsp); }
        }

        private float earnedScore;
        private double startDsp;
        // GO is tied to song time zero when earliest first hit is >= approach lead + 0.15.
        // 실제 노트 판정 시간과 음악의 상대적 동기화는 변경하지 않는다.
        private double openingLeadInSeconds;
        private const double FirstNoteVisibleAfterGoSeconds = 0.15d;

        private bool running;
        private bool chartPaused;
        private bool rescheduleAudioOnResume;

        private double pausedAtDsp;

        [Header("노트 타격음 연결")]
        [Tooltip("Systems의 NoteSoundManager를 연결합니다. 비워두면 씬에서 자동으로 찾습니다.")]
        [SerializeField] private NoteSoundManager noteSoundManager;

        private AudioSource songAudio;
        private Text countdownText;
        private Font runtimeFont;

        private Texture2D circleTexture;
        private Texture2D ringTexture;
        private Texture2D squareTexture;

        private Sprite circleSprite;
        private Sprite ringSprite;
        private Sprite squareSprite;

        public int Combo
        {
            get { return combo; }
        }

        public int EmptyClickCount { get { return emptyClickCount; } }
        public MouseModeChartAsset ActiveChartAsset { get { return chartAsset; } }

        public int Score
        {
            get
            {
                if (totalJudgeCount == 0)
                    return 0;

                return Mathf.Clamp(
                    Mathf.RoundToInt(
                        1000000f *
                        Mathf.Max(0f, earnedScore - emptyClickPenaltyTotal) /
                        totalJudgeCount
                    ),
                    0,
                    1000000
                );
            }
        }

        private void Awake()
        {
            // The selection comes from SongSelectScene. When the user starts
            // Mouse directly in the editor, keep its Inspector chart as-is.
            if (gameObject.scene.name == "Mouse" &&
                MouseModeSongSelection.CurrentChart != null)
                chartAsset = MouseModeSongSelection.CurrentChart;

            songAudio = GetComponent<AudioSource>();
            if (noteSoundManager == null)
                noteSoundManager = FindObjectOfType<NoteSoundManager>();
            songAudio.playOnAwake = false;
            songAudio.loop = false;

            runtimeFont = uiFont != null
                ? uiFont
                : Resources.GetBuiltinResource<Font>(
                    "Arial.ttf"
                );

            MakeSprites();
            PrewarmJudgementVfxPool();
        }

#if ENABLE_INPUT_SYSTEM
        private void OnEnable()
        {
            // Active Input Handling = Input System 또는 Both 일 때 사용됩니다.
            // 새 입력 시스템 패키지가 없는 프로젝트에서는 이 코드가 빌드에서 제외됩니다.
            leftClickAction = new InputAction("LuluLeftClick", InputActionType.Button,
                "<Mouse>/leftButton", "press(behavior=0)");
            rightClickAction = new InputAction("LuluRightClick", InputActionType.Button,
                "<Mouse>/rightButton", "press(behavior=0)");
            wheelScrollAction = new InputAction("LuluWheelScroll", InputActionType.PassThrough,
                "<Mouse>/scroll/y");

            leftClickAction.performed += OnLeftClickPerformed;
            rightClickAction.performed += OnRightClickPerformed;
            wheelScrollAction.performed += OnWheelScrollPerformed;

            leftClickAction.Enable();
            rightClickAction.Enable();
            wheelScrollAction.Enable();
        }

        private void OnDisable()
        {
            if (leftClickAction != null)
            {
                leftClickAction.performed -= OnLeftClickPerformed;
                leftClickAction.Disable();
                leftClickAction.Dispose();
                leftClickAction = null;
            }
            if (rightClickAction != null)
            {
                rightClickAction.performed -= OnRightClickPerformed;
                rightClickAction.Disable();
                rightClickAction.Dispose();
                rightClickAction = null;
            }
            if (wheelScrollAction != null)
            {
                wheelScrollAction.performed -= OnWheelScrollPerformed;
                wheelScrollAction.Disable();
                wheelScrollAction.Dispose();
                wheelScrollAction = null;
            }
            queuedInputs.Clear();
        }

        private void OnLeftClickPerformed(InputAction.CallbackContext context)
        {
            QueueInput(MouseModeNoteKind.Left, GetEventDspTime(context.time), 0f);
        }

        private void OnRightClickPerformed(InputAction.CallbackContext context)
        {
            QueueInput(MouseModeNoteKind.Right, GetEventDspTime(context.time), 0f);
        }

        private void OnWheelScrollPerformed(InputAction.CallbackContext context)
        {
            float scroll = context.ReadValue<float>();
            if (Mathf.Abs(scroll) > 0.01f)
                QueueInput(MouseModeNoteKind.Wheel, GetEventDspTime(context.time),
                    Mathf.Sign(scroll));
        }

        private static double GetEventDspTime(double eventTime)
        {
            // 두 시간원은 서로 원점이 다를 수 있습니다.
            // 시간 차이(입력 이벤트가 현재 시각보다 얼마나 앞섰는지)만 DSP에 적용합니다.
            double age = InputState.currentTime - eventTime;
            if (double.IsNaN(age) || double.IsInfinity(age) || age < 0d)
                age = 0d;
            return AudioSettings.dspTime - Math.Min(2.0d, age);
        }
#endif

        private void QueueInput(MouseModeNoteKind kind, double dspTime, float scrollDirection)
        {
            if (!running || chartPaused || !Application.isFocused ||
                Time.timeScale <= 0f || AudioListener.pause)
                return;

            queuedInputs.Add(new QueuedInput
            {
                kind = kind,
                dspTime = dspTime,
                scrollDirection = scrollDirection
            });
        }

        private void ProcessInputQueue()
        {
            // 큐를 시간 순서로 소모합니다. 입력이 누락된 프레임이 있더라도
            // 다음 Update에서 원래 이벤트 시간을 기준으로 판정합니다.
            for (int i = 0; i < queuedInputs.Count; i++)
            {
                QueuedInput input = queuedInputs[i];
                HandleRhythmInput(input.kind, input.dspTime - startDsp,
                    input.scrollDirection);
            }
            queuedInputs.Clear();
        }

        private void ProcessLegacyMouseInputs()
        {
#if !ENABLE_INPUT_SYSTEM
            // Legacy Input은 GetMouseButtonDown의 정확한 하드웨어 시각을 제공하지 않습니다.
            // 프레임 중앙 시각을 추정하되 최대 40ms까지만 보정합니다.
            double frameCorrection = compensateLegacyInputFrameDelay
                ? Math.Min(0.040d, Math.Max(0d, Time.unscaledDeltaTime) * 0.5d)
                : 0d;
            double inputSongSeconds = AudioSettings.dspTime - frameCorrection - startDsp;

            if (Input.GetMouseButtonDown(0))
                HandleRhythmInput(MouseModeNoteKind.Left, inputSongSeconds, 0f);
            if (Input.GetMouseButtonDown(1))
                HandleRhythmInput(MouseModeNoteKind.Right, inputSongSeconds, 0f);
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
                HandleRhythmInput(MouseModeNoteKind.Wheel, inputSongSeconds,
                    Mathf.Sign(scroll));
#endif
        }

        private void HandleRhythmInput(MouseModeNoteKind kind,
            double rawSongSeconds, float scrollDirection)
        {
            if (rawSongSeconds < 0d)
                return;

            double seconds = rawSongSeconds + judgeOffsetSeconds;
            if (kind == MouseModeNoteKind.Left || kind == MouseModeNoteKind.Right)
            {
                // 정박 입력은 언제나 시도합니다. 오입력 횟수로 일시 잠그거나
                // 판정 범위 밖의 입력으로 미래 노트를 미리 MISS 처리하지 않습니다.
                // 빈 클릭 자체의 점수/콤보 불이익은 기존대로 유지합니다.
                if (!Judge(kind, seconds))
                    RegisterEmptyClick();
                return;
            }

            currentScrollDirection = scrollDirection >= 0f ? 1f : -1f;
            if (!TryRegisterWheelEndInput(seconds) &&
                !Judge(MouseModeNoteKind.Wheel, seconds))
                TryRegisterWheelMiddleInput(seconds);
        }

        private void RegisterEmptyClick()
        {
            if (!penalizeEmptyClicks) return;
            emptyClickCount++;
            emptyClickPenaltyTotal += Mathf.Max(0f, emptyClickPenalty);
            combo = 0;
            // 기존 점수 UI 갱신 이벤트 유지. 빈 입력은 노트 오브젝트를
            // 만들어 판정하거나 채보 진행을 변경하지 않습니다.
            Judged?.Invoke("MISS", combo, Score);
        }

        private void Start()
        {
            StartChart();
        }

        public void StartChart()
        {
            StopChart();

            if (chartAsset == null ||
                chartAsset.data == null ||
                noteLayer == null)
            {
                Debug.LogError(
                    "Chart Asset과 Note Layer를 연결해 주세요.",
                    this
                );
                return;
            }

            MouseModeChartData chart = chartAsset.data;
            chart.EnsureLists();

            if (chart.bpm <= 0f ||
                chart.approachSeconds <= 0f)
            {
                Debug.LogError(
                    "BPM과 접근 시간은 0보다 커야 합니다.",
                    this
                );
                return;
            }

            Transform playArea = noteLayer.parent;

            if (playArea != null)
            {
                Transform pinoArea =
                    playArea.Find("PinoMoveArea");

                if (pinoArea != null &&
                    noteLayer.GetSiblingIndex() <
                    pinoArea.GetSiblingIndex())
                {
                    noteLayer.SetSiblingIndex(
                        pinoArea.GetSiblingIndex()
                    );
                }
            }

            totalJudgeCount = 0;
            judgedCount = 0;
            lastJudgementTime = 0d;
            songCompleted = false;
            PerfectCount = 0;
            GreatCount = 0;
            GoodCount = 0;
            MissCount = 0;

            foreach (MouseModeChartNote item in chart.notes)
            {
                if (item == null)
                    continue;

                if (item.kind == MouseModeNoteKind.Wheel &&
                    item.endBeat <= item.beat)
                {
                    Debug.LogWarning(
                        "잘못된 휠 노트 종료 박자: " +
                        item.id,
                        this
                    );
                    continue;
                }

                ActiveNote note = new ActiveNote
                {
                    data = item,

                    hitTime =
                        MouseModeChartTiming.BeatToSeconds(
                            chart,
                            item.beat
                        ),

                    endTime =
                        MouseModeChartTiming.BeatToSeconds(
                            chart,
                            item.kind == MouseModeNoteKind.Wheel
                                ? item.endBeat
                                : item.beat
                        )
                };

                notes.Add(note);
                lastJudgementTime = Math.Max(lastJudgementTime, note.endTime);
                totalJudgeCount +=
                    item.kind == MouseModeNoteKind.Wheel
                        ? 2
                        : 1;
            }

            notes.Sort(
                (a, b) => a.hitTime.CompareTo(b.hitTime)
            );

            Debug.Log("[Mouse Chart] CURRENT SONG ID=" + chart.songId +
                " / notes=" + notes.Count +
                " / firstHit=" + (notes.Count > 0 ? notes[0].hitTime.ToString("F3") : "NONE") +
                " / audio=" + (chartAsset.song != null ? chartAsset.song.name : "NONE"), this);

            ValidateVisualReadability(EffectiveNoteLead(chart));

            foreach (MouseModeChartAttack attack in chart.attacks)
            {
                if (attack != null)
                    attacks.Add(attack);
            }

            attacks.Sort(
                (a, b) => a.beat.CompareTo(b.beat)
            );

            nextSpawn = 0;
            nextAttack = 0;
            combo = 0;
            earnedScore = 0f;
            emptyClickPenaltyTotal = 0f;
            emptyClickCount = 0;
            queuedInputs.Clear();
            visualOrderDirty = true;

            chartPaused = false;
            rescheduleAudioOnResume = false;

            double earliestHit = notes.Count > 0
                ? notes[0].hitTime
                : 0d;

            // 기존 구현은 GO 이전 생성 금지 후 GO 순간에 모든 대기 노트를
            // while문으로 한꺼번에 만들어, 시작할 때 노트가 튀어나왔다.
            // GO를 음악 시작보다 조금 앞에 배치해 첫 노트부터 온전한 접근 시간을 준다.
            // 첫 타격이 늦은 채보에서는 불필요한 리드인을 추가하지 않는다.
            openingLeadInSeconds = notes.Count > 0
                ? Math.Max(0d,
                    EffectiveNoteLead(chart) - earliestHit +
                    FirstNoteVisibleAfterGoSeconds)
                : 0d;

            double preRoll = 3.0d + readySeconds;

            // startDsp는 실제 음악 시작 시각이다.
            // GO는 이보다 openingLeadInSeconds만큼 먼저 표시된다.
            startDsp = AudioSettings.dspTime + preRoll + openingLeadInSeconds;

            songAudio.clip = chartAsset.song;

            if (chartAsset.song != null)
            {
                songAudio.PlayScheduled(startDsp);
            }
            else
            {
                Debug.LogWarning(
                    "음악 없이 채보를 테스트합니다.",
                    this
                );
            }

            if (showCountdown)
                CreateCountdownText();

            running = true;
        }

        public void StopChart()
        {
            running = false;
            chartPaused = false;
            rescheduleAudioOnResume = false;
            queuedInputs.Clear();
            visualOrderDirty = true;

            if (songAudio != null)
                songAudio.Stop();

            foreach (ActiveNote note in notes)
                DestroyVisual(note);

            notes.Clear();
            attacks.Clear();

            foreach (Feedback feedback in feedbacks)
            {
                if (feedback.rect != null)
                    Destroy(feedback.rect.gameObject);
            }

            feedbacks.Clear();

            foreach (TapPulse pulse in tapPulses)
                if (pulse.rect != null) Destroy(pulse.rect.gameObject);
            tapPulses.Clear();

            for (int i = 0; i < judgementVfxs.Count; i++)
                ReturnJudgementVfxRect(judgementVfxs[i].rect);
            judgementVfxs.Clear();

            // 리스타트 시 이전 타격 잔상이 남지 않게 정리합니다.
            foreach (HitPop pop in hitPops)
            {
                if (pop.rect == null) continue;
                if (pop.destroyAtEnd)
                    Destroy(pop.rect.gameObject);
                else
                    pop.rect.localScale = pop.baseScale;
            }
            hitPops.Clear();

            if (countdownText != null)
                Destroy(countdownText.gameObject);

            countdownText = null;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && running)
                EnterChartPause();
        }

        private void EnterChartPause()
        {
            if (!running || chartPaused)
                return;

            chartPaused = true;
            queuedInputs.Clear();
            pausedAtDsp = AudioSettings.dspTime;

            rescheduleAudioOnResume =
                pausedAtDsp < startDsp;

            if (songAudio != null &&
                songAudio.clip != null)
            {
                if (rescheduleAudioOnResume)
                    songAudio.Stop();
                else
                    songAudio.Pause();
            }
        }

        private void ExitChartPause()
        {
            if (!running || !chartPaused)
                return;

            double duration = Math.Max(
                0d,
                AudioSettings.dspTime - pausedAtDsp
            );

            startDsp += duration;

            if (songAudio != null &&
                songAudio.clip != null)
            {
                if (rescheduleAudioOnResume)
                    songAudio.PlayScheduled(startDsp);
                else
                    songAudio.UnPause();
            }

            chartPaused = false;
            rescheduleAudioOnResume = false;
            queuedInputs.Clear();
        }

        private void Update()
        {
            if (!running ||
                chartAsset == null ||
                chartAsset.data == null)
            {
                return;
            }

            bool pauseRequested =
                !Application.isFocused ||
                Time.timeScale <= 0f ||
                AudioListener.pause;

            if (pauseRequested)
            {
                EnterChartPause();
                return;
            }

            if (chartPaused)
                ExitChartPause();

            MouseModeChartData chart =
                chartAsset.data;

            double songSeconds =
                AudioSettings.dspTime - startDsp;

            double inputSeconds =
                songSeconds + judgeOffsetSeconds;

            // 화면의 GO는 음악 재생보다 짧게 앞선다.
            // 이 시간부터 노트 접근 표시를 시작하되, 음악·노트 판정의 기준은
            // 실제 startDsp(음악 시작)로 유지해 박자 오차가 생기지 않게 한다.
            double countdownSeconds = songSeconds + openingLeadInSeconds;
            UpdateCountdown(countdownSeconds);
            UpdateFeedbacks(Time.unscaledDeltaTime);
            UpdateTapPulses(Time.unscaledDeltaTime);
            UpdateDistinctJudgementEffects(Time.unscaledDeltaTime);
            UpdateHitPops(Time.unscaledDeltaTime);

            // GO 이전에는 노트를 생성하지 않는다.
            // 하지만 GO 이후 음악이 아직 시작되지 않은 준비 시간에는
            // 정상적인 타임라인에 따라 하나씩 나타나도록 허용한다.
            if (countdownSeconds < 0d)
                return;

            // 노트 생성.
            while (
                nextSpawn < notes.Count &&
                notes[nextSpawn].hitTime -
                EffectiveNoteLead(chart) <= songSeconds
            )
            {
                CreateVisual(notes[nextSpawn]);
                nextSpawn++;
            }

            // 탄막 이벤트.
            while (
                nextAttack < attacks.Count &&
                MouseModeChartTiming.BeatToSeconds(
                    chart,
                    attacks[nextAttack].beat
                ) <= songSeconds
            )
            {
                AttackTriggered?.Invoke(
                    attacks[nextAttack]
                );

                nextAttack++;
            }

            // 입력 처리. 새 Input System: 이벤트 시각 / 구형 Input: 프레임 지연 추정.
            // 게임 시작 전 입력은 HandleRhythmInput에서 무시합니다.
#if ENABLE_INPUT_SYSTEM
            ProcessInputQueue();
#else
            ProcessLegacyMouseInputs();
#endif

            // Cytus의 공통 타이밍 가이드 원리를 참고한 시각 우선순위.
            // 개별 노트마다 큰 원을 모두 표시하지 않고, 다음 판정 노트
            // (동시치기는 같은 시각의 노트들)만 원을 강조한다.
            // 실제 판정, 노트 생성 시각, 좌우 입력 규칙은 바꾸지 않는다.
            double nextVisibleHit = double.PositiveInfinity;
            foreach (ActiveNote candidate in notes)
            {
                if (!candidate.finished && candidate.created && !candidate.started &&
                    candidate.hitTime < nextVisibleHit)
                    nextVisibleHit = candidate.hitTime;
            }

            foreach (ActiveNote note in notes)
            {
                if (note.finished || !note.created)
                    continue;

                if (
                    note.data.kind ==
                    MouseModeNoteKind.Wheel &&
                    note.started
                )
                {
                    // 시작을 놓쳤더라도 공은 계속 이동.
                    float progress = Mathf.Clamp01(
                        (float)(
                            (songSeconds - note.hitTime) /
                            Math.Max(
                                0.001d,
                                note.endTime - note.hitTime
                            )
                        )
                    );

                    UpdateWheelMovement(
                        note,
                        progress
                    );

                    // 끝 근처까지 휠을 돌렸으면
                    // 시작 판정과 무관하게 종료 PERFECT.
                    if (
                        inputSeconds >= note.endTime &&
                        note.lastEndScrollTime <= note.endTime &&
                        note.endTime - note.lastEndScrollTime
                            <= wheelEndScrollBufferSeconds
                    )
                    {
                        string wheelEndResult = GetWheelEndResult(note, "PERFECT");
                        FinishNote(
                            note,
                            wheelEndResult,
                            GetJudgeScore(wheelEndResult),
                            true
                        );
                    }
                    else if (
                        inputSeconds >
                        note.endTime + goodWindow
                    )
                    {
                        FinishNote(
                            note,
                            "MISS",
                            0f,
                            true
                        );
                    }
                }
                else
                {
                    UpdateApproachRing(
                        note,
                        songSeconds,
                        EffectiveNoteLead(chart),
                        note.hitTime - nextVisibleHit <= 0.075d
                    );

                    if (
                        inputSeconds >
                        note.hitTime + goodWindow
                    )
                    {
                        if (
                            note.data.kind ==
                            MouseModeNoteKind.Wheel
                        )
                        {
                            StartWheel(
                                note,
                                "MISS",
                                0f
                            );
                        }
                        else
                        {
                            FinishNote(
                                note,
                                "MISS",
                                0f,
                                true
                            );
                        }
                    }
                }
            }

            if (visualOrderDirty)
            {
                RefreshVisibleNoteOrder();
                visualOrderDirty = false;
            }

            if (
                countdownText != null &&
                countdownText.gameObject.activeSelf
            )
            {
                countdownText.transform.SetAsLastSibling();
            }

            // Finish only after every note / wheel-end judgement is settled
            // AND the authored audio tail has played. No mid-song result popups.
            double audioDuration = chartAsset.song != null
                ? chartAsset.song.length
                : 0d;
            double finishAt = Math.Max(audioDuration,
                lastJudgementTime + goodWindow);
            if (!songCompleted && totalJudgeCount > 0 &&
                judgedCount >= totalJudgeCount &&
                songSeconds >= finishAt)
            {
                songCompleted = true;
                running = false;
                SongCompleted?.Invoke();
            }
        }

        /// <summary>
        /// Checks the actual on-screen note size and the Inspector's approach
        /// duration, not a small assumed chart-preview diameter. This is a
        /// diagnostic only; never silently move musically authored notes.
        /// </summary>
        private void ValidateVisualReadability(float lookAheadSeconds)
        {
            if (noteLayer == null || notes.Count <= 1)
                return;

            int bodyOverlaps = 0;
            int approachInterferences = 0;
            string bodyExamples = "";
            float diameter = GetVisualNoteSize();
            float approachDiameter = diameter * 1.04f * GetReadableApproachScale();

            for (int i = 0; i < notes.Count; i++)
            {
                ActiveNote a = notes[i];
                if (a.data.kind == MouseModeNoteKind.Wheel) continue;
                Vector2 posA = ToUI(a.data.position);

                for (int j = i + 1; j < notes.Count; j++)
                {
                    ActiveNote b = notes[j];
                    if (b.hitTime - a.hitTime > lookAheadSeconds) break;
                    if (b.data.kind == MouseModeNoteKind.Wheel) continue;

                    float distance = Vector2.Distance(posA, ToUI(b.data.position));
                    if (distance < diameter)
                    {
                        bodyOverlaps++;
                        if (bodyOverlaps <= 6)
                            bodyExamples += " [" + a.data.id + "/" + b.data.id + "]";
                    }
                    // An approaching future note may hide a current note
                    // even when their solid note bodies don't touch.
                    else if (distance < (diameter + approachDiameter) * 0.5f)
                    {
                        approachInterferences++;
                    }
                }
            }

            if (bodyOverlaps > 0 || approachInterferences > 0)
            {
                Debug.LogWarning(
                    "[Mouse Chart/가독성] 실제 NoteLayer 기준: 노트 본체 겹침 " +
                    bodyOverlaps + "쌍" + bodyExamples +
                    ", 접근 원이 다른 노트에 닿을 수 있는 쌍 " +
                    approachInterferences + "쌍. 노트 지름=" +
                    diameter.ToString("0.#") + "px / 접근 시간=" +
                    lookAheadSeconds.ToString("0.##") +
                    "초. 채보 배치와 접근 원 배율을 점검하세요.", this
                );
            }
        }

        /// <summary>
        /// 늦게 생성된 노트가 먼저 쳐야 하는 노트를 가리지 않도록 표시 순서를 정리한다.
        /// 이미 시작한 휠은 일반 노트 뒤에 두고, 다음 판정 노트가 가장 위에 온다.
        /// </summary>
        private void RefreshVisibleNoteOrder()
        {
            // 시작한 휠의 본체는 새로 판정할 노트보다 뒤.
            foreach (ActiveNote note in notes)
            {
                if (note.started && !note.finished && note.root != null)
                    note.root.SetAsLastSibling();
            }

            // notes는 hitTime 오름차순이므로 역순으로 올리면 앞선 노트가 맨 위.
            for (int i = notes.Count - 1; i >= 0; i--)
            {
                ActiveNote note = notes[i];
                if (!note.started && !note.finished && note.root != null)
                    note.root.SetAsLastSibling();
            }

            // 피드백은 노트보다 위에서 읽혀야 한다.
            foreach (HitPop pop in hitPops)
            {
                if (pop.destroyAtEnd && pop.rect != null)
                    pop.rect.SetAsLastSibling();
            }

            if (judgementVfxLayer != null)
                judgementVfxLayer.SetAsLastSibling();

            foreach (Feedback feedback in feedbacks)
            {
                if (feedback.rect != null)
                    feedback.rect.SetAsLastSibling();
            }
        }

        // 롱노트 종료 입력은 시작과 독립 판정.
        private bool TryRegisterWheelEndInput(
            double seconds
        )
        {
            ActiveNote best = null;
            double closest = double.MaxValue;

            foreach (ActiveNote note in notes)
            {
                if (
                    note.finished ||
                    !note.created ||
                    !note.started ||
                    note.data.kind != MouseModeNoteKind.Wheel
                )
                {
                    continue;
                }

                // 시작 휠 틱을 종료 휠 틱으로 중복 사용하지 않음.
                if (
                    seconds <=
                    note.hitTime + goodWindow
                )
                {
                    continue;
                }

                double untilEnd =
                    note.endTime - seconds;

                if (
                    untilEnd > wheelEndScrollBufferSeconds ||
                    untilEnd < -goodWindow
                )
                {
                    continue;
                }

                double distance =
                    Math.Abs(untilEnd);

                if (distance < closest)
                {
                    closest = distance;
                    best = note;
                }
            }

            if (best == null)
                return false;

            best.lastEndScrollTime = seconds;
            FlashWheelInput(best);

            if (seconds >= best.endTime)
            {
                // 끝지점에서 입력한 경우 실제 오차로 판정.
                string result = GetJudgeResult(
                    Math.Abs(
                        seconds - best.endTime
                    )
                );

                result = GetWheelEndResult(best, result);
                UpdateWheelMovement(best, 1f);

                FinishNote(
                    best,
                    result,
                    GetJudgeScore(result),
                    true
                );
            }

            // 끝지점 이전 입력은 기억해 두었다가
            // 실제 끝지점에 도착했을 때 판정.
            return true;
        }

        // Discrete wheel ticks are tracked against distinct musical-time
        // windows, so a single scroll burst cannot fill the entire long note.
        // Start and end timing are unchanged; no extra combo/score events occur.
        private bool TryRegisterWheelMiddleInput(double seconds)
        {
            ActiveNote target = null;
            double nearestEnd = double.PositiveInfinity;
            foreach (ActiveNote note in notes)
            {
                if (note.finished || !note.created || !note.started ||
                    note.data.kind != MouseModeNoteKind.Wheel)
                    continue;

                if (seconds <= note.hitTime + goodWindow ||
                    seconds >= note.endTime - wheelEndScrollBufferSeconds)
                    continue;

                if (note.endTime < nearestEnd)
                {
                    nearestEnd = note.endTime;
                    target = note;
                }
            }

            if (target == null) return false;

            int count = GetWheelMiddleSegmentCount(target);
            double elapsed = seconds - target.hitTime;
            double duration = Math.Max(0.001d, target.endTime - target.hitTime);
            int index = Mathf.Clamp((int)(elapsed / duration * count), 0, count - 1);
            int bit = 1 << index;
            if ((target.wheelMiddleMask & bit) == 0)
            {
                target.wheelMiddleMask |= bit;
                target.wheelMiddleCount++;
                // 한 구간당 첫 정상 휠 입력에만 유지 틱을 재생합니다.
                if (noteSoundManager != null)
                    noteSoundManager.PlayLongHoldTick();
            }
            FlashWheelInput(target);
            return true;
        }

        private int GetWheelMiddleSegmentCount(ActiveNote note)
        {
            double duration = Math.Max(0d, note.endTime - note.hitTime);
            return Mathf.Clamp(
                Mathf.FloorToInt((float)Math.Max(0d, duration - 0.25d) /
                    Mathf.Max(0.35f, wheelMiddleSegmentSeconds)), 1, 16);
        }

        private string GetWheelEndResult(ActiveNote note, string timeResult)
        {
            if (timeResult == "MISS" || !requireWheelMiddleInput ||
                !softenWheelEndWhenUnderplayed ||
                note.endTime - note.hitTime < 0.95d)
                return timeResult;

            int segments = GetWheelMiddleSegmentCount(note);
            int required = Mathf.Max(1,
                Mathf.CeilToInt(segments * Mathf.Clamp01(wheelMiddleRequiredRatio)));
            if (note.wheelMiddleCount >= required) return timeResult;

            // Forgiving for EASY: inadequate continuous scroll lowers the
            // end judgement instead of invalidating the full long note.
            return "GOOD";
        }

        private void FlashWheelInput(ActiveNote note)
        {
            if (note == null) return;
            note.rollAngle += currentScrollDirection * 36f;
            note.wheelInputFlash = 0.18f;
            if (note.wheelInputRing != null)
            {
                Color c = NoteColor(MouseModeNoteKind.Wheel);
                c.a = 0.84f;
                note.wheelInputRing.color = c;
            }
        }

        private string GetJudgeResult(
            double distance
        )
        {
            if (distance <= perfectWindow)
                return "PERFECT";

            if (distance <= greatWindow)
                return "GREAT";

            if (distance <= goodWindow)
                return "GOOD";

            return "MISS";
        }

        private static float GetJudgeScore(
            string result
        )
        {
            switch (result)
            {
                case "PERFECT":
                    return 1f;

                case "GREAT":
                    return 0.8f;

                case "GOOD":
                    return 0.5f;

                default:
                    return 0f;
            }
        }

        private bool Judge(MouseModeNoteKind kind, double seconds)
        {
            ActiveNote best = null;
            double closest = Math.Max(0.001f, goodWindow);

            // 판정선 안에서 가장 가까운 동일 종류 노트를 찾습니다.
            // 초과 입력은 유효 판정이 아니며, 다음 노트를 선점해 MISS 처리하지 않습니다.
            foreach (ActiveNote note in notes)
            {
                if (note.finished || note.started || !note.created ||
                    note.data.kind != kind)
                    continue;

                double distance = Math.Abs(note.hitTime - seconds);
                if (distance <= closest)
                {
                    closest = distance;
                    best = note;
                }
            }

            if (best == null)
                return false;

            string result = GetJudgeResult(closest);
            float score = GetJudgeScore(result);
            if (kind == MouseModeNoteKind.Wheel)
                StartWheel(best, result, score);
            else
                FinishNote(best, result, score, true);
            return true;
        }

        private void StartWheel(
            ActiveNote note,
            string result,
            float score
        )
        {
            if (note.started || note.finished)
                return;

            note.started = true;
            visualOrderDirty = true;
            note.wheelMiddleMask = 0;
            note.wheelMiddleCount = 0;
            // A missed wheel start is advanced by the clock with no input;
            // it must not pretend that the player scrolled.
            if (result != "MISS")
                FlashWheelInput(note);

            if (note.approach != null)
                note.approach.gameObject.SetActive(false);
            if (note.hitTarget != null)
                note.hitTarget.gameObject.SetActive(false);

            RegisterJudgement(
                note,
                result,
                score,
                true
            );
        }

        private void FinishNote(
            ActiveNote note,
            string result,
            float score,
            bool showFeedback
        )
        {
            if (note.finished)
                return;

            note.finished = true;
            visualOrderDirty = true;

            RegisterJudgement(
                note,
                result,
                score,
                showFeedback
            );

            DestroyVisual(note);
        }

        private void RegisterJudgement(
            ActiveNote note,
            string result,
            float score,
            bool showFeedback
        )
        {
            if (showFeedback)
                ShowFeedback(note, result);

            if (score > 0f && showHitPop)
                CreateHitPop(note, result);

            if (showFeedback && useDistinctJudgementEffects)
            {
                // MISS는 노트가 사라지는 방향으로 약하게 소멸합니다.
                // 휠 시작/끝, 클릭 노트는 동일한 판정 종류를 공유합니다.
                CreateDistinctJudgementEffect(note, result);
            }
            else if (score > 0f && (showCompactHitPulse || showOldTapRing))
            {
                CreateTapPulse(note, result);
            }

            // 노트 판정이 확정된 시점에 실제 게임 사운드를 재생합니다.
            // 휠 노트는 시작(StartWheel)과 끝(FinishNote)을 구분합니다.
            PlayNoteJudgementSound(note, result);

            earnedScore += score;
            judgedCount++;
            switch (result)
            {
                case "PERFECT": PerfectCount++; break;
                case "GREAT": GreatCount++; break;
                case "GOOD": GoodCount++; break;
                default: MissCount++; break;
            }

            combo = score > 0f
                ? combo + 1
                : 0;

            if (logEveryJudgement)
            {
                Debug.Log("[Mouse Chart] " + result + " / " + combo +
                    " combo / " + Score, this);
            }
            visualOrderDirty = true;

            Judged?.Invoke(
                result,
                combo,
                Score
            );
        }

        private void PlayNoteJudgementSound(ActiveNote note, string result)
        {
            // Runtime sound manager is spawned after scene Awake().
            // Resolve it lazily at first judgement without touching UI.
            if (noteSoundManager == null)
                noteSoundManager = FindObjectOfType<NoteSoundManager>();
            if (noteSoundManager == null || note == null || note.data == null)
                return;

            NoteSoundManager.NoteJudgement judgement;
            switch (result)
            {
                case "PERFECT": judgement = NoteSoundManager.NoteJudgement.Perfect; break;
                case "GREAT": judgement = NoteSoundManager.NoteJudgement.Great; break;
                case "GOOD": judgement = NoteSoundManager.NoteJudgement.Good; break;
                default: judgement = NoteSoundManager.NoteJudgement.Miss; break;
            }

            NoteSoundManager.NoteSoundType soundType =
                NoteSoundManager.NoteSoundType.Normal;
            if (note.data.kind == MouseModeNoteKind.Wheel)
            {
                soundType = note.finished
                    ? NoteSoundManager.NoteSoundType.LongRelease
                    : NoteSoundManager.NoteSoundType.LongStart;
            }
            noteSoundManager.PlayJudgement(soundType, judgement);
        }

        private void CreateVisual(
            ActiveNote note
        )
        {
            float visualSize = GetVisualNoteSize();

            GameObject obj = new GameObject(
                "MouseNote_" + note.data.id,
                typeof(RectTransform)
            );

            RectTransform root =
                obj.GetComponent<RectTransform>();

            root.SetParent(noteLayer, false);

            root.anchorMin =
                root.anchorMax =
                root.pivot =
                    new Vector2(0.5f, 0.5f);

            root.sizeDelta =
                new Vector2(visualSize, visualSize);

            if (
                note.data.kind ==
                MouseModeNoteKind.Wheel
            )
            {
                note.uiPath =
                    BuildWheelUIPath(note.data);

                root.anchoredPosition =
                    note.uiPath[0];

                DrawWheelRails(note);
            }
            else
            {
                root.anchoredPosition =
                    ToUI(note.data.position);
            }

            root.SetAsLastSibling();

            note.root = root;
            note.visualPriorityGroup = obj.AddComponent<CanvasGroup>();
            note.visualPriorityGroup.interactable = false;
            note.visualPriorityGroup.blocksRaycasts = false;
            note.created = true;
            visualOrderDirty = true;

            Color color =
                NoteColor(note.data.kind);

            Color dark =
                new Color(0.10f, 0.10f, 0.16f, 1f);

            Color inner =
                new Color(0.17f, 0.17f, 0.23f, 1f);

            Color approachColor = color;
            approachColor.a =
                Mathf.Min(approachOpacity, 0.50f) * 0.60f;

            note.approach = AddImage(
                root,
                "ApproachRing",
                ringSprite,
                approachColor,
                visualSize * 1.04f
            );

            note.approach.SetAsFirstSibling();

            note.approachImage =
                note.approach.GetComponent<Image>();

            note.approach.localScale =
                Vector3.one * GetReadableApproachScale();

            AddImage(
                root,
                "DarkOutline",
                circleSprite,
                dark,
                visualSize
            );

            // A fixed, thin landing rim makes the exact visual hit moment
            // obvious without changing the approach scale chosen in Inspector.
            note.hitTarget = AddImage(
                root,
                "HitTargetRing",
                ringSprite,
                new Color(0.97f, 0.96f, 1f, 0f),
                visualSize * 1.04f
            );
            note.hitTargetImage = note.hitTarget.GetComponent<Image>();

            AddImage(
                root,
                "ColorBody",
                circleSprite,
                color,
                visualSize * 0.90f
            );

            AddImage(
                root,
                "InnerCircle",
                circleSprite,
                inner,
                visualSize * 0.66f
            );


            // Color is secondary: the marking remains readable in grayscale.
            CreateNoteTypeMarks(root, note.data.kind, visualSize);

            if (runtimeFont != null)
            {
                string labelName =
                    note.data.kind == MouseModeNoteKind.Left
                        ? "LeftLabel"
                        : note.data.kind == MouseModeNoteKind.Right
                            ? "RightLabel"
                            : "WheelLabel";

                Text label = CreateText(
                    labelName,
                    Mathf.RoundToInt(
                        visualSize * 0.38f
                    ),
                    root
                );

                label.text =
                    note.data.kind == MouseModeNoteKind.Left
                        ? "L"
                        : note.data.kind == MouseModeNoteKind.Right
                            ? "R"
                            : "W";

                label.color =
                    new Color(0.98f, 0.98f, 1f, 1f);

                label.rectTransform.sizeDelta =
                    new Vector2(
                        visualSize,
                        visualSize
                    );

                label.rectTransform.anchoredPosition =
                    Vector2.zero;
            }

            if (
                note.data.kind ==
                MouseModeNoteKind.Wheel
            )
            {
                RectTransform inputRing = AddImage(
                    root, "WheelInputResponse", ringSprite,
                    new Color(0.94f, 0.70f, 0.32f, 0f),
                    visualSize * 1.05f
                );
                note.wheelInputRing = inputRing.GetComponent<Image>();
                inputRing.SetAsLastSibling();
                note.rollingMark = AddImage(
                    root,
                    "RollingSpot",
                    circleSprite,
                    new Color(1f, 1f, 1f, 0.95f),
                    visualSize * 0.115f
                );

                note.rollingMark.anchoredPosition =
                    new Vector2(0f, visualSize * 0.32f);
            }
        }

        // No extra fonts or art assets are required. These marks are part of the note itself.
        private void CreateNoteTypeMarks(RectTransform root, MouseModeNoteKind kind, float size)
        {
            Color ink = new Color(0.98f, 0.98f, 0.99f, 0.96f);
            if (kind == MouseModeNoteKind.Left)
            {
                // L: two short bars on the LEFT edge, in addition to the L label.
                for (int i = 0; i < 2; i++)
                {
                    RectTransform bar = AddImage(root, "LeftDoubleMark_" + i,
                        squareSprite, ink, 1f);
                    bar.sizeDelta = new Vector2(size * 0.125f, size * 0.038f);
                    bar.anchoredPosition = new Vector2(-size * 0.32f,
                        (i == 0 ? -1f : 1f) * size * 0.075f);
                }
            }
            else if (kind == MouseModeNoteKind.Right)
            {
                // R: the exact same two white horizontal bars as L,
                // reflected across the note's vertical centerline.
                for (int i = 0; i < 2; i++)
                {
                    RectTransform bar = AddImage(root, "RightDoubleMark_" + i,
                        squareSprite, ink, 1f);
                    bar.sizeDelta = new Vector2(size * 0.125f, size * 0.038f);
                    bar.anchoredPosition = new Vector2(size * 0.32f,
                        (i == 0 ? -1f : 1f) * size * 0.075f);
                }
            }
            else
            {
                // W: a small circular dial with two ticks, distinct from tap notes.
                RectTransform dial = AddImage(root, "WheelDialMark", ringSprite, ink,
                    size * 0.18f);
                dial.anchoredPosition = new Vector2(0f, size * 0.32f);
                for (int i = 0; i < 2; i++)
                {
                    RectTransform tick = AddImage(root, "WheelDialTick_" + i,
                        squareSprite, ink, 1f);
                    tick.sizeDelta = new Vector2(size * 0.062f, size * 0.028f);
                    tick.anchoredPosition = new Vector2(
                        (i == 0 ? -1f : 1f) * size * 0.105f, size * 0.32f);
                }
            }
        }

        /// <summary>
        /// A timing guide, never a location requirement. Returns the initial
        /// radius scale at spawn and reaches 1 exactly at the authored hit time.
        /// </summary>
        public static float ApproachScaleAt(double hitTime, double now,
            float leadSeconds, float initialScale)
        {
            float remaining = Mathf.Clamp01(
                (float)((hitTime - now) / Math.Max(0.01f, leadSeconds)));
            return Mathf.Lerp(1f, Mathf.Max(1f, initialScale), remaining);
        }

        // The fixed target stays faint for upcoming notes; the next
        // judgement becomes clearly readable as its hit time approaches.
        // Pure helper so visual timing can be tested without pressing a key.
        public static float HitGuideOpacityAt(double hitTime, double now,
            float leadSeconds, bool isCurrentTarget)
        {
            if (!isCurrentTarget) return 0.12f;
            float remaining = Mathf.Clamp01(
                (float)((hitTime - now) / Math.Max(.01f, leadSeconds)));
            return Mathf.Lerp(.34f, .96f, 1f - remaining);
        }

        private void UpdateApproachRing(
            ActiveNote note,
            double songSeconds,
            float approachSeconds,
            bool isCurrentTarget
        )
        {
            if (note.approach == null)
                return;

            // 표시부터 판정까지 모든 노트에 같은 시간/선형 속도를 적용.
            // 다음 노트만 원을 보이게 하면, 이전 노트를 치는 순간 후속 원이
            // 축소된 상태로 갑자기 나타나 속도가 제각각인 것처럼 느껴진다.
            float remaining = Mathf.Clamp01(
                (float)((note.hitTime - songSeconds) /
                Math.Max(0.01f, approachSeconds))
            );

            // All notes use their own hit timestamp. A ring still reaches
            // exactly the note's diameter at its judgement moment.
            float scale = ApproachScaleAt(
                note.hitTime, songSeconds,
                approachSeconds, GetReadableApproachScale());

            note.approach.gameObject.SetActive(true);
            note.approach.localScale = Vector3.one * scale;

            // 후속 노트의 원도 처음부터 움직이되 연하게 보여 과밀을 줄인다.
            // 현재 판정 대상이 될 때는 크기를 새로 계산하지 않고 밝기만 강조한다.
            if (note.visualPriorityGroup != null)
            {
                note.visualPriorityGroup.alpha = isCurrentTarget
                    ? 1f : Mathf.Lerp(0.72f, 0.92f, 1f - remaining);
            }

            if (note.approachImage != null)
            {
                Color color = NoteColor(note.data.kind);
                // A future cue is visible from its first frame, and grows
                // brighter as the judgement nears instead of popping in.
                float visibility = isCurrentTarget
                    ? Mathf.Lerp(0.88f, 1f, 1f - remaining)
                    : Mathf.Lerp(0.55f, 0.84f, 1f - remaining);

                // On the final approach, add neutral contrast rather than a
                // baked glow or an artificial change to the judgement clock.
                float secondsUntilHit = (float)(note.hitTime - songSeconds);
                float finishing = isCurrentTarget
                    ? Mathf.Clamp01((0.20f - secondsUntilHit) / 0.20f)
                    : 0f;
                color = Color.Lerp(color,
                    new Color(0.97f, 0.96f, 1f, 1f), finishing * 0.48f);
                color.a = Mathf.Clamp01(approachOpacity) * visibility;
                note.approachImage.color = color;
            }
            if (note.hitTargetImage != null)
            {
                Color targetColor = new Color(0.97f, 0.96f, 1f, 1f);
                targetColor.a = HitGuideOpacityAt(
                    note.hitTime, songSeconds, approachSeconds, isCurrentTarget);
                note.hitTargetImage.color = targetColor;
            }
        }

        private List<Vector2> BuildWheelUIPath(
            MouseModeChartNote data
        )
        {
            List<Vector2> result =
                new List<Vector2>();

            if (
                data.path != null &&
                data.path.Count > 0
            )
            {
                foreach (Vector2 point in data.path)
                    result.Add(ToUI(point));
            }

            if (result.Count == 0)
                result.Add(ToUI(data.position));

            // Use the SAME gently rounded path for the head and the two rails.
            // No JSON data or note timing is modified.
            return SmoothWheelUIPath(result);
        }

        // Open Chaikin curve: preserves the exact start and end positions,
        // avoiding the long miter spikes and triangular corners of the old rail.
        private static List<Vector2> SmoothWheelUIPath(List<Vector2> source)
        {
            if (source == null || source.Count < 2)
                return source;

            List<Vector2> points = new List<Vector2>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                if (points.Count == 0 ||
                    Vector2.Distance(points[points.Count - 1], source[i]) > 0.5f)
                    points.Add(source[i]);
            }

            if (points.Count < 3)
                return points;

            for (int pass = 0; pass < 2; pass++)
            {
                List<Vector2> rounded = new List<Vector2>(points.Count * 2);
                rounded.Add(points[0]);
                for (int i = 0; i < points.Count - 1; i++)
                {
                    Vector2 a = points[i];
                    Vector2 b = points[i + 1];
                    rounded.Add(Vector2.Lerp(a, b, 0.25f));
                    rounded.Add(Vector2.Lerp(a, b, 0.75f));
                }
                rounded.Add(points[points.Count - 1]);
                points = rounded;
            }
            return points;
        }

        private void DrawWheelRails(
            ActiveNote note
        )
        {
            List<Vector2> points =
                note.uiPath;

            if (
                points == null ||
                points.Count < 2
            )
            {
                Debug.LogWarning(
                    "휠 노트 경로점이 2개 미만입니다. ID=" +
                    note.data.id,
                    this
                );
                return;
            }

            float[] distances =
                new float[points.Count];

            float totalDistance = 0f;

            for (int i = 1; i < points.Count; i++)
            {
                totalDistance += Vector2.Distance(
                    points[i - 1],
                    points[i]
                );

                distances[i] = totalDistance;
            }

            if (totalDistance < 0.01f)
                return;

            // Use a constant-width corridor around the rounded centerline.
            // The previous miter could grow to 1.8x at corners and form spikes.
            float halfGap = wheelRailGap * 0.5f;
            Vector2[] left = new Vector2[points.Count];
            Vector2[] right = new Vector2[points.Count];

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 tangent;
                if (i == 0)
                    tangent = points[1] - points[0];
                else if (i == points.Count - 1)
                    tangent = points[i] - points[i - 1];
                else
                    tangent = points[i + 1] - points[i - 1];

                if (tangent.sqrMagnitude < 0.0001f)
                    tangent = Vector2.right;

                tangent.Normalize();
                Vector2 normal = new Vector2(-tangent.y, tangent.x) * halfGap;
                left[i] = points[i] + normal;
                right[i] = points[i] - normal;
            }

            Color railColor = Color.Lerp(
                NoteColor(note.data.kind),
                new Color(0.10f, 0.10f, 0.15f, 1f),
                0.24f);
            railColor.a = Mathf.Min(wheelPathOpacity, 0.60f);

            for (int i = 1; i < points.Count; i++)
            {
                float endProgress = distances[i] / totalDistance;
                AddRailLine(note, left[i - 1], left[i], railColor, endProgress);
                AddRailLine(note, right[i - 1], right[i], railColor, endProgress);
            }

            // Intentionally NO lines across the start/end: those short closing
            // strokes made short and curved paths appear triangular. The moving
            // wheel head and existing END circle are the endpoints instead.

            if (showWheelEndMarker)
            {
                float endSize = GetVisualNoteSize() * 0.64f;
                Vector2 endPosition = points[points.Count - 1];

                RectTransform endOutline = AddImage(
                    noteLayer, "WheelEndOutline", circleSprite,
                    new Color(0.10f, 0.10f, 0.16f, 0.98f), endSize
                );
                endOutline.anchoredPosition = endPosition;
                endOutline.SetAsLastSibling();
                note.pathParts.Add(endOutline.gameObject);

                RectTransform endFill = AddImage(
                    noteLayer, "WheelEndFill", circleSprite,
                    NoteColor(MouseModeNoteKind.Wheel), endSize * 0.85f
                );
                endFill.anchoredPosition = endPosition;
                endFill.SetAsLastSibling();
                note.pathParts.Add(endFill.gameObject);

                if (runtimeFont != null)
                {
                    Text endLabel = CreateText(
                        "WheelEndText", Mathf.Max(12, Mathf.RoundToInt(endSize * 0.28f)), noteLayer
                    );
                    endLabel.text = "END";
                    endLabel.color = Color.white;
                    endLabel.rectTransform.sizeDelta = new Vector2(endSize, endSize * 0.7f);
                    endLabel.rectTransform.anchoredPosition = endPosition;
                    endLabel.rectTransform.SetAsLastSibling();
                    note.pathParts.Add(endLabel.gameObject);
                }
            }
        }

        private void AddRailLine(
            ActiveNote note,
            Vector2 a,
            Vector2 b,
            Color color,
            float endProgress
        )
        {
            Vector2 difference = b - a;

            if (difference.sqrMagnitude < 0.001f)
                return;

            RectTransform line = AddImage(
                noteLayer,
                "WheelRail",
                squareSprite,
                color,
                1f
            );

            line.SetAsFirstSibling();

            line.sizeDelta = new Vector2(
                difference.magnitude +
                    wheelRailThickness * 0.55f,
                wheelRailThickness
            );

            line.anchoredPosition =
                (a + b) * 0.5f;

            float angle =
                Mathf.Atan2(
                    difference.y,
                    difference.x
                ) * Mathf.Rad2Deg;

            line.localRotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    angle
                );

            note.pathParts.Add(
                line.gameObject
            );

            note.rails.Add(
                new RailPiece
                {
                    image =
                        line.GetComponent<Image>(),
                    endProgress =
                        endProgress,
                    normalColor =
                        color
                }
            );
        }

        private void UpdateWheelMovement(
            ActiveNote note,
            float progress
        )
        {
            if (note.root == null)
                return;

            Vector2 target =
                GetWheelPositionByDistance(
                    note.uiPath,
                    progress
                );

            note.root.anchoredPosition =
                target;

            // Travel follows the song; the white wheel dot rotates ONLY
            // when the player actually scrolls (FlashWheelInput).

            if (note.rollingMark != null)
            {
                float radians =
                    note.rollAngle * Mathf.Deg2Rad;

                float markerRadius =
                    GetVisualNoteSize() * 0.265f;

                note.rollingMark.anchoredPosition =
                    new Vector2(
                        -Mathf.Sin(radians),
                        Mathf.Cos(radians)
                    ) * markerRadius;
            }

            if (note.wheelInputRing != null)
            {
                note.wheelInputFlash = Mathf.Max(0f,
                    note.wheelInputFlash - Time.unscaledDeltaTime);
                float intensity = Mathf.Clamp01(note.wheelInputFlash / 0.18f);
                Color c = NoteColor(MouseModeNoteKind.Wheel);
                c.a = 0.65f * intensity;
                note.wheelInputRing.color = c;
                note.wheelInputRing.rectTransform.sizeDelta = Vector2.one *
                    GetVisualNoteSize() * Mathf.Lerp(1.08f, 1.24f, 1f - intensity);
            }

            foreach (RailPiece piece in note.rails)
            {
                if (piece.image == null)
                    continue;

                Color c = piece.normalColor;

                if (
                    piece.endProgress > 0f &&
                    piece.endProgress < progress
                )
                {
                    c.a *= note.wheelMiddleCount > 0 ? 0.40f : 0.20f;
                }

                piece.image.color = c;
            }
        }

        private static Vector2 GetWheelPositionByDistance(
            List<Vector2> points,
            float progress
        )
        {
            if (
                points == null ||
                points.Count == 0
            )
            {
                return Vector2.zero;
            }

            if (points.Count == 1)
                return points[0];

            float total = 0f;

            for (int i = 1; i < points.Count; i++)
            {
                total += Vector2.Distance(
                    points[i - 1],
                    points[i]
                );
            }

            if (total < 0.001f)
                return points[0];

            float remaining =
                Mathf.Clamp01(progress) * total;

            for (int i = 1; i < points.Count; i++)
            {
                float length = Vector2.Distance(
                    points[i - 1],
                    points[i]
                );

                if (
                    remaining <= length ||
                    i == points.Count - 1
                )
                {
                    float t =
                        length > 0.001f
                            ? Mathf.Clamp01(
                                remaining / length
                            )
                            : 1f;

                    return Vector2.Lerp(
                        points[i - 1],
                        points[i],
                        t
                    );
                }

                remaining -= length;
            }

            return points[points.Count - 1];
        }

        private RectTransform AddImage(
            Transform parent,
            string objectName,
            Sprite sprite,
            Color color,
            float size
        )
        {
            GameObject obj = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

            RectTransform rt =
                obj.GetComponent<RectTransform>();

            rt.SetParent(parent, false);

            rt.anchorMin =
                rt.anchorMax =
                rt.pivot =
                    new Vector2(0.5f, 0.5f);

            rt.sizeDelta =
                new Vector2(size, size);

            Image img =
                obj.GetComponent<Image>();

            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;

            return rt;
        }

        private void CreateCountdownText()
        {
            if (runtimeFont == null)
            {
                Debug.LogWarning(
                    "카운트다운 폰트를 찾지 못했습니다.",
                    this
                );
                return;
            }

            countdownText = CreateText(
                "StartCountdown",
                76
            );

            RectTransform rt =
                countdownText.rectTransform;

            rt.anchorMin =
                rt.anchorMax =
                    new Vector2(0.5f, 0.5f);

            rt.sizeDelta =
                new Vector2(500f, 160f);

            rt.anchoredPosition =
                Vector2.zero;

            countdownText.color =
                new Color(
                    0.93f,
                    0.88f,
                    1f,
                    1f
                );

            countdownText.text = "READY";

            // This legacy UI Text only carries countdown state.
            // MouseCountdownPresentationUI owns the visible TMP wording.
            countdownText.enabled = false;
        }

        private void UpdateCountdown(
            double songSeconds
        )
        {
            if (countdownText == null)
                return;

            // Never reveal READY from the underlying legacy Text.
            countdownText.enabled = false;

            if (songSeconds >= 0.5d)
            {
                countdownText.gameObject.SetActive(
                    false
                );
                return;
            }

            countdownText.gameObject.SetActive(
                true
            );

            double remaining = -songSeconds;

            if (remaining > 3d)
                countdownText.text = "READY";
            else if (remaining > 2d)
                countdownText.text = "3";
            else if (remaining > 1d)
                countdownText.text = "2";
            else if (remaining > 0d)
                countdownText.text = "1";
            else
                countdownText.text = "GO!";
        }

        private Text CreateText(
            string objectName,
            int fontSize,
            Transform parent = null
        )
        {
            GameObject obj = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text)
            );

            RectTransform rt =
                obj.GetComponent<RectTransform>();

            rt.SetParent(
                parent != null
                    ? parent
                    : noteLayer,
                false
            );

            rt.anchorMin =
                rt.anchorMax =
                rt.pivot =
                    new Vector2(0.5f, 0.5f);

            Text text =
                obj.GetComponent<Text>();

            text.font = runtimeFont;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment =
                TextAnchor.MiddleCenter;

            text.horizontalOverflow =
                HorizontalWrapMode.Overflow;

            text.verticalOverflow =
                VerticalWrapMode.Overflow;

            text.raycastTarget = false;

            Outline outline =
                obj.AddComponent<Outline>();

            outline.effectColor =
                new Color(
                    0.10f,
                    0.09f,
                    0.16f,
                    0.75f
                );

            outline.effectDistance =
                new Vector2(1f, -1f);

            return text;
        }

        private void ShowFeedback(
            ActiveNote note,
            string result
        )
        {
            if (
                noteLayer == null ||
                runtimeFont == null
            )
            {
                return;
            }

            // 롱노트 종료 시에는 현재 공의 위치,
            // 즉 레일 끝지점 위치를 사용.
            Vector2 center;

            if (note.root != null)
            {
                center =
                    note.root.anchoredPosition;
            }
            else
            {
                Vector2 point =
                    note.data.kind == MouseModeNoteKind.Wheel
                        ? MouseModeChartTiming.GetPathPosition(
                            note.data,
                            0f
                        )
                        : note.data.position;

                center = ToUI(point);
            }

            // 텍스트 중심을 노트 아래로 배치.
            // 21px은 텍스트 높이 42px의 절반.
            float belowY =
                center.y -
                GetVisualNoteSize() * 0.5f -
                feedbackBelowNoteGap -
                21f;

            float minX =
                -noteLayer.rect.width * 0.5f + 105f;

            float maxX =
                noteLayer.rect.width * 0.5f - 105f;

            Text text = CreateText(
                "Judge_" + result,
                feedbackFontSize
            );

            RectTransform rt =
                text.rectTransform;

            rt.sizeDelta =
                new Vector2(210f, 42f);

            Vector2 position = new Vector2(
                Mathf.Clamp(
                    center.x,
                    Mathf.Min(minX, maxX),
                    Mathf.Max(minX, maxX)
                ),
                belowY
            );

            rt.anchoredPosition = position;
            rt.SetAsLastSibling();

            text.text = result;

            Color color;

            switch (result)
            {
                case "PERFECT":
                    color = new Color(
                        0.91f, 0.82f, 1f
                    );
                    break;

                case "GREAT":
                    color = new Color(
                        0.72f, 0.90f, 1f
                    );
                    break;

                case "GOOD":
                    color = new Color(
                        1f, 0.89f, 0.67f
                    );
                    break;

                default:
                    color = new Color(
                        1f, 0.62f, 0.68f
                    );
                    break;
            }

            text.color = color;

            feedbacks.Add(
                new Feedback
                {
                    rect = rt,
                    text = text,
                    baseColor = color,
                    startPosition = position,
                    elapsed = 0f,
                    result = result
                }
            );
        }

        private void CreateHitPop(ActiveNote note, string result)
        {
            if (note.root == null || noteLayer == null)
                return;

            // 휠 시작 성공: 움직이는 원본을 잠깐 키웠다가 돌려놓습니다.
            // 일반 노트 및 휠 종료 성공: 원본은 정상적으로 제거하고,
            // 동일한 L/R/W 표시가 남은 복제 본체만 0.16초간 확대·페이드합니다.
            bool keepOriginal = note.data.kind == MouseModeNoteKind.Wheel &&
                                note.started && !note.finished;

            RectTransform visual = keepOriginal
                ? note.root
                : Instantiate(note.root, noteLayer, false);

            if (visual == null) return;

            if (!keepOriginal)
            {
                visual.name = "HitPop_" + note.data.id;
                visual.localScale = Vector3.one;
                Transform approachRing = visual.Find("ApproachRing");
                if (approachRing != null)
                    approachRing.gameObject.SetActive(false);
                Transform landingRing = visual.Find("HitTargetRing");
                if (landingRing != null)
                    landingRing.gameObject.SetActive(false);
            }

            CanvasGroup group = visual.GetComponent<CanvasGroup>();
            if (group == null)
                group = visual.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = false;
            group.blocksRaycasts = false;
            visual.SetAsLastSibling();

            // 같은 휠 원본이 재등록될 경우 이전 확대로 배율이 누적되지 않도록 합니다.
            for (int i = hitPops.Count - 1; i >= 0; i--)
            {
                if (hitPops[i].rect == visual && !hitPops[i].destroyAtEnd)
                    hitPops.RemoveAt(i);
            }

            Vector3 baseScale = keepOriginal ? visual.localScale : Vector3.one;
            hitPops.Add(new HitPop
            {
                rect = visual,
                group = group,
                baseScale = baseScale,
                elapsed = 0f,
                destroyAtEnd = !keepOriginal,
                result = result
            });

            // 정확한 판정일수록 공격적인 팝, GOOD은 짧고 작은 확인 반응.
            visual.localScale = baseScale * GetImpactPopScale(result);
        }

        private float GetImpactPopScale(string result)
        {
            float perfectScale = Mathf.Clamp(
                hitPopScale * hitVisualImpactMultiplier, 1.12f, 1.36f);
            if (result == "PERFECT") return perfectScale;
            if (result == "GREAT") return Mathf.Lerp(1f, perfectScale, 0.65f);
            return Mathf.Lerp(1f, perfectScale, 0.30f);
        }

        private void UpdateHitPops(float deltaTime)
        {
            for (int i = hitPops.Count - 1; i >= 0; i--)
            {
                HitPop pop = hitPops[i];
                if (pop.rect == null)
                {
                    hitPops.RemoveAt(i);
                    continue;
                }

                pop.elapsed += Mathf.Max(0f, deltaTime);
                float duration = Mathf.Max(0.01f, hitPopDuration) *
                    (pop.result == "PERFECT" ? 1.08f :
                     pop.result == "GREAT" ? 0.88f : 0.70f);
                float maxScale = GetImpactPopScale(pop.result);
                float t = Mathf.Clamp01(pop.elapsed / duration);

                float scale;
                if (pop.destroyAtEnd)
                {
                    // Immediate punch -> firm recoil, no delayed soft growth.
                    scale = Mathf.Lerp(maxScale, 0.92f,
                        1f - (1f - t) * (1f - t) * (1f - t));
                    if (pop.group != null)
                    {
                        float fade = Mathf.Clamp01((t - 0.30f) / 0.70f);
                        pop.group.alpha = 1f - Mathf.SmoothStep(0f, 1f, fade);
                    }
                }
                else
                {
                    // 휠 진행 중에는 본체를 없애면 안 되므로 다시 원래 크기로 복귀.
                    // Wheel start uses the same immediate punch, then settles.
                    scale = Mathf.Lerp(maxScale, 1f,
                        1f - (1f - t) * (1f - t) * (1f - t));
                }

                pop.rect.localScale = pop.baseScale * scale;

                if (t < 1f) continue;
                if (pop.destroyAtEnd)
                    Destroy(pop.rect.gameObject);
                else
                {
                    pop.rect.localScale = pop.baseScale;
                    if (pop.group != null) pop.group.alpha = 1f;
                }
                hitPops.RemoveAt(i);
            }
        }

        // 판정 종류를 색뿐 아니라 실루엣과 움직임으로 구분합니다.
        // 이미지, 파티클 에셋, 사운드 모두 사용하지 않습니다.
        private void CreateDistinctJudgementEffect(ActiveNote note, string result)
        {
            if (noteLayer == null || note == null || note.data == null ||
                ringSprite == null || squareSprite == null || circleSprite == null)
                return;
            if (judgementVfxLayer == null)
                PrewarmJudgementVfxPool();

            Vector2 center = note.root != null
                ? note.root.anchoredPosition
                : ToUI(note.data.position);
            float size = GetVisualNoteSize();

            if (result == "PERFECT")
            {
                // 단단한 2중 충격파 + 상하좌우로 뻗는 짧은 눈금.
                Color inner = new Color(0.97f, 0.94f, 1f, 0.90f);
                Color outer = new Color(0.73f, 0.59f, 0.87f, 0.68f);
                AddJudgementVfxPart(center, ringSprite, inner,
                    Vector2.one * (size * 1.04f),
                    Vector2.one * (size * 1.52f),
                    Vector2.zero, Vector2.zero, 0f, 0.17f);
                AddJudgementVfxPart(center, ringSprite, outer,
                    Vector2.one * (size * 1.22f),
                    Vector2.one * (size * 1.82f),
                    Vector2.zero, Vector2.zero, 0f, 0.23f);

                Color mark = new Color(0.99f, 0.95f, 0.87f, 0.88f);
                for (int i = 0; i < 4; i++)
                {
                    float angle = i * 90f;
                    float radians = angle * Mathf.Deg2Rad;
                    Vector2 direction = new Vector2(
                        Mathf.Cos(radians), Mathf.Sin(radians));
                    AddJudgementVfxPart(center, squareSprite, mark,
                        new Vector2(size * 0.20f, 4f),
                        new Vector2(size * 0.13f, 2f),
                        direction * (size * 0.58f),
                        direction * (size * 0.15f), angle, 0.17f);
                }
            }
            else if (result == "GREAT")
            {
                // 단일 얇은 원 + 좌우로 가볍게 갈라지는 짧은 선.
                Color blue = new Color(0.66f, 0.84f, 0.91f, 0.75f);
                AddJudgementVfxPart(center, ringSprite, blue,
                    Vector2.one * (size * 1.03f),
                    Vector2.one * (size * 1.48f),
                    Vector2.zero, Vector2.zero, 0f, 0.17f);
                for (int i = -1; i <= 1; i += 2)
                {
                    Vector2 direction = Vector2.right * i;
                    AddJudgementVfxPart(center, squareSprite, blue,
                        new Vector2(size * 0.17f, 3f),
                        new Vector2(size * 0.12f, 2f),
                        direction * (size * 0.55f),
                        direction * (size * 0.10f), 0f, 0.15f);
                }
            }
            else if (result == "GOOD")
            {
                // 큰 충격파 없이 아래쪽에서 짧게 반응하는 두 개의 점.
                Color amber = new Color(0.94f, 0.76f, 0.49f, 0.67f);
                for (int i = -1; i <= 1; i += 2)
                {
                    AddJudgementVfxPart(center, circleSprite, amber,
                        Vector2.one * (size * 0.115f),
                        Vector2.one * (size * 0.055f),
                        new Vector2(i * size * 0.35f, -size * 0.42f),
                        new Vector2(i * size * 0.045f, -size * 0.035f),
                        0f, 0.14f);
                }
            }
            else if (result == "MISS")
            {
                // 성공 이펙트와 반대로, 중심에서 흐려지며 내려가는 작은 X.
                Color muted = new Color(0.75f, 0.42f, 0.49f, 0.57f);
                AddJudgementVfxPart(center, squareSprite, muted,
                    new Vector2(size * 0.23f, 3.5f),
                    new Vector2(size * 0.15f, 2f),
                    Vector2.zero, Vector2.down * (size * 0.09f),
                    45f, 0.19f);
                AddJudgementVfxPart(center, squareSprite, muted,
                    new Vector2(size * 0.23f, 3.5f),
                    new Vector2(size * 0.15f, 2f),
                    Vector2.zero, Vector2.down * (size * 0.09f),
                    -45f, 0.19f);
            }
        }

        private void PrewarmJudgementVfxPool()
        {
            if (noteLayer == null || !useDistinctJudgementEffects ||
                ringSprite == null)
                return;

            GameObject rootObject = new GameObject(
                "JudgementFXLayer", typeof(RectTransform));
            judgementVfxLayer = rootObject.GetComponent<RectTransform>();
            judgementVfxLayer.SetParent(noteLayer, false);
            judgementVfxLayer.anchorMin = judgementVfxLayer.anchorMax =
                judgementVfxLayer.pivot = new Vector2(0.5f, 0.5f);
            judgementVfxLayer.anchoredPosition = Vector2.zero;
            judgementVfxLayer.sizeDelta = Vector2.zero;

            // 첫 판정 순간의 GameObject 생성 스파이크 완화.
            for (int i = 0; i < 18; i++)
            {
                RectTransform rt = AddImage(judgementVfxLayer,
                    "JudgementFX", ringSprite, Color.clear, 1f);
                rt.gameObject.SetActive(false);
                judgementVfxPool.Push(rt);
            }
        }

        private void AddJudgementVfxPart(
            Vector2 center, Sprite sprite, Color color,
            Vector2 startSize, Vector2 endSize,
            Vector2 offset, Vector2 movement,
            float rotationDegrees, float duration)
        {
            RectTransform rt;
            if (judgementVfxPool.Count > 0)
            {
                rt = judgementVfxPool.Pop();
                if (rt == null) return;
                rt.gameObject.SetActive(true);
            }
            else
            {
                rt = AddImage(judgementVfxLayer != null
                    ? judgementVfxLayer : noteLayer,
                    "JudgementFX", sprite, color, startSize.x);
            }

            rt.name = "JudgementFX";
            rt.anchorMin = rt.anchorMax = rt.pivot =
                new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = center + offset;
            rt.sizeDelta = startSize;
            rt.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            rt.localScale = Vector3.one;
            rt.SetAsLastSibling();
            Image image = rt.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;

            judgementVfxs.Add(new JudgementVfxPart
            {
                rect = rt,
                image = image,
                origin = center + offset,
                startSize = startSize,
                endSize = endSize,
                movement = movement,
                color = color,
                elapsed = 0f,
                duration = Mathf.Max(0.01f, duration)
            });
        }

        private void UpdateDistinctJudgementEffects(float dt)
        {
            dt = Mathf.Max(0f, dt);
            for (int i = judgementVfxs.Count - 1; i >= 0; i--)
            {
                JudgementVfxPart part = judgementVfxs[i];
                if (part.rect == null || part.image == null)
                {
                    judgementVfxs.RemoveAt(i);
                    continue;
                }

                part.elapsed += dt;
                float t = Mathf.Clamp01(part.elapsed / part.duration);
                float eased = 1f - (1f - t) * (1f - t) * (1f - t);
                part.rect.anchoredPosition = part.origin + part.movement * eased;
                part.rect.sizeDelta = Vector2.Lerp(part.startSize, part.endSize, eased);
                Color c = part.color;
                c.a *= (1f - Mathf.SmoothStep(0f, 1f, t));
                part.image.color = c;

                if (t >= 1f)
                {
                    ReturnJudgementVfxRect(part.rect);
                    judgementVfxs.RemoveAt(i);
                }
            }
        }

        private void ReturnJudgementVfxRect(RectTransform rt)
        {
            if (rt == null) return;
            rt.gameObject.SetActive(false);
            if (judgementVfxPool.Count < 64)
                judgementVfxPool.Push(rt);
            else
                Destroy(rt.gameObject);
        }

        private void CreateTapPulse(ActiveNote note, string result)
        {
            if (noteLayer == null || ringSprite == null) return;
            Vector2 center = note.root != null ? note.root.anchoredPosition : ToUI(note.data.position);
            float size = GetVisualNoteSize();
            Color color = NoteColor(note.data.kind);
            color.a = result == "PERFECT" ? 0.94f :
                result == "GREAT" ? 0.76f : 0.48f;
            RectTransform pulse = AddImage(noteLayer, "TapPulse", ringSprite, color, size * 1.08f);
            pulse.anchoredPosition = center;
            pulse.SetAsLastSibling();
            tapPulses.Add(new TapPulse
            {
                rect = pulse,
                image = pulse.GetComponent<Image>(),
                color = color,
                elapsed = 0f,
                initialSize = size
            });
        }

        private void UpdateTapPulses(float dt)
        {
            const float lifetime = 0.13f;
            for (int i = tapPulses.Count - 1; i >= 0; i--)
            {
                TapPulse pulse = tapPulses[i];
                pulse.elapsed += dt;
                float t = Mathf.Clamp01(pulse.elapsed / lifetime);
                if (pulse.rect != null)
                    pulse.rect.sizeDelta = Vector2.one * (pulse.initialSize * Mathf.Lerp(1.08f, 1.58f,
                        1f - (1f - t) * (1f - t)));
                if (pulse.image != null)
                {
                    Color c = pulse.color;
                    c.a *= (1f - t);
                    pulse.image.color = c;
                }
                if (t >= 1f)
                {
                    if (pulse.rect != null) Destroy(pulse.rect.gameObject);
                    tapPulses.RemoveAt(i);
                }
            }
        }

        private void UpdateFeedbacks(
            float deltaTime
        )
        {
            for (int i = feedbacks.Count - 1;
                 i >= 0;
                 i--)
            {
                Feedback feedback =
                    feedbacks[i];

                feedback.elapsed += deltaTime;

                float lifetime = Mathf.Min(feedbackDuration,
                    feedback.result == "PERFECT" ? 0.36f :
                    feedback.result == "GREAT" ? 0.33f :
                    feedback.result == "GOOD" ? 0.27f : 0.30f);
                lifetime = Mathf.Max(0.05f, lifetime);
                float fraction = Mathf.Clamp01(feedback.elapsed / lifetime);
                float eased = 1f - (1f - fraction) * (1f - fraction);

                if (feedback.rect != null)
                {
                    // MISS는 아래로 가라앉고 성공 판정은 위로 살짝 뜹니다.
                    float rise = feedback.result == "PERFECT" ? 15f :
                        feedback.result == "GREAT" ? 10f :
                        feedback.result == "GOOD" ? 4f : -7f;
                    feedback.rect.anchoredPosition =
                        feedback.startPosition + Vector2.up * rise * eased;
                    float startScale = feedback.result == "PERFECT" ? 1.11f :
                        feedback.result == "GREAT" ? 1.055f :
                        feedback.result == "GOOD" ? 1f : 0.94f;
                    feedback.rect.localScale = Vector3.one *
                        Mathf.Lerp(startScale, 1f, eased);
                }

                if (feedback.text != null)
                {
                    Color color =
                        feedback.baseColor;

                    color.a = Mathf.Clamp01((1f - fraction) *
                        (feedback.result == "MISS" ? 1.13f : 1.6f));

                    feedback.text.color = color;
                }

                if (
                    feedback.elapsed >= lifetime
                )
                {
                    if (feedback.rect != null)
                    {
                        Destroy(
                            feedback.rect.gameObject
                        );
                    }

                    feedbacks.RemoveAt(i);
                }
            }
        }

        private float EffectiveNoteLead(MouseModeChartData chart)
        {
            // 각 노트의 생성, 접근 원 축소, GO 리드인을 하나의 동일한 시간으로 계산.
            // 채보 JSON의 짧은 접근 시간이나 과거 Inspector 값으로 강제로 단축하지 않는다.
            // 실제 노트 판정 시각과 음악 동기화는 그대로 유지한다.
            return Mathf.Clamp(approachTravelSeconds, 0.70f, 2.20f);
        }

        public static float NoteSizeFromInspector(float baseSize, float visualScale)
        {
            // The Inspector owns this scale; no 124px or stage-height cap.
            return Mathf.Max(1f, baseSize * visualScale);
        }

        private float GetVisualNoteSize()
        {
            return NoteSizeFromInspector(noteSize, noteVisualScale);
        }

        private float GetReadableApproachScale()
        {
            // Never silently cap an Inspector ring scale to 2.2.
            // Only keep the minimum required for a ring shrinking to 1x.
            return Mathf.Max(1f, approachStartScale);
        }

        private Vector2 ToUI(
            Vector2 normalized
        )
        {
            if (noteLayer == null)
                return Vector2.zero;

            float width = noteLayer.rect.width;
            float height = noteLayer.rect.height;
            float padding = GetVisualNoteSize() * 0.62f;
            float halfW = Mathf.Max(0f, width * 0.5f - padding);
            float halfH = Mathf.Max(0f, height * 0.5f - padding);

            return new Vector2(
                Mathf.Clamp((Mathf.Clamp01(normalized.x) - 0.5f) * width, -halfW, halfW),
                Mathf.Clamp((0.5f - Mathf.Clamp01(normalized.y)) * height, -halfH, halfH)
            );
        }

        private Color NoteColor(
            MouseModeNoteKind kind
        )
        {
            if (useDistinctNotePalette)
            {
                if (kind == MouseModeNoteKind.Left) return new Color(0.67f, 0.47f, 0.84f);
                if (kind == MouseModeNoteKind.Right) return new Color(0.34f, 0.76f, 0.78f);
                return new Color(0.94f, 0.70f, 0.32f);
            }
            if (kind == MouseModeNoteKind.Left) return leftColor;
            if (kind == MouseModeNoteKind.Right) return rightColor;
            return wheelColor;
        }

        private void DestroyVisual(
            ActiveNote note
        )
        {
            if (note.root != null)
                Destroy(note.root.gameObject);

            note.root = null;
            note.visualPriorityGroup = null;

            foreach (GameObject part in note.pathParts)
            {
                if (part != null)
                    Destroy(part);
            }

            note.pathParts.Clear();
            note.rails.Clear();
            note.uiPath = null;
        }

        private void MakeSprites()
        {
            circleTexture =
                MakeTexture(false, false);

            ringTexture =
                MakeTexture(true, false);

            squareTexture =
                MakeTexture(false, true);

            Rect spriteRect =
                new Rect(0f, 0f, 64f, 64f);

            Vector2 spritePivot =
                new Vector2(0.5f, 0.5f);

            circleSprite = Sprite.Create(
                circleTexture,
                spriteRect,
                spritePivot,
                100f
            );

            ringSprite = Sprite.Create(
                ringTexture,
                spriteRect,
                spritePivot,
                100f
            );

            squareSprite = Sprite.Create(
                squareTexture,
                spriteRect,
                spritePivot,
                100f
            );
        }

        private static Texture2D MakeTexture(
            bool ring,
            bool square
        )
        {
            const int size = 64;

            Texture2D texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false
            );

            texture.filterMode =
                FilterMode.Bilinear;

            texture.wrapMode =
                TextureWrapMode.Clamp;

            Color[] pixels =
                new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float radius = Vector2.Distance(
                        new Vector2(
                            x + 0.5f,
                            y + 0.5f
                        ),
                        new Vector2(
                            size * 0.5f,
                            size * 0.5f
                        )
                    );

                    float alpha;

                    if (square)
                    {
                        alpha = 1f;
                    }
                    else if (ring)
                    {
                        alpha =
                            Mathf.Clamp01(
                                (radius - 28f) * 1.5f
                            ) *
                            Mathf.Clamp01(
                                (30.5f - radius) * 1.5f
                            );
                    }
                    else
                    {
                        alpha = Mathf.Clamp01(
                            (30.5f - radius) * 1.5f
                        );
                    }

                    pixels[y * size + x] =
                        new Color(
                            1f,
                            1f,
                            1f,
                            alpha
                        );
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return texture;
        }

        private void OnDestroy()
        {
            if (circleSprite != null)
                Destroy(circleSprite);

            if (ringSprite != null)
                Destroy(ringSprite);

            if (squareSprite != null)
                Destroy(squareSprite);

            if (circleTexture != null)
                Destroy(circleTexture);

            if (ringTexture != null)
                Destroy(ringTexture);

            if (squareTexture != null)
                Destroy(squareTexture);
        }
    }
}
