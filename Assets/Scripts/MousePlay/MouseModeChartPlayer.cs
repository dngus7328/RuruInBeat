
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
            new Color(0.52f, 0.36f, 0.69f);

        [SerializeField]
        private Color rightColor =
            new Color(0.22f, 0.49f, 0.60f);

        [SerializeField]
        private Color wheelColor =
            new Color(0.71f, 0.48f, 0.24f);

        [Header("가독성: 기존 채보를 변경하지 않고 표시만 보정")]
        [SerializeField, Range(0.65f, 1f)]
        private float noteVisualScale = 0.84f;

        [Tooltip("씬에 과거 2.3배 설정이 남아 있어도 게임 중 접근 원 확대를 제한합니다.")]
        [SerializeField, Range(1.3f, 1.8f)]
        private float readableApproachScaleLimit = 1.58f;

        [Tooltip("휠 노트의 끝점에 END 표시")]
        [SerializeField]
        private bool showWheelEndMarker = true;

        [Header("접근 원")]
        [SerializeField, Range(1.3f, 3f)]
        private float approachStartScale = 2.3f;

        [SerializeField, Range(0.2f, 1f)]
        private float approachOpacity = 0.9f;

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

        [Header("휠 롱노트 종료")]
        [Tooltip("끝지점 직전 휠 입력을 유지 중이라고 인정할 시간")]
        [SerializeField, Range(0.02f, 0.30f)]
        private float wheelEndScrollBufferSeconds = 0.15f;

        [Header("판정 시간 (초)")]
        [SerializeField] private float perfectWindow = 0.05f;
        [SerializeField] private float greatWindow = 0.09f;
        [SerializeField] private float goodWindow = 0.13f;
        [SerializeField] private float judgeOffsetSeconds = 0f;

        public event Action<MouseModeChartAttack> AttackTriggered;
        public event Action<string, int, int> Judged;

        private class ActiveNote
        {
            public MouseModeChartNote data;

            public double hitTime;
            public double endTime;

            public RectTransform root;
            public RectTransform approach;
            public Image approachImage;
            public RectTransform rollingMark;

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
        }

        private readonly List<ActiveNote> notes =
            new List<ActiveNote>();

        private readonly List<MouseModeChartAttack> attacks =
            new List<MouseModeChartAttack>();

        private readonly List<Feedback> feedbacks =
            new List<Feedback>();

        private int nextSpawn;
        private int nextAttack;
        private int combo;
        private int totalJudgeCount;

        private float earnedScore;
        private double startDsp;

        private bool running;
        private bool chartPaused;
        private bool rescheduleAudioOnResume;

        private double pausedAtDsp;

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

        public int Score
        {
            get
            {
                if (totalJudgeCount == 0)
                    return 0;

                return Mathf.Clamp(
                    Mathf.RoundToInt(
                        1000000f *
                        earnedScore /
                        totalJudgeCount
                    ),
                    0,
                    1000000
                );
            }
        }

        private void Awake()
        {
            songAudio = GetComponent<AudioSource>();
            songAudio.playOnAwake = false;
            songAudio.loop = false;

            runtimeFont = uiFont != null
                ? uiFont
                : Resources.GetBuiltinResource<Font>(
                    "Arial.ttf"
                );

            MakeSprites();
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

                totalJudgeCount +=
                    item.kind == MouseModeNoteKind.Wheel
                        ? 2
                        : 1;
            }

            notes.Sort(
                (a, b) => a.hitTime.CompareTo(b.hitTime)
            );

            ValidateVisualReadability(chart.approachSeconds);

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

            chartPaused = false;
            rescheduleAudioOnResume = false;

            double earliestHit = notes.Count > 0
                ? notes[0].hitTime
                : 0d;

            double preRoll = Math.Max(
                3.0d + readySeconds,
                chart.approachSeconds -
                earliestHit + 0.2d
            );

            startDsp =
                AudioSettings.dspTime + preRoll;

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

            UpdateCountdown(songSeconds);
            UpdateFeedbacks(Time.unscaledDeltaTime);

            // 노트 생성.
            while (
                nextSpawn < notes.Count &&
                notes[nextSpawn].hitTime -
                chart.approachSeconds <= songSeconds
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

            // 입력 처리.
            if (songSeconds >= 0d)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    Judge(
                        MouseModeNoteKind.Left,
                        inputSeconds
                    );
                }

                if (Input.GetMouseButtonDown(1))
                {
                    Judge(
                        MouseModeNoteKind.Right,
                        inputSeconds
                    );
                }

                if (
                    Mathf.Abs(
                        Input.mouseScrollDelta.y
                    ) > 0.01f
                )
                {
                    // 종료 휠 입력을 먼저 확인.
                    // 시작 MISS 여부와 관계없이 처리.
                    if (!TryRegisterWheelEndInput(
                        inputSeconds
                    ))
                    {
                        Judge(
                            MouseModeNoteKind.Wheel,
                            inputSeconds
                        );
                    }
                }
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
                        FinishNote(
                            note,
                            "PERFECT",
                            1f,
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
                        chart.approachSeconds
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

            RefreshVisibleNoteOrder();

            if (
                countdownText != null &&
                countdownText.gameObject.activeSelf
            )
            {
                countdownText.transform.SetAsLastSibling();
            }
        }

        /// <summary>
        /// 접근 시간 안에 같은 위치에 생성되는 노트가 있는지 검사합니다.
        /// 채보 좌표는 바꾸지 않고 한 번만 경고합니다.
        /// </summary>
        private void ValidateVisualReadability(float lookAheadSeconds)
        {
            if (noteLayer == null || notes.Count <= 1)
                return;

            int collisionCount = 0;
            string examples = "";
            float minDistance = GetVisualNoteSize() * 0.95f;

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
                    if (Vector2.Distance(posA, ToUI(b.data.position)) >= minDistance)
                        continue;

                    collisionCount++;
                    if (collisionCount <= 4)
                        examples += " [" + a.data.id + "/" + b.data.id + "]";
                }
            }

            if (collisionCount > 0)
            {
                Debug.LogWarning(
                    "[Mouse Chart/가독성] 접근 시간 안에 노트 위치가 겹치는 쌍 " +
                    collisionCount + "개 감지." + examples +
                    " 채보 JSON의 position과 beat를 조정해야 합니다.", this
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

            if (seconds >= best.endTime)
            {
                // 끝지점에서 입력한 경우 실제 오차로 판정.
                string result = GetJudgeResult(
                    Math.Abs(
                        seconds - best.endTime
                    )
                );

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

        private void Judge(
            MouseModeNoteKind kind,
            double seconds
        )
        {
            ActiveNote best = null;

            double closest =
                Math.Max(0.001f, goodWindow);

            foreach (ActiveNote note in notes)
            {
                if (
                    note.finished ||
                    note.started ||
                    !note.created ||
                    note.data.kind != kind
                )
                {
                    continue;
                }

                double distance =
                    Math.Abs(
                        note.hitTime - seconds
                    );

                if (distance <= closest)
                {
                    closest = distance;
                    best = note;
                }
            }

            if (best == null)
                return;

            string result =
                GetJudgeResult(closest);

            float score =
                GetJudgeScore(result);

            if (kind == MouseModeNoteKind.Wheel)
            {
                StartWheel(
                    best,
                    result,
                    score
                );
            }
            else
            {
                FinishNote(
                    best,
                    result,
                    score,
                    true
                );
            }
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

            if (note.approach != null)
                note.approach.gameObject.SetActive(false);

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

            earnedScore += score;

            combo = score > 0f
                ? combo + 1
                : 0;

            Debug.Log(
                "[Mouse Chart] " +
                result +
                " / " +
                combo +
                " combo / " +
                Score,
                this
            );

            Judged?.Invoke(
                result,
                combo,
                Score
            );
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
            note.created = true;

            Color color =
                NoteColor(note.data.kind);

            Color dark =
                new Color(0.10f, 0.10f, 0.16f, 1f);

            Color inner =
                new Color(0.17f, 0.17f, 0.23f, 1f);

            Color approachColor = color;
            approachColor.a =
                Mathf.Min(approachOpacity, 0.66f) * 0.65f;

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


            if (note.data.kind != MouseModeNoteKind.Wheel)
            {
                bool isLeft = note.data.kind == MouseModeNoteKind.Left;
                RectTransform sideStripe = AddImage(
                    root,
                    isLeft ? "LeftInputStripe" : "RightInputStripe",
                    squareSprite,
                    new Color(0.99f, 0.99f, 1f, 0.96f),
                    1f
                );
                sideStripe.sizeDelta = new Vector2(
                    visualSize * 0.085f,
                    visualSize * 0.32f
                );
                sideStripe.anchoredPosition = new Vector2(
                    (isLeft ? -1f : 1f) * visualSize * 0.31f,
                    0f
                );
            }

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
                note.rollingMark = AddImage(
                    root,
                    "RollingSpot",
                    circleSprite,
                    new Color(1f, 1f, 1f, 0.95f),
                    visualSize * 0.115f
                );

                note.rollingMark.anchoredPosition =
                    new Vector2(
                        0f,
                        visualSize * 0.265f
                    );
            }
        }

        private void UpdateApproachRing(
            ActiveNote note,
            double songSeconds,
            float approachSeconds
        )
        {
            if (note.approach == null)
                return;

            float remaining = Mathf.Clamp01(
                (float)(
                    (note.hitTime - songSeconds) /
                    Math.Max(
                        0.01f,
                        approachSeconds
                    )
                )
            );

            float scale = Mathf.Lerp(
                1f,
                GetReadableApproachScale(),
                Mathf.Pow(remaining, 0.8f)
            );

            note.approach.localScale =
                Vector3.one * scale;

            if (note.approachImage != null)
            {
                Color color =
                    NoteColor(note.data.kind);

                color.a = Mathf.Min(approachOpacity, 0.66f) *
                    Mathf.Lerp(
                        1f,
                        0.55f,
                        remaining
                    );

                note.approachImage.color = color;
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

            return result;
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

            float halfGap =
                wheelRailGap * 0.5f;

            Vector2[] left =
                new Vector2[points.Count];

            Vector2[] right =
                new Vector2[points.Count];

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 incoming = i > 0
                    ? (points[i] - points[i - 1]).normalized
                    : (points[1] - points[0]).normalized;

                Vector2 outgoing = i < points.Count - 1
                    ? (points[i + 1] - points[i]).normalized
                    : incoming;

                Vector2 tangent =
                    incoming + outgoing;

                if (tangent.sqrMagnitude < 0.001f)
                {
                    tangent = outgoing.sqrMagnitude > 0.001f
                        ? outgoing
                        : Vector2.right;
                }

                tangent.Normalize();

                Vector2 normal =
                    new Vector2(
                        -tangent.y,
                        tangent.x
                    );

                Vector2 outNormal =
                    new Vector2(
                        -outgoing.y,
                        outgoing.x
                    );

                float miter = halfGap /
                    Mathf.Max(
                        0.55f,
                        Mathf.Abs(
                            Vector2.Dot(
                                normal,
                                outNormal
                            )
                        )
                    );

                miter = Mathf.Min(
                    miter,
                    halfGap * 1.8f
                );

                left[i] =
                    points[i] + normal * miter;

                right[i] =
                    points[i] - normal * miter;
            }

            Color railColor = Color.Lerp(
                NoteColor(note.data.kind),
                new Color(
                    0.10f,
                    0.10f,
                    0.15f,
                    1f
                ),
                0.24f
            );

            railColor.a =
                Mathf.Min(wheelPathOpacity, 0.60f);

            for (int i = 1; i < points.Count; i++)
            {
                float endProgress =
                    distances[i] / totalDistance;

                AddRailLine(
                    note,
                    left[i - 1],
                    left[i],
                    railColor,
                    endProgress
                );

                AddRailLine(
                    note,
                    right[i - 1],
                    right[i],
                    railColor,
                    endProgress
                );
            }

            AddRailLine(
                note,
                left[0],
                right[0],
                railColor,
                0f
            );

            int last = points.Count - 1;

            AddRailLine(
                note,
                left[last],
                right[last],
                railColor,
                1f
            );

            if (showWheelEndMarker)
            {
                float endSize = GetVisualNoteSize() * 0.64f;
                Vector2 endPosition = points[last];

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

            float distance = Vector2.Distance(
                note.root.anchoredPosition,
                target
            );

            note.root.anchoredPosition =
                target;

            float radius = Mathf.Max(
                8f,
                GetVisualNoteSize() * 0.31f
            );

            note.rollAngle -=
                distance / radius * Mathf.Rad2Deg;

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
                    c.a *= 0.25f;
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
                    elapsed = 0f
                }
            );
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

                float fraction = Mathf.Clamp01(
                    feedback.elapsed /
                    feedbackDuration
                );

                if (feedback.rect != null)
                {
                    feedback.rect.anchoredPosition =
                        feedback.startPosition +
                        Vector2.up * (
                            10f * fraction
                        );
                }

                if (feedback.text != null)
                {
                    Color color =
                        feedback.baseColor;

                    color.a = Mathf.Clamp01(
                        (1f - fraction) * 1.5f
                    );

                    feedback.text.color = color;
                }

                if (
                    feedback.elapsed >=
                    feedbackDuration
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

        private float GetVisualNoteSize()
        {
            // 기존 씬에 저장된 noteSize 값을 유지하면서 시각 크기만 조절.
            float height = noteLayer != null ? noteLayer.rect.height : 720f;
            return Mathf.Clamp(
                Mathf.Min(noteSize * noteVisualScale, height * 0.14f),
                52f, 96f
            );
        }

        private float GetReadableApproachScale()
        {
            // Inspector에 이전 2.3배가 남아 있더라도 그 값이 화면을 덮지 않게 제한.
            return Mathf.Clamp(approachStartScale, 1.25f, readableApproachScaleLimit);
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
            if (kind == MouseModeNoteKind.Left)
                return leftColor;

            if (kind == MouseModeNoteKind.Right)
                return rightColor;

            return wheelColor;
        }

        private void DestroyVisual(
            ActiveNote note
        )
        {
            if (note.root != null)
                Destroy(note.root.gameObject);

            note.root = null;

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
