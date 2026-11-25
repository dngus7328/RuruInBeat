using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

public sealed class MouseAuthoringEditModeTests
{
    private static Type Runtime(string name)
    {
        var t=Type.GetType(name+", Assembly-CSharp");Assert.IsNotNull(t,name);return t;
    }
    [Test]
    public void PreviewHitsoundsUseRealTempoMapAndWheelStartMiddleEnd()
    {
        const string ns="LuluInBeat.MouseMode.ChartSystem.";
        var dataType=Runtime(ns+"MouseModeChartData");
        var noteType=Runtime(ns+"MouseModeChartNote");
        var tempoType=Runtime(ns+"MouseModeTempoChange");
        var data=Activator.CreateInstance(dataType);
        var notes=(IList)dataType.GetField("notes").GetValue(data);
        var tempos=(IList)dataType.GetField("tempoChanges").GetValue(data);
        var tap=Activator.CreateInstance(noteType);
        noteType.GetField("beat").SetValue(tap,2f);
        notes.Add(tap);
        var wheel=Activator.CreateInstance(noteType);
        noteType.GetField("kind").SetValue(wheel,Enum.ToObject(Runtime(ns+"MouseModeNoteKind"),2));
        noteType.GetField("beat").SetValue(wheel,4f);
        noteType.GetField("endBeat").SetValue(wheel,6f);
        notes.Add(wheel);
        var tempo=Activator.CreateInstance(tempoType);
        tempoType.GetField("beat").SetValue(tempo,5f);
        tempoType.GetField("bpm").SetValue(tempo,60f);
        tempos.Add(tempo);
        var mixer=Runtime(ns+"EditorTools.RuruChartPreviewMix");
        var method=mixer.GetMethod("BuildCues",BindingFlags.NonPublic|BindingFlags.Static);
        Assert.IsNotNull(method);
        var cues=(IList)method.Invoke(null,new[]{data});
        double[] expected={1,2,2.25,2.5,3,3.5};
        int[] kinds={0,1,2,2,2,3};
        Assert.AreEqual(expected.Length,cues.Count);
        for(int i=0;i<cues.Count;i++)
        {
            var type=cues[i].GetType();
            Assert.AreEqual(expected[i],(double)type.GetField("seconds").GetValue(cues[i]),.00001);
            Assert.AreEqual(kinds[i],(int)type.GetField("kind").GetValue(cues[i]));
        }
    }

    [Test]
    public void InspectorVolleyCreatesTheExpectedPelletSchedule()
    {
        var type=Runtime("MouseCustomBulletPattern");
        var asset=ScriptableObject.CreateInstance(type);
        try
        {
            var volleys=(IList)type.GetField("volleys").GetValue(asset);
            var v=volleys[0];var vt=v.GetType();
            vt.GetField("repeat").SetValue(v,3);vt.GetField("bullets").SetValue(v,5);
            vt.GetField("intervalBeats").SetValue(v,1.5f);
            vt.GetField("spread").SetValue(v,40f);
            vt.GetField("rotationPerRepeat").SetValue(v,10f);
            var result=(IList)type.GetMethod("Build").Invoke(asset,null);
            Assert.AreEqual(15,result.Count);
            var pt=result[0].GetType();
            Assert.AreEqual(-110f,(float)pt.GetField("angle").GetValue(result[0]),.001f);
            Assert.AreEqual(-70f,(float)pt.GetField("angle").GetValue(result[4]),.001f);
            Assert.AreEqual(3f,(float)pt.GetField("atBeat").GetValue(result[10]),.001f);
            Assert.AreEqual(-90f,(float)pt.GetField("angle").GetValue(result[10]),.001f);
        }
        finally{UnityEngine.Object.DestroyImmediate(asset);}
    }

    [Test]
    public void EditedUILayoutDoesNotResetUneditedLiveProperties()
    {
        var type=Runtime("RuruUILayoutElement");
        var template=new GameObject("Template",typeof(RectTransform));
        var live=new GameObject("Live",typeof(RectTransform));
        try
        {
            var t=(RectTransform)template.transform;var l=(RectTransform)live.transform;
            t.anchoredPosition=new Vector2(10,20);t.sizeDelta=new Vector2(100,50);
            var marker=template.AddComponent(type);
            type.GetMethod("CaptureBaseline").Invoke(marker,new object[]{"test"});
            t.anchoredPosition=new Vector2(80,40);
            l.sizeDelta=new Vector2(300,70);
            type.GetMethod("ApplyTo").Invoke(marker,new object[]{l,true});
            Assert.AreEqual(new Vector2(80,40),l.anchoredPosition);
            Assert.AreEqual(new Vector2(300,70),l.sizeDelta);
        }
        finally{UnityEngine.Object.DestroyImmediate(template);UnityEngine.Object.DestroyImmediate(live);}
    }
}
