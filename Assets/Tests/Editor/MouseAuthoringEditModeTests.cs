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
