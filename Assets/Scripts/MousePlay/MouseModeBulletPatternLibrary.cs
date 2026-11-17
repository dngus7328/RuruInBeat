using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Shared by the game and chart editor. All positions are normalized (top-left origin).
// Every speed is measured in stage-heights PER BEAT, independent of BPM.
public static class MouseModeBulletPatternLibrary
{
    public struct Pellet
    {
        public float atBeat, lifeBeats;
        public Vector2 origin;
        public float angle, speed, acceleration;
        public float wave, waveCycles, wavePhase;
        public float turnBeat, turnDegrees;
        public float pauseStart, pauseLength;
        public int color;
    }

    public static readonly string[] Ids = {
        "adv_helix", "adv_flower", "adv_vortex", "adv_accel", "adv_brake",
        "adv_turn", "adv_wave", "adv_lattice", "adv_gateflow", "adv_chain",
        "adv_orbit", "adv_ribbon", "adv_crossfire", "adv_clock"
    };
    public static readonly string[] Labels = {
        "이중 나선", "꽃잎 방사", "회전 소용돌이", "가속탄 편대", "감속 후 재가속",
        "박자 방향전환", "S자 물결", "엇갈림 격자", "이동하는 회피 문", "연쇄 분열",
        "원궤도 물결", "리듬 리본", "교차 발사원", "시계바늘 탄막"
    };

    public static bool IsSupported(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        id = id.Split('|')[0];
        for (int i = 0; i < Ids.Length; i++) if (Ids[i] == id) return true;
        return false;
    }

    public static float GetParameter(string encoded, string key, float fallback)
    {
        if (string.IsNullOrEmpty(encoded)) return fallback;
        string[] parts = encoded.Split('|');
        for (int i = 1; i < parts.Length; i++)
        {
            string[] pair = parts[i].Split('=');
            if (pair.Length != 2 || pair[0] != key) continue;
            float value;
            if (float.TryParse(pair[1], NumberStyles.Float,
                CultureInfo.InvariantCulture, out value)) return value;
        }
        return fallback;
    }

    public static string SetParameter(string encoded, string key, float value)
    {
        string[] parts = (encoded ?? "").Split('|');
        var output = new List<string>();
        output.Add(parts[0]);
        for (int i = 1; i < parts.Length; i++)
            if (!parts[i].StartsWith(key + "=", StringComparison.Ordinal)) output.Add(parts[i]);
        output.Add(key + "=" + value.ToString("0.###", CultureInfo.InvariantCulture));
        return string.Join("|", output.ToArray());
    }

    public static Vector2 Position(Pellet s, float elapsedBeats, float width, float height)
    {
        float t = Mathf.Max(0f, elapsedBeats);
        float movingT = t;
        if (s.pauseLength > 0f && t > s.pauseStart)
            movingT -= Mathf.Min(s.pauseLength, t - s.pauseStart);
        movingT = Mathf.Max(0f, movingT);
        float turnT = s.turnBeat > 0f ? Mathf.Min(movingT, s.turnBeat) : movingT;
        float afterTurn = s.turnBeat > 0f ? Mathf.Max(0f, movingT - s.turnBeat) : 0f;
        float distance1 = s.speed * turnT + 0.5f * s.acceleration * turnT * turnT;
        float velocityAtTurn = Mathf.Max(.015f, s.speed + s.acceleration * turnT);
        float distance2 = velocityAtTurn * afterTurn + .5f * s.acceleration * afterTurn * afterTurn;
        distance1 = Mathf.Max(0f, distance1);
        distance2 = Mathf.Max(0f, distance2);
        float a = s.angle * Mathf.Deg2Rad;
        float b = (s.angle + s.turnDegrees) * Mathf.Deg2Rad;
        Vector2 d1 = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        Vector2 d2 = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
        Vector2 local = new Vector2((s.origin.x - .5f) * width, (.5f - s.origin.y) * height)
                        + (d1 * distance1 + d2 * distance2) * height;
        if (Mathf.Abs(s.wave) > .0001f)
        {
            Vector2 side = new Vector2(-d1.y, d1.x);
            float offset = s.wave * Mathf.Sin(movingT * s.waveCycles * Mathf.PI * 2f + s.wavePhase);
            // Begin at the specified origin even when phase is nonzero.
            offset -= s.wave * Mathf.Sin(s.wavePhase);
            local += side * offset * height;
        }
        return local;
    }

    private static void Add(List<Pellet> list, float beat, float x, float y,
        float angle, float speed, int color = 0, float life = 13f,
        float accel = 0f, float wave = 0f, float freq = 0f, float phase = 0f,
        float turn = 0f, float bend = 0f, float pause = 0f, float pauseLen = 0f)
    {
        list.Add(new Pellet
        {
            atBeat = beat,
            lifeBeats = life,
            origin = new Vector2(x, y),
            angle = angle,
            speed = speed,
            acceleration = accel,
            wave = wave,
            waveCycles = freq,
            wavePhase = phase,
            turnBeat = turn,
            turnDegrees = bend,
            pauseStart = pause,
            pauseLength = pauseLen,
            color = color
        });
    }

    // These patterns are musical building blocks, NOT random full-screen barrages.
    // The deliberate omissions/open corridors favor mixed rhythm + dodging.
    public static List<Pellet> Generate(string id)
    {
        string encoded = id ?? "";
        id = encoded.Split('|')[0];
        var s = new List<Pellet>(220);
        const float v = .112f;
        switch (id)
        {
            case "adv_helix":
                for (int k = 0; k < 36; k++) for (int arm = 0; arm < 2; arm++)
                        Add(s, k * .25f, .5f, .13f, -90f + k * 15f + arm * 180f, v * .80f, arm, 15f);
                break;
            case "adv_flower":
                for (int volley = 0; volley < 5; volley++) for (int i = 0; i < 24; i++)
                    {
                        float a = i * 15f + volley * 9f;
                        // A fan-shaped missing petal centered downward makes an entrance.
                        if (Mathf.Abs(Mathf.DeltaAngle(a, -90f + volley * 5f)) < 23f) continue;
                        Add(s, volley * 1.5f, .5f, .18f, a, v * (.63f + .12f * Mathf.Sin(i * Mathf.PI / 6f)), 2);
                    }
                break;
            case "adv_vortex":
                for (int k = 0; k < 48; k++)
                {
                    float a = -90f + k * 16f;
                    Add(s, k * .1875f, .5f + .13f * Mathf.Sin(k * .14f), .12f, a, v * .72f, 1,
                        15f, 0f, .035f, .30f, k * .34f);
                }
                break;
            case "adv_accel":
                for (int wave = 0; wave < 9; wave++) for (int j = -2; j <= 2; j++)
                        Add(s, wave * .75f, .5f, .08f, -90f + j * 10f + Mathf.Sin(wave * .8f) * 12f,
                            v * .30f, 3, 13f, v * .015f);
                break;
            case "adv_brake":
                for (int wave = 0; wave < 7; wave++) for (int j = -3; j <= 3; j++)
                        Add(s, wave * .85f, .5f, .075f, -90f + j * 12f, v * .74f, 2, 15f,
                            0f, 0f, 0f, 0f, 0f, 0f, 1.7f, 1.0f);
                break;
            case "adv_turn":
                for (int wave = 0; wave < 7; wave++) for (int j = -3; j <= 3; j++)
                        Add(s, wave * 1f, .5f, .09f, -90f + j * 8f, v * .89f, 0, 14f,
                            0f, 0f, 0f, 0f, 2.5f, (wave % 2 == 0 ? 34f : -34f));
                break;
            case "adv_wave":
                for (int wave = 0; wave < 10; wave++) for (int j = -2; j <= 2; j++)
                        Add(s, wave * .6f, .5f, .08f, -90f + j * 10f, v * .84f, 1, 14f,
                            0f, .075f, .23f, ((wave + j) % 4) * .60f);
                break;
            case "adv_lattice":
                for (int row = 0; row < 9; row++) for (int col = 0; col < 9; col++)
                    {
                        if (Mathf.Abs(col - (row % 2 == 0 ? 3 : 5)) <= 1) continue;
                        Add(s, row * .75f, .10f + col * .10f, .025f, -90f, v * .93f, 0, 13f);
                    }
                break;
            case "adv_gateflow":
                for (int row = 0; row < 12; row++)
                {
                    int gap = 3 + Mathf.RoundToInt(1.5f * Mathf.Sin(row * .40f));
                    for (int col = 0; col < 11; col++)
                    {
                        if (Mathf.Abs(col - gap) <= 2) continue;
                        Add(s, row * .65f, .07f + col * .086f, .025f, -90f, v * .92f, 2, 13f);
                    }
                }
                break;
            case "adv_chain":
                for (int seed = 0; seed < 6; seed++)
                {
                    float x = .17f + seed * .13f;
                    Add(s, seed * .9f, x, .04f, -90f, v * .62f, 3, 2f);
                    Pellet parent = s[s.Count - 1];
                    Vector2 split = Position(parent, 2f, 1f, 1f);
                    for (int j = -2; j <= 2; j++)
                        Add(s, seed * .9f + 2f, split.x + .5f, .5f - split.y, -90f + j * 17f, v * .79f, 3, 12f);
                }
                break;
            case "adv_orbit":
                for (int row = 0; row < 16; row++) for (int j = -2; j <= 2; j++)
                        Add(s, row * .5f, .5f, .11f, -90f + j * 9f, v * .72f, 1, 14f,
                            0f, .055f, .43f, row * .38f);
                break;
            case "adv_ribbon":
                for (int row = 0; row < 26; row++)
                {
                    float x = .5f + .26f * Mathf.Sin(row * .28f);
                    for (int j = -1; j <= 1; j++)
                        Add(s, row * .32f, x + j * .026f, .04f, -90f + (j * 9f), v * .85f, 2, 14f);
                }
                break;
            case "adv_crossfire":
                for (int volley = 0; volley < 10; volley++)
                {
                    bool left = volley % 2 == 0;
                    for (int j = -2; j <= 2; j++)
                        Add(s, volley * .75f, left ? .07f : .93f, .10f,
                            left ? -45f + j * 9f : -135f + j * 9f, v * .80f, 0, 14f);
                }
                break;
            case "adv_clock":
                for (int tick = 0; tick < 12; tick++)
                {
                    float a = -90f + tick * 30f;
                    for (int j = -2; j <= 2; j++)
                        Add(s, tick * .75f, .5f, .18f, a + j * 6f, v * .75f, 3, 14f);
                }
                break;
        }
        float speed = Mathf.Clamp(GetParameter(encoded, "speed", 1f), .40f, 1.6f);
        float spacing = Mathf.Clamp(GetParameter(encoded, "spacing", 1f), .55f, 1.8f);
        float rotation = Mathf.Clamp(GetParameter(encoded, "rotation", 0f), -90f, 90f);
        float sourceX = Mathf.Clamp(GetParameter(encoded, "x", .5f), .2f, .8f);
        float spread = Mathf.Clamp(GetParameter(encoded, "spread", 1f), .7f, 1.7f);
        float density = Mathf.Clamp(GetParameter(encoded, "density", 1f), .35f, 1f);
        for (int i = 0; i < s.Count; i++)
        {
            Pellet pellet = s[i];
            pellet.speed *= speed;
            pellet.acceleration *= speed;
            pellet.atBeat *= spacing;
            // Spread is spatial (not temporal). Preserve downwards firing as the
            // centerline and fan out deviations from it.
            float heading = Mathf.DeltaAngle(-90f, pellet.angle);
            pellet.angle = -90f + heading * spread + rotation;
            pellet.origin.x = Mathf.Clamp(sourceX + (pellet.origin.x - .5f) * spread, .035f, .965f);
            s[i] = pellet;
        }
        // Density is deterministic so preview and runtime select identical shots.
        // Density 1 retains the exact original pattern; smaller values omit
        // some projectiles without increasing speed or changing music timing.
        if (density < .999f)
            for (int i = s.Count - 1; i >= 0; i--)
            {
                float choice = ((i * 73 + 19) % 101) / 101f;
                if (choice >= density) s.RemoveAt(i);
            }
        return s;
    }
}
