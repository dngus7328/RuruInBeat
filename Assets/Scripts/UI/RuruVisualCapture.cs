using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using LuluInBeat.MouseMode.ChartSystem;

// Explicit command-line opt-in. Normal game launches never install this component.
public sealed class RuruVisualCapture : MonoBehaviour
{
    private string output;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        string[] args=Environment.GetCommandLineArgs();
        int i=Array.IndexOf(args,"-ruru-ui-capture");
        if(i<0 || i+1>=args.Length) return;
        var go=new GameObject("RuruVisualCapture_QA");
        DontDestroyOnLoad(go);
        var qa=go.AddComponent<RuruVisualCapture>();
        qa.output=Path.GetFullPath(args[i+1]);
        Directory.CreateDirectory(qa.output);
        Application.runInBackground=true;
        Screen.SetResolution(1920,1080,FullScreenMode.Windowed);
        qa.StartCoroutine(qa.Run());
    }
    private IEnumerator Shot(string name)
    {
        Canvas.ForceUpdateCanvases();
        yield return new WaitForEndOfFrame();
        var image=ScreenCapture.CaptureScreenshotAsTexture();
        if(image==null) { Debug.LogError("Missing QA screenshot: "+name); Application.Quit(2); yield break; }
        File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
        Destroy(image);
    }
    private IEnumerator Load(string scene)
    {
        Time.timeScale=1f; AudioListener.pause=false;
        yield return SceneManager.LoadSceneAsync(scene,LoadSceneMode.Single);
        yield return new WaitForSecondsRealtime(2f);
    }
    private IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(7f);
        yield return Shot("01-title");
        var title=FindObjectOfType<TitleMenuController>();
        if(title!=null)
        {
            title.SetInputEnabled(true); title.OpenSettings();
            yield return new WaitForSecondsRealtime(.7f);
            yield return Shot("02-settings");
        }
        yield return Load("SongSelectScene"); yield return Shot("03-song-select");
        yield return Load("Tutorial"); yield return new WaitForSecondsRealtime(2f);
        yield return Shot("04-tutorial-dialogue");
        var catalog = Resources.Load<SongSelectChartCatalog>("SongSelectChartCatalog");
        if (catalog != null)
        {
            foreach (var chart in catalog.chartFiles)
                if (chart != null && chart.data != null && chart.data.songId == "FullBandHit_MOUSE")
                { MouseModeSongSelection.Choose(chart); break; }
        }
        yield return Load("Mouse");
        var boot=FindObjectOfType<MouseBootSequenceUI>();
        float deadline=Time.realtimeSinceStartup+22f;
        while(boot!=null && !boot.IsInitialGoFinished && Time.realtimeSinceStartup<deadline) yield return null;
        yield return new WaitForSecondsRealtime(.8f);
        yield return Shot("05-gameplay");
        var pino=FindObjectOfType<PinoMouseCursorUI>(true);
        var pause=FindObjectOfType<MouseRhythmPauseUI>(true);
        if(pino!=null)
        {
            pino.PauseGame(); yield return new WaitForSecondsRealtime(.65f);
            yield return Shot("06-pause");
            pino.ResumeGame(); yield return new WaitForSecondsRealtime(6f);
        }
        else if(pause!=null)
        {
            pause.PauseGame(); yield return new WaitForSecondsRealtime(.65f);
            yield return Shot("06-pause");
            pause.ResumeGame(); yield return new WaitForSecondsRealtime(6f);
        }
        var result=FindObjectOfType<MouseModeResultFlow>();
        var finish=typeof(MouseModeResultFlow).GetMethod("Finish",BindingFlags.Instance|BindingFlags.NonPublic);
        if(result!=null && finish!=null)
        {
            finish.Invoke(result,new object[]{true});
            yield return new WaitForSecondsRealtime(2f);
            yield return Shot("07-result");
        }
        Application.Quit(0);
    }
}
