using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;

public sealed class RuruStoryAuthoringEditModeTests
{
    [Test]
    public void StoryImageStateInheritsReplacesAndClearsWithoutBakedUI()
    {
        Type storyType = Type.GetType("RuruStorySequence, Assembly-CSharp", true);
        Type lineType = storyType.GetNestedType("Line");
        ScriptableObject story = ScriptableObject.CreateInstance(storyType);
        Texture2D texture = new Texture2D(16,16);
        Sprite background = Sprite.Create(texture,new Rect(0,0,16,16),Vector2.one*.5f);
        Sprite firstPortrait = Sprite.Create(texture,new Rect(0,0,8,8),Vector2.one*.5f);
        Sprite secondPortrait = Sprite.Create(texture,new Rect(8,8,8,8),Vector2.one*.5f);
        try
        {
            IList lines = (IList)storyType.GetField("lines").GetValue(story);
            object a = Activator.CreateInstance(lineType);
            lineType.GetField("background").SetValue(a,background);
            lineType.GetField("leftPortrait").SetValue(a,firstPortrait);
            lines.Add(a);
            object b = Activator.CreateInstance(lineType);
            lineType.GetField("leftPortrait").SetValue(b,secondPortrait);
            lines.Add(b);
            object c = Activator.CreateInstance(lineType);
            lineType.GetField("clearLeft").SetValue(c,true);
            lines.Add(c);
            object frame = storyType.GetMethod("Resolve").Invoke(story,new object[]{1});
            Assert.AreSame(background,frame.GetType().GetField("background").GetValue(frame));
            Assert.AreSame(secondPortrait,frame.GetType().GetField("left").GetValue(frame));
            frame = storyType.GetMethod("Resolve").Invoke(story,new object[]{2});
            Assert.AreSame(background,frame.GetType().GetField("background").GetValue(frame));
            Assert.IsNull(frame.GetType().GetField("left").GetValue(frame));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(story);
            UnityEngine.Object.DestroyImmediate(background);
            UnityEngine.Object.DestroyImmediate(firstPortrait);
            UnityEngine.Object.DestroyImmediate(secondPortrait);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
