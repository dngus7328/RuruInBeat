using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Player movement is independent of rhythm note sprite positions.
/// The editor and game must emit the entire authored barrage for pop patterns.
/// </summary>
public sealed class MouseBarrageFlowEditModeTests
{
    [TestCase("pop_twinfan")]
    [TestCase("pop_echo")]
    [TestCase("pop_pendulum")]
    [TestCase("pop_ribbon")]
    [TestCase("pop_gate")]
    [TestCase("pop_pinwheel")]
    [TestCase("pop_aim")]
    [TestCase("pop_pulse")]
    [TestCase("pop_seedflower")]
    public void NotesDoNotSilentlyDeleteProjectiles(string pattern)
    {
        Type library = Type.GetType(
            "MouseModeBulletPatternLibrary, Assembly-CSharp");
        Type chartType = Type.GetType(
            "LuluInBeat.MouseMode.ChartSystem.MouseModeChartData, Assembly-CSharp");
        Type noteType = Type.GetType(
            "LuluInBeat.MouseMode.ChartSystem.MouseModeChartNote, Assembly-CSharp");
        Assert.IsNotNull(library);
        Assert.IsNotNull(chartType);
        Assert.IsNotNull(noteType);

        MethodInfo generate = library.GetMethod("Generate",
            BindingFlags.Public | BindingFlags.Static);
        MethodInfo generateForChart = library.GetMethod("GenerateForChart",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(generate);
        Assert.IsNotNull(generateForChart);

        object chart = Activator.CreateInstance(chartType);
        IList notes = chartType.GetField("notes").GetValue(chart) as IList;
        Assert.IsNotNull(notes);
        // Previously, these visually placed notes were mistakenly interpreted
        // as compulsory movement paths, deleting many of the projectiles.
        for (int i = 0; i < 12; i++)
        {
            object note = Activator.CreateInstance(noteType);
            noteType.GetField("kind").SetValue(note, Enum.ToObject(
                noteType.GetField("kind").FieldType, i % 2));
            noteType.GetField("beat").SetValue(note, 16f + i * .5f);
            noteType.GetField("position").SetValue(note,
                new Vector2(.2f + (i % 4) * .2f, .2f + (i % 3) * .3f));
            notes.Add(note);
        }

        ICollection authored = generate.Invoke(null, new object[] { pattern })
            as ICollection;
        ICollection runtime = generateForChart.Invoke(null,
            new object[] { pattern, 16f, chart }) as ICollection;

        Assert.IsNotNull(authored);
        Assert.IsNotNull(runtime);
        Assert.Greater(authored.Count, 0, pattern);
        Assert.AreEqual(authored.Count, runtime.Count,
            "Moving the visual note sprites must never delete a projectile.");
    }

    [Test]
    public void Enemy01SourceSheetLoadsAsTwelveAnimatedFrames()
    {
        Texture2D sheet = Resources.Load<Texture2D>("MouseEnemies/Enemy01");
        Assert.IsNotNull(sheet, "The real enemy art must be loadable at runtime.");
        Assert.AreEqual(0, sheet.width % 4, "Sprite sheet needs four columns.");
        Assert.AreEqual(0, sheet.height % 3, "Sprite sheet needs three rows.");
        Assert.Greater(sheet.width, 120);
        Assert.Greater(sheet.height, 90);

        // A sheet with a blank or badly arranged animation cell can load
        // without an error, yet make Enemy01 vanish on individual frames.
        Color32[] pixels = sheet.GetPixels32();
        int cellWidth = sheet.width / 4;
        int cellHeight = sheet.height / 3;
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 4; col++)
            {
                int visiblePixels = 0;
                for (int y = row * cellHeight; y < (row + 1) * cellHeight; y += 2)
                {
                    for (int x = col * cellWidth; x < (col + 1) * cellWidth; x += 2)
                    {
                        if (pixels[y * sheet.width + x].a > 32)
                            visiblePixels++;
                    }
                }
                int samples = (cellWidth / 2) * (cellHeight / 2);
                Assert.Greater(visiblePixels, samples / 200,
                    "Enemy01 animated frame " + (row * 4 + col) +
                    " is nearly transparent or missing.");
            }
        }
    }
}
