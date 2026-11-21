using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using LuluInBeat.MouseMode.ChartSystem;

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
        "pop_pinwheel", "pop_aim", "pop_pulse", "pop_seedflower",
        "boot_pulse", "boot_answer", "boot_gate", "boot_sync",
        "mix_arc", "mix_cross", "mix_gate", "mix_sweep",
        "mix_echo", "mix_comet", "mix_ribbon",
        "fh_beat", "fh_curtain", "fh_cross", "fh_gate",
        "fh_sweep", "fh_orbit", "fh_comet", "fh_finale"
    };
    public static readonly string[] Labels = {
        "이중 나선", "꽃잎 방사", "회전 소용돌이", "가속탄 편대", "감속 후 재가속",
        "박자 방향전환", "S자 물결", "엇갈림 격자", "이동하는 회피 문", "연쇄 분열",
        "원궤도 물결", "리듬 리본", "교차 발사원", "시계바늘 탄막",
        "팝 투윈 부채꼴", "팝 반향", "팝 진자", "팝 리본", "팝 회피문",
        "팝 바람개비", "팝 대각 조준형", "팝 맥박", "팝 씨앗 꽃",
        "시작의 박동: 이중 박동", "시작의 박동: 좌우 응답",
        "시작의 박동: 이동 회피문", "시작의 박동: 엇박 연속탄",
        "열리는 부채", "교차 대각선", "움직이는 틈새", "박자 쓸기",
        "응답의 파동", "분열하는 별", "흐르는 리본",
        "Full-Band Hit: 양쪽 박동", "Full-Band Hit: 열린 커튼",
        "Full-Band Hit: 대각 응답", "Full-Band Hit: 완만한 회피문",
        "Full-Band Hit: 왕복 쓸기", "Full-Band Hit: 유선형 흐름",
        "Full-Band Hit: 분열 신호", "Full-Band Hit: 피날레"
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

    // Integrate Pop'n Dash attacks into the *same* playable note route.
    // Do not change where bullets are fired, their rhythms, or their shapes:
    // open gaps only where the player must click/scroll or move to the next
    // note. Runtime and chart editor both call this exact overload.
    public static List<Pellet> GenerateForChart(
        string encodedId, float attackBeat, MouseModeChartData chart)
    {
        List<Pellet> pellets = Generate(encodedId);
        if (chart == null || chart.notes == null ||
            string.IsNullOrEmpty(encodedId) ||
            !encodedId.StartsWith("pop_", StringComparison.Ordinal))
            return pellets;

        var orderedNotes = new List<MouseModeChartNote>();
        foreach (MouseModeChartNote chartNote in chart.notes)
            if (chartNote != null) orderedNotes.Add(chartNote);
        if (orderedNotes.Count == 0) return pellets;
        orderedNotes.Sort((a, b) => a.beat.CompareTo(b.beat));

        // Use the song's quarter-beat grid (sixteenth-note subdivisions).
        // Existing free-form emission spacing made some pulses feel unrelated
        // to the notes, even when the attack marker itself was on the beat.
        for (int pelletIndex = 0; pelletIndex < pellets.Count; pelletIndex++)
        {
            Pellet timedPellet = pellets[pelletIndex];
            timedPellet.atBeat = Mathf.Round(timedPellet.atBeat * 4f) * .25f;
            pellets[pelletIndex] = timedPellet;
        }

        for (int pelletIndex = pellets.Count - 1; pelletIndex >= 0; pelletIndex--)
            if (ConflictsWithRequiredCursorRoute(
                pellets[pelletIndex], attackBeat, orderedNotes))
                pellets.RemoveAt(pelletIndex);

        // Do not spawn an uncharted second pellet behind every other
        // survivor. This used to add extra clustered shots on top of the
        // nine authored Pop'n Dash motifs, making notes harder to read.
        // The original timed motif and all chart-route safety gaps remain.
        return pellets;
    }

    // A 900x450 reference area matches the game's normalized chart. Protect
    // actual hit moments, held wheel paths, and the travel between nearby taps.
    // This isn't automatic dodging or a new input rule. It only omits shots
    // that would demand the player to be in two places at once.
    private static bool ConflictsWithRequiredCursorRoute(
        Pellet shot, float eventBeat, List<MouseModeChartNote> orderedNotes)
    {
        const float referenceW = 900f;
        const float referenceH = 450f;
        const float tapClearance = 66f;
        const float wheelClearance = 60f;
        const float travelClearance = 35f;
        float bornBeat = eventBeat + shot.atBeat;
        float lastBeat = bornBeat + shot.lifeBeats;
        Vector2 previousTapPosition = Vector2.zero;
        float previousTapBeat = 0f;
        bool hasPreviousTap = false;

        foreach (MouseModeChartNote chartNote in orderedNotes)
        {
            // Nearby taps provide a continuous, short movement corridor,
            // in addition to the timing window around each actual click.
            if (chartNote.kind != MouseModeNoteKind.Wheel)
            {
                if (hasPreviousTap)
                {
                    float interval = chartNote.beat - previousTapBeat;
                    if (interval >= .45f && interval <= 2.25f)
                    {
                        for (int segment = 1; segment <= 3; segment++)
                        {
                            float ratio = segment * .25f;
                            float passageBeat = Mathf.Lerp(
                                previousTapBeat, chartNote.beat, ratio);
                            if (passageBeat < bornBeat || passageBeat > lastBeat)
                                continue;
                            Vector2 passageTarget = Vector2.Lerp(
                                previousTapPosition, chartNote.position, ratio);
                            if (NearRequiredPoint(shot, passageBeat - bornBeat,
                                passageTarget, referenceW, referenceH,
                                travelClearance))
                                return true;
                        }
                    }
                }
                previousTapPosition = chartNote.position;
                previousTapBeat = chartNote.beat;
                hasPreviousTap = true;

                if (chartNote.beat + .35f < bornBeat ||
                    chartNote.beat - .35f > lastBeat)
                    continue;
                for (int offsetIndex = -1; offsetIndex <= 1; offsetIndex++)
                {
                    float testBeat = chartNote.beat + offsetIndex * .32f;
                    if (testBeat < bornBeat || testBeat > lastBeat) continue;
                    if (NearRequiredPoint(shot, testBeat - bornBeat,
                        chartNote.position, referenceW, referenceH,
                        tapClearance))
                        return true;
                }
            }
            else
            {
                float wheelStart = Mathf.Max(bornBeat, chartNote.beat - .2f);
                float wheelEnd = Mathf.Min(lastBeat, chartNote.endBeat + .2f);
                if (wheelStart > wheelEnd) continue;
                int samples = Mathf.Max(1,
                    Mathf.CeilToInt((wheelEnd - wheelStart) / .20f));
                for (int sampleIndex = 0; sampleIndex <= samples; sampleIndex++)
                {
                    float testBeat = Mathf.Lerp(wheelStart, wheelEnd,
                        sampleIndex / (float)samples);
                    float progress = Mathf.Clamp01(
                        (testBeat - chartNote.beat) /
                        Mathf.Max(.01f, chartNote.endBeat - chartNote.beat));
                    Vector2 wheelPosition =
                        MouseModeChartTiming.GetPathPosition(chartNote, progress);
                    if (NearRequiredPoint(shot, testBeat - bornBeat,
                        wheelPosition, referenceW, referenceH,
                        wheelClearance))
                        return true;
                }
            }
        }
        return false;
    }

    private static bool NearRequiredPoint(
        Pellet shot, float elapsedBeats, Vector2 normalizedPoint,
        float stageWidth, float stageHeight, float clearance)
    {
        Vector2 projectilePosition =
            Position(shot, elapsedBeats, stageWidth, stageHeight);
        Vector2 requiredPosition = new Vector2(
            (Mathf.Clamp01(normalizedPoint.x) - .5f) * stageWidth,
            (.5f - Mathf.Clamp01(normalizedPoint.y)) * stageHeight);
        return (projectilePosition - requiredPosition).sqrMagnitude <
               clearance * clearance;
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
            // BOOTBEAT: musical waves that cannot be bypassed by
            // camping at the initial center position.
            // Outer exits remain visible; avoid unavoidable full-width walls.
            case "boot_pulse":
                // Four clearly separated accents across the full playfield.
                // Rhythm-first: no central triplets after every volley.
                for (int pulse = 0; pulse < 4; pulse++)
                {
                    float direction = pulse % 2 == 0 ? 5f : -5f;
                    for (int wing = 0; wing < 4; wing++)
                    {
                        float laneX = .16f + wing * .22f;
                        float fan = (wing - 1.5f) * 6f;
                        Add(s, pulse * 1.20f, laneX, .05f,
                            -90f + fan + direction, v * 1.12f,
                            pulse % 2, 9f);
                    }
                }
                // One deliberate center-area pressure beat. Moving Pino
                // matters, but the player can still concentrate on notes.
                Add(s, 1.80f, .467f, .05f, -90f,
                    v * 1.12f, 2, 9f);
                break;
            case "boot_answer":
                // Alternating left and right calls, not simultaneous walls.
                for (int call = 0; call < 4; call++)
                {
                    bool fromLeft = call % 2 == 0;
                    for (int wing = -1; wing <= 1; wing++)
                    {
                        Add(s, call * 1.25f,
                            fromLeft ? .21f : .79f, .05f,
                            -90f + (fromLeft ? 18f : -18f) + wing * 16f,
                            v * 1.10f, call % 2, 9f);
                    }
                }
                Add(s, 2.05f, .467f, .055f, -90f,
                    v * 1.15f, 2, 9f);
                break;
            case "boot_gate":
                // Three readable gates instead of five packed 11-lane rows.
                // Each has a two-lane opening; the opening changes sides.
                int[] openFrom = { 2, 4, 2 };
                for (int row = 0; row < openFrom.Length; row++)
                {
                    for (int lane = 0; lane < 8; lane++)
                    {
                        if (lane >= openFrom[row] && lane < openFrom[row] + 2)
                            continue;
                        Add(s, row * 1.70f, .10f + lane * .114f,
                            .045f, -90f, v * 1.12f, row % 2, 9f);
                    }
                }
                Add(s, 4.25f, .467f, .045f, -90f,
                    v * 1.12f, 2, 9f);
                break;
            case "boot_sync":
                // Offbeats stay distinct. Left/right pairs occupy different
                // parts of the board and leave the middle mostly open.
                float[] syncHits = { 0f, .75f, 1.50f, 2.50f, 3.25f, 4.25f };
                for (int i = 0; i < syncHits.Length; i++)
                {
                    bool fromLeft = i % 2 == 0;
                    for (int wing = -1; wing <= 1; wing += 2)
                    {
                        Add(s, syncHits[i],
                            fromLeft ? .23f : .77f, .05f,
                            -90f + (fromLeft ? 17f : -17f) + wing * 11f,
                            v * 1.10f, i % 2, 9f);
                    }
                }
                Add(s, 1.85f, .467f, .05f, -90f,
                    v * 1.12f, 2, 9f);
                Add(s, 3.75f, .535f, .05f, -90f,
                    v * 1.12f, 2, 9f);
                break;

            // Full-Band Hit: eight deliberately different dodge gestures.
            // The chart is click-timed globally; note graphic positions do NOT
            // constrain the Pino's cursor path. Keep the centre/side escapes
            // readable and send accents in short musical groups.
            // Each pellet begins just above the playfield, not on the Pino.
            case "fh_beat":
                // Left/right 2-shot calls. The gap is on the opposite side.
                for (int call = 0; call < 4; call++)
                {
                    bool left = call % 2 == 0;
                    for (int wing = -1; wing <= 1; wing += 2)
                        Add(s, call * 1.20f, left ? .20f : .80f, -.055f,
                            -90f + (left ? 12f : -12f) + wing * 9f,
                            .174f, call % 2, 10f);
                }
                break;
            case "fh_curtain":
                // Three clean curtains separated by ample movement time.
                // The broad central hole drifts only one narrow step.
                for (int row = 0; row < 3; row++)
                {
                    float shift = row == 1 ? .055f : row == 2 ? -.055f : 0f;
                    float[] lanes = { .12f, .29f, .71f, .88f };
                    for (int lane = 0; lane < lanes.Length; lane++)
                        Add(s, row * 2.35f, lanes[lane] + shift, -.055f,
                            -90f, .170f, row % 2, 9f);
                }
                break;
            case "fh_cross":
                // Two symmetric slants, alternating in three separated beats.
                // Aim is never homing; the middle changes pressure gradually.
                for (int wave = 0; wave < 3; wave++)
                {
                    float bend = wave == 1 ? 4f : 0f;
                    Add(s, wave * 1.80f, .17f, -.055f,
                        -75f + bend, .175f, 0, 10f);
                    Add(s, wave * 1.80f, .83f, -.055f,
                        -105f - bend, .175f, 1, 10f);
                }
                break;
            case "fh_gate":
                // A three-lane-wide safe channel: centre -> one lane right ->
                // centre. Never teleport the opening across the whole screen.
                int[] safeFrom = { 2, 3, 2 };
                for (int row = 0; row < safeFrom.Length; row++)
                    for (int lane = 0; lane < 7; lane++)
                    {
                        if (lane >= safeFrom[row] &&
                            lane < safeFrom[row] + 3) continue;
                        Add(s, row * 2.40f, .11f + lane * .13f, -.055f,
                            -90f, .171f, row % 2, 10f);
                    }
                break;
            case "fh_sweep":
                // One stroke left-to-right, then an answering return.
                // Individual accents do not form an instant solid wall.
                for (int step = 0; step < 8; step++)
                {
                    int lane = step < 4 ? step : 7 - step;
                    Add(s, step * .77f, .18f + lane * .20f, -.055f,
                        -90f + (step < 4 ? 4f : -4f), .176f,
                        step < 4 ? 0 : 1, 10f);
                }
                break;
            case "fh_orbit":
                // Small, predictable sine motion; fewer shots than a rain grid.
                // This is a moving stream, not a screen-filling spiral.
                for (int step = 0; step < 10; step++)
                {
                    float phase = step * .64f;
                    Add(s, step * .68f,
                        .50f + .22f * Mathf.Sin(phase), -.055f,
                        -90f + 7f * Mathf.Cos(phase), .171f,
                        step % 2, 9f, 0f, .024f, .17f, step * .22f);
                }
                break;
            case "fh_comet":
                // A visible seed separates into four children after two beats.
                // Children appear at the actual split point (no teleport).
                for (int seed = 0; seed < 2; seed++)
                {
                    float start = seed * 3.20f;
                    Add(s, start, seed == 0 ? .28f : .72f, -.055f,
                        -90f, .169f, 3, 2f);
                    Pellet parent = s[s.Count - 1];
                    Vector2 split = Position(parent, 2f, 1f, 1f);
                    for (int wing = -2; wing <= 2; wing++)
                    {
                        if (wing == 0) continue;
                        Add(s, start + 2f, split.x + .5f,
                            .5f - split.y, -90f + wing * 14f,
                            .180f, 3, 9f);
                    }
                }
                break;
            case "fh_finale":
                // Open -> answer -> central accent -> parting fan.
                // Different screen regions take turns; no all-lane flood.
                float[] finalBeats = { 0f, 1.45f, 2.90f, 4.40f };
                for (int phase = 0; phase < finalBeats.Length; phase++)
                {
                    float[] positions = phase == 0 ?
                        new float[] { .22f, .78f } :
                        phase == 1 ? new float[] { .12f, .26f, .40f } :
                        phase == 2 ? new float[] { .46f, .54f } :
                        new float[] { .60f, .74f, .88f };
                    foreach (float x in positions)
                        Add(s, finalBeats[phase], x, -.055f,
                            -90f + (x < .5f ? 5f : -5f),
                            .178f, phase % 3, 10f);
                }
                break;

            // Readable mixed-mode motifs. Each has a different dodge rule
            // (fan, cross, gap, sweep, echo, split, moving ribbon).
            // Max ~16 projectiles per event, no procedural random walls.
            case "mix_arc":
                // Alternating four-shot fans from distinct left/right sources.
                for (int volley = 0; volley < 3; volley++)
                {
                    bool left = volley % 2 == 0;
                    float x = left ? .23f : .77f;
                    for (int wing = -2; wing <= 2; wing++)
                    {
                        if (wing == 0) continue;
                        Add(s, volley * 1.55f, x, .055f,
                            -90f + (left ? 13f : -13f) + wing * 10f,
                            v * 1.08f, volley % 2, 10f);
                    }
                }
                break;
            case "mix_cross":
                // Mirrored diagonal pairs alternate; the middle stays readable.
                for (int wave = 0; wave < 3; wave++)
                {
                    float outward = wave % 2 == 0 ? 0f : 7f;
                    for (int wing = 0; wing < 2; wing++)
                    {
                        Add(s, wave * 1.45f,
                            .14f, .06f, -90f + 24f + outward + wing * 12f,
                            v * 1.05f, 1, 10f);
                        Add(s, wave * 1.45f + .50f,
                            .86f, .06f, -90f - 24f - outward - wing * 12f,
                            v * 1.05f, 0, 10f);
                    }
                }
                break;
            case "mix_gate":
                // Three lanes of deliberately open corridors; openings shift.
                int[] gateOffsets = { 1, 3, 2 };
                for (int row = 0; row < gateOffsets.Length; row++)
                    for (int lane = 0; lane < 7; lane++)
                    {
                        if (lane >= gateOffsets[row] &&
                            lane < gateOffsets[row] + 2) continue;
                        Add(s, row * 1.65f, .11f + lane * .13f, .045f,
                            -90f, v * 1.06f, row % 2, 9f);
                    }
                break;
            case "mix_sweep":
                // A single wave moves right, then answers from the left.
                for (int step = 0; step < 10; step++)
                {
                    float x = step < 5 ? .15f + step * .16f :
                        .85f - (step - 5) * .16f;
                    Add(s, step * .55f, x, .05f,
                        -90f + (step < 5 ? 8f : -8f),
                        v * 1.12f, step < 5 ? 1 : 0, 9f);
                }
                break;
            case "mix_echo":
                // Call and response: three notes left, then three right.
                for (int call = 0; call < 3; call++)
                {
                    bool left = call % 2 == 0;
                    for (int wing = -1; wing <= 1; wing++)
                        Add(s, call * 1.8f,
                            left ? .29f : .71f, .055f,
                            -90f + (left ? 18f : -18f) + wing * 12f,
                            v * 1.10f, call % 2, 9f);
                }
                break;
            case "mix_comet":
                // A slow seed splits into four readable diagonal children.
                for (int seed = 0; seed < 2; seed++)
                {
                    float start = seed * 2.25f;
                    float sourceX = seed == 0 ? .30f : .70f;
                    Add(s, start, sourceX, .05f, -90f,
                        v * .76f, 3, 2.0f);
                    Pellet parent = s[s.Count - 1];
                    Vector2 splitPoint = Position(parent, 2.0f, 1f, 1f);
                    for (int wing = -2; wing <= 2; wing++)
                    {
                        if (wing == 0) continue;
                        Add(s, start + 2f, splitPoint.x + .5f,
                            .5f - splitPoint.y, -90f + wing * 17f,
                            v * 1.05f, 3, 9f);
                    }
                }
                break;
            case "mix_ribbon":
                // A quiet sine-like stream, visibly curving without tracking.
                for (int step = 0; step < 12; step++)
                {
                    float x = .50f + .27f * Mathf.Sin(step * .45f);
                    Add(s, step * .45f, x, .055f,
                        -90f + 10f * Mathf.Cos(step * .45f),
                        v * 1.08f, step % 2, 10f,
                        0f, .035f, .21f, step * .4f);
                }
                break;

            // Pop'n Dash: nine chart-authored motifs. The editor and runtime
            // consume the same deterministic trajectories; no random spawns.
            // Each has a recognizable rhythm and open routes for mouse notes.
            case "pop_twinfan":
                for (int volley = 0; volley < 3; volley++)
                    for (int side = 0; side < 2; side++)
                        for (int wing = -1; wing <= 1; wing++)
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
                for (int phrase = 0; phrase < 3; phrase++)
                {
                    bool left = phrase % 2 == 0;
                    float sourceX = left ? .28f : .72f;
                    for (int echo = 0; echo < 2; echo++)
                        for (int wing = -1; wing <= 1; wing++)
                            Add(s, phrase * 1.85f + echo * .28f,
                                sourceX, .07f,
                                -90f + wing * 15f + (left ? 8f : -8f),
                                v * (.86f + echo * .06f), 1, 12f);
                }
                break;
            case "pop_pendulum":
                for (int step = 0; step < 9; step++)
                {
                    float sourceX = .50f + .23f * Mathf.Sin(step * .48f);
                    for (int side = -1; side <= 1; side += 2)
                        Add(s, step * .62f,
                            sourceX, .06f, -90f + side * 14f,
                            v * .94f, 2, 12f);
                }
                break;
            case "pop_ribbon":
                for (int step = 0; step < 16; step++)
                {
                    float sourceX = .50f + .21f * Mathf.Sin(step * .33f);
                    Add(s, step * .34f, sourceX, .035f,
                        -90f + 9f * Mathf.Cos(step * .33f),
                        v * .96f, 2, 12f, 0f,
                        .040f, .29f, step * .38f);
                }
                break;
            case "pop_gate":
                for (int row = 0; row < 4; row++)
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
                for (int tick = 0; tick < 12; tick++)
                    for (int arm = 0; arm < 2; arm++)
                    {
                        float a = -90f + tick * 30f + arm * 180f;
                        Add(s, tick * .50f, .50f, .12f, a,
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
                for (int pulse = 0; pulse < 5; pulse++)
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
        float sourceCenterX = Mathf.Clamp(GetParameter(encoded, "x", .5f), .2f, .8f);
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
            pellet.origin.x = Mathf.Clamp(sourceCenterX + (pellet.origin.x - .5f) * spread, .035f, .965f);
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
