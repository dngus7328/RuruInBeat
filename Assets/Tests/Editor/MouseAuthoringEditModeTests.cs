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
    public void CopyExistingSongTimingPreservesAuthoredNotesAndAttacks()
    {
        const string ns="LuluInBeat.MouseMode.ChartSystem.";
        var dataType=Runtime(ns+"MouseModeChartData");
        var source=Activator.CreateInstance(dataType);var target=Activator.CreateInstance(dataType);
        dataType.GetField("bpm").SetValue(source,132.11574f);
        dataType.GetField("beatZeroSeconds").SetValue(source,.0617943f);
        var marker=Activator.CreateInstance(Runtime(ns+"MouseModeTempoChange"));
        marker.GetType().GetField("beat").SetValue(marker,8f);
        marker.GetType().GetField("bpm").SetValue(marker,132.24284f);
        ((IList)dataType.GetField("tempoChanges").GetValue(source)).Add(marker);
        var notes=(IList)dataType.GetField("notes").GetValue(target);
        var attacks=(IList)dataType.GetField("attacks").GetValue(target);
        var note=Activator.CreateInstance(Runtime(ns+"MouseModeChartNote"));
        var attack=Activator.CreateInstance(Runtime(ns+"MouseModeChartAttack"));
        notes.Add(note);attacks.Add(attack);
        var editor=Runtime(ns+"EditorTools.RuruSimpleChartEditor");
        editor.GetMethod("CopyTiming",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new[]{target,source});
        Assert.AreEqual(132.11574f,(float)dataType.GetField("bpm").GetValue(target),.00001f);
        Assert.AreEqual(.0617943f,(float)dataType.GetField("beatZeroSeconds").GetValue(target),.000001f);
        var copied=(IList)dataType.GetField("tempoChanges").GetValue(target);
        Assert.AreEqual(1,copied.Count);Assert.AreNotSame(marker,copied[0]);
        Assert.AreSame(notes,dataType.GetField("notes").GetValue(target));
        Assert.AreSame(attacks,dataType.GetField("attacks").GetValue(target));
        Assert.AreSame(note,notes[0]);Assert.AreSame(attack,attacks[0]);
    }

    [Test]
    public void PreviewAudioIsImportedAsPlayablePcmAsset()
    {
        var mixer=Runtime("LuluInBeat.MouseMode.ChartSystem.EditorTools.RuruChartPreviewMix");
        var import=mixer.GetMethod("ImportPreview",BindingFlags.NonPublic|BindingFlags.Static);
        var release=mixer.GetMethod("Release",BindingFlags.NonPublic|BindingFlags.Static);
        var pcm=new float[4410];
        for(int i=0;i<pcm.Length;i++)pcm[i]=i<2205?0f:.25f;
        AudioClip clip=null;
        try
        {
            clip=(AudioClip)import.Invoke(null,new object[]{pcm,1,44100});
            Assert.IsNotNull(clip);
            Assert.IsTrue(EditorUtility.IsPersistent(clip),"AudioUtil must receive imported asset");
            Assert.AreEqual(44100,clip.frequency);
            Assert.AreEqual(4410,clip.samples);
            var actual=new float[4410];
            Assert.IsTrue(clip.GetData(actual,0));
            Assert.AreEqual(0f,actual[100],.0001f);
            Assert.AreEqual(.25f,actual[3000],.0001f);
        }
        finally{if(clip!=null)release.Invoke(null,new object[]{clip});}
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
