
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LuluInBeat.MouseMode.ChartSystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class MouseRhythmPresentationUI : MonoBehaviour
    {
        [Header("폰트")]
        [SerializeField] private TMP_FontAsset noteFont;
        [SerializeField] private TMP_FontAsset judgementFont;

        [Header("노트 외형")]
        [SerializeField, Range(20f, 64f)]
        private float noteLetterSize = 38f;

        [SerializeField, Range(0.02f, 0.25f)]
        private float noteAppearSeconds = 0.09f;

        [SerializeField, Range(0.5f, 1f)]
        private float noteStartOpacity = 0.60f;

        [SerializeField]
        private Color noteCenterColor =
            new Color(0.12f, 0.11f, 0.19f, 1f);

        [SerializeField]
        private Color noteOutlineColor =
            new Color(0.08f, 0.07f, 0.14f, 1f);

        [Header("판정 글씨")]
        [SerializeField, Range(35f, 70f)]
        private float judgementFontSize = 57f;

        [SerializeField, Range(1f, 1.6f)]
        private float judgementStartScale = 1.28f;

        [SerializeField, Range(0.3f, 0.6f)]
        private float judgementDuration = 0.43f;

        [SerializeField, Range(0f, 3f)]
        private float judgementLetterSpacing = 0.8f;

        private sealed class NoteAppearance
        {
            public int id;
            public RectTransform root;

            public readonly List<Image> images =
                new List<Image>();

            public readonly List<Color> colors =
                new List<Color>();

            public TextMeshProUGUI label;
            public float time;
        }

        private sealed class JudgementAppearance
        {
            public int id;
            public RectTransform root;
            public RectTransform visual;
            public CanvasGroup group;
            public Vector2 initialRootPosition;
            public float time;
        }

        private readonly List<NoteAppearance> notes =
            new List<NoteAppearance>();

        private readonly List<JudgementAppearance> judgements =
            new List<JudgementAppearance>();

        private readonly HashSet<int> knownNotes =
            new HashSet<int>();

        private readonly HashSet<int> knownJudgements =
            new HashSet<int>();

        private RectTransform noteLayer;

        private void Awake()
        {
            noteLayer = GetComponent<RectTransform>();
        }

        private void LateUpdate()
        {
            if (noteLayer == null ||
                !Application.isFocused ||
                Time.timeScale <= 0f ||
                AudioListener.pause)
            {
                return;
            }

            // 기존 채보 재생기가 생성한
            // 노트와 판정 오브젝트만 찾아서 꾸민다.
            int count = noteLayer.childCount;

            for (int i = 0; i < count; i++)
            {
                RectTransform child =
                    noteLayer.GetChild(i) as RectTransform;

                if (child == null)
                    continue;

                if (child.name.StartsWith("MouseNote_"))
                {
                    SetupNote(child);
                }
                else if (child.name.StartsWith("Judge_"))
                {
                    SetupJudgement(child);
                }
            }

            float dt = Time.deltaTime;

            UpdateNotes(dt);
            UpdateJudgements(dt);
        }

        private void SetupNote(RectTransform root)
        {
            int id = root.GetInstanceID();

            if (!knownNotes.Add(id))
                return;

            Image outline =
                GetChildImage(root, "DarkOutline");

            Image body =
                GetChildImage(root, "ColorBody");

            Image center =
                GetChildImage(root, "InnerCircle");

            if (outline != null)
                outline.color = noteOutlineColor;

            if (center != null)
                center.color = noteCenterColor;

            NoteAppearance note = new NoteAppearance
            {
                id = id,
                root = root
            };

            AddNotePart(note, outline);
            AddNotePart(note, body);
            AddNotePart(note, center);

            // The shrinking ApproachRing and the authored solid note rim
            // already communicate the landing time. Do not create an extra
            // pale HitTimingOutline on every note: it obscures dense patterns.

            Text originalLetter =
                GetChildText(root, "LeftLabel");

            string letter = "L";

            if (originalLetter == null)
            {
                originalLetter =
                    GetChildText(root, "RightLabel");

                letter = "R";
            }

            if (originalLetter == null)
            {
                originalLetter =
                    GetChildText(root, "WheelLabel");

                letter = "W";
            }

            if (originalLetter != null)
                originalLetter.enabled = false;

            RectTransform letterRect = NewRect(
                "RhythmNoteLetter",
                root
            );

            letterRect.sizeDelta = root.rect.size;

            TextMeshProUGUI label =
                letterRect.gameObject
                    .AddComponent<TextMeshProUGUI>();

            label.font = RuruConsoleUI.Font();

            label.text = letter;
            label.fontSize = noteLetterSize;
            label.fontStyle = FontStyles.Bold;

            label.alignment =
                TextAlignmentOptions.Center;

            label.enableWordWrapping = false;
            label.raycastTarget = false;

            label.color =
                new Color(1f, 0.99f, 1f, 1f);

            label.outlineColor =
                new Color(0.09f, 0.07f, 0.16f, 0.8f);

            label.outlineWidth = 0.10f;

            note.label = label;

            Transform rollingSpot =
                root.Find("RollingSpot");

            if (rollingSpot != null)
                rollingSpot.SetAsLastSibling();

            notes.Add(note);
        }

        private void SetupJudgement(RectTransform root)
        {
            int id = root.GetInstanceID();

            if (!knownJudgements.Add(id))
                return;

            Text original = root.GetComponent<Text>();

            if (original == null)
                return;

            string result = original.text;

            // 기본 판정 글씨만 숨긴다.
            // 판정 결과와 위치는 그대로 사용한다.
            original.enabled = false;

            RectTransform visual = NewRect(
                "RhythmJudgementVisual",
                root
            );

            visual.sizeDelta =
                new Vector2(360f, 92f);

            CanvasGroup group =
                visual.gameObject
                    .AddComponent<CanvasGroup>();

            group.alpha = 1f;
            group.interactable = false;
            group.blocksRaycasts = false;

            TextMeshProUGUI text =
                visual.gameObject
                    .AddComponent<TextMeshProUGUI>();

            text.font = RuruConsoleUI.Font();

            text.text = result;
            text.fontStyle = FontStyles.Bold;
            text.fontSize = judgementFontSize;

            text.enableAutoSizing = false;
            text.enableWordWrapping = false;

            text.alignment =
                TextAlignmentOptions.Center;

            text.characterSpacing =
                judgementLetterSpacing;

            text.raycastTarget = false;

            // Apply verdict colors on the FIRST rendered frame.
            // The later polish pass can override colors from its Inspector,
            // but newly spawned judgements should never flash a flat tint.
            Color upper;
            Color lower;
            GetJudgementGradient(result, out upper, out lower);
            text.color = Color.white;
            text.enableVertexGradient = true;
            text.colorGradient = new VertexGradient(
                upper, upper, lower, lower
            );

            // 두꺼운 외곽선과 이중 그림자 대신
            // 얇고 어두운 외곽선만 적용한다.
            text.outlineColor =
                new Color(0.12f, 0.09f, 0.19f, 0.85f);

            text.outlineWidth = 0.075f;

            // The original player writes signed FAST/LATE milliseconds as a
            // legacy Text child. Its main verdict has been replaced by TMP,
            // so move the readout into the same animated, fading visual.
            // This keeps the device-error text readable for the full verdict
            // animation instead of hiding it behind the larger TMP glyphs.
            Text legacyTiming = GetChildText(root, "InputTimingError");
            if (legacyTiming != null &&
                !string.IsNullOrEmpty(legacyTiming.text))
            {
                RectTransform timingRect = NewRect(
                    "RhythmTimingText", visual
                );
                timingRect.sizeDelta = new Vector2(220f, 24f);
                timingRect.anchoredPosition = new Vector2(0f, -36f);

                TextMeshProUGUI timingText =
                    timingRect.gameObject.AddComponent<TextMeshProUGUI>();
                timingText.font = RuruConsoleUI.Font();
                timingText.text = legacyTiming.text;
                timingText.fontSize = 18f;
                timingText.fontStyle = FontStyles.Normal;
                timingText.alignment = TextAlignmentOptions.Center;
                timingText.enableAutoSizing = false;
                timingText.enableWordWrapping = false;
                timingText.raycastTarget = false;
                timingText.color = new Color(.84f, .81f, .89f, .94f);

                // The source remains for the gameplay code to update its
                // fade and destroy it with the parent feedback object.
                legacyTiming.enabled = false;
            }

            judgements.Add(new JudgementAppearance
            {
                id = id,
                root = root,
                visual = visual,
                group = group,
                initialRootPosition = root.anchoredPosition,
                time = 0f
            });
        }

        private void UpdateNotes(float dt)
        {
            for (int i = notes.Count - 1; i >= 0; i--)
            {
                NoteAppearance note = notes[i];

                if (note.root == null)
                {
                    knownNotes.Remove(note.id);
                    notes.RemoveAt(i);
                    continue;
                }

                note.time += dt;

                float amount = EaseOutCubic(
                    Mathf.Clamp01(
                        note.time / noteAppearSeconds
                    )
                );

                float alpha = Mathf.Lerp(
                    noteStartOpacity,
                    1f,
                    amount
                );

                // 노트 본체만 등장 연출.
                // 접근 원은 기존 박자 애니메이션을 유지한다.
                for (int j = 0; j < note.images.Count; j++)
                {
                    if (note.images[j] == null)
                        continue;

                    Color color = note.colors[j];
                    color.a *= alpha;

                    note.images[j].color = color;
                }

                if (note.label != null)
                {
                    Color color = note.label.color;
                    color.a = alpha;

                    note.label.color = color;
                }
            }
        }

        private void UpdateJudgements(float dt)
        {
            for (
                int i = judgements.Count - 1;
                i >= 0;
                i--
            )
            {
                JudgementAppearance fx = judgements[i];

                if (fx.root == null ||
                    fx.visual == null)
                {
                    knownJudgements.Remove(fx.id);
                    judgements.RemoveAt(i);
                    continue;
                }

                fx.time += dt;

                float t = fx.time;
                float scale;

                // 0~75ms: 크게 등장한 뒤 빠르게 축소.
                if (t < 0.075f)
                {
                    scale = Mathf.Lerp(
                        judgementStartScale,
                        0.965f,
                        EaseOutCubic(t / 0.075f)
                    );
                }
                // 75~145ms: 살짝 되돌아와 원래 크기에 정착.
                else if (t < 0.145f)
                {
                    scale = Mathf.Lerp(
                        0.965f,
                        1f,
                        EaseOutCubic(
                            (t - 0.075f) / 0.07f
                        )
                    );
                }
                else
                {
                    scale = 1f;
                }

                float holdUntil = Mathf.Max(
                    0.16f,
                    judgementDuration - 0.17f
                );

                float opacity;

                if (t < 0.018f)
                {
                    opacity = EaseOutCubic(
                        t / 0.018f
                    );
                }
                else if (t < holdUntil)
                {
                    opacity = 1f;
                }
                else
                {
                    opacity = 1f - EaseOutCubic(
                        (t - holdUntil) / 0.17f
                    );
                }

                // 기존 재생기가 판정 글자를
                // 위로 움직여도 최초 위치에 고정한다.
                // 따라서 노트 바로 아래에서 움직이지 않는다.
                fx.visual.anchoredPosition =
                    fx.initialRootPosition -
                    fx.root.anchoredPosition;

                fx.visual.localScale =
                    Vector3.one * scale;

                fx.group.alpha =
                    Mathf.Clamp01(opacity);
            }
        }

        private static void AddNotePart(
            NoteAppearance note,
            Image image
        )
        {
            if (image == null)
                return;

            note.images.Add(image);
            note.colors.Add(image.color);
        }

        private static Image GetChildImage(
            RectTransform parent,
            string name
        )
        {
            Transform item = parent.Find(name);

            return item != null
                ? item.GetComponent<Image>()
                : null;
        }

        private static Text GetChildText(
            RectTransform parent,
            string name
        )
        {
            Transform item = parent.Find(name);

            return item != null
                ? item.GetComponent<Text>()
                : null;
        }

        private static RectTransform NewRect(
            string name,
            Transform parent
        )
        {
            GameObject obj = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer)
            );

            RectTransform rt =
                obj.GetComponent<RectTransform>();

            rt.SetParent(parent, false);

            rt.anchorMin =
                rt.anchorMax =
                rt.pivot =
                    new Vector2(0.5f, 0.5f);

            rt.anchoredPosition = Vector2.zero;

            return rt;
        }

        private static void GetJudgementGradient(
            string result, out Color top, out Color bottom)
        {
            switch (result)
            {
                case "PERFECT":
                    top = new Color32(255, 249, 228, 255);
                    bottom = new Color32(196, 145, 238, 255);
                    return;
                case "GREAT":
                    top = new Color32(245, 255, 255, 255);
                    bottom = new Color32(111, 201, 236, 255);
                    return;
                case "GOOD":
                    top = new Color32(255, 248, 213, 255);
                    bottom = new Color32(245, 178, 108, 255);
                    return;
                default:
                    top = new Color32(255, 231, 234, 255);
                    bottom = new Color32(237, 110, 142, 255);
                    return;
            }
        }

        private static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);

            float inv = 1f - t;

            return 1f - inv * inv * inv;
        }
    }
}
