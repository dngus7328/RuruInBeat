using System.Collections.Generic;
using UnityEngine;

public class LongNoteVisual : MonoBehaviour
{
    // 기존 프리팹 직렬화 데이터 보존
    [SerializeField, HideInInspector] private Sprite headSprite;
    [SerializeField, HideInInspector] private Sprite bodySprite;
    [SerializeField, HideInInspector] private Sprite tailSprite;

    [Header("Editable Visual References")]
    [SerializeField] private SpriteRenderer headRenderer;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private SpriteRenderer tailRenderer;

    [Header("Body")]
    [Tooltip("center 원본 전체가 아니라 중앙의 얇은 띠만 Body로 사용합니다.")]
    [Range(1, 24)]
    [SerializeField] private int bodyBandPixels = 4;

    [Min(0.01f)]
    [SerializeField] private float bodyWidthMultiplier = 1f;

    [Tooltip("Body를 Head/Tail 중심보다 조금 더 안쪽으로 넣습니다.")]
    [Min(0f)]
    [SerializeField] private float capUnderlap = 0.02f;

    [Min(0.001f)]
    [SerializeField] private float minimumBodyWorldLength = 0.01f;

    [Header("Rendering")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 10;

    [Header("Hold Feedback")]
    [SerializeField]
    private Color holdColor =
        new Color(0.90f, 0.82f, 1f, 1f);

    [Range(0f, 1f)]
    [SerializeField] private float holdGlowAmount = 0.20f;

    [SerializeField] private float holdGlowSpeed = 5f;

    private float holdWorldLength;
    private bool holding;
    private Color originalHeadColor = Color.white;

    private Sprite sourceBodySprite;
    private float sourceBodyWidth;

    private static readonly Dictionary<long, Sprite>
        bodyBandCache =
            new Dictionary<long, Sprite>();

    private void Awake()
    {
        AutoAssignExistingChildren();
        MigrateLegacySpritesIfNeeded();
        RememberSourceBody();
        ApplyRendererSetup();

        if (headRenderer != null)
        {
            originalHeadColor =
                headRenderer.color;
        }
    }

    private void Reset()
    {
        AutoAssignExistingChildren();
    }

    private void OnValidate()
    {
        bodyBandPixels =
            Mathf.Clamp(
                bodyBandPixels,
                1,
                24
            );

        bodyWidthMultiplier =
            Mathf.Max(
                0.01f,
                bodyWidthMultiplier
            );

        capUnderlap =
            Mathf.Max(
                0f,
                capUnderlap
            );

        minimumBodyWorldLength =
            Mathf.Max(
                0.001f,
                minimumBodyWorldLength
            );

        AutoAssignExistingChildren();
        MigrateLegacySpritesIfNeeded();
        ApplyRendererSetup();
    }

    private void Update()
    {
        if (!holding ||
            headRenderer == null)
        {
            return;
        }

        float wave =
            (
                Mathf.Sin(
                    Time.unscaledTime *
                    holdGlowSpeed
                ) +
                1f
            ) *
            0.5f;

        headRenderer.color =
            Color.Lerp(
                originalHeadColor,
                holdColor,
                wave * holdGlowAmount
            );
    }

    // =========================================================
    // LongNoteRuntime API
    // =========================================================

    public void BuildForWorldLength(
        float worldLength
    )
    {
        AutoAssignExistingChildren();
        MigrateLegacySpritesIfNeeded();

        if (!ValidateParts())
            return;

        RemoveLegacyRuntimeObjects();

        RememberSourceBody();
        PrepareBodyBand();
        ApplyRendererSetup();

        holdWorldLength =
            Mathf.Max(
                0.01f,
                worldLength
            );

        headRenderer.enabled = true;
        bodyRenderer.enabled = true;
        tailRenderer.enabled = true;

        originalHeadColor =
            headRenderer.color;

        holding = false;

        /*
         * LongNote Root의 위치가 Head 위치다.
         * Head만 0에 고정하고 Body/Tail은 WORLD 좌표로 맞춘다.
         * 서로 다른 부모/로컬 좌표를 사용해도 틀어지지 않는다.
         */
        Vector3 headLocal =
            headRenderer.transform.localPosition;

        headLocal.y = 0f;

        headRenderer.transform.localPosition =
            headLocal;

        AlignXToHeadWorld();

        SetStreamTravelDistance(
            0f
        );
    }

    public void SetStreamTravelDistance(
        float worldDistance
    )
    {
        if (headRenderer == null ||
            bodyRenderer == null ||
            tailRenderer == null)
        {
            return;
        }

        worldDistance =
            Mathf.Clamp(
                worldDistance,
                0f,
                holdWorldLength
            );

        float remainingWorld =
            Mathf.Max(
                0f,
                holdWorldLength -
                worldDistance
            );

        /*
         * 핵심:
         * Sprite.bounds / 투명 여백을 연결 기준으로 쓰지 않는다.
         *
         * Head 중심 ---- Body(뒤) ---- Tail 중심
         *
         * Body가 두 Cap 아래까지 들어가고,
         * Head/Tail의 sortingOrder가 더 높아서 접합부를 덮는다.
         * 따라서 Cap 이미지 내부 여백 때문에 틈이 생길 수 없다.
         */
        Vector3 headWorld =
            headRenderer.transform.position;

        Vector3 tailWorld =
            tailRenderer.transform.position;

        tailWorld.x =
            headWorld.x;

        tailWorld.y =
            headWorld.y +
            remainingWorld;

        tailRenderer.transform.position =
            tailWorld;

        if (remainingWorld <=
            minimumBodyWorldLength)
        {
            bodyRenderer.enabled =
                false;

            return;
        }

        bodyRenderer.enabled = true;

        float bodyBottomWorld =
            headWorld.y -
            capUnderlap;

        float bodyTopWorld =
            tailWorld.y +
            capUnderlap;

        SetBodyBetweenWorld(
            bodyBottomWorld,
            bodyTopWorld,
            headWorld.x
        );
    }

    public void BeginHoldVisual()
    {
        holding = true;
    }

    public void EndHoldVisual()
    {
        holding = false;

        if (headRenderer != null)
        {
            headRenderer.color =
                originalHeadColor;
        }
    }

    public void ResetHoldVisual()
    {
        holding = false;

        if (headRenderer != null)
        {
            headRenderer.enabled = true;
            headRenderer.color =
                originalHeadColor;
        }

        if (bodyRenderer != null)
        {
            bodyRenderer.enabled = true;
        }

        if (tailRenderer != null)
        {
            tailRenderer.enabled = true;
        }

        SetStreamTravelDistance(
            0f
        );
    }

    public void HideAll()
    {
        holding = false;

        if (headRenderer != null)
            headRenderer.enabled = false;

        if (bodyRenderer != null)
            bodyRenderer.enabled = false;

        if (tailRenderer != null)
            tailRenderer.enabled = false;
    }

    // =========================================================
    // Body
    // =========================================================

    private void RememberSourceBody()
    {
        if (bodyRenderer == null ||
            bodyRenderer.sprite == null)
        {
            return;
        }

        /*
         * 이미 런타임 band sprite로 바뀐 뒤 다시 Build되더라도
         * 원본을 덮어쓰지 않는다.
         */
        if (sourceBodySprite == null)
        {
            sourceBodySprite =
                bodyRenderer.sprite;

            sourceBodyWidth =
                sourceBodySprite.bounds.size.x;
        }
    }

    private void PrepareBodyBand()
    {
        if (bodyRenderer == null)
            return;

        if (sourceBodySprite == null)
        {
            RememberSourceBody();
        }

        if (sourceBodySprite == null)
            return;

        Sprite band =
            GetOrCreateBodyBand(
                sourceBodySprite,
                bodyBandPixels
            );

        if (band != null)
        {
            bodyRenderer.sprite =
                band;
        }

        bodyRenderer.drawMode =
            SpriteDrawMode.Tiled;

        bodyRenderer.tileMode =
            SpriteTileMode.Continuous;

        Vector2 size =
            bodyRenderer.size;

        size.x =
            Mathf.Max(
                0.001f,
                sourceBodyWidth *
                bodyWidthMultiplier
            );

        bodyRenderer.size =
            size;
    }

    private Sprite GetOrCreateBodyBand(
        Sprite source,
        int requestedBandPixels
    )
    {
        if (source == null ||
            source.texture == null)
        {
            return source;
        }

        int bandPixels =
            Mathf.Max(
                1,
                requestedBandPixels
            );

        long key =
            ((long)source.GetInstanceID() << 32) ^
            (uint)bandPixels;

        if (bodyBandCache.TryGetValue(
                key,
                out Sprite cached
            ) &&
            cached != null)
        {
            return cached;
        }

        Rect textureRect;

        try
        {
            textureRect =
                source.textureRect;
        }
        catch
        {
            return source;
        }

        float bandHeight =
            Mathf.Clamp(
                bandPixels,
                1f,
                textureRect.height
            );

        Rect bandRect =
            new Rect(
                textureRect.x,
                textureRect.y +
                    (
                        textureRect.height -
                        bandHeight
                    ) *
                    0.5f,
                textureRect.width,
                bandHeight
            );

        /*
         * Texture 픽셀을 읽거나 복사하지 않는다.
         * 같은 Texture의 중앙 띠만 Sprite Rect로 참조한다.
         */
        Sprite band =
            Sprite.Create(
                source.texture,
                bandRect,
                new Vector2(
                    0.5f,
                    0.5f
                ),
                source.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect
            );

        band.name =
            source.name +
            "_LongBodyBand";

        band.hideFlags =
            HideFlags.HideAndDontSave;

        bodyBandCache[key] =
            band;

        return band;
    }

    private void SetBodyBetweenWorld(
        float bottomWorld,
        float topWorld,
        float worldX
    )
    {
        if (bodyRenderer == null ||
            bodyRenderer.sprite == null)
        {
            return;
        }

        float worldHeight =
            topWorld -
            bottomWorld;

        if (worldHeight <=
            minimumBodyWorldLength)
        {
            bodyRenderer.enabled =
                false;

            return;
        }

        float scaleY =
            Mathf.Abs(
                bodyRenderer.transform.lossyScale.y
            );

        if (scaleY <
            0.0001f)
        {
            scaleY = 1f;
        }

        Vector2 size =
            bodyRenderer.size;

        size.x =
            Mathf.Max(
                0.001f,
                sourceBodyWidth *
                bodyWidthMultiplier
            );

        size.y =
            worldHeight /
            scaleY;

        bodyRenderer.size =
            size;

        /*
         * Body band는 pivot 0.5로 생성되므로
         * WORLD 중앙에 정확히 놓는다.
         */
        Vector3 bodyWorld =
            bodyRenderer.transform.position;

        bodyWorld.x =
            worldX;

        bodyWorld.y =
            (
                bottomWorld +
                topWorld
            ) *
            0.5f;

        bodyRenderer.transform.position =
            bodyWorld;
    }

    // =========================================================
    // Rendering / hierarchy
    // =========================================================

    private void ApplyRendererSetup()
    {
        if (bodyRenderer != null)
        {
            bodyRenderer.sortingLayerName =
                sortingLayerName;

            bodyRenderer.sortingOrder =
                sortingOrder;

            bodyRenderer.maskInteraction =
                SpriteMaskInteraction.None;
        }

        if (tailRenderer != null)
        {
            tailRenderer.sortingLayerName =
                sortingLayerName;

            tailRenderer.sortingOrder =
                sortingOrder + 2;

            tailRenderer.maskInteraction =
                SpriteMaskInteraction.None;
        }

        if (headRenderer != null)
        {
            headRenderer.sortingLayerName =
                sortingLayerName;

            headRenderer.sortingOrder =
                sortingOrder + 3;

            headRenderer.maskInteraction =
                SpriteMaskInteraction.None;
        }
    }

    private void AlignXToHeadWorld()
    {
        if (headRenderer == null)
            return;

        float worldX =
            headRenderer.transform.position.x;

        if (bodyRenderer != null)
        {
            Vector3 p =
                bodyRenderer.transform.position;

            p.x =
                worldX;

            bodyRenderer.transform.position =
                p;
        }

        if (tailRenderer != null)
        {
            Vector3 p =
                tailRenderer.transform.position;

            p.x =
                worldX;

            tailRenderer.transform.position =
                p;
        }
    }

    private void AutoAssignExistingChildren()
    {
        if (headRenderer == null)
        {
            Transform child =
                transform.Find("Head");

            if (child != null)
            {
                headRenderer =
                    child.GetComponent<SpriteRenderer>();
            }
        }

        if (bodyRenderer == null)
        {
            Transform child =
                transform.Find("Body");

            if (child != null)
            {
                bodyRenderer =
                    child.GetComponent<SpriteRenderer>();
            }
        }

        if (tailRenderer == null)
        {
            Transform child =
                transform.Find("Tail");

            if (child != null)
            {
                tailRenderer =
                    child.GetComponent<SpriteRenderer>();
            }
        }
    }

    private void MigrateLegacySpritesIfNeeded()
    {
        if (headRenderer != null &&
            headRenderer.sprite == null &&
            headSprite != null)
        {
            headRenderer.sprite =
                headSprite;
        }

        if (bodyRenderer != null &&
            bodyRenderer.sprite == null &&
            bodySprite != null)
        {
            bodyRenderer.sprite =
                bodySprite;
        }

        if (tailRenderer != null &&
            tailRenderer.sprite == null &&
            tailSprite != null)
        {
            tailRenderer.sprite =
                tailSprite;
        }
    }

    private bool ValidateParts()
    {
        if (headRenderer == null ||
            bodyRenderer == null ||
            tailRenderer == null)
        {
            Debug.LogError(
                "LongNoteVisual: Head / Body / Tail SpriteRenderer가 없습니다.",
                this
            );

            return false;
        }

        if (headRenderer.sprite == null ||
            bodyRenderer.sprite == null ||
            tailRenderer.sprite == null)
        {
            Debug.LogError(
                "LongNoteVisual: Head / Body / Tail 중 Sprite가 비어 있습니다.",
                this
            );

            return false;
        }

        return true;
    }

    private void RemoveLegacyRuntimeObjects()
    {
        string[] names =
        {
            "Generated",
            "RuntimeStreamMask",
            "LongNoteStreamMask",
            "StreamMask",
            "Stream"
        };

        for (int i = 0;
             i < names.Length;
             i++)
        {
            Transform existing =
                transform.Find(
                    names[i]
                );

            if (existing == null)
                continue;

            existing.gameObject.SetActive(
                false
            );

            if (Application.isPlaying)
            {
                Destroy(
                    existing.gameObject
                );
            }
            else
            {
                DestroyImmediate(
                    existing.gameObject
                );
            }
        }
    }

    [ContextMenu("Create / Repair Editable Visuals")]
    public void CreateOrRepairEditableVisuals()
    {
        AutoAssignExistingChildren();
        MigrateLegacySpritesIfNeeded();

        if (!ValidateParts())
            return;

        RememberSourceBody();
        PrepareBodyBand();
        ApplyRendererSetup();
        AlignXToHeadWorld();
    }

    [ContextMenu("Clear Legacy Generated")]
    public void ClearGenerated()
    {
        RemoveLegacyRuntimeObjects();
        holding = false;
    }
}
