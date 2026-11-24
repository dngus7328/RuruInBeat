using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Ruru In Beat/Story Sequence", fileName = "StorySequence")]
public sealed class RuruStorySequence : ScriptableObject
{
    public string sceneTitle;
    public List<Line> lines = new List<Line>();

    [Serializable]
    public sealed class Line
    {
        public string speaker;
        [TextArea(3, 8)] public string dialogue;
        [Tooltip("Assign the independently drawn background; None keeps the previous background.")]
        public Sprite background;
        public bool clearBackground;
        [Tooltip("Assign the independently drawn front-facing dialogue portrait.")]
        public Sprite leftPortrait;
        public bool clearLeft;
        public Sprite rightPortrait;
        public bool clearRight;
        public SpeakerPosition speaking = SpeakerPosition.None;
        public Entrance entrance = Entrance.Fade;
        [Min(0f)] public float transitionSeconds = .2f;
    }

    public enum SpeakerPosition { None, Left, Right }
    public enum Entrance { Immediate, Fade, Slide }

    public int Count => lines == null ? 0 : lines.Count;

    public struct Frame
    {
        public Sprite background, left, right;
    }

    // Resolves the complete image state even when jumping between lines in the editor.
    public Frame Resolve(int index)
    {
        Frame state = default;
        if (lines == null) return state;
        for (int i = 0; i <= index && i < lines.Count; i++)
        {
            Line line = lines[i];
            if (line == null) continue;
            if (line.clearBackground) state.background = null;
            else if (line.background != null) state.background = line.background;
            if (line.clearLeft) state.left = null;
            else if (line.leftPortrait != null) state.left = line.leftPortrait;
            if (line.clearRight) state.right = null;
            else if (line.rightPortrait != null) state.right = line.rightPortrait;
        }
        return state;
    }
}
