using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class RuruStoryStage : MonoBehaviour
{
    private Image backdrop, left, right;
    private Coroutine transition;
    private static readonly Vector2 LeftMin = new Vector2(.035f, .13f);
    private static readonly Vector2 LeftMax = new Vector2(.49f, .96f);
    private static readonly Vector2 RightMin = new Vector2(.51f, .13f);
    private static readonly Vector2 RightMax = new Vector2(.965f, .96f);

    public static RuruStoryStage Create(RectTransform parent)
    {
        GameObject go = new GameObject("AuthoredStoryStage", typeof(RectTransform),
            typeof(RectMask2D), typeof(RuruStoryStage));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.SetAsFirstSibling();
        RuruStoryStage stage = go.GetComponent<RuruStoryStage>();
        stage.backdrop = stage.MakeImage("Background", Vector2.zero, Vector2.one);
        stage.backdrop.preserveAspect = false;
        stage.left = stage.MakeImage("LeftPortrait", LeftMin, LeftMax);
        stage.right = stage.MakeImage("RightPortrait", RightMin, RightMax);
        stage.Clear();
        return stage;
    }

    private Image MakeImage(string label, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image image = go.GetComponent<Image>();
        image.raycastTarget = false; image.preserveAspect = true;
        image.enabled = false;
        return image;
    }

    public void Clear()
    {
        if (transition != null) StopCoroutine(transition);
        transition = null;
        foreach (Image image in new[] { backdrop, left, right })
        {
            if (image == null) continue;
            image.sprite = null; image.enabled = false;
            image.color = Color.white;
            image.rectTransform.anchoredPosition = Vector2.zero;
        }
    }

    public void Show(RuruStorySequence sequence, int index)
    {
        if (sequence == null || index < 0 || index >= sequence.Count)
        { Clear(); return; }
        if (transition != null) StopCoroutine(transition);
        RuruStorySequence.Line line = sequence.lines[index];
        if (line == null) { Clear(); return; }
        RuruStorySequence.Frame frame = sequence.Resolve(index);
        bool bgChanged = SetSprite(backdrop, frame.background);
        bool lChanged = SetSprite(left, frame.left);
        bool rChanged = SetSprite(right, frame.right);
        // Keep the active speaker above the other portrait without dimming either.
        if (line.speaking == RuruStorySequence.SpeakerPosition.Left) left.transform.SetAsLastSibling();
        else if (line.speaking == RuruStorySequence.SpeakerPosition.Right) right.transform.SetAsLastSibling();
        float duration = Mathf.Clamp(line.transitionSeconds, 0f, 2f);
        if (line.entrance != RuruStorySequence.Entrance.Immediate && duration > 0f)
            transition = StartCoroutine(Animate(line.entrance, duration, bgChanged, lChanged, rChanged));
        else transition = null;
    }

    private static bool SetSprite(Image image, Sprite sprite)
    {
        bool changed = image.sprite != sprite;
        image.sprite = sprite;
        image.enabled = sprite != null;
        image.color = Color.white;
        image.rectTransform.anchoredPosition = Vector2.zero;
        return changed && sprite != null;
    }

    private IEnumerator Animate(RuruStorySequence.Entrance mode, float duration,
        bool bg, bool l, bool r)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            t = 1f - Mathf.Pow(1f-t,3f);
            if (bg) Apply(backdrop,t,0f);
            if (l) Apply(left,t,mode == RuruStorySequence.Entrance.Slide ? -28f : 0f);
            if (r) Apply(right,t,mode == RuruStorySequence.Entrance.Slide ? 28f : 0f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        Apply(backdrop,1f,0f); Apply(left,1f,0f); Apply(right,1f,0f);
        transition = null;
    }

    private static void Apply(Image image,float t,float shift)
    {
        image.color = new Color(1f,1f,1f,t);
        image.rectTransform.anchoredPosition = new Vector2(shift*(1f-t),0f);
    }
}
