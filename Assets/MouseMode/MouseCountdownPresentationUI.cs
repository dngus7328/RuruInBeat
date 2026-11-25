
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LuluInBeat.MouseMode.ChartSystem
{
    /// <summary>
    /// NoteLayer에 추가합니다.
    /// 기존 StartCountdown의 시간과 활성 상태를 이용하고,
    /// 외형과 애니메이션만 변경합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class MouseCountdownPresentationUI : MonoBehaviour
    {
        [Header("폰트")]
        [SerializeField]
        private TMP_FontAsset countdownFont;

        [Header("시작 문구")]
        [SerializeField]
        private string readyLabel = "READY";

        [SerializeField]
        private string goLabel = "GO!";

        [Header("글자 크기")]
        [SerializeField, Range(30f, 120f)]
        private float readyFontSize = 86f;

        [SerializeField, Range(50f, 160f)]
        private float numberFontSize = 124f;

        [SerializeField, Range(40f, 140f)]
        private float goFontSize = 100f;

        [Header("글자 간격")]
        [SerializeField, Range(0f, 12f)]
        private float readyLetterSpacing = 7f;

        [SerializeField, Range(0f, 8f)]
        private float goLetterSpacing = 2f;

        [Header("색상")]
        [SerializeField]
        private Color readyColor =
            new Color(0.91f, 0.87f, 0.99f, 1f);

        [SerializeField]
        private Color numberColor =
            new Color(1f, 0.97f, 1f, 1f);

        [SerializeField]
        private Color goColor =
            new Color(0.79f, 0.92f, 1f, 1f);

        [SerializeField]
        private Color outlineColor =
            new Color(0.14f, 0.11f, 0.23f, 0.95f);

        private enum Phase
        {
            Ready,
            Number,
            Go
        }

        private Text source;
        private RectTransform visual;
        private TextMeshProUGUI label;
        private CanvasGroup group;

        private string lastSourceText;
        private float phaseElapsed;
        private Phase phase;

        private void LateUpdate()
        {
            if (!Application.isFocused ||
                Time.timeScale <= 0f ||
                AudioListener.pause)
            {
                return;
            }

            // 기존 재생기가 생성한 카운트다운을 찾습니다.
            if (source == null)
            {
                Transform root =
                    transform.Find("StartCountdown");

                if (root == null)
                    return;

                Text found =
                    root.GetComponent<Text>();

                if (found == null)
                    return;

                source = found;

                CreateVisual();

                lastSourceText = null;
            }

            // 기존 재생기가 카운트다운을 종료하면
            // 새 글씨도 함께 숨겨집니다.
            if (!source.gameObject.activeInHierarchy)
                return;

            // 기존 기본 UI Text는 보이지 않게 합니다.
            source.enabled = false;

            string state = source.text == null
                ? string.Empty
                : source.text.Trim();

            if (state != lastSourceText)
            {
                lastSourceText = state;
                phaseElapsed = 0f;

                SetPhase(state);
            }
            else
            {
                phaseElapsed += Time.deltaTime;
            }

            Animate();
        }

        private void CreateVisual()
        {
            GameObject obj = new GameObject(
                "Countdown_AnimatedTMP",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI),
                typeof(CanvasGroup)
            );

            visual =
                obj.GetComponent<RectTransform>();

            visual.SetParent(source.transform, false);

            visual.anchorMin =
                new Vector2(0.5f, 0.5f);

            visual.anchorMax =
                new Vector2(0.5f, 0.5f);

            visual.pivot =
                new Vector2(0.5f, 0.5f);

            visual.anchoredPosition =
                Vector2.zero;

            visual.sizeDelta =
                new Vector2(650f, 170f);

            label =
                obj.GetComponent<TextMeshProUGUI>();

            label.font = RuruConsoleUI.Font();

            label.fontStyle = FontStyles.Bold;
            label.enableAutoSizing = false;
            label.enableWordWrapping = false;

            label.alignment =
                TextAlignmentOptions.Center;

            label.raycastTarget = false;

            label.outlineColor = outlineColor;
            label.outlineWidth = 0.085f;

            group =
                obj.GetComponent<CanvasGroup>();

            group.interactable = false;
            group.blocksRaycasts = false;
        }

        private void SetPhase(string state)
        {
            if (state == "READY")
            {
                phase = Phase.Ready;

                label.text = readyLabel;
                label.fontSize = readyFontSize;
                label.characterSpacing = readyLetterSpacing;
                label.color = readyColor;
            }
            else if (state == "GO!" || state == "GO")
            {
                phase = Phase.Go;

                label.text = goLabel;
                label.fontSize = goFontSize;
                label.characterSpacing = goLetterSpacing;
                label.color = goColor;
            }
            else
            {
                phase = Phase.Number;

                label.text = state;
                label.fontSize = numberFontSize;
                label.characterSpacing = 0f;
                label.color = numberColor;
            }
        }

        private void Animate()
        {
            float time = phaseElapsed;

            float scale;
            float opacity;
            float y;

            switch (phase)
            {
                case Phase.Ready:
                    {
                        // READY:
                        // 살짝 작게 등장한 뒤 원래 크기로.
                        float progress =
                            OutCubic(time / 0.20f);

                        scale = Mathf.Lerp(
                            0.94f,
                            1f,
                            progress
                        );

                        y = Mathf.Lerp(
                            -7f,
                            0f,
                            progress
                        );

                        opacity =
                            Mathf.Clamp01(time / 0.12f);

                        break;
                    }

                case Phase.Number:
                    {
                        // 3, 2, 1:
                        // 박자마다 빠르게 축소합니다.
                        float progress =
                            OutCubic(time / 0.12f);

                        scale = Mathf.Lerp(
                            1.27f,
                            1f,
                            progress
                        );

                        y = 0f;

                        float appear =
                            Mathf.Clamp01(time / 0.035f);

                        float fade = 1f - Smooth01(
                            (time - 0.82f) / 0.16f
                        );

                        opacity = appear * fade;

                        break;
                    }

                default:
                    {
                        // GO!:
                        // 짧게 강조한 뒤 사라집니다.
                        float progress =
                            OutCubic(time / 0.12f);

                        scale = Mathf.Lerp(
                            1.15f,
                            1f,
                            progress
                        );

                        y = 0f;

                        float appear =
                            Mathf.Clamp01(time / 0.025f);

                        float fade = 1f - Smooth01(
                            (time - 0.28f) / 0.20f
                        );

                        opacity = appear * fade;

                        break;
                    }
            }

            visual.localScale =
                Vector3.one * scale;

            visual.anchoredPosition =
                new Vector2(0f, y);

            group.alpha =
                Mathf.Clamp01(opacity);
        }

        private static float OutCubic(float t)
        {
            t = Mathf.Clamp01(t);

            float inverse = 1f - t;

            return 1f -
                inverse * inverse * inverse;
        }

        private static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);

            return t * t * (3f - 2f * t);
        }

        private void OnDisable()
        {
            if (source != null)
                source.enabled = true;
        }

        private void OnEnable()
        {
            if (source != null)
                source.enabled = false;
        }

        private void OnDestroy()
        {
            if (source != null)
                source.enabled = true;

            if (visual != null)
                Destroy(visual.gameObject);
        }
    }
}
