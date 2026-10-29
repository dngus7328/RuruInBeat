using System;
using System.IO;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [Serializable]
    public class SaveData
    {
        // GAME
        public int judgeOffset = 0;
        public float noteSpeed = 5f;
        public float brightness = 100f;
        public bool effectDisplay = true;

        // AUDIO
        public float masterVolume = 1f;
        public float musicVolume = 1f;
        public float sfxVolume = 1f;
        public float hitSoundVolume = 1f;

        // DISPLAY
        public int windowMode = 0;
        public int resolutionIndex = 2;
        public bool fullscreen = true;
        public bool vSync = true;
        public int targetFrameRate = 120;
    }

    public SaveData Data { get; private set; }

    private string savePath;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.BeforeSceneLoad
    )]
    private static void CreateAutomatically()
    {
        if (Instance != null)
            return;

        SaveManager existing =
            FindFirstObjectByType<SaveManager>();

        if (existing != null)
        {
            Instance = existing;
            return;
        }

        GameObject obj =
            new GameObject("SaveManager");

        obj.AddComponent<SaveManager>();
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

        savePath =
            Path.Combine(
                Application.persistentDataPath,
                "save.json"
            );

        Load();
    }

    public void Save()
    {
        if (Data == null)
        {
            Data = new SaveData();
        }

        try
        {
            string json =
                JsonUtility.ToJson(
                    Data,
                    true
                );

            File.WriteAllText(
                savePath,
                json
            );
        }
        catch (Exception e)
        {
            Debug.LogError(
                "SaveManager 저장 실패: " +
                e.Message
            );
        }
    }

    public void Load()
    {
        if (!File.Exists(savePath))
        {
            Data = new SaveData();

            Save();
            return;
        }

        try
        {
            string json =
                File.ReadAllText(
                    savePath
                );

            Data =
                JsonUtility.FromJson<SaveData>(
                    json
                );

            if (Data == null)
            {
                Data = new SaveData();
            }

            Data.judgeOffset =
                Mathf.Clamp(
                    Data.judgeOffset,
                    -30,
                    30
                );

            Data.noteSpeed =
                Mathf.Clamp(
                    Data.noteSpeed,
                    1f,
                    10f
                );

            Data.brightness =
                Mathf.Clamp(
                    Data.brightness,
                    0f,
                    100f
                );

            Data.windowMode =
                Mathf.Clamp(
                    Data.windowMode,
                    0,
                    2
                );

            Data.resolutionIndex =
                Mathf.Clamp(
                    Data.resolutionIndex,
                    0,
                    4
                );
        }
        catch (Exception e)
        {
            Debug.LogError(
                "SaveManager 불러오기 실패: " +
                e.Message
            );

            Data = new SaveData();
        }
    }

    public void SaveNow()
    {
        Save();
    }

    private void OnApplicationPause(
        bool pauseStatus
    )
    {
        if (pauseStatus)
        {
            Save();
        }
    }

    private void OnApplicationQuit()
    {
        Save();
    }
}