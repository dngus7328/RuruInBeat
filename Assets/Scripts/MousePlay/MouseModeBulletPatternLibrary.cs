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
        "adv_orbit", "adv_ribbon", "adv_crossfire", "adv_clock",
        "pop_twinfan", "pop_echo", "pop_pendulum", "pop_ribbon", "pop_gate",
        "pop_pinwheel", "pop_aim", "pop_pulse", "pop_seedflower"
    };
    public static readonly string[] Labels = {
        "이중 나선", "꽃잎 방사", "회전 소용돌이", "가속탄 편대", "감속 후 재가속",
        "박자 방향전환", "S자 물결", "엇갈림 격자", "이동하는 회피 문", "연쇄 분열",
        "원궤도 물결", "리듬 리본", "교차 발사원", "시계바늘 탄막",
        "팝 투윈 부채꼴", "팝 반향", "팝 진자", "팝 리본", "팝 회피문",
        "팝 바람개비", "팝 대각 조준형", "팝 맥박", "팝 씨앗 꽃"
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
            // Pop'n Dash: nine chart-authored motifs. The editor and runtime
            // consume the same deterministic trajectories; no random spawns.
            // Each has a recognizable rhythm and open routes for mouse notes.
            case "pop_twinfan":
                for (int volley = 0; volley < 4; volley++)
                    for (int side = 0; side < 2; side++)
                        for (int wing = -2; wing <= 2; wing++)
                        {
                            if (wing == 0) continue;
                            float sourceX = side == 0 ? .30f : .70f;
                            float heading = -90f + wing * 12f + (side == 0 ? -8f : 8f);
                            Add(s, volley * 1.65f + side * .30f,
                                sourceX, .075f, heading, v * .94f,
                                side, 13f);
                        }
                break;
            case "pop_echo":
                for (int phrase = 0; phrase < 4; phrase++)
                {
                    bool left = phrase % 2 == 0;
                    float sourceX = left ? .28f : .72f;
                    for (int echo = 0; echo < 3; echo++)
                        for (int wing = -1; wing <= 1; wing++)
                            Add(s, phrase * 1.85f + echo * .28f,
                                sourceX, .07f,
                                -90f + wing * 15f + (left ? 8f : -8f),
                                v * (.86f + echo * .06f), 1, 12f);
                }
                break;
            case "pop_pendulum":
                for (int step = 0; step < 12; step++)
                {
                    float sourceX = .50f + .23f * Mathf.Sin(step * .48f);
                    for (int side = -1; side <= 1; side += 2)
                        Add(s, step * .62f,
                            sourceX, .06f, -90f + side * 14f,
                            v * .94f, 2, 12f);
                }
                break;
            case "pop_ribbon":
                for (int step = 0; step < 24; step++)
                {
                    float sourceX = .50f + .21f * Mathf.Sin(step * .33f);
                    Add(s, step * .34f, sourceX, .035f,
                        -90f + 9f * Mathf.Cos(step * .33f),
                        v * .96f, 2, 12f, 0f,
                        .040f, .29f, step * .38f);
                }
                break;
            case "pop_gate":
                for (int row = 0; row < 6; row++)
                {
                    // Every wall has a two-column opening; it drifts gradually.
                    int gap = 2 + Mathf.RoundToInt(1.1f * Mathf.Sin(row * .45f));
                    for (int lane = 0; lane < 8; lane++)
                    {
                        if (Mathf.Abs(lane - gap) <= 1) continue;
                        Add(s, row * 1.3f, .10f + lane * .113f,
                            .035f, -90f, v * .86f, 0, 12f);
                    }
                }
                break;
            case "pop_pinwheel":
                for (int tick = 0; tick < 24; tick++)
                    for (int arm = 0; arm < 2; arm++)
                    {
                        float a = -90f + tick * 19f + arm * 180f;
                        Add(s, tick * .32f, .50f, .12f, a,
                            v * .78f, 1, 11f);
                    }
                break;
            case "pop_aim":
                // Readable diagonal fan, with its direction locked at launch.
                // Unlike an invisible homing attack, no trajectory changes.
                for (int volley = 0; volley < 5; volley++)
                {
                    bool left = volley % 2 == 0;
                    for (int wing = -1; wing <= 1; wing++)
                        Add(s, volley * 1.4f,
                            left ? .30f : .70f, .07f,
                            -90f + (left ? -17f : 17f) + wing * 11f,
                            v * .96f, 3, 12f);
                }
                break;
            case "pop_pulse":
                for (int pulse = 0; pulse < 7; pulse++)
                    for (int wing = -2; wing <= 2; wing++)
                    {
                        if (wing == 0) continue;
                        Add(s, pulse * 1.05f,
                            .50f, .09f, -90f + wing * 12.5f,
                            v * (pulse % 2 == 0 ? .82f : .97f), 2, 12f);
                    }
                break;
            case "pop_seedflower":
                for (int seed = 0; seed < 3; seed++)
                {
                    float start = seed * 2.0f;
                    float sourceX = .30f + seed * .20f;
                    Add(s, start, sourceX, .05f, -90f,
                        v * .66f, 3, 2.2f);
                    Pellet parent = s[s.Count - 1];
                    Vector2 split = Position(parent, 2.2f, 1f, 1f);
                    for (int wing = -2; wing <= 2; wing++)
                    {
                        if (wing == 0) continue;
                        Add(s, start + 2.2f, split.x + .5f, .5f - split.y,
                            -90f + wing * 16f, v * .91f, 3, 10f);
                    }
                }
                break;
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
