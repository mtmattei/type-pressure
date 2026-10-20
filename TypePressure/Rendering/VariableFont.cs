using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using SkiaSharp;

namespace TypePressure.Rendering;

/// <summary>Evaluates original OpenType gvar tuples and draws quadratic glyph outlines.</summary>
public sealed class VariableFont
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    public FontData Data { get; }
    public VariableFont(string name)
    {
        using var source = OpenAsset(name + ".json.gz");
        using var gzip = new GZipStream(source, CompressionMode.Decompress);
        Data = JsonSerializer.Deserialize<FontData>(gzip, Json)!;
    }
    public static Stream OpenAsset(string suffix)
    {
        var assembly = typeof(VariableFont).Assembly;
        return assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n => n.EndsWith(suffix, StringComparison.Ordinal)))!;
    }
    public string GlyphName(int codepoint) => Data.Cmap.GetValueOrDefault(codepoint.ToString()) ?? ".notdef";
    public Dictionary<string, float> Normalize(float weight, float width, float slant)
    {
        var result = new Dictionary<string, float>();
        foreach (var a in Data.Axes)
        {
            var value = Math.Clamp(a.Tag switch { "wght" => weight, "wdth" => width, "slnt" => slant, _ => a.Default }, a.Min, a.Max);
            var normalized = value == a.Default ? 0 : value < a.Default ? (value-a.Default)/(a.Default-a.Min) : (value-a.Default)/(a.Max-a.Default);
            for (var i = 1; i < a.Map.Length; i++)
            {
                if (normalized > a.Map[i][0]) continue;
                var previous = a.Map[i-1]; var next = a.Map[i];
                normalized = previous[1] + (normalized-previous[0])/(next[0]-previous[0])*(next[1]-previous[1]); break;
            }
            result[a.Tag] = normalized;
        }
        return result;
    }
    public SKPoint[] Evaluate(string glyphName, Dictionary<string,float> location)
    {
        var glyph = Data.Glyphs[glyphName];
        var points = glyph.Points.Select(p => new SKPoint(p[0],p[1])).ToArray();
        foreach (var variation in glyph.Variations)
        {
            float scalar = 1;
            foreach (var (tag, support) in variation.Support)
            {
                var v=location.GetValueOrDefault(tag);var low=support[0];var peak=support[1];var high=support[2];
                if (peak == 0 || v == peak) continue;
                if (v <= low || v >= high) { scalar=0;break; }
                scalar *= v < peak ? (v-low)/(peak-low) : (high-v)/(high-peak);
            }
            if (Math.Abs(scalar)<.000001f) continue;
            for (var i=0;i<points.Length;i++) { points[i].X += variation.Delta[i][0]*scalar; points[i].Y += variation.Delta[i][1]*scalar; }
        }
        return points;
    }
    public GlyphOutline CreateOutline(int codepoint,float weight,float width,float slant)
    {
        var location=Normalize(weight,width,slant);var name=GlyphName(codepoint);
        var points=Evaluate(name,location);var path=BuildPath(name,location,points,0);
        // Phantom points encode the left side origin and advance width.
        path.Transform(SKMatrix.CreateTranslation(-points[^4].X,0));
        return new GlyphOutline(path,points[^3].X-points[^4].X);
    }
    private SKPath BuildPath(string name,Dictionary<string,float> location,SKPoint[] points,int depth)
    {
        var path = new SKPath { FillType=SKPathFillType.Winding };var glyph=Data.Glyphs[name];
        if(depth>12)return path;
        if(glyph.Components.Length>0)
        {
            for(var i=0;i<glyph.Components.Length;i++)
            {
                var c=glyph.Components[i];using var component=BuildPath(c.Glyph,location,Evaluate(c.Glyph,location),depth+1);
                var matrix=new SKMatrix(c.Matrix[0],c.Matrix[2],points[i].X,c.Matrix[1],c.Matrix[3],points[i].Y,0,0,1);
                component.Transform(matrix);path.AddPath(component);
            }
            return path;
        }
        var first=0;
        foreach(var last in glyph.Ends)
        {
            if(last<first)continue;
            var start=points[first];var end=points[last];
            var firstOn=glyph.Flags[first]!=0;var lastOn=glyph.Flags[last]!=0;
            var origin=firstOn?start:lastOn?end:Mid(start,end);
            path.MoveTo(origin);
            var i=firstOn?first+1:first;var limit=lastOn&&!firstOn?last-1:last;
            SKPoint? control=null;
            for(;i<=limit;i++)
            {
                var p=points[i];
                if(glyph.Flags[i]!=0){if(control.HasValue)path.QuadTo(control.Value,p);else path.LineTo(p);control=null;}
                else {if(control.HasValue)path.QuadTo(control.Value,Mid(control.Value,p));control=p;}
            }
            if(control.HasValue)path.QuadTo(control.Value,origin);
            path.Close();first=last+1;
        }
        return path;
    }
    private static SKPoint Mid(SKPoint a,SKPoint b)=>new((a.X+b.X)/2,(a.Y+b.Y)/2);
    public sealed record GlyphOutline(SKPath Path,float Advance):IDisposable { public void Dispose()=>Path.Dispose(); }
    public sealed class FontData { public string Name{get;set;}="";public float Units{get;set;}public Axis[] Axes{get;set;}=[];public Dictionary<string,string> Cmap{get;set;}=[];public Dictionary<string,Glyph> Glyphs{get;set;}=[]; }
    public sealed class Axis { public string Tag{get;set;}="";public float Min{get;set;}public float Default{get;set;}public float Max{get;set;}public float[][] Map{get;set;}=[]; }
    public sealed class Glyph { public float[][] Points{get;set;}=[];public int[] Ends{get;set;}=[];public int[] Flags{get;set;}=[];public Component[] Components{get;set;}=[];public Variation[] Variations{get;set;}=[]; }
    public sealed class Component { public string Glyph{get;set;}="";public float[] Matrix{get;set;}=[]; }
    public sealed class Variation { public Dictionary<string,float[]> Support{get;set;}=[];public float[][] Delta{get;set;}=[]; }
}
