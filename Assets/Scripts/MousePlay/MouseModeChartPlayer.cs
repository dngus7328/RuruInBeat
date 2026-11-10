
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

        [Header("접근 원")]
        [SerializeField, Range(1.3f, 3f)]
        private float approachStartScale = 2.3f;

        [SerializeField, Range(0.2f, 1f)]
        private float approachOpacity = 0.9f;

        [Header("휠 롱노트 레일")]
        [SerializeField, Range(24f, 90f)]
        private float wheelRailGap = 50f;

        [SerializeField, Range(2f, 12f)]
        private float wheelRailThickness = 5f;

        [SerializeField, Range(0.2f, 1f)]
        private float wheelPathOpacity = 0.85f;

        [Header("시작 안내")]
        [SerializeField]
        private bool showCountdown = true;

        [SerializeField, Range(0f, 3f)]
        private float readySeconds = 0.6f;

        [SerializeField]
        private Font uiFont;

        [Header("노트 아래 판정")]
        [SerializeField, Range(0.1f, 2f)]
        private float feedbackDuration = 0.65f;

        [SerializeField, Range(14, 48)]
        private int feedbackFontSize = 25;

        [Header("판정 (초)")]
        [SerializeField]
        private float perfectWindow = 0.05f;

        [SerializeField]
        private float greatWindow = 0.09f;

        [SerializeField]
        private float goodWindow = 0.13f;

        [SerializeField]
        private float judgeOffsetSeconds = 0f;

        public event Action<MouseModeChartAttack>
            AttackTriggered;

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

            public string startJudge;
            public float startScore;
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

        private float earnedScore;
        private double startDsp;

        private bool running;

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
                if (notes.Count == 0)
                    return 0;

                return Mathf.Clamp(
                    Mathf.RoundToInt(
                        1000000f * earnedScore / notes.Count
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
                    "MouseModeChartPlayer: " +
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
                    "MouseModeChartPlayer: " +
                    "BPM과 접근 시간은 0보다 커야 합니다.",
                    this
                );

                return;
            }

            // 노트가 피노 이동 영역에 가려지지 않도록
            // NoteLayer를 PinoMoveArea보다 앞에 표시한다.
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

            foreach (MouseModeChartNote item in chart.notes)
            {
                if (item == null)
                    continue;

                if (item.kind == MouseModeNoteKind.Wheel &&
                    item.endBeat <= item.beat)
                {
                    Debug.LogWarning(
                        "휠 노트의 종료 박자가 잘못됐습니다. ID=" +
                        item.id,
                        this
                    );

                    continue;
                }

                ActiveNote note = new ActiveNote();

                note.data = item;

                note.hitTime =
                    MouseModeChartTiming.BeatToSeconds(
                        chart,
                        item.beat
                    );

                float endBeat =
                    item.kind == MouseModeNoteKind.Wheel
                        ? item.endBeat
                        : item.beat;

                note.endTime =
                    MouseModeChartTiming.BeatToSeconds(
                        chart,
                        endBeat
                    );

                notes.Add(note);
            }

            notes.Sort(
                (a, b) => a.hitTime.CompareTo(b.hitTime)
            );

            foreach (MouseModeChartAttack attack
                in chart.attacks)
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

            double earliestHit =
                notes.Count > 0
                    ? notes[0].hitTime
                    : 0d;

            double preRoll = Math.Max(
                3.0d + readySeconds,
                chart.approachSeconds -
                earliestHit + 0.2d
            );

            startDsp =
                AudioSettings.dspTime + preRoll;

            if (chartAsset.song != null)
            {
                songAudio.clip = chartAsset.song;
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

        private void Update()
        {
            if (!running ||
                chartAsset == null ||
                chartAsset.data == null)
            {
                return;
            }

            if (!Application.isFocused ||
                Time.timeScale <= 0f ||
                AudioListener.pause)
            {
                return;
            }

            MouseModeChartData chart =
                chartAsset.data;

            double songSeconds =
                AudioSettings.dspTime - startDsp;

            double inputSeconds =
                songSeconds + judgeOffsetSeconds;

            UpdateCountdown(songSeconds);
            UpdateFeedbacks(Time.unscaledDeltaTime);

            // 노트 등장
            while (nextSpawn < notes.Count &&
                notes[nextSpawn].hitTime -
                chart.approachSeconds <= songSeconds)
            {
                CreateVisual(notes[nextSpawn]);
                nextSpawn++;
            }

            // 탄막 패턴 이벤트
            while (nextAttack < attacks.Count &&
                MouseModeChartTiming.BeatToSeconds(
                    chart,
                    attacks[nextAttack].beat
                ) <= songSeconds)
            {
                AttackTriggered?.Invoke(
                    attacks[nextAttack]
                );

                nextAttack++;
            }

            // 클릭 위치와 관계없이 판정
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

                if (Mathf.Abs(
                    Input.mouseScrollDelta.y
                ) > 0.01f)
                {
                    Judge(
                        MouseModeNoteKind.Wheel,
                        inputSeconds
                    );
                }
            }

            // 노트 상태 갱신
            foreach (ActiveNote note in notes)
            {
                if (note.finished || !note.created)
                    continue;

                if (note.data.kind ==
                    MouseModeNoteKind.Wheel &&
                    note.started)
                {
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

                    if (songSeconds >= note.endTime)
                    {
                        FinishNote(
                            note,
                            note.startJudge,
                            note.startScore,
                            false
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

                    // 입력하지 않았을 때는 MISS
                    if (inputSeconds >
                        note.hitTime + goodWindow)
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

            if (countdownText != null &&
                countdownText.gameObject.activeSelf)
            {
                countdownText.transform.SetAsLastSibling();
            }
        }

        private void Judge(
            MouseModeNoteKind kind,
            double seconds)
        {
            ActiveNote best = null;

            double closest =
                Math.Max(0.001f, goodWindow);

            foreach (ActiveNote note in notes)
            {
                if (note.finished ||
                    note.started ||
                    !note.created ||
                    note.data.kind != kind)
                {
                    continue;
                }

                double distance =
                    Math.Abs(note.hitTime - seconds);

                if (distance <= closest)
                {
                    closest = distance;
                    best = note;
                }
            }

            if (best == null)
                return;

            string result =
                closest <= perfectWindow
                    ? "PERFECT"
                    : closest <= greatWindow
                        ? "GREAT"
                        : "GOOD";

            float weight =
                result == "PERFECT"
                    ? 1f
                    : result == "GREAT"
                        ? 0.8f
                        : 0.5f;

            if (kind == MouseModeNoteKind.Wheel)
            {
                best.started = true;
                best.startJudge = result;
                best.startScore = weight;

                if (best.approach != null)
                {
                    best.approach.gameObject
                        .SetActive(false);
                }

                ShowFeedback(best, result);
            }
            else
            {
                FinishNote(
                    best,
                    result,
                    weight,
                    true
                );
            }
        }

        private void FinishNote(
            ActiveNote note,
            string result,
            float score,
            bool showFeedback)
        {
            if (note.finished)
                return;

            note.finished = true;

            if (showFeedback)
                ShowFeedback(note, result);

            earnedScore += score;

            combo = score > 0f
                ? combo + 1
                : 0;

            Debug.Log(
                "[Mouse Chart] " +
                result +
                " / " + combo +
                " combo / " + Score,
                this
            );

            Judged?.Invoke(
                result,
                combo,
                Score
            );

            DestroyVisual(note);
        }

        private void CreateVisual(ActiveNote note)
        {
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
                new Vector2(noteSize, noteSize);

            if (note.data.kind ==
                MouseModeNoteKind.Wheel)
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

            // 얇은 접근 원
            Color approachColor = color;

            approachColor.a =
                approachOpacity * 0.65f;

            note.approach = AddImage(
                root,
                "ApproachRing",
                ringSprite,
                approachColor,
                noteSize * 1.04f
            );

            note.approach.SetAsFirstSibling();

            note.approachImage =
                note.approach.GetComponent<Image>();

            note.approach.localScale =
                Vector3.one * approachStartScale;

            // 노트 외곽선
            AddImage(
                root,
                "DarkOutline",
                circleSprite,
                dark,
                noteSize
            );

            // 노트 본체 색상
            AddImage(
                root,
                "ColorBody",
                circleSprite,
                color,
                noteSize * 0.90f
            );

            // 노트 중앙
            AddImage(
                root,
                "InnerCircle",
                circleSprite,
                inner,
                noteSize * 0.66f
            );

            // L / R / W 문자
            if (runtimeFont != null)
            {
                string labelName =
                    note.data.kind ==
                    MouseModeNoteKind.Left
                        ? "LeftLabel"
                        : note.data.kind ==
                          MouseModeNoteKind.Right
                            ? "RightLabel"
                            : "WheelLabel";

                Text label = CreateText(
                    labelName,
                    Mathf.RoundToInt(
                        noteSize * 0.38f
                    ),
                    root
                );

                label.text =
                    note.data.kind ==
                    MouseModeNoteKind.Left
                        ? "L"
                        : note.data.kind ==
                          MouseModeNoteKind.Right
                            ? "R"
                            : "W";

                label.color =
                    new Color(
                        0.98f,
                        0.98f,
                        1f,
                        1f
                    );

                label.rectTransform.sizeDelta =
                    new Vector2(
                        noteSize,
                        noteSize
                    );

                label.rectTransform.anchoredPosition =
                    Vector2.zero;
            }

            // 휠 롱노트 공 표면의 회전 표시
            if (note.data.kind ==
                MouseModeNoteKind.Wheel)
            {
                note.rollingMark = AddImage(
                    root,
                    "RollingSpot",
                    circleSprite,
                    new Color(1f, 1f, 1f, 0.95f),
                    noteSize * 0.115f
                );

                note.rollingMark.anchoredPosition =
                    new Vector2(
                        0f,
                        noteSize * 0.265f
                    );
            }
        }

        private void UpdateApproachRing(
            ActiveNote note,
            double songSeconds,
            float approachSeconds)
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

            // 판정 시각에 접근 원이 본체와 겹침
            float scale = Mathf.Lerp(
                1f,
                approachStartScale,
                Mathf.Pow(remaining, 0.8f)
            );

            note.approach.localScale =
                Vector3.one * scale;

            if (note.approachImage != null)
            {
                Color color =
                    NoteColor(note.data.kind);

                color.a =
                    approachOpacity *
                    Mathf.Lerp(
                        1f,
                        0.55f,
                        remaining
                    );

                note.approachImage.color = color;
            }
        }

        private List<Vector2> BuildWheelUIPath(
            MouseModeChartNote data)
        {
            List<Vector2> result =
                new List<Vector2>();

            if (data.path != null &&
                data.path.Count > 0)
            {
                foreach (Vector2 point in data.path)
                {
                    result.Add(ToUI(point));
                }
            }

            if (result.Count == 0)
            {
                result.Add(
                    ToUI(data.position)
                );
            }

            return result;
        }

        private void DrawWheelRails(
            ActiveNote note)
        {
            List<Vector2> points =
                note.uiPath;

            if (points == null ||
                points.Count < 2)
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

            for (int i = 1;
                 i < points.Count;
                 i++)
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

            // 경로 중심선 양옆으로 레일 생성
            for (int i = 0;
                 i < points.Count;
                 i++)
            {
                Vector2 incoming =
                    i > 0
                        ? (points[i] -
                           points[i - 1]).normalized
                        : (points[1] -
                           points[0]).normalized;

                Vector2 outgoing =
                    i < points.Count - 1
                        ? (points[i + 1] -
                           points[i]).normalized
                        : incoming;

                Vector2 tangent =
                    incoming + outgoing;

                if (tangent.sqrMagnitude < 0.001f)
                {
                    tangent =
                        outgoing.sqrMagnitude > 0.001f
                            ? outgoing
                            : Vector2.right;
                }

                tangent.Normalize();

                Vector2 normal = new Vector2(
                    -tangent.y,
                    tangent.x
                );

                Vector2 outNormal = new Vector2(
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
                    points[i] +
                    normal * miter;

                right[i] =
                    points[i] -
                    normal * miter;
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
                wheelPathOpacity;

            // 두 줄의 레일
            for (int i = 1;
                 i < points.Count;
                 i++)
            {
                float endProgress =
                    distances[i] /
                    totalDistance;

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

            // 레일 시작 마감
            AddRailLine(
                note,
                left[0],
                right[0],
                railColor,
                0f
            );

            // 레일 끝 마감
            int last =
                points.Count - 1;

            AddRailLine(
                note,
                left[last],
                right[last],
                railColor,
                1f
            );
        }

        private void AddRailLine(
            ActiveNote note,
            Vector2 a,
            Vector2 b,
            Color color,
            float endProgress)
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

            line.localRotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Atan2(
                        difference.y,
                        difference.x
                    ) * Mathf.Rad2Deg
                );

            note.pathParts.Add(
                line.gameObject
            );

            RailPiece piece = new RailPiece();

            piece.image =
                line.GetComponent<Image>();

            piece.endProgress =
                endProgress;

            piece.normalColor = color;

            note.rails.Add(piece);
        }

        private void UpdateWheelMovement(
            ActiveNote note,
            float progress)
        {
            if (note.root == null)
                return;

            Vector2 target =
                GetWheelPositionByDistance(
                    note.uiPath,
                    progress
                );

            float distance =
                Vector2.Distance(
                    note.root.anchoredPosition,
                    target
                );

            note.root.anchoredPosition = target;

            // 이동 거리에 따라 공의 표면 표시 회전
            float radius = Mathf.Max(
                8f,
                noteSize * 0.31f
            );

            note.rollAngle -=
                distance / radius *
                Mathf.Rad2Deg;

            if (note.rollingMark != null)
            {
                float radians =
                    note.rollAngle *
                    Mathf.Deg2Rad;

                float markerRadius =
                    noteSize * 0.265f;

                note.rollingMark.anchoredPosition =
                    new Vector2(
                        -Mathf.Sin(radians),
                        Mathf.Cos(radians)
                    ) * markerRadius;
            }

            // 지나간 레일을 흐리게 표시
            foreach (RailPiece piece in note.rails)
            {
                if (piece.image == null)
                    continue;

                Color c =
                    piece.normalColor;

                if (piece.endProgress > 0f &&
                    piece.endProgress < progress)
                {
                    c.a *= 0.25f;
                }

                piece.image.color = c;
            }
        }

        private static Vector2 GetWheelPositionByDistance(
            List<Vector2> points,
            float progress)
        {
            if (points == null ||
                points.Count == 0)
            {
                return Vector2.zero;
            }

            if (points.Count == 1)
                return points[0];

            float total = 0f;

            for (int i = 1;
                 i < points.Count;
                 i++)
            {
                total += Vector2.Distance(
                    points[i - 1],
                    points[i]
                );
            }

            if (total < 0.001f)
                return points[0];

            float remaining =
                Mathf.Clamp01(progress) *
                total;

            for (int i = 1;
                 i < points.Count;
                 i++)
            {
                Vector2 a = points[i - 1];
                Vector2 b = points[i];

                float length =
                    Vector2.Distance(a, b);

                if (remaining <= length ||
                    i == points.Count - 1)
                {
                    float t =
                        length > 0.001f
                            ? Mathf.Clamp01(
                                remaining / length
                            )
                            : 1f;

                    return Vector2.Lerp(
                        a,
                        b,
                        t
                    );
                }

                remaining -= length;
            }

            return points[
                points.Count - 1
            ];
        }

        private RectTransform AddImage(
            Transform parent,
            string objectName,
            Sprite sprite,
            Color color,
            float size)
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

            Image image =
                obj.GetComponent<Image>();

            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;

            return rt;
        }

        private void CreateCountdownText()
        {
            if (runtimeFont == null)
            {
                Debug.LogWarning(
                    "카운트다운 폰트를 찾지 못했습니다. " +
                    "UI Font를 연결해 주세요.",
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
        }

        private void UpdateCountdown(
            double songSeconds)
        {
            if (countdownText == null)
                return;

            if (songSeconds >= 0.5d)
            {
                countdownText.gameObject
                    .SetActive(false);

                return;
            }

            countdownText.gameObject
                .SetActive(true);

            double remaining =
                -songSeconds;

            if (remaining > 3d)
            {
                countdownText.text = "READY";
            }
            else if (remaining > 2d)
            {
                countdownText.text = "3";
            }
            else if (remaining > 1d)
            {
                countdownText.text = "2";
            }
            else if (remaining > 0d)
            {
                countdownText.text = "1";
            }
            else
            {
                countdownText.text = "GO!";
            }
        }

        private Text CreateText(
            string objectName,
            int fontSize,
            Transform parent = null)
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
                    0.1f,
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
            string result)
        {
            if (noteLayer == null ||
                runtimeFont == null)
            {
                return;
            }

            Vector2 center;

            if (note.root != null)
            {
                center =
                    note.root.anchoredPosition;
            }
            else
            {
                Vector2 position =
                    note.data.kind ==
                    MouseModeNoteKind.Wheel
                        ? MouseModeChartTiming
                            .GetPathPosition(
                                note.data,
                                0f
                            )
                        : note.data.position;

                center = ToUI(position);
            }

            // 노트 아래에 판정 글자 표시
            float belowY =
                center.y -
                noteSize * 0.5f -
                24f;

            float minY =
                -noteLayer.rect.height *
                0.5f + 20f;

            float maxY =
                noteLayer.rect.height *
                0.5f - 20f;

            // 화면 아래쪽에서 잘릴 경우 위에 표시
            if (belowY < minY)
            {
                belowY =
                    center.y +
                    noteSize * 0.5f +
                    24f;
            }

            Text text = CreateText(
                "Judge_" + result,
                feedbackFontSize
            );

            RectTransform rt =
                text.rectTransform;

            rt.sizeDelta =
                new Vector2(210f, 42f);

            Vector2 positionUI =
                new Vector2(
                    center.x,
                    Mathf.Clamp(
                        belowY,
                        minY,
                        maxY
                    )
                );

            rt.anchoredPosition = positionUI;
            rt.SetAsLastSibling();

            text.text = result;

            Color color;

            switch (result)
            {
                case "PERFECT":
                    color = new Color(
                        0.91f,
                        0.82f,
                        1f
                    );
                    break;

                case "GREAT":
                    color = new Color(
                        0.72f,
                        0.9f,
                        1f
                    );
                    break;

                case "GOOD":
                    color = new Color(
                        1f,
                        0.89f,
                        0.67f
                    );
                    break;

                default:
                    color = new Color(
                        1f,
                        0.62f,
                        0.68f
                    );
                    break;
            }

            text.color = color;

            Feedback feedback = new Feedback();

            feedback.rect = rt;
            feedback.text = text;
            feedback.baseColor = color;
            feedback.startPosition = positionUI;
            feedback.elapsed = 0f;

            feedbacks.Add(feedback);
        }

        private void UpdateFeedbacks(float deltaTime)
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
                        Vector2.up *
                        (10f * fraction);
                }

                if (feedback.text != null)
                {
                    Color color =
                        feedback.baseColor;

                    color.a = Mathf.Clamp01(
                        (1f - fraction) *
                        1.5f
                    );

                    feedback.text.color = color;
                }

                if (feedback.elapsed >=
                    feedbackDuration)
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

        private Vector2 ToUI(Vector2 normalized)
        {
            return new Vector2(
                (
                    Mathf.Clamp01(normalized.x) -
                    0.5f
                ) * noteLayer.rect.width,

                (
                    0.5f -
                    Mathf.Clamp01(normalized.y)
                ) * noteLayer.rect.height
            );
        }

        private Color NoteColor(
            MouseModeNoteKind kind)
        {
            if (kind == MouseModeNoteKind.Left)
                return leftColor;

            if (kind == MouseModeNoteKind.Right)
                return rightColor;

            return wheelColor;
        }

        private void DestroyVisual(ActiveNote note)
        {
            if (note.root != null)
            {
                Destroy(note.root.gameObject);
            }

            note.root = null;

            foreach (GameObject part
                in note.pathParts)
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
            circleTexture = MakeTexture(
                false,
                false
            );

            ringTexture = MakeTexture(
                true,
                false
            );

            squareTexture = MakeTexture(
                false,
                true
            );

            circleSprite = Sprite.Create(
                circleTexture,
                new Rect(0f, 0f, 64f, 64f),
                new Vector2(0.5f, 0.5f),
                100f
            );

            ringSprite = Sprite.Create(
                ringTexture,
                new Rect(0f, 0f, 64f, 64f),
                new Vector2(0.5f, 0.5f),
                100f
            );

            squareSprite = Sprite.Create(
                squareTexture,
                new Rect(0f, 0f, 64f, 64f),
                new Vector2(0.5f, 0.5f),
                100f
            );
        }

        private static Texture2D MakeTexture(
            bool ring,
            bool square)
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
                        // 약 2픽셀 두께의 접근 원
                        alpha =
                            Mathf.Clamp01(
                                (radius - 28f) *
                                1.5f
                            ) *
                            Mathf.Clamp01(
                                (30.5f - radius) *
                                1.5f
                            );
                    }
                    else
                    {
                        alpha =
                            Mathf.Clamp01(
                                (30.5f - radius) *
                                1.5f
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
