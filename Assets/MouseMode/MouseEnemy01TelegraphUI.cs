using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One animated Enemy01 per simultaneously active chart attack.
/// This is an advance warning visual: it does not spawn bullets or alter Pino's hitbox.
/// The director provides DSP-based appear/fire/last-shot/depart times.
/// </summary>
[DisallowMultipleComponent]
public sealed class MouseEnemy01TelegraphUI : MonoBehaviour
{
    [Serializable]
    public struct Cue
    {
        public double appearAt;
        public double fireAt;
        public double lastShotAt;
        public double disappearAt;
        public float sourceX;
        // Exact projectile emission times grouped into readable sprite pulses.
        public double[] volleyTimes;
    }

    [Header("Sprite sheet: 4 columns x 3 rows")]
    [SerializeField] private string resourcePath = "MouseEnemies/Enemy01";
    [Header("Enemy size and floating")]
    [SerializeField, Min(40f)] private float maxSize = 190f;
    [SerializeField, Range(0.16f, 0.34f)] private float screenTopFraction = 0.26f;
    [SerializeField, Range(0f, 24f)] private float floatAmplitude = 10f;
    [SerializeField, Range(0.15f, 2f)] private float floatCyclesPerSecond = 0.70f;
    [SerializeField, Range(0f, 12f)] private float swayAmplitude = 4f;
    [SerializeField, Min(1f)] private float idleFPS = 6f;
    [SerializeField] private bool idlePingPong = true;
    [SerializeField, Range(0.05f, 0.45f)] private float fadeSeconds = 0.20f;

    private sealed class ScheduledEnemy
    {
        public Cue cue;
        public int slot;
        public float phase;
    }

    private sealed class EnemyView
    {
        public RectTransform transform;
        public Image image;
        public bool wasVisible;
        public float previousX;
    }

    private readonly List<ScheduledEnemy> scheduled = new List<ScheduledEnemy>();
    private readonly List<ScheduledEnemy> visible = new List<ScheduledEnemy>();
    private readonly List<EnemyView> views = new List<EnemyView>();
    private readonly List<Sprite> frames = new List<Sprite>();

    private RectTransform stage;
    private bool missingSheetWarning;

    public void Setup(RectTransform bulletLayer)
    {
        stage = bulletLayer;
        LoadFrames();
    }

    public void SetCues(List<Cue> newCues)
    {
        scheduled.Clear();
        visible.Clear();

        foreach (EnemyView view in views)
        {
            view.image.enabled = false;
            view.wasVisible = false;
        }

        if (newCues == null || stage == null)
            return;

        // Interval allocation: overlapping attack windows never share a sprite.
        // Non-overlapping events reuse the same image to avoid allocating one
        // hierarchy object for each attack over the entire song.
        List<Cue> sorted = new List<Cue>(newCues);
        sorted.Sort((a, b) => a.appearAt.CompareTo(b.appearAt));
        List<double> availableAt = new List<double>();

        for (int i = 0; i < sorted.Count; i++)
        {
            Cue cue = sorted[i];
            if (cue.disappearAt <= cue.appearAt)
                continue;

            int slot = 0;
            while (slot < availableAt.Count &&
                availableAt[slot] > cue.appearAt)
                slot++;

            if (slot == availableAt.Count)
                availableAt.Add(cue.disappearAt);
            else
                availableAt[slot] = cue.disappearAt;

            scheduled.Add(new ScheduledEnemy
            {
                cue = cue,
                slot = slot,
                phase = slot * 1.7f + (i % 3) * .6f
            });
        }

        while (views.Count < availableAt.Count)
            CreateView(views.Count);
    }

    public void Tick(double songSeconds)
    {
        if (stage == null || double.IsNaN(songSeconds) || !LoadFrames())
        {
            HideAll();
            return;
        }

        visible.Clear();
        foreach (ScheduledEnemy s in scheduled)
        {
            if (songSeconds >= s.cue.appearAt &&
                songSeconds < s.cue.disappearAt)
                visible.Add(s);
        }

        // The positions are distributed so simultaneous attackers never share
        // exactly the same location, even if their sourceX values are identical.
        visible.Sort((a, b) =>
        {
            int xOrder = a.cue.sourceX.CompareTo(b.cue.sourceX);
            return xOrder != 0 ? xOrder : a.cue.fireAt.CompareTo(b.cue.fireAt);
        });

        float width = Mathf.Max(1f, stage.rect.width);
        float height = Mathf.Max(1f, stage.rect.height);
        float size = Mathf.Min(maxSize, Mathf.Max(40f, height * .42f));
        float horizontalLimit = Mathf.Max(0f, width * .5f - size * .5f - 7f);
        float verticalLimit = Mathf.Max(0f, height * .5f - size * .5f -
            floatAmplitude - 4f);
        float baselineY = Mathf.Clamp(
            (.5f - screenTopFraction) * height,
            -verticalLimit, verticalLimit);

        int count = visible.Count;
        float gap = count <= 1 ? 0f :
            Mathf.Min(size * .98f, horizontalLimit * 2f / (count - 1));
        float sourceAverage = 0f;
        foreach (ScheduledEnemy active in visible)
            sourceAverage += active.cue.sourceX;
        if (count > 0)
            sourceAverage /= count;

        float totalSpread = gap * Mathf.Max(0, count - 1);
        float leftmost = Mathf.Clamp(
            (sourceAverage - .5f) * width - totalSpread * .5f,
            -horizontalLimit, horizontalLimit - totalSpread);

        // Views belonging to inactive attacks are hidden below.
        bool[] occupied = new bool[views.Count];

        for (int i = 0; i < count; i++)
        {
            ScheduledEnemy active = visible[i];
            EnemyView view = views[active.slot];
            occupied[active.slot] = true;

            float targetX = count == 1 ?
                Mathf.Clamp((active.cue.sourceX - .5f) * width,
                    -horizontalLimit, horizontalLimit) :
                leftmost + gap * i;

            if (!view.wasVisible)
                view.previousX = targetX;
            else
                view.previousX = Mathf.Lerp(
                    view.previousX, targetX,
                    Mathf.Clamp01(Time.unscaledDeltaTime * 8f));

            double alive = Math.Max(0d, songSeconds - active.cue.appearAt);
            float phase = (float)(alive * Math.PI * 2d *
                floatCyclesPerSecond) + active.phase;
            float bob = Mathf.Sin(phase) * floatAmplitude;
            float sway = Mathf.Sin(phase * .65f + active.phase) * swayAmplitude;
            float finalX = Mathf.Clamp(view.previousX + sway,
                -horizontalLimit, horizontalLimit);

            RectTransform rect = view.transform;
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(finalX, baselineY + bob);

            // 6 FPS ping-pong playback, like Pino's idle animation.
            int length = frames.Count;
            int span = idlePingPong && length > 1 ? (length - 1) * 2 : length;
            int step = Mathf.FloorToInt((float)songSeconds * idleFPS);
            int frame = ((step % span) + span) % span;
            if (idlePingPong && frame >= length)
                frame = span - frame;
            view.image.sprite = frames[frame];

            Cue cue = active.cue;
            float fadeIn = Mathf.Clamp01((float)
                ((songSeconds - cue.appearAt) / fadeSeconds));
            float fadeOut = Mathf.Clamp01((float)
                ((cue.disappearAt - songSeconds) / fadeSeconds));
            float alpha = Mathf.Min(fadeIn, fadeOut);

            // Animate at the REAL firing timestamps, not only when the first
            // barrage starts. This keeps the enemy visibly responsible for
            // every stream/fan/rain burst, while leaving its art unchanged.
            double lastVolley = double.NegativeInfinity;
            if (cue.volleyTimes != null)
            {
                foreach (double when in cue.volleyTimes)
                {
                    if (when > songSeconds) break;
                    lastVolley = when;
                }
            }
            float recoil = double.IsNegativeInfinity(lastVolley) ? 0f
                : Mathf.Clamp01(1f - (float)
                    ((songSeconds - lastVolley) / .18d));
            rect.localScale = Vector3.one * (1f + recoil * .135f);
            Color tint = Color.Lerp(Color.white,
                new Color(1f, .82f, .85f, 1f), recoil * .70f);
            tint.a = alpha;
            view.image.color = tint;
            view.image.enabled = alpha > .001f;
            view.wasVisible = true;
            // BulletLayer is below the actual NoteLayer. Put the attacker's
            // sprite above its bullets so it never vanishes BEHIND the barrage;
            // notes and the timing guides still render over this layer.
            rect.SetAsLastSibling();
        }

        for (int i = 0; i < views.Count; i++)
        {
            if (occupied[i]) continue;
            views[i].image.enabled = false;
            views[i].wasVisible = false;
        }
    }

    private void CreateView(int slot)
    {
        GameObject go = new GameObject(
            "Enemy01_AnimatedPreAttack_" + (slot + 1).ToString("00"),
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(stage, false);
        rect.anchorMin = new Vector2(.5f, .5f);
        rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.localRotation = Quaternion.identity;

        Image image = go.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = false;
        rect.SetAsFirstSibling();

        views.Add(new EnemyView { transform = rect, image = image });
    }

    private void HideAll()
    {
        foreach (EnemyView view in views)
        {
            view.image.enabled = false;
            view.wasVisible = false;
        }
    }

    private bool LoadFrames()
    {
        if (frames.Count == 12)
            return true;

        Texture2D sheet = Resources.Load<Texture2D>(resourcePath);
        if (sheet == null || sheet.width % 4 != 0 ||
            sheet.height % 3 != 0)
        {
            if (!missingSheetWarning)
            {
                missingSheetWarning = true;
                Debug.LogWarning("[Enemy01] Missing/invalid 4x3 sheet: " +
                    "Assets/Resources/" + resourcePath + ".png", this);
            }
            return false;
        }

        float w = sheet.width / 4f;
        float h = sheet.height / 3f;
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 4; col++)
            {
                Sprite sprite = Sprite.Create(sheet,
                    new Rect(col * w, (2 - row) * h, w, h),
                    new Vector2(.5f, .5f),
                    100f, 0, SpriteMeshType.FullRect);
                sprite.name = "Enemy01_" + (row * 4 + col).ToString("00");
                frames.Add(sprite);
            }
        }

        missingSheetWarning = false;
        return frames.Count == 12;
    }

    private void OnDestroy()
    {
        foreach (Sprite sprite in frames)
            if (sprite != null)
                Destroy(sprite);
        frames.Clear();
    }
}
