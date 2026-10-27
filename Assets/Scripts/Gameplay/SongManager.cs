using UnityEngine;

public class SongManager : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip songClip;

    [Header("Start")]
    [SerializeField] private float startDelay = 1f;
    [SerializeField] private bool playOnStart = true;

    private double songStartDspTime;
    private bool started;

    public bool IsStarted => started;

    public double SongTime
    {
        get
        {
            if (!started)
                return -startDelay;

            return AudioSettings.dspTime - songStartDspTime;
        }
    }

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void Start()
    {
        if (playOnStart)
        {
            StartSong();
        }
    }

    public void StartSong()
    {
        songStartDspTime =
            AudioSettings.dspTime + startDelay;

        started = true;

        if (audioSource == null)
            return;

        if (songClip == null)
            return;

        audioSource.clip = songClip;
        audioSource.playOnAwake = false;

        audioSource.PlayScheduled(
            songStartDspTime
        );
    }
}