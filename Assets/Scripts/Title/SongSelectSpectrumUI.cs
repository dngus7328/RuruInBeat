using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Soft matte music spectrum rendered as UI geometry inside the EXISTING
/// jacket placeholder. No texture, shader, sprite or extra artwork required.
/// The user's authored jacket panel RectTransform is never moved or resized.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class SongSelectSpectrumUI : MaskableGraphic
{
    private const int BarCount = 30;
    private AudioSource music;
    private readonly float[] spectrum = new float[256];
    private readonly float[] bars = new float[BarCount];
    private float lastRefresh;

    public void AttachSource(AudioSource source)
    {
        music = source;
        raycastTarget = false;
        color = new Color(.78f, .67f, .86f, .83f);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        raycastTarget = false;
    }

    private void Update()
    {
        if (Time.unscaledTime - lastRefresh < .045f) return;
        lastRefresh = Time.unscaledTime;

        bool audible = music != null && music.isPlaying &&
            music.volume > .002f;
        if (audible)
            music.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
        else
            System.Array.Clear(spectrum, 0, spectrum.Length);

        for (int i = 0; i < BarCount; i++)
        {
            int start = Mathf.Clamp(1 + Mathf.FloorToInt(
                Mathf.Pow(i / (float)BarCount, 1.45f) * 210f),
                1, 242);
            int end = Mathf.Min(spectrum.Length, start + 6);
            float power = 0f;
            for (int j = start; j < end; j++)
                power = Mathf.Max(power, spectrum[j]);

            float target = audible ? Mathf.Clamp01(
                Mathf.Log(1f + power * 1300f) / 3.4f) : .02f;
            bars[i] = Mathf.Lerp(bars[i], target,
                target > bars[i] ? .48f : .17f);
        }
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.width <= 0f || r.height <= 0f)
            return;

        float distance = r.width / BarCount;
        float thickness = Mathf.Max(1.5f, distance * .58f);
        float floor = r.yMin + r.height * .27f;
        float usableHeight = r.height * .62f;
        for (int i = 0; i < BarCount; i++)
        {
            float magnitude = Mathf.Clamp01(bars[i]);
            float x = r.xMin + (i + .5f) * distance;
            float height = Mathf.Max(2f, usableHeight * magnitude);
            Color tint = color;
            tint.a *= .35f + .65f * magnitude;
            AddQuad(vh, new Rect(x - thickness / 2f, floor,
                thickness, height), tint);
        }
    }

    private static void AddQuad(VertexHelper helper, Rect r, Color tint)
    {
        int index = helper.currentVertCount;
        var vertex = UIVertex.simpleVert;
        vertex.color = tint;
        vertex.position = new Vector3(r.xMin, r.yMin);
        helper.AddVert(vertex);
        vertex.position = new Vector3(r.xMin, r.yMax);
        helper.AddVert(vertex);
        vertex.position = new Vector3(r.xMax, r.yMax);
        helper.AddVert(vertex);
        vertex.position = new Vector3(r.xMax, r.yMin);
        helper.AddVert(vertex);
        helper.AddTriangle(index, index + 1, index + 2);
        helper.AddTriangle(index, index + 2, index + 3);
    }
}
