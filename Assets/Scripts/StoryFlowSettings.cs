using System;
using System.Collections.Generic;
using UnityEngine;
using LuluInBeat.MouseMode.ChartSystem;

public enum StoryTrigger
{
    BeforeTutorial, AfterTutorial, AfterSongClear
}

[Serializable]
public sealed class StoryRoute
{
    public StoryTrigger trigger;
    public StoryData story;
    [Tooltip("곡 클리어 이후에만 사용. 비워 두면 모든 곡이 대상입니다.")]
    public MouseModeChartAsset chart;
    [Tooltip("해당 스토리를 끝까지 본 뒤에는 자동으로 재생하지 않습니다.")]
    public bool oncePerSave = true;
}

[CreateAssetMenu(menuName = "Ruru In Beat/Story/스토리 연결 설정", fileName = "StoryFlowSettings")]
public sealed class StoryFlowSettings : ScriptableObject
{
    public StoryRoute[] routes = new StoryRoute[0];
}

// All game transitions retain their original destination when no route matches.
public static class StoryFlow
{
    public const string SceneName = "StoryScene";
    private static readonly Queue<StoryRoute> pending = new Queue<StoryRoute>();
    private static string destination;
    public static StoryRoute Current { get; private set; }
    public static bool HasRoute => Current != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        pending.Clear();
        Current = null;
        destination = null;
    }

    public static StoryRoute[] SelectRoutes(StoryFlowSettings settings, StoryTrigger trigger,
        MouseModeChartAsset chart, List<string> completed)
    {
        var result = new List<StoryRoute>();
        if (settings == null || settings.routes == null) return result.ToArray();
        var seen = new HashSet<string>();
        foreach (StoryRoute route in settings.routes)
        {
            if (route == null || route.trigger != trigger || route.story == null ||
                !route.story.HasLines) continue;
            if (trigger == StoryTrigger.AfterSongClear && route.chart != null &&
                route.chart != chart) continue;
            if (route.oncePerSave && completed != null && completed.Contains(route.story.Id)) continue;
            if (seen.Add(route.story.Id)) result.Add(route);
        }
        return result.ToArray();
    }

    public static bool Load(StoryTrigger trigger, string fallback, MouseModeChartAsset chart = null)
    {
        if (SceneLoadingTransition.IsLoading) return false;
        var save = SaveManager.Instance;
        var settings = Resources.Load<StoryFlowSettings>("StoryFlowSettings");
        var routes = SelectRoutes(settings, trigger, chart,
            save != null && save.Data != null ? save.Data.completedStoryIds : null);
        if (routes.Length == 0 || !Application.CanStreamedLevelBeLoaded(SceneName))
            return SceneLoadingTransition.Load(fallback);
        if (!Application.CanStreamedLevelBeLoaded(fallback)) return SceneLoadingTransition.Load(fallback);
        pending.Clear();
        foreach (var route in routes) pending.Enqueue(route);
        destination = fallback;
        Current = pending.Dequeue();
        if (SceneLoadingTransition.Load(SceneName)) return true;
        Reset();
        return false;
    }

    // Stay in the same StoryScene for consecutive chapters; record only completed chapters.
    public static StoryData FinishCurrent()
    {
        if (Current == null) return null;
        var save = SaveManager.Instance;
        if (Current.oncePerSave && save != null && save.Data != null)
        {
            if (save.Data.completedStoryIds == null) save.Data.completedStoryIds = new List<string>();
            if (!save.Data.completedStoryIds.Contains(Current.story.Id))
            {
                save.Data.completedStoryIds.Add(Current.story.Id);
                save.Save();
            }
        }
        Current = pending.Count > 0 ? pending.Dequeue() : null;
        return Current != null ? Current.story : null;
    }

    public static bool Leave(string directExit)
    {
        string next = string.IsNullOrEmpty(destination) ? directExit : destination;
        if (!SceneLoadingTransition.Load(next)) return false;
        Reset();
        return true;
    }
}
