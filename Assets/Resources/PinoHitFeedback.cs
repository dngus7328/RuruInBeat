using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 기반 피노 피격 연출. BulletDirector에서 Play()를 호출한다.
/// SpriteRenderer/Image 중 피노에 실제로 존재하는 비주얼을 잠깐 붉게 바꾸고,
/// 피노 발 아래에 HIT 글자를 띄운다. 별도의 안정화 HUD나 Rigidbody2D는 필요 없다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PinoHitFeedback : MonoBehaviour
{
    [Header("피격 사운드 - 비워두면 Resources/LuluPinoHit 사용")]
    [SerializeField] private AudioClip hitClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.65f;
    [Header("피격 연출")]
    [SerializeField, Range(0.05f, 1f)] private float redDuration = 0.20f;
    [SerializeField, Range(0.15f, 1.5f)] private float textDuration = 0.62f;
    [SerializeField] private Color redTint = new Color(1f, 0.22f, 0.28f, 1f);
    [SerializeField] private int hitFontSize = 32;
    [SerializeField, Min(0f)] private float textBelowFeet = 12f;

    private AudioSource source;
    private RectTransform target;
    private RectTransform popup;
    private Text popupText;
    private CanvasGroup popupFade;
    private readonly Dictionary<Graphic, Color> graphicOriginal = new Dictionary<Graphic, Color>();
    private readonly Dictionary<SpriteRenderer, Color> spriteOriginal = new Dictionary<SpriteRenderer, Color>();
    private float hitAt = -100f;
    private bool tintActive;
    private NoteSoundManager noteAudio;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        if (source == null) source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        if (hitClip == null) hitClip = Resources.Load<AudioClip>("LuluPinoHit");
    }

    public void Play(RectTransform pino)
    {
        if (pino == null) return;
        if (target != pino)
        {
            RestoreColors();
            target = pino;
            RebuildTargets();
        }
        hitAt = Time.unscaledTime;
        if (hitClip != null && source != null)
        {
            float gain = Mathf.Clamp01(volume);
            SaveManager save = SaveManager.Instance;
            if (save != null && save.Data != null)
                gain *= Mathf.Clamp01(save.Data.masterVolume) *
                    Mathf.Clamp01(save.Data.sfxVolume);
            if (gain > 0f) source.PlayOneShot(hitClip, gain);
        }
        else
        {
            if (noteAudio == null)
                noteAudio = NoteSoundManager.EnsureForScene(gameObject.scene);
            if (noteAudio != null)
                noteAudio.PlayPinoHit();
        }
        ShowPopup();
        tintActive = true;
        ApplyRed();
    }

    private void RebuildTargets()
    {
        graphicOriginal.Clear();
        spriteOriginal.Clear();
        if (target == null) return;
        foreach (Graphic g in target.GetComponentsInChildren<Graphic>(true))
            if (g != null && g is Image) graphicOriginal[g] = g.color;
        foreach (SpriteRenderer s in target.GetComponentsInChildren<SpriteRenderer>(true))
            if (s != null) spriteOriginal[s] = s.color;
    }

    private void ApplyRed()
    {
        foreach (var pair in graphicOriginal)
            if (pair.Key != null) pair.Key.color = MultiplyTint(pair.Value, redTint);
        foreach (var pair in spriteOriginal)
            if (pair.Key != null) pair.Key.color = MultiplyTint(pair.Value, redTint);
    }

    private static Color MultiplyTint(Color original, Color tint)
    {
        return new Color(original.r * tint.r, original.g * tint.g,
            original.b * tint.b, original.a);
    }

    private void RestoreColors()
    {
        foreach (var pair in graphicOriginal)
            if (pair.Key != null) pair.Key.color = pair.Value;
        foreach (var pair in spriteOriginal)
            if (pair.Key != null) pair.Key.color = pair.Value;
    }

    private void ShowPopup()
    {
        if (target == null) return;
        if (popup == null)
        {
            GameObject go = new GameObject("PinoHIT_Visual", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(CanvasGroup), typeof(Text));
            popup = go.GetComponent<RectTransform>();
            popup.SetParent(target.parent, false); // 피노 스프라이트에 색상 적용되지 않도록 형제로 생성
            popup.sizeDelta = new Vector2(150f, 52f);
            popupFade = go.GetComponent<CanvasGroup>();
            popupText = go.GetComponent<Text>();
            popupText.text = "HIT";
            popupText.alignment = TextAnchor.MiddleCenter;
            popupText.fontSize = hitFontSize;
            popupText.fontStyle = FontStyle.Bold;
            popupText.color = new Color(1f, 0.32f, 0.36f, 1f);
            popupText.raycastTarget = false;
            // Unity 버전별 내장 폰트 경로 차이를 피하기 위해 OS 글꼴 사용
            popupText.font = Font.CreateDynamicFontFromOSFont("Arial", hitFontSize);
        }
        popup.gameObject.SetActive(true);
        popup.SetAsLastSibling();
        popup.anchoredPosition = GetBelowPinoPosition();
        popupFade.alpha = 1f;
    }

    private void LateUpdate()
    {
        float elapsed = Time.unscaledTime - hitAt;
        if (tintActive)
        {
            if (elapsed < redDuration) ApplyRed();
            else
            {
                RestoreColors();
                tintActive = false;
            }
        }

        if (popup == null || target == null || !popup.gameObject.activeSelf) return;
        if (elapsed >= textDuration)
        {
            popup.gameObject.SetActive(false);
            return;
        }
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, textDuration));
        popup.anchoredPosition = GetBelowPinoPosition() +
            new Vector2(0f, -10f * (1f - Mathf.Pow(1f - t, 2f)));
        popupFade.alpha = 1f - Mathf.SmoothStep(0.35f, 1f, t);
    }

    // 피노 RectTransform의 발 아래 위치를 부모 로컬 좌표로 환산한다.
    // Pivot이 중앙이 아니거나 피노가 움직여도 아래쪽을 따라간다.
    private Vector2 GetBelowPinoPosition()
    {
        if (target == null || popup == null || popup.parent == null)
            return Vector2.zero;
        Vector3 feetWorld = target.TransformPoint(
            new Vector3(target.rect.center.x, target.rect.yMin, 0f));
        Vector3 feetLocal = popup.parent.InverseTransformPoint(feetWorld);
        RectTransform parentRect = popup.parent as RectTransform;
        if (parentRect != null)
        {
            // popup의 앵커가 중앙일 때 anchoredPosition의 원점 보정
            Vector2 anchorCenter = (popup.anchorMin + popup.anchorMax) * 0.5f;
            Vector2 anchorOrigin = new Vector2(
                parentRect.rect.xMin + parentRect.rect.width * anchorCenter.x,
                parentRect.rect.yMin + parentRect.rect.height * anchorCenter.y);
            return (Vector2)feetLocal - anchorOrigin + Vector2.down * (popup.rect.height * 0.5f + textBelowFeet);
        }
        return (Vector2)feetLocal + Vector2.down * (popup.rect.height * 0.5f + textBelowFeet);
    }

    private void OnDisable() { RestoreColors(); tintActive = false; if (popup != null) popup.gameObject.SetActive(false); }
    private void OnDestroy() { RestoreColors(); if (popup != null) Destroy(popup.gameObject); }
}
