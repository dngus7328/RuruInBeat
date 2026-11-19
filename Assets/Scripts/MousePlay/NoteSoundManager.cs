using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 《루루 인 비트》의 실제 노트 판정 사운드 관리자.
/// 판정 로직은 소유하지 않으며, 실제 노트 판정/피격 시 호출된다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class NoteSoundManager : MonoBehaviour
{
    public enum NoteSoundType
    {
        Normal,
        LongStart,
        LongRelease
    }

    public enum NoteJudgement
    {
        Perfect,
        Great,
        Good,
        Miss
    }

    [System.Serializable]
    public class JudgementClips
    {
        public AudioClip perfect;
        public AudioClip great;
        public AudioClip good;
        public AudioClip miss;

        public AudioClip Get(NoteJudgement judgement)
        {
            switch (judgement)
            {
                case NoteJudgement.Perfect: return perfect;
                case NoteJudgement.Great: return great;
                case NoteJudgement.Good: return good;
                case NoteJudgement.Miss: return miss;
                default: return null;
            }
        }
    }

    [Header("노트 종류별 판정음")]
    [SerializeField] private JudgementClips normal = new JudgementClips();
    [SerializeField] private JudgementClips longStart = new JudgementClips();
    [SerializeField] private JudgementClips longRelease = new JudgementClips();

    [Header("별도 이벤트음")]
    [SerializeField] private AudioClip longHoldTick;
    [SerializeField] private AudioClip pinoHit;

    [Header("음량")]
    [Range(0f, 1f)] [SerializeField] private float noteVolume = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float holdVolume = 0.4f;
    [Range(0f, 1f)] [SerializeField] private float hitVolume = 0.8f;

    private AudioSource source;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        LoadBuiltInPalette();
    }

    // Shared mouse scene has no manually wired hitsound manager. Install a
    // plain audio-only object after loading it. Nothing is created in the UI.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureMouseModeSound()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "Mouse")
            return;

        NoteSoundManager[] existing =
            Object.FindObjectsOfType<NoteSoundManager>(true);
        foreach (NoteSoundManager candidate in existing)
            if (candidate != null && candidate.gameObject.scene == scene)
                return;

        GameObject go = new GameObject(
            "MouseNoteJudgementAudio", typeof(AudioSource),
            typeof(NoteSoundManager));
        SceneManager.MoveGameObjectToScene(go, scene);
    }

    private void LoadBuiltInPalette()
    {
        MouseNoteAudioPalette palette =
            Resources.Load<MouseNoteAudioPalette>("MouseNoteAudioPalette");
        if (palette == null)
        {
            Debug.LogWarning("[MouseAudio] MouseNoteAudioPalette missing; " +
                "load/import Assets/Resources/MouseNoteAudioPalette.asset", this);
            return;
        }

        // Respect clips a designer already configured in the Inspector.
        if (normal.perfect == null) normal.perfect = palette.normalPerfect;
        if (normal.great == null) normal.great = palette.normalGreat;
        if (normal.good == null) normal.good = palette.normalGood;
        if (normal.miss == null) normal.miss = palette.normalMiss;
        if (longStart.perfect == null)
            longStart.perfect = palette.longStartPerfect;
        if (longStart.great == null)
            longStart.great = palette.longStartGreat;
        if (longStart.good == null)
            longStart.good = palette.longStartGood;
        if (longStart.miss == null)
            longStart.miss = palette.longStartMiss;
        if (longRelease.perfect == null)
            longRelease.perfect = palette.longReleasePerfect;
        if (longRelease.great == null)
            longRelease.great = palette.longReleaseGreat;
        if (longRelease.good == null)
            longRelease.good = palette.longReleaseGood;
        if (longRelease.miss == null)
            longRelease.miss = palette.longReleaseMiss;
        if (longHoldTick == null)
            longHoldTick = palette.holdTick;
        if (pinoHit == null)
            pinoHit = palette.pinoHit;
    }

    /// <summary>노트의 실제 판정이 확정된 순간 한 번 호출.</summary>
    public void PlayJudgement(NoteSoundType noteType, NoteJudgement judgement)
    {
        JudgementClips clips = null;
        switch (noteType)
        {
            case NoteSoundType.Normal: clips = normal; break;
            case NoteSoundType.LongStart: clips = longStart; break;
            case NoteSoundType.LongRelease: clips = longRelease; break;
        }

        if (clips != null)
            Play(clips.Get(judgement), noteVolume);
    }

    /// <summary>롱노트 유지 틱이 발생했을 때만 호출.</summary>
    public void PlayLongHoldTick()
    {
        Play(longHoldTick, holdVolume);
    }

    /// <summary>피노가 실제로 피격되었을 때만 호출.</summary>
    public void PlayPinoHit()
    {
        Play(pinoHit, hitVolume);
    }

    private void Play(AudioClip clip, float volume)
    {
        if (clip == null || volume <= 0f) return;
        source.PlayOneShot(clip, volume);
    }
}
