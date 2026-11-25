
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mouse 모드 NoteLayer에 부착하는 시각 효과 전용 스크립트.
/// 1) 판정 TMP 텍스트에 세로 그라데이션 적용
/// 2) 노트 위에 추가로 그려진 밝은 테두리 장식만 비표시
/// 채보, 판정, 오디오, 일시정지 및 기존 노트 오브젝트는 수정하지 않음.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(32010)]
public sealed class MouseNoteJudgementPolishUI : MonoBehaviour
{
    [Header("판정 글자 그라데이션")]
    [SerializeField] private bool useJudgementGradient = true;

    [Header("PERFECT: 위 / 아래")]
    [SerializeField] private Color perfectTop = new Color32(255, 249, 228, 255);
    [SerializeField] private Color perfectBottom = new Color32(196, 145, 238, 255);

    [Header("GREAT: 위 / 아래")]
    [SerializeField] private Color greatTop = new Color32(245, 255, 255, 255);
    [SerializeField] private Color greatBottom = new Color32(111, 201, 236, 255);

    [Header("GOOD: 위 / 아래")]
    [SerializeField] private Color goodTop = new Color32(255, 248, 213, 255);
    [SerializeField] private Color goodBottom = new Color32(245, 178, 108, 255);

    [Header("MISS: 위 / 아래")]
    [SerializeField] private Color missTop = new Color32(255, 231, 234, 255);
    [SerializeField] private Color missBottom = new Color32(237, 110, 142, 255);

    [Header("HIT: 위 / 아래")]
    [SerializeField] private Color hitTop = new Color32(255, 229, 232, 255);
    [SerializeField] private Color hitBottom = new Color32(223, 86, 115, 255);

    [Header("노트의 밝은 테두리 정리")]
    [SerializeField] private bool hidePaleNoteTrim = true;

    [Tooltip("노트 내부의 추가 장식 중, 밝은 회백색 원형 테두리만 숨깁니다.")]
    [SerializeField, Range(0.5f, 1f)] private float paleThreshold = 0.72f;

    [Header("검사 주기 (초)")]
    [SerializeField, Range(0.04f, 0.5f)] private float scanInterval = 0.08f;

    private float nextScanTime;
    private readonly List<Image> noteImages = new List<Image>(16);
    private readonly List<TMP_Text> judgeTexts = new List<TMP_Text>(8);
    private readonly HashSet<int> configuredJudgements = new HashSet<int>();

    private void LateUpdate()
    {
        if (Time.unscaledTime < nextScanTime)
            return;

        nextScanTime = Time.unscaledTime + scanInterval;

        // NoteLayer 직속 MouseNote_ 및 Judge_만 확인.
        // HUD, 시작/재개 카운트다운에는 영향을 주지 않습니다.
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform root = transform.GetChild(i);
            if (root == null)
                continue;

            if (hidePaleNoteTrim && root.name.StartsWith("MouseNote_", StringComparison.Ordinal))
            {
                CleanNoteTrim(root);
            }
            else if (useJudgementGradient && root.name.StartsWith("Judge_", StringComparison.Ordinal))
            {
                StyleJudgement(root);
            }
        }
    }

    private void CleanNoteTrim(Transform noteRoot)
    {
        noteImages.Clear();
        noteRoot.GetComponentsInChildren(true, noteImages);

        for (int i = 0; i < noteImages.Count; i++)
        {
            Image img = noteImages[i];
            if (img == null)
                continue;

            string n = img.gameObject.name;

            // 기존 원, 접근 링, 휠 회전점은 유지합니다.
            if (n == "ApproachRing" ||
                n == "HitTargetRing" ||
                n == "DarkOutline" ||
                n == "ColorBody" ||
                n == "InnerCircle" ||
                n == "RollingSpot")
            {
                // 다만 기존 노트에 나중에 추가된 밝은 Outline 효과만 제거.
                DisablePaleOutline(img);
                continue;
            }

            // 이전 노트 장식 스크립트에서 덧그렸을 수 있는
            // 흰색/밝은 회백색 테두리 이미지에만 적용합니다.
            bool rimName =
                ContainsIgnoreCase(n, "rim") ||
                ContainsIgnoreCase(n, "trim") ||
                ContainsIgnoreCase(n, "highlight") ||
                ContainsIgnoreCase(n, "timing") ||
                ContainsIgnoreCase(n, "stroke") ||
                ContainsIgnoreCase(n, "outline") ||
                ContainsIgnoreCase(n, "accent");

            // 오브젝트 이름이 달라도 노트 중앙의 큰 원형 밝은 장식이라면
            // 테두리일 가능성이 높습니다. 접근 원/본체는 위에서 제외됩니다.
            RectTransform partRect = img.rectTransform;
            RectTransform rootRect = noteRoot as RectTransform;
            bool centeredPaleCircle = false;

            if (partRect != null && rootRect != null && IsPale(img.color))
            {
                float noteSize = Mathf.Max(rootRect.rect.width, rootRect.rect.height);
                float partSize = Mathf.Max(partRect.rect.width, partRect.rect.height);
                float sizeRatio = noteSize > 0.01f ? partSize / noteSize : 0f;
                centeredPaleCircle =
                    sizeRatio >= 0.62f && sizeRatio <= 1.25f &&
                    partRect.anchoredPosition.sqrMagnitude < 100f;
            }

            if ((rimName || centeredPaleCircle) && IsPale(img.color))
                img.enabled = false;

            DisablePaleOutline(img);
        }
    }

    private void DisablePaleOutline(Image img)
    {
        Outline[] effects = img.GetComponents<Outline>();
        for (int i = 0; i < effects.Length; i++)
        {
            Outline outline = effects[i];
            if (outline != null && outline.enabled && IsPale(outline.effectColor))
                outline.enabled = false;
        }
    }

    private bool IsPale(Color c)
    {
        if (c.a < 0.15f)
            return false;

        float minimum = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
        float maximum = Mathf.Max(c.r, Mathf.Max(c.g, c.b));

        // 흰색, 회백색, 옅은 라벤더 계열만 대상.
        return minimum >= paleThreshold && maximum - minimum <= 0.24f;
    }

    private static bool ContainsIgnoreCase(string value, string part)
    {
        return value.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void StyleJudgement(Transform judgeRoot)
    {
        string verdict = judgeRoot.name.Substring("Judge_".Length).ToUpperInvariant();
        Color upper;
        Color lower;

        switch (verdict)
        {
            case "PERFECT":
                upper = perfectTop;
                lower = perfectBottom;
                break;
            case "GREAT":
                upper = greatTop;
                lower = greatBottom;
                break;
            case "GOOD":
                upper = goodTop;
                lower = goodBottom;
                break;
            case "MISS":
                upper = missTop;
                lower = missBottom;
                break;
            case "HIT":
                upper = hitTop;
                lower = hitBottom;
                break;
            default:
                return;
        }

        judgeTexts.Clear();
        judgeRoot.GetComponentsInChildren(true, judgeTexts);

        for (int i = 0; i < judgeTexts.Count; i++)
        {
            TMP_Text text = judgeTexts[i];
            // Only the main TMP verdict is graded. FAST/LATE is a small
            // neutral readout beneath it and must stay readable at all times.
            if (text == null || text.gameObject.name != "RhythmJudgementVisual" ||
                !configuredJudgements.Add(text.GetInstanceID()))
                continue;

            // 글자색(흰색)과 꼭짓점 그라데이션 색을 곱해 사용.
            // 알파는 보존해 기존 페이드 애니메이션을 방해하지 않음.
            Color oldColor = text.color;
            text.color = new Color(1f, 1f, 1f, oldColor.a);
            text.enableVertexGradient = true;
            text.colorGradient = new VertexGradient(
                upper, upper,
                lower, lower
            );
            text.SetVerticesDirty();
        }
    }

    private void OnDisable()
    {
        nextScanTime = 0f;
    }
}
