
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(32000)]
public sealed class MouseTextShadowUI : MonoBehaviour
{
    [Header("TMP 검은 그림자")]
    [SerializeField, Range(0f, 1f)]
    private float shadowOpacity = 0.78f;

    [SerializeField, Range(-1f, 1f)]
    private float shadowOffsetX = 0.20f;

    [SerializeField, Range(-1f, 1f)]
    private float shadowOffsetY = -0.24f;

    [SerializeField, Range(-1f, 1f)]
    private float shadowDilate = 0.08f;

    [SerializeField, Range(0f, 1f)]
    private float shadowSoftness = 0.10f;

    [Header("기본 UI Text")]
    [SerializeField]
    private bool includeLegacyText = true;

    [SerializeField]
    private Vector2 legacyShadowOffset =
        new Vector2(2f, -3f);

    private static readonly int UnderlayColorId =
        Shader.PropertyToID("_UnderlayColor");

    private static readonly int UnderlayOffsetXId =
        Shader.PropertyToID("_UnderlayOffsetX");

    private static readonly int UnderlayOffsetYId =
        Shader.PropertyToID("_UnderlayOffsetY");

    private static readonly int UnderlayOffsetId =
        Shader.PropertyToID("_UnderlayOffset");

    private static readonly int UnderlayDilateId =
        Shader.PropertyToID("_UnderlayDilate");

    private static readonly int UnderlaySoftnessId =
        Shader.PropertyToID("_UnderlaySoftness");

    private readonly List<TMP_Text> tmpTexts =
        new List<TMP_Text>(32);

    private readonly List<Text> legacyTexts =
        new List<Text>(16);

    private readonly HashSet<TMP_Text> styledTmp =
        new HashSet<TMP_Text>();

    private readonly HashSet<Text> styledLegacy =
        new HashSet<Text>();

    private bool warnedAboutUnsupportedShader;

    private void LateUpdate()
    {
        // 일시정지 상태에서도 새 글자를 감지
        tmpTexts.Clear();

        GetComponentsInChildren(true, tmpTexts);

        foreach (TMP_Text text in tmpTexts)
        {
            if (text == null ||
                !text.enabled ||
                styledTmp.Contains(text))
            {
                continue;
            }

            if (ApplyTmpShadow(text))
            {
                styledTmp.Add(text);
            }
        }

        if (!includeLegacyText)
            return;

        legacyTexts.Clear();

        GetComponentsInChildren(true, legacyTexts);

        foreach (Text text in legacyTexts)
        {
            if (text == null ||
                !text.enabled ||
                styledLegacy.Contains(text))
            {
                continue;
            }

            Shadow shadow =
                text.GetComponent<Shadow>();

            if (shadow == null)
            {
                shadow =
                    text.gameObject.AddComponent<Shadow>();
            }

            shadow.effectColor = new Color(
                0f, 0f, 0f, shadowOpacity
            );

            shadow.effectDistance =
                legacyShadowOffset;

            shadow.useGraphicAlpha = true;

            styledLegacy.Add(text);
        }
    }

    private bool ApplyTmpShadow(TMP_Text text)
    {
        // 각 글자 오브젝트의 개별 머티리얼 사용
        Material material = text.fontMaterial;

        if (material == null ||
            !material.HasProperty(UnderlayColorId))
        {
            if (!warnedAboutUnsupportedShader)
            {
                Debug.LogWarning(
                    "[MouseTextShadowUI] " +
                    "일부 TMP 폰트가 Underlay를 지원하지 않습니다. " +
                    "Distance Field SDF 머티리얼을 사용하세요.",
                    this
                );

                warnedAboutUnsupportedShader = true;
            }

            return false;
        }

        // 검은 그림자 활성화
        material.EnableKeyword("UNDERLAY_ON");
        material.DisableKeyword("UNDERLAY_INNER");

        material.SetColor(
            UnderlayColorId,
            new Color(0f, 0f, 0f, shadowOpacity)
        );

        if (material.HasProperty(UnderlayOffsetXId) &&
            material.HasProperty(UnderlayOffsetYId))
        {
            material.SetFloat(
                UnderlayOffsetXId,
                shadowOffsetX
            );

            material.SetFloat(
                UnderlayOffsetYId,
                shadowOffsetY
            );
        }
        else if (material.HasProperty(UnderlayOffsetId))
        {
            material.SetVector(
                UnderlayOffsetId,
                new Vector4(
                    shadowOffsetX,
                    shadowOffsetY,
                    0f,
                    0f
                )
            );
        }

        if (material.HasProperty(UnderlayDilateId))
        {
            material.SetFloat(
                UnderlayDilateId,
                shadowDilate
            );
        }

        if (material.HasProperty(UnderlaySoftnessId))
        {
            material.SetFloat(
                UnderlaySoftnessId,
                shadowSoftness
            );
        }

        text.fontMaterial = material;

        text.extraPadding = true;
        text.UpdateMeshPadding();
        text.SetMaterialDirty();

        return true;
    }
}
