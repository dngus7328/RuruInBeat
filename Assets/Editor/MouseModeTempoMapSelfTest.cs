#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using LuluInBeat.MouseMode.ChartSystem;

public static class MouseModeTempoMapSelfTest
{
    [MenuItem("Tools/Lulu In Beat/Verify Tempo Map Timing")]
    private static void Run()
    {
        try
        {
            MouseModeChartData chart = new MouseModeChartData
            {
                bpm = 120f,
                beatZeroSeconds = 0.1f
            };
            chart.EnsureLists();

            Check("Legacy beat 8", MouseModeChartTiming.BeatToSeconds(chart, 8f), 4.1);
            Check("Legacy inverse", MouseModeChartTiming.SecondsToBeat(chart, 4.1), 8d);

            chart.tempoChanges.Add(new MouseModeTempoChange { beat = 8f, bpm = 180f });
            chart.tempoChanges.Add(new MouseModeTempoChange { beat = 16f, bpm = 90f });
            chart.EnsureLists();

            Check("First change boundary", MouseModeChartTiming.BeatToSeconds(chart, 8f), 4.1);
            Check("180 BPM segment", MouseModeChartTiming.BeatToSeconds(chart, 12f),
                4.1 + 4d * 60d / 180d);
            Check("Second change boundary", MouseModeChartTiming.BeatToSeconds(chart, 16f),
                4.1 + 8d * 60d / 180d);
            Check("90 BPM segment", MouseModeChartTiming.BeatToSeconds(chart, 20f),
                4.1 + 8d * 60d / 180d + 4d * 60d / 90d);
            Check("Local BPM at beat 12", MouseModeChartTiming.SecondsPerBeatAt(chart, 12f),
                60d / 180d);
            Check("Negative beat", MouseModeChartTiming.BeatToSeconds(chart, -2f), -0.9d);

            for (int i = 0; i <= 320; i++)
            {
                float beat = i * 0.25f;
                double seconds = MouseModeChartTiming.BeatToSeconds(chart, beat);
                Check("Round-trip beat " + beat.ToString("0.##"),
                    MouseModeChartTiming.SecondsToBeat(chart, seconds), beat);
            }

            Debug.Log("[MouseTempoMap] PASS: Legacy, boundaries, local BPM, negative beat and 321 round trips.");
            EditorUtility.DisplayDialog("Tempo Map Test", "모든 시간변환 검사에 통과했습니다. Unity Play Mode에서 노트/탄막 실제 동작은 별도 검사해야 합니다.", "확인");
        }
        catch (Exception exception)
        {
            Debug.LogError("[MouseTempoMap] FAIL: " + exception);
            EditorUtility.DisplayDialog("Tempo Map Test", "검사 실패: " + exception.Message, "확인");
        }
    }

    private static void Check(string label, double actual, double expected)
    {
        if (Math.Abs(actual - expected) > 0.00001d)
            throw new Exception(label + ": got " + actual.ToString("0.000000") +
                ", expected " + expected.ToString("0.000000"));
    }
}
#endif
