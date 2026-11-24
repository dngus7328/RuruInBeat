using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Launches the real Mouse scene against each selected song and asks the real
/// bullet director to create its runtime schedule. This smoke test catches
/// missing scene references, chart mismatches and pattern factory failures.
/// It does not claim to verify human playability or audiovisual sync.
/// </summary>
public sealed class MouseSixSongScenePlayModeTests
{
    private static readonly string[] ChartNames =
    {
        "BOOTBEAT_MOUSE_LV6",
        "ArcadeGroove_MOUSE",
        "FullBandHit_MOUSE",
        "MabatakiNoSenritsu_MOUSE",
        "RhythmGameTheme_MOUSE",
        "popn_dash"
    };

    private static readonly int[] AttackCounts = { 20, 17, 15, 16, 14, 19 };

    private static Type RuntimeType(string name)
    {
        Type found = Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(found, "Runtime type missing: " + name);
        return found;
    }

    private static FieldInfo Field(Type type, string name)
    {
        FieldInfo field = type.GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic |
            BindingFlags.Public);
        Assert.IsNotNull(field, "Missing field: " + type.Name + "." + name);
        return field;
    }

    private static void Invoke(object instance, string methodName)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.NonPublic |
            BindingFlags.Public);
        Assert.IsNotNull(method, "Missing method " + methodName);
        method.Invoke(instance, null);
    }

    private static int Count(object collection)
    {
        ICollection value = collection as ICollection;
        Assert.IsNotNull(value, "Expected a projectile collection.");
        return value.Count;
    }

    [UnityTest]
    public IEnumerator EverySelectedSongCreatesPlayableBulletScheduleInRealMouseScene()
    {
#if UNITY_EDITOR
        Time.timeScale = 1f;
        for (int index = 0; index < ChartNames.Length; index++)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(
                "Mouse", LoadSceneMode.Single);
            Assert.IsNotNull(load);
            yield return load;
            Assert.AreEqual("Mouse", SceneManager.GetActiveScene().name);

            Type bootType = RuntimeType("MouseBootSequenceUI");
            MonoBehaviour boot = UnityEngine.Object.FindObjectOfType(bootType)
                as MonoBehaviour;
            Assert.IsNotNull(boot);
            // The boot animation owns its own delayed activation. This test
            // manually starts its song so the test is deterministic.
            boot.StopAllCoroutines();
            boot.enabled = false;

            Type playerType = RuntimeType(
                "LuluInBeat.MouseMode.ChartSystem.MouseModeChartPlayer");
            MonoBehaviour player = UnityEngine.Object.FindObjectOfType(
                playerType) as MonoBehaviour;
            Assert.IsNotNull(player);
            player.enabled = false;

            string path = "Assets/MouseMode/ChartSystem/Charts/" +
                ChartNames[index] + ".asset";
            UnityEngine.Object chart = AssetDatabase.LoadMainAssetAtPath(path);
            Assert.IsNotNull(chart, "Missing playable song asset: " + path);
            Field(playerType, "chartAsset").SetValue(player, chart);
            Invoke(player, "StartChart");

            Assert.IsTrue((bool)playerType.GetProperty(
                "IsChartPlaying").GetValue(player),
                "Chart did not start for " + ChartNames[index]);

            Type directorType = RuntimeType("MouseModeBulletDirector");
            MonoBehaviour director = UnityEngine.Object.FindObjectOfType(
                directorType) as MonoBehaviour;
            Assert.IsNotNull(director, "Missing BulletDirector in Mouse scene");
            Invoke(director, "FindReferences");
            Invoke(director, "BuildScheduleIfNeeded");

            object scheduled = Field(directorType, "scheduledChart")
                .GetValue(director);
            Assert.AreSame(chart, scheduled,
                "BulletDirector did not follow the selected song.");

            int shotCount =
                Count(Field(directorType, "shots").GetValue(director)) +
                Count(Field(directorType, "advancedShots").GetValue(director)) +
                Count(Field(directorType, "rainCues").GetValue(director)) +
                Count(Field(directorType, "specialAttacks").GetValue(director));
            Assert.Greater(shotCount, 0,
                ChartNames[index] + " created no live barrage schedule.");

            object data = chart.GetType().GetField("data").GetValue(chart);
            ICollection attacks = data.GetType().GetField("attacks")
                .GetValue(data) as ICollection;
            Assert.IsNotNull(attacks);
            Assert.AreEqual(AttackCounts[index], attacks.Count,
                "Unexpected attack cadence for " + ChartNames[index]);

            // No gameplay graphics may appear during READY/3/2/1.
            Transform noteLayer = GameObject.Find("NoteLayer")?.transform;
            Assert.IsNotNull(noteLayer);
            Invoke(player, "StopChart");
            yield return null;
        }
#else
        Assert.Ignore("Scene asset load requires Unity Editor PlayMode.");
        yield break;
#endif
    }
    /// <summary>
    /// Exercise the real runtime input path, not just isolated timing formulas:
    /// a late left/right click must create a GREAT and a readable LATE label;
    /// a wrong mouse button must MISS the authored note, not award a hit;
    /// an empty click may notify the HUD but must not count as a chart note.
    /// The real Mouse scene, authored chart, UI objects and judgement callbacks
    /// are used. Inputs are injected at exact song-local timestamps so CI does
    /// not depend on physical mouse devices or real-time audio playback.
    /// </summary>
    [UnityTest]
    public IEnumerator SyntheticMouseInputProducesRealVerdictsAndOnScreenTimingFeedback()
    {
#if UNITY_EDITOR
        Time.timeScale = 1f;
        AsyncOperation load = SceneManager.LoadSceneAsync("Mouse", LoadSceneMode.Single);
        Assert.IsNotNull(load);
        yield return load;

        Type bootType = RuntimeType("MouseBootSequenceUI");
        MonoBehaviour boot = UnityEngine.Object.FindObjectOfType(bootType)
            as MonoBehaviour;
        Assert.IsNotNull(boot);
        boot.StopAllCoroutines();
        boot.enabled = false;

        Type playerType = RuntimeType(
            "LuluInBeat.MouseMode.ChartSystem.MouseModeChartPlayer");
        MonoBehaviour player = UnityEngine.Object.FindObjectOfType(playerType)
            as MonoBehaviour;
        Assert.IsNotNull(player);
        player.enabled = false;

        UnityEngine.Object chart = AssetDatabase.LoadMainAssetAtPath(
            "Assets/MouseMode/ChartSystem/Charts/BOOTBEAT_MOUSE_LV6.asset");
        Assert.IsNotNull(chart);
        Field(playerType, "chartAsset").SetValue(player, chart);
        Invoke(player, "StartChart");

        IList activeNotes = Field(playerType, "notes").GetValue(player)
            as IList;
        Assert.IsNotNull(activeNotes);
        object first = null;
        object second = null;
        Type noteType = null;
        foreach (object entry in activeNotes)
        {
            object data = Field(entry.GetType(), "data").GetValue(entry);
            object kind = Field(data.GetType(), "kind").GetValue(data);
            if (kind.ToString() != "Left" && kind.ToString() != "Right")
                continue;
            if (first == null) first = entry;
            else { second = entry; break; }
        }
        Assert.IsNotNull(first, "A playable song needs click notes.");
        Assert.IsNotNull(second, "A playable song needs two click notes.");
        noteType = first.GetType();

        MethodInfo createVisual = playerType.GetMethod("CreateVisual",
            BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo handleInput = playerType.GetMethod("HandleRhythmInput",
            BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo offsetMethod = playerType.GetMethod(
            "EffectiveJudgeOffsetSeconds",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(createVisual);
        Assert.IsNotNull(handleInput);
        Assert.IsNotNull(offsetMethod);
        double savedOffset = (double)offsetMethod.Invoke(player, null);

        List<string> actualNoteVerdicts = new List<string>();
        List<string> hudVerdicts = new List<string>();
        Action<string> noteListener = actualNoteVerdicts.Add;
        Action<string, int, int> hudListener =
            (result, combo, score) => hudVerdicts.Add(result);
        EventInfo chartEvent = playerType.GetEvent("ChartNoteJudged");
        EventInfo hudEvent = playerType.GetEvent("Judged");
        Assert.IsNotNull(chartEvent);
        Assert.IsNotNull(hudEvent);
        chartEvent.AddEventHandler(player, noteListener);
        hudEvent.AddEventHandler(player, hudListener);

        // Generate the same runtime graphic used by the live Update loop.
        createVisual.Invoke(player, new[] { first });
        RectTransform firstRoot = Field(noteType, "root")
            .GetValue(first) as RectTransform;
        Assert.IsNotNull(firstRoot);
        Canvas firstCanvas = firstRoot.GetComponentInParent<Canvas>();
        Camera firstCamera = firstCanvas != null &&
            firstCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? (firstCanvas.worldCamera != null
                    ? firstCanvas.worldCamera : Camera.main)
                : null;
        Vector2 firstScreen = RectTransformUtility.WorldToScreenPoint(
            firstCamera, firstRoot.position);
        object firstData = Field(noteType, "data").GetValue(first);
        object correctButton = Field(firstData.GetType(), "kind")
            .GetValue(firstData);
        double firstHitTime = (double)Field(noteType, "hitTime")
            .GetValue(first);
        MethodInfo updateOrder = playerType.GetMethod("UpdateInputOrderLabel",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(updateOrder);
        updateOrder.Invoke(player, new object[] { first, 1, firstHitTime - 0.30d });
        UnityEngine.UI.Text orderTag = Field(noteType, "orderLabel")
            .GetValue(first) as UnityEngine.UI.Text;
        Assert.IsNotNull(orderTag, "Live click notes need an order label.");
        Assert.IsTrue(orderTag.enabled, "The next note should show order 1.");
        Assert.AreEqual("1", orderTag.text);
        Assert.IsFalse(orderTag.raycastTarget,
            "Order labels must never intercept mouse input.");
        updateOrder.Invoke(player, new object[] { first, 4, firstHitTime - 0.30d });
        Assert.IsFalse(orderTag.enabled,
            "Only the next three input steps should be labelled.");
        handleInput.Invoke(player, new object[] {
            correctButton, firstHitTime + 0.070d - savedOffset,
            0f, firstScreen
        });
        Assert.AreEqual(1, (int)playerType.GetProperty("GreatCount")
            .GetValue(player), "70ms late must score GREAT.");
        Assert.AreEqual(0, (int)playerType.GetProperty("PerfectCount")
            .GetValue(player));
        CollectionAssert.AreEqual(new[] { "GREAT" }, actualNoteVerdicts);

        Transform noteLayer = Field(playerType, "noteLayer")
            .GetValue(player) as Transform;
        Assert.IsNotNull(noteLayer);
        UnityEngine.UI.Text[] labels =
            noteLayer.GetComponentsInChildren<UnityEngine.UI.Text>(true);
        Assert.IsTrue(Array.Exists(labels, item =>
            item.name == "Judge_GREAT" && item.text == "GREAT"),
            "The real GREAT verdict must be visibly created.");
        Assert.IsTrue(Array.Exists(labels, item =>
            item.name == "InputTimingError" && item.text == "LATE 70ms"),
            "The rendered judgement must include the signed ms error.");

        createVisual.Invoke(player, new[] { second });
        RectTransform secondRoot = Field(noteType, "root")
            .GetValue(second) as RectTransform;
        Assert.IsNotNull(secondRoot);
        Canvas secondCanvas = secondRoot.GetComponentInParent<Canvas>();
        Camera secondCamera = secondCanvas != null &&
            secondCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? (secondCanvas.worldCamera != null
                    ? secondCanvas.worldCamera : Camera.main)
                : null;
        Vector2 secondScreen = RectTransformUtility.WorldToScreenPoint(
            secondCamera, secondRoot.position);
        object secondData = Field(noteType, "data").GetValue(second);
        object secondKind = Field(secondData.GetType(), "kind")
            .GetValue(secondData);
        object wrongButton = Enum.Parse(secondKind.GetType(),
            secondKind.ToString() == "Left" ? "Right" : "Left");
        double secondHitTime = (double)Field(noteType, "hitTime")
            .GetValue(second);
        handleInput.Invoke(player, new object[] {
            wrongButton, secondHitTime - savedOffset,
            0f, secondScreen
        });
        Assert.AreEqual(1, (int)playerType.GetProperty("MissCount")
            .GetValue(player), "Wrong button must consume the real note as MISS.");
        CollectionAssert.AreEqual(new[] { "GREAT", "MISS" },
            actualNoteVerdicts);

        // Deliberately enable empty-click penalties for this check.
        Field(playerType, "penalizeEmptyClicks").SetValue(player, true);
        handleInput.Invoke(player, new object[] {
            correctButton, Math.Max(firstHitTime, secondHitTime) +
                100d - savedOffset, 0f, firstScreen
        });
        CollectionAssert.AreEqual(new[] { "GREAT", "MISS" },
            actualNoteVerdicts,
            "A blank click must not emit ChartNoteJudged.");
        CollectionAssert.AreEqual(new[] { "GREAT", "MISS", "MISS" },
            hudVerdicts,
            "The normal HUD still receives blank-click feedback.");

        chartEvent.RemoveEventHandler(player, noteListener);
        hudEvent.RemoveEventHandler(player, hudListener);
        Invoke(player, "StopChart");
        yield return null;
#else
        Assert.Ignore("Scene asset load requires Unity Editor PlayMode.");
        yield break;
#endif
    }

    [UnityTest]
    public IEnumerator EveryAuthoredNoteAcceptsItsActualTimedInputInAllSixSongs()
    {
#if UNITY_EDITOR
        Time.timeScale = 1f;
        foreach (string chartName in ChartNames)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("Mouse", LoadSceneMode.Single);
            Assert.IsNotNull(load);
            yield return load;
            var boot = UnityEngine.Object.FindObjectOfType(
                RuntimeType("MouseBootSequenceUI")) as MonoBehaviour;
            Assert.IsNotNull(boot);
            boot.StopAllCoroutines();
            boot.enabled = false;
            Type playerType = RuntimeType("LuluInBeat.MouseMode.ChartSystem.MouseModeChartPlayer");
            var player = UnityEngine.Object.FindObjectOfType(playerType) as MonoBehaviour;
            Assert.IsNotNull(player);
            player.enabled = false;
            var chart = AssetDatabase.LoadMainAssetAtPath(
                "Assets/MouseMode/ChartSystem/Charts/" + chartName + ".asset");
            Assert.IsNotNull(chart);
            Field(playerType, "chartAsset").SetValue(player, chart);
            Invoke(player, "StartChart");
            IList notes = Field(playerType, "notes").GetValue(player) as IList;
            Assert.IsNotNull(notes);
            Assert.Greater(notes.Count, 0);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo create = playerType.GetMethod("CreateVisual", flags);
            MethodInfo input = playerType.GetMethod("HandleRhythmInput", flags);
            MethodInfo moveWheel = playerType.GetMethod("UpdateWheelMovement", flags);
            MethodInfo offset = playerType.GetMethod("EffectiveJudgeOffsetSeconds", flags);
            Assert.IsNotNull(create);
            Assert.IsNotNull(input);
            Assert.IsNotNull(moveWheel);
            Assert.IsNotNull(offset);
            double correction = (double)offset.Invoke(player, null);
            int expected = 0;
            foreach (object note in notes)
            {
                Type type = note.GetType();
                object data = Field(type, "data").GetValue(note);
                object kind = Field(data.GetType(), "kind").GetValue(data);
                double hit = (double)Field(type, "hitTime").GetValue(note);
                double end = (double)Field(type, "endTime").GetValue(note);
                create.Invoke(player, new[] { note });
                var root = Field(type, "root").GetValue(note) as RectTransform;
                Assert.IsNotNull(root, chartName);
                Vector2 Point()
                {
                    var canvas = root.GetComponentInParent<Canvas>();
                    Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                        ? (canvas.worldCamera != null ? canvas.worldCamera : Camera.main) : null;
                    return RectTransformUtility.WorldToScreenPoint(camera, root.position);
                }
                input.Invoke(player, new object[] { kind, hit - correction, 1f, Point() });
                expected++;
                if (kind.ToString() == "Wheel")
                {
                    Assert.IsTrue((bool)Field(type, "started").GetValue(note), chartName);
                    var guides = Field(type, "wheelGuideGroup").GetValue(note) as CanvasGroup;
                    Assert.IsNotNull(guides, chartName + " missing wheel guide");
                    Assert.AreEqual(1f, guides.alpha, .001f);
                    // Feed scroll ticks throughout the actual sustained span.
                    // Start/end are separate judgements; middle ticks are not.
                    for (double at = hit + .20d; at < end - .16d; at += .12d)
                    {
                        moveWheel.Invoke(player, new object[] { note, (float)((at-hit)/(end-hit)) });
                        input.Invoke(player, new object[] { kind, at - correction, 1f, Point() });
                    }
                    moveWheel.Invoke(player, new object[] { note, 1f });
                    input.Invoke(player, new object[] { kind, end - correction, 1f, Point() });
                    expected++;
                }
                Assert.IsTrue((bool)Field(type, "finished").GetValue(note),
                    chartName + " unresolved note at " + hit.ToString("F3"));
                Assert.AreEqual(expected, (int)playerType.GetProperty("PerfectCount").GetValue(player),
                    chartName + " imperfect synthetic input at " + hit.ToString("F3"));
                // Flush destroyed UI objects in bounded batches, instead of
                // accumulating every note's effect in a single rendered frame.
                if (expected % 24 < 2) yield return null;
            }
            Assert.AreEqual(0, (int)playerType.GetProperty("MissCount").GetValue(player));
            Assert.AreEqual(expected, (int)playerType.GetProperty("PerfectCount").GetValue(player));
            Invoke(player, "StopChart");
            yield return null;
        }
#else
        Assert.Ignore("Scene asset load requires Unity Editor PlayMode.");
        yield break;
#endif
    }

}
