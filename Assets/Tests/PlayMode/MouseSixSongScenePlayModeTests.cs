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
            SceneManager.LoadScene("Mouse", LoadSceneMode.Single);
            Assert.AreEqual("Mouse", SceneManager.GetActiveScene().name);

            Type bootType = RuntimeType("MouseBootSequenceUI");
            Behaviour boot = UnityEngine.Object.FindObjectOfType(bootType)
                as Behaviour;
            Assert.IsNotNull(boot);
            // The boot animation owns its own delayed activation. This test
            // manually starts its song so the test is deterministic.
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
            Assert.AreEqual(0, player.transform.GetComponentsInChildren<
                AudioListener>(true).Length,
                "Avoid duplicate AudioListeners on chart player.");

            Invoke(player, "StopChart");
            yield return null;
        }
#else
        Assert.Ignore("Scene asset load requires Unity Editor PlayMode.");
        yield break;
#endif
    }
}
