using System;
using System.Collections.Generic;
using UnityEngine;

public enum ChartNoteType
{
    Tap,
    Long,
    Error,
    Hit
}

[Serializable]
public class ChartNoteData
{
    public NoteLane lane = NoteLane.Q;

    public ChartNoteType type = ChartNoteType.Tap;

    [Min(0f)]
    public float hitTime = 0f;

    [Min(0f)]
    public float duration = 0f;
}

[CreateAssetMenu(
    fileName = "NewChart",
    menuName = "Ruru In Beat/Chart Data"
)]
public class ChartData : ScriptableObject
{
    [Header("Music")]
    public AudioClip audioClip;

    [Header("Timing")]
    [Min(1f)]
    public float bpm = 120f;

    public float offsetSeconds = 0f;

    [Header("Gameplay")]
    [Min(0.1f)]
    public float travelTime = 1.5f;

    [Header("Notes")]
    public List<ChartNoteData> notes =
        new List<ChartNoteData>();
}
