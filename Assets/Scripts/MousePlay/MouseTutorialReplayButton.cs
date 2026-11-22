using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Replays the tutorial from the existing Song Select screen.
/// Reuses a copy of that screen's authored Back button so the button's
/// material, font and dimensions match the current PC interface.
/// </summary>
public sealed class MouseTutorialReplayButton : MonoBehaviour
{
    private bool loading;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void DirectEntry()
    {
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || scene.name != "SongSelectScene")
            return;

        Button original = null;
        foreach (Button candidate in FindObjectsOfType<Button>(true))
        {
            if (candidate.gameObject.scene == scene &&
                candidate.name == "BackToTitle")
            {
                original = candidate;
                break;
            }
        }
        if (original == null ||
            original.transform.parent.Find("TutorialReplayButton") != null)
            return;

        GameObject copy = Instantiate(original.gameObject,
            original.transform.parent, false);
        copy.name = "TutorialReplayButton";
        RectTransform rect = copy.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = new Vector2(.143f, .875f);
            rect.anchorMax = new Vector2(.248f, .932f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        foreach (TMP_Text text in copy.GetComponentsInChildren<TMP_Text>(true))
            text.text = "튜토리얼";

        Button button = copy.GetComponent<Button>();
        if (button == null)
        {
            Destroy(copy);
            return;
        }
        button.onClick.RemoveAllListeners();
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        MouseTutorialReplayButton handler =
            copy.AddComponent<MouseTutorialReplayButton>();
        button.onClick.AddListener(handler.Replay);
    }

    private void Replay()
    {
        if (loading)
            return;

        MouseModeSongSelection.Choose(null);
        loading = SceneLoadingTransition.Load("Tutorial");
    }
}
