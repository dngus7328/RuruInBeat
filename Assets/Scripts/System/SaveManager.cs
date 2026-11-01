using System;
using System.IO;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    // =========================================================
    // Display Mode
    // =========================================================

    public const int WINDOW_MODE_FULLSCREEN = 0;
    public const int WINDOW_MODE_BORDERLESS = 1;
    public const int WINDOW_MODE_WINDOWED = 2;

    // -1 = 현재 모니터 네이티브 해상도
    public const int RESOLUTION_NATIVE = -1;

    [Serializable]
    public class SaveData
    {
        // =====================================================
        // GAME
        // =====================================================

        public int judgeOffset = 0;
        public float noteSpeed = 5f;
        public float brightness = 100f;
        public bool effectDisplay = true;

        // =====================================================
        // AUDIO
        // =====================================================

        public float masterVolume = 1f;
        public float musicVolume = 1f;
        public float sfxVolume = 1f;
        public float hitSoundVolume = 1f;

        // =====================================================
        // DISPLAY
        // =====================================================

        // 0 = Fullscreen
        // 1 = Borderless Window
        // 2 = Windowed
        public int windowMode =
            WINDOW_MODE_BORDERLESS;

        // -1 = 모니터 네이티브 해상도
        // 0 이상 = 해상도 Dropdown Index
        public int resolutionIndex =
            RESOLUTION_NATIVE;

        // 기존 코드 호환용
        // Borderless도 화면 전체를 사용하므로 true
        public bool fullscreen = true;

        public bool vSync = false;

        public int targetFrameRate = 120;
    }

    public SaveData Data { get; private set; }

    private string savePath;

    // =========================================================
    // Auto Create
    // =========================================================

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
            new GameObject(
                "SaveManager"
            );

        obj.AddComponent<SaveManager>();
    }

    // =========================================================
    // Awake
    // =========================================================

    private void Awake()
    {
        if (
            Instance != null &&
            Instance != this
        )
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(
            gameObject
        );

        savePath =
            Path.Combine(
                Application.persistentDataPath,
                "save.json"
            );

        Load();
    }

    // =========================================================
    // Save
    // =========================================================

    public void Save()
    {
        if (Data == null)
        {
            Data =
                new SaveData();
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

    // =========================================================
    // Load
    // =========================================================

    public void Load()
    {
        if (!File.Exists(savePath))
        {
            Data =
                new SaveData();

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
                Data =
                    new SaveData();
            }

            // =============================================
            // GAME
            // =============================================

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

            // =============================================
            // DISPLAY
            // =============================================

            Data.windowMode =
                Mathf.Clamp(
                    Data.windowMode,
                    WINDOW_MODE_FULLSCREEN,
                    WINDOW_MODE_WINDOWED
                );

            // -1은 네이티브 해상도이므로 허용
            if (Data.resolutionIndex < RESOLUTION_NATIVE)
            {
                Data.resolutionIndex =
                    RESOLUTION_NATIVE;
            }

            // 지원 해상도 인덱스가 현재 0~4라면
            // 그 이상은 네이티브로 복구
            if (Data.resolutionIndex > 4)
            {
                Data.resolutionIndex =
                    RESOLUTION_NATIVE;
            }

            // FPS 허용값 보정
            switch (Data.targetFrameRate)
            {
                case 60:
                case 120:
                case 144:
                case 240:
                case -1:
                    break;

                default:
                    Data.targetFrameRate = 120;
                    break;
            }

            // Borderless / Fullscreen은
            // 기존 fullscreen bool과도 동기화
            Data.fullscreen =
                Data.windowMode !=
                WINDOW_MODE_WINDOWED;
        }
        catch (Exception e)
        {
            Debug.LogError(
                "SaveManager 불러오기 실패: " +
                e.Message
            );

            Data =
                new SaveData();
        }
    }

    // =========================================================
    // Manual Save
    // =========================================================

    public void SaveNow()
    {
        Save();
    }

    // =========================================================
    // Application
    // =========================================================

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