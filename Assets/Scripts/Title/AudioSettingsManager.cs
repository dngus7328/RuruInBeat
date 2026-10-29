using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioSettingsManager : MonoBehaviour
{
    public static AudioSettingsManager Instance { get; private set; }

    [Header("Optional Audio Sources")]
    [Tooltip("나중에 효과음 AudioSource들을 추가. 비워둬도 됩니다.")]
    [SerializeField] private AudioSource[] sfxSources;

    [Tooltip("나중에 타격음 AudioSource들을 추가. 비워둬도 됩니다.")]
    [SerializeField] private AudioSource[] hitSoundSources;

    private AudioSource musicSource;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad
    )]
    private static void CreateAutomatically()
    {
        if (Instance != null)
            return;

        AudioSettingsManager existing =
            FindFirstObjectByType<AudioSettingsManager>();

        if (existing != null)
        {
            Instance = existing;
            existing.FindMusicSource();
            existing.ApplyAll();
            return;
        }

        GameObject obj =
            new GameObject("AudioSettingsManager");

        obj.AddComponent<AudioSettingsManager>();
    }

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;

        FindMusicSource();
        ApplyAll();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(
        Scene scene,
        LoadSceneMode mode
    )
    {
        FindMusicSource();
        ApplyAll();
    }

    private void FindMusicSource()
    {
        musicSource = null;

        SongManager songManager =
            FindFirstObjectByType<SongManager>();

        if (songManager == null)
            return;

        musicSource =
            songManager.GetComponent<AudioSource>();

        if (musicSource == null)
        {
            musicSource =
                songManager.GetComponentInChildren<AudioSource>(
                    true
                );
        }
    }

    public void ApplyAll()
    {
        ApplyMusic();
        ApplySFX();
        ApplyHitSound();
    }

    public void ApplyMusic()
    {
        if (musicSource == null)
        {
            FindMusicSource();
        }

        if (musicSource == null)
            return;

        musicSource.volume =
            GetMasterVolume() *
            GetMusicVolume();
    }

    public void ApplySFX()
    {
        if (sfxSources == null)
            return;

        float finalVolume =
            GetMasterVolume() *
            GetSFXVolume();

        for (int i = 0;
             i < sfxSources.Length;
             i++)
        {
            AudioSource source =
                sfxSources[i];

            if (source == null)
                continue;

            source.volume =
                finalVolume;
        }
    }

    public void ApplyHitSound()
    {
        if (hitSoundSources == null)
            return;

        float finalVolume =
            GetMasterVolume() *
            GetHitSoundVolume();

        for (int i = 0;
             i < hitSoundSources.Length;
             i++)
        {
            AudioSource source =
                hitSoundSources[i];

            if (source == null)
                continue;

            source.volume =
                finalVolume;
        }
    }

    private float GetMasterVolume()
    {
        if (SaveManager.Instance == null ||
            SaveManager.Instance.Data == null)
        {
            return 1f;
        }

        return Mathf.Clamp01(
            SaveManager.Instance.Data.masterVolume
        );
    }

    private float GetMusicVolume()
    {
        if (SaveManager.Instance == null ||
            SaveManager.Instance.Data == null)
        {
            return 1f;
        }

        return Mathf.Clamp01(
            SaveManager.Instance.Data.musicVolume
        );
    }

    private float GetSFXVolume()
    {
        if (SaveManager.Instance == null ||
            SaveManager.Instance.Data == null)
        {
            return 1f;
        }

        return Mathf.Clamp01(
            SaveManager.Instance.Data.sfxVolume
        );
    }

    private float GetHitSoundVolume()
    {
        if (SaveManager.Instance == null ||
            SaveManager.Instance.Data == null)
        {
            return 1f;
        }

        return Mathf.Clamp01(
            SaveManager.Instance.Data.hitSoundVolume
        );
    }
}