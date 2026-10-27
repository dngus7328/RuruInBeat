using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ComboDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform digitRoot;
    [SerializeField] private GameObject comboLabel;

    [Header("Digit Sprites - 0 to 9")]
    [SerializeField] private Sprite[] digitSprites = new Sprite[10];

    [Header("Display")]
    [SerializeField] private int minimumDisplayCombo = 1;

    [Header("Digit Size")]
    [Tooltip("0번 숫자 Sprite의 높이를 기준으로 화면 크기를 맞춥니다.")]
    [SerializeField] private float digitHeight = 95f;

    [SerializeField] private float digitSpacing = -6f;

    [Header("Animation")]
    [SerializeField] private float popScale = 1.15f;
    [SerializeField] private float settleScale = 1f;
    [SerializeField] private float popDuration = 0.09f;

    private readonly List<Image> digitImages =
        new List<Image>();

    private Coroutine animationRoutine;

    private void Awake()
    {
        Hide();
    }

    public void SetCombo(int combo)
    {
        if (combo < minimumDisplayCombo)
        {
            Hide();
            return;
        }

        if (comboLabel != null)
        {
            comboLabel.SetActive(true);
        }

        BuildNumber(combo);

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
        }

        animationRoutine =
            StartCoroutine(
                PlayPopAnimation()
            );
    }

    public void ResetCombo()
    {
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);

            animationRoutine = null;
        }

        Hide();
    }

    private void BuildNumber(int combo)
    {
        ClearDigits();

        if (digitRoot == null)
            return;

        if (digitSprites == null ||
            digitSprites.Length < 10)
            return;

        string value =
            combo.ToString();

        float referenceHeight =
            GetReferenceSpriteHeight();

        if (referenceHeight <= 0f)
            return;

        // 모든 숫자가 동일한 "원본 픽셀 배율"을 사용한다.
        float pixelScale =
            digitHeight /
            referenceHeight;

        List<float> widths =
            new List<float>();

        List<float> heights =
            new List<float>();

        float totalWidth = 0f;

        for (int i = 0;
             i < value.Length;
             i++)
        {
            int digit =
                value[i] - '0';

            Sprite sprite =
                GetDigitSprite(digit);

            if (sprite == null)
            {
                widths.Add(0f);
                heights.Add(0f);
                continue;
            }

            // 중요:
            // 각 숫자를 별도로 95 높이에 맞추는 게 아니라
            // 같은 pixelScale을 사용한다.
            float width =
                sprite.rect.width *
                pixelScale;

            float height =
                sprite.rect.height *
                pixelScale;

            widths.Add(width);
            heights.Add(height);

            totalWidth += width;
        }

        if (value.Length > 1)
        {
            totalWidth +=
                digitSpacing *
                (value.Length - 1);
        }

        float currentX =
            -totalWidth * 0.5f;

        for (int i = 0;
             i < value.Length;
             i++)
        {
            int digit =
                value[i] - '0';

            Sprite sprite =
                GetDigitSprite(digit);

            if (sprite == null)
                continue;

            float width =
                widths[i];

            float height =
                heights[i];

            GameObject digitObject =
                new GameObject(
                    $"Digit_{digit}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image)
                );

            digitObject.transform.SetParent(
                digitRoot,
                false
            );

            RectTransform rect =
                digitObject.GetComponent<RectTransform>();

            Image image =
                digitObject.GetComponent<Image>();

            image.sprite =
                sprite;

            image.preserveAspect =
                false;

            image.raycastTarget =
                false;

            rect.anchorMin =
                new Vector2(
                    0.5f,
                    0.5f
                );

            rect.anchorMax =
                new Vector2(
                    0.5f,
                    0.5f
                );

            rect.pivot =
                new Vector2(
                    0.5f,
                    0.5f
                );

            rect.sizeDelta =
                new Vector2(
                    width,
                    height
                );

            rect.anchoredPosition =
                new Vector2(
                    currentX +
                    width * 0.5f,
                    0f
                );

            currentX +=
                width +
                digitSpacing;

            digitImages.Add(
                image
            );
        }
    }

    private float GetReferenceSpriteHeight()
    {
        // 0번 Sprite를 기준 크기로 사용
        if (digitSprites != null &&
            digitSprites.Length > 0 &&
            digitSprites[0] != null)
        {
            return digitSprites[0].rect.height;
        }

        // 혹시 0이 비어 있으면
        // 첫 번째 유효한 Sprite 사용
        foreach (Sprite sprite in digitSprites)
        {
            if (sprite != null)
            {
                return sprite.rect.height;
            }
        }

        return 0f;
    }

    private Sprite GetDigitSprite(int digit)
    {
        if (digitSprites == null)
            return null;

        if (digit < 0 ||
            digit >= digitSprites.Length)
            return null;

        return digitSprites[digit];
    }

    private void ClearDigits()
    {
        foreach (Image image in digitImages)
        {
            if (image != null)
            {
                Destroy(
                    image.gameObject
                );
            }
        }

        digitImages.Clear();
    }

    private IEnumerator PlayPopAnimation()
    {
        if (digitRoot == null)
            yield break;

        digitRoot.localScale =
            Vector3.one *
            popScale;

        float elapsed = 0f;

        while (elapsed < popDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    popDuration
                );

            float eased =
                1f -
                Mathf.Pow(
                    1f - t,
                    3f
                );

            float scale =
                Mathf.Lerp(
                    popScale,
                    settleScale,
                    eased
                );

            digitRoot.localScale =
                Vector3.one *
                scale;

            yield return null;
        }

        digitRoot.localScale =
            Vector3.one *
            settleScale;

        animationRoutine = null;
    }

    private void Hide()
    {
        ClearDigits();

        if (comboLabel != null)
        {
            comboLabel.SetActive(false);
        }

        if (digitRoot != null)
        {
            digitRoot.localScale =
                Vector3.one;
        }
    }
}