using UnityEngine;

/// <summary>
/// Prefab-independent audio references for the six mouse-mode songs.
/// The original WAV files are reused; no synthesized or new audio assets.
/// </summary>
[CreateAssetMenu(menuName = "Lulu In Beat/Mouse Mode/Note Sound Palette")]
public sealed class MouseNoteAudioPalette : ScriptableObject
{
    [Header("Tap")]
    public AudioClip normalPerfect;
    public AudioClip normalGreat;
    public AudioClip normalGood;
    public AudioClip normalMiss;

    [Header("Wheel begin")]
    public AudioClip longStartPerfect;
    public AudioClip longStartGreat;
    public AudioClip longStartGood;
    public AudioClip longStartMiss;

    [Header("Wheel finish")]
    public AudioClip longReleasePerfect;
    public AudioClip longReleaseGreat;
    public AudioClip longReleaseGood;
    public AudioClip longReleaseMiss;

    [Header("Other")]
    public AudioClip holdTick;
    public AudioClip pinoHit;
}
