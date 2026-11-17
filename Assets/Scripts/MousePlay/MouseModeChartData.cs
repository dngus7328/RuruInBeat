using System;
using System.Collections.Generic;
using UnityEngine;

namespace LuluInBeat.MouseMode.ChartSystem
{
    public enum MouseModeNoteKind
    {
        Left,
        Right,
        Wheel
    }

    [Serializable]
    public class MouseModeChartNote
    {
        public int id;
        public MouseModeNoteKind kind;
        public float beat = 4f;
        public Vector2 position = new Vector2(0.5f, 0.5f);

        // Wheel 노트일 때만 사용한다.
        public float endBeat = 6f;
        public List<Vector2> path = new List<Vector2>();
    }

    [Serializable]
    public class MouseModeChartAttack
    {
        public float beat = 4f;
        public string patternId = "basic";
    }

    [Serializable]
    public class MouseModeChartData
    {
        public int version = 1;
        public string songId = "new_song";
        // 채보별 임시 레벨; 플레이 테스트 후 확정. 기존 JSON에는 기본값 1 적용.
        public int level = 1;
        public string difficulty = "NORMAL";
        public float bpm = 120f;
        public float beatZeroSeconds = 0f;
        public float approachSeconds = 1.2f;
        public List<MouseModeChartNote> notes = new List<MouseModeChartNote>();
        public List<MouseModeChartAttack> attacks = new List<MouseModeChartAttack>();

        public void EnsureLists()
        {
            if (notes == null) notes = new List<MouseModeChartNote>();
            if (attacks == null) attacks = new List<MouseModeChartAttack>();
            foreach (MouseModeChartNote note in notes)
                if (note != null && note.path == null)
                    note.path = new List<Vector2>();
        }

        public int NextId()
        {
            EnsureLists();
            int result = 1;
            foreach (MouseModeChartNote note in notes)
                if (note != null) result = Math.Max(result, note.id + 1);
            return result;
        }
    }

    [CreateAssetMenu(fileName = "NewMouseChart",
        menuName = "Lulu In Beat/Mouse Mode/Chart")]
    public class MouseModeChartAsset : ScriptableObject
    {
        public AudioClip song;
        public MouseModeChartData data = new MouseModeChartData();
    }

    public static class MouseModeChartTiming
    {
        public static double BeatToSeconds(MouseModeChartData data, float beat)
        {
            return data.beatZeroSeconds + beat * 60.0 / Math.Max(1.0, data.bpm);
        }

        // 좌상단 (0,0), 우하단 (1,1) 좌표.
        public static Vector2 GetPathPosition(MouseModeChartNote note, float progress)
        {
            if (note == null) return new Vector2(0.5f, 0.5f);
            if (note.path == null || note.path.Count == 0) return note.position;
            if (note.path.Count == 1) return note.path[0];

            float total = 0f;
            for (int i = 1; i < note.path.Count; i++)
                total += Vector2.Distance(note.path[i - 1], note.path[i]);

            if (total < 0.0001f) return note.path[0];
            float remaining = Mathf.Clamp01(progress) * total;

            for (int i = 1; i < note.path.Count; i++)
            {
                Vector2 a = note.path[i - 1];
                Vector2 b = note.path[i];
                float length = Vector2.Distance(a, b);
                if (remaining <= length || i == note.path.Count - 1)
                    return Vector2.Lerp(a, b,
                        length > 0.0001f ? Mathf.Clamp01(remaining / length) : 1f);
                remaining -= length;
            }
            return note.path[note.path.Count - 1];
        }
    }
}
