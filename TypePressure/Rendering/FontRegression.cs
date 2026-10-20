using System.Text.Json;

namespace TypePressure.Rendering;

public static class FontRegression
{
    public static void Run()
    {
        var count=0;float worst=0;
        foreach(var name in new[]{"RobotoFlex","Anybody"})
        {
            var font=new VariableFont(name);using var stream=VariableFont.OpenAsset(name+".checks.json");using var json=JsonDocument.Parse(stream);
            foreach(var sample in json.RootElement.EnumerateArray())
            {
                var location=sample.GetProperty("location");
                var normalized=font.Normalize(location.GetProperty("wght").GetSingle(),location.GetProperty("wdth").GetSingle(),location.TryGetProperty("slnt",out var slant)?slant.GetSingle():0);
                var glyph=font.GlyphName(char.ConvertToUtf32(sample.GetProperty("character").GetString()!,0));
                var actual=font.Evaluate(glyph,normalized);var expected=sample.GetProperty("points");
                if(actual.Length!=expected.GetArrayLength())throw new Exception("Point count mismatch: "+glyph);
                for(var i=0;i<actual.Length;i++)
                {
                    var delta=Math.Max(Math.Abs(actual[i].X-expected[i][0].GetSingle()),Math.Abs(actual[i].Y-expected[i][1].GetSingle()));worst=Math.Max(worst,delta);
                    // FontTools rounds instance coordinates and metrics to font units.
                    if(delta>2)throw new Exception($"{name}/{glyph} point {i}: {delta} units from independent FontTools instance.");
                }
                using var outline=font.CreateOutline(char.ConvertToUtf32(sample.GetProperty("character").GetString()!,0),location.GetProperty("wght").GetSingle(),location.GetProperty("wdth").GetSingle(),location.TryGetProperty("slnt",out slant)?slant.GetSingle():0);
                if(outline.Advance<=0)throw new Exception("Invalid glyph advance.");count++;
            }
        }
        var simulation=new PressureSimulation{Paused=true};simulation.Step(.05);if(simulation.Time!=0)throw new Exception("Freeze advanced time.");
        simulation.Paused=false;simulation.Demo=true;
        var scenes=new HashSet<int>();
        for(var i=0;i<2300;i++){simulation.Step(.05);scenes.Add(simulation.DemoScene);var a=simulation.Axes(i%8,i%9,8,.5f,.5f,1.78f);if(a.Weight<100||a.Weight>1000||a.Width<25||a.Width>151||a.Slant< -10||a.Slant>0)throw new Exception("Out-of-range demo axis.");}
        if(scenes.Count!=7)throw new Exception("Demo omitted a scene.");
        Console.WriteLine($"PASS: {count} independent font instances; max difference {worst:F3} font units. Freeze and two complete demo loops passed.");
    }
}
