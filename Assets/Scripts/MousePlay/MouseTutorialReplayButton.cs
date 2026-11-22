using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// An opt-in tutorial replay action. Never inject a button into SongSelect.
/// First-time tutorial entry remains owned by TitleMenuController.
/// </summary>
public sealed class MouseTutorialReplayButton : MonoBehaviour
{
    private bool loading;

    public void Replay()
    {
        if (loading || SceneLoadingTransition.IsLoading)
            return;

        MouseModeSongSelection.Choose(null);
        loading = SceneLoadingTransition.Load("Tutorial");
    }
}
