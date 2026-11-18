using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One non-damaging animated enemy shown before scheduled mouse-mode barrages.
/// Its clock is the same DSP song time used by MouseModeBulletDirector.
/// No extra chart attack, hitbox or projectile is created here.
/// </summary>
[DisallowMultipleComponent]
public sealed class MouseEnemy01TelegraphUI : MonoBehaviour
{
    [Serializable]
    public struct Cue
    {
        public double appearAt;
        public double fireAt;
        public double disappearAt;
        public float sourceX;
    }

    [Header("Enemy01 visual")]
    [SerializeField] private string resourcePath = "MouseEnemies/Enemy01";
    [SerializeField, Min(20f)] private float maxSize = 115f;
    [SerializeField, Range(0.06f, 0.34f)] private float screenTopFraction = 0.16f;
    [SerializeField, Min(1f)] private float idleFPS = 6f;
    [SerializeField] private bool idlePingPong = true;
    [SerializeField, Min(0.02f)] private float fadeSeconds = 0.18f;

    private readonly List<Cue> cues = new List<Cue>();
    private readonly List<Sprite> frameSprites = new List<Sprite>();
    private RectTransform stage;
    private RectTransform enemyRect;
    private Image enemyImage;
    private bool imageWarningShown;
    private double previousTime = double.NaN;
    private double lastCastTime = double.NegativeInfinity;
    private int nextCueIndex;
    private float currentX = 0.5f;

    public void Setup(RectTransform bulletLayer)
    {
        stage = bulletLayer;
        if (enemyRect == null)
            CreateVisual();
    }

    public void SetCues(List<Cue> newCues)
    {
        cues.Clear();
        if (newCues != null)
            cues.AddRange(newCues);
        cues.Sort((a, b) => a.appearAt.CompareTo(b.appearAt));
        nextCueIndex = 0;
        lastCastTime = double.NegativeInfinity;
        previousTime = double.NaN;
        currentX = 0.5f;
        if (enemyImage != null)
            enemyImage.enabled = false;
    }

    public void Tick(double songSeconds)
    {
        if (double.IsNaN(songSeconds) || stage == null)
            return;
        if (enemyRect == null)
            CreateVisual();
        if (enemyImage == null)
            return;

        // The chart was restarted/rewound: re-arm all cast previews.
        if (!double.IsNaN(previousTime) && songSeconds < previousTime - 0.10d)
        {
            nextCueIndex = 0;
            lastCastTime = double.NegativeInfinity;
        }
        previousTime = songSeconds;

        Cue? visible = null;
        double nearestPending = double.PositiveInfinity;
        double longestVisibleUntil = double.NegativeInfinity;
        float targetX = currentX;

        for (int i = 0; i < cues.Count; i++)
        {
            Cue cue = cues[i];
            if (songSeconds < cue.appearAt || songSeconds > cue.disappearAt)
                continue;

            if (cue.fireAt >= songSeconds && cue.fireAt < nearestPending)
            {
                visible = cue;
                nearestPending = cue.fireAt;
                targetX = cue.sourceX;
            }
            else if (visible == null && cue.disappearAt > longestVisibleUntil)
            {
                visible = cue;
                longestVisibleUntil = cue.disappearAt;
                targetX = cue.sourceX;
            }
        }

        if (!visible.HasValue)
        {
            enemyImage.enabled = false;
            return;
        }

        if (!LoadFrames())
        {
            enemyImage.enabled = false;
            return;
        }

        // A cue fires once, and all elapsed cues are consumed even after a
        // frame drop. This does NOT schedule additional shots.
        while (nextCueIndex < cues.Count && cues[nextCueIndex].fireAt <= songSeconds)
        {
            if (cues[nextCueIndex].fireAt >= songSeconds - 0.25d)
                lastCastTime = cues[nextCueIndex].fireAt;
            nextCueIndex++;
        }

        Cue selected = visible.Value;
        float enter = Mathf.Clamp01((float)((songSeconds - selected.appearAt) /
            Math.Max(.02f, fadeSeconds)));
        float exit = Mathf.Clamp01((float)((selected.disappearAt - songSeconds) /
            Math.Max(.02f, fadeSeconds)));
        float alpha = Mathf.Min(enter, exit);

        float center = Mathf.Clamp(targetX, .14f, .86f);
        float smooth = Mathf.Clamp01(Time.unscaledDeltaTime * 7f);
        currentX = Mathf.Lerp(currentX, center, smooth);

        float width = stage.rect.width;
        float height = stage.rect.height;
        float size = Mathf.Min(maxSize, Mathf.Max(56f, height * .27f));
        enemyRect.sizeDelta = new Vector2(size, size);
        enemyRect.anchoredPosition = new Vector2(
            (currentX - 0.5f) * width,
            (0.5f - screenTopFraction) * height);

        // The same 6 FPS ping-pong frame convention as Pino's idle animation.
        int cycle = Mathf.Max(1, frameSprites.Count);
        int step = Mathf.FloorToInt((float)Math.Max(0d, songSeconds - selected.appearAt) *
            Mathf.Max(1f, idleFPS));
        int index = step % (idlePingPong && cycle > 1 ? (cycle - 1) * 2 : cycle);
        if (idlePingPong && index >= cycle)
            index = (cycle - 1) * 2 - index;
        enemyImage.sprite = frameSprites[index];

        // A restrained warning/attack reaction; the original artwork stays intact.
        float untilFire = (float)(selected.fireAt - songSeconds);
        float charge = Mathf.Clamp01(1f - untilFire / .45f);
        float recoil = Mathf.Clamp01(1f - (float)(songSeconds - lastCastTime) / .19f);
        float pulse = charge > 0f && untilFire > 0f
            ? (0.94f + 0.06f * Mathf.Sin((float)songSeconds * 15f)) : 1f;
        enemyRect.localScale = Vector3.one * (1f + recoil * .07f);
        enemyImage.color = new Color(pulse, pulse, pulse, alpha);
        enemyImage.enabled = alpha > .001f;
        enemyRect.SetAsFirstSibling(); // The enemy is always behind bullets/notes.
    }

    private void CreateVisual()
    {
        if (stage == null || enemyRect != null)
            return;

        GameObject go = new GameObject("Enemy01_AnimatedPreAttack",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        enemyRect = go.GetComponent<RectTransform>();
        enemyRect.SetParent(stage, false);
        enemyRect.anchorMin = new Vector2(.5f, .5f);
        enemyRect.anchorMax = new Vector2(.5f, .5f);
        enemyRect.pivot = new Vector2(.5f, .5f);
        enemyRect.localRotation = Quaternion.identity;
        enemyImage = go.GetComponent<Image>();
        enemyImage.raycastTarget = false;
        enemyImage.preserveAspect = true;
        enemyImage.enabled = false;
        enemyRect.SetAsFirstSibling();
        LoadFrames();
    }

    private bool LoadFrames()
    {
        if (frameSprites.Count == 12)
            return true;

        Texture2D sheet = Resources.Load<Texture2D>(resourcePath);
        if (sheet == null)
        {
            if (!imageWarningShown)
            {
                imageWarningShown = true;
                Debug.LogWarning("[Enemy01] Missing sprite sheet: Assets/Resources/" +
                    resourcePath + ".png", this);
            }
            return false;
        }

        if (sheet.width % 4 != 0 || sheet.height % 3 != 0)
        {
            if (!imageWarningShown)
            {
                imageWarningShown = true;
                Debug.LogError("[Enemy01] Sprite sheet must have 4 columns and 3 rows.", this);
            }
            return false;
        }

        // Supplied sheet: 4 columns x 3 rows, read left-to-right/top-to-bottom.
        // Sprite coordinates have a bottom-left origin.
        float w = sheet.width / 4f, h = sheet.height / 3f;
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < 4; col++)
            {
                Rect rect = new Rect(col * w, (2 - row) * h, w, h);
                Sprite sprite = Sprite.Create(sheet, rect, new Vector2(.5f, .5f),
                    100f, 0, SpriteMeshType.FullRect);
                sprite.name = "Enemy01_" + (row * 4 + col).ToString("00");
                frameSprites.Add(sprite);
            }
        imageWarningShown = false;
        return true;
    }

    private void OnDestroy()
    {
        foreach (Sprite sprite in frameSprites)
            if (sprite != null)
                Destroy(sprite);
        frameSprites.Clear();
    }
}
