using System.Text;
using SkiaSharp;

namespace TypePressure.Rendering;

/// <summary>The same text-only exhibition renderer is used by the app and video export.</summary>
public sealed class DemoRenderer
{
    private readonly VariableFont _roboto, _anybody;
    private readonly Dictionary<int,FontAxes> _smooth=[];
    private string _layout="";
    private float _reference;
    public DemoRenderer(VariableFont roboto,VariableFont anybody){_roboto=roboto;_anybody=anybody;}
    public void Reset(){_smooth.Clear();_layout="";}

    public void Draw(SKCanvas canvas,float width,float height,PressureSimulation simulation,bool advance=true)
    {
        canvas.Clear(new SKColor(242,240,233));
        var portrait=height>width*1.15f;
        var storm=simulation.DemoScene==6;
        var rows=storm?(portrait?9:4):(portrait?5:1);
        var text=string.IsNullOrWhiteSpace(simulation.Text)?" ":simulation.Text;
        if(storm&&!portrait&&text.Length<12)text+=" "+text;
        var characters=text.EnumerateRunes().Select(r=>r.Value).ToArray();
        var font=simulation.Anybody?_anybody:_roboto;
        var layout=$"{text}|{simulation.Anybody}|{rows}";
        if(layout!=_layout)
        {
            _layout=layout;_reference=0;_smooth.Clear();
            foreach(var c in characters){using var outline=font.CreateOutline(c,750,140,0);_reference+=outline.Advance-font.Data.Units*.027f;}
        }
        var availableWidth=width*.9f;
        var size=Math.Min(availableWidth/Math.Max(_reference,1)*font.Data.Units,height*.84f/(rows*.95f));
        var rowStep=size*.94f;
        using var paint=new SKPaint{Color=new SKColor(32,34,31),IsAntialias=true};
        for(var row=0;row<rows;row++)
        {
            var cy=height/2+(row-(rows-1)/2f)*rowStep;
            var outlines=new List<VariableFont.GlyphOutline>();float advanceWidth=0;
            for(var i=0;i<characters.Length;i++)
            {
                var axes=simulation.Axes(i,row,characters.Length,(i+.5f)/characters.Length,cy/height,height/width);
                var key=row*64+i;
                if(_smooth.TryGetValue(key,out var previous))
                    axes=advance?new(previous.Weight+(axes.Weight-previous.Weight)*.24f,previous.Width+(axes.Width-previous.Width)*.24f,previous.Slant+(axes.Slant-previous.Slant)*.24f):previous;
                if(advance)_smooth[key]=axes;
                var outline=font.CreateOutline(characters[i],axes.Weight,axes.Width,axes.Slant);
                outlines.Add(outline);advanceWidth+=outline.Advance-font.Data.Units*.027f;
            }
            var rowSize=Math.Min(size,availableWidth/Math.Max(advanceWidth,1)*font.Data.Units);
            var scale=rowSize/font.Data.Units;
            var x=(width-advanceWidth*scale)/2;
            var baseline=cy+rowSize*.35f;
            foreach(var outline in outlines)
            {
                canvas.Save();canvas.Translate(x,baseline);canvas.Scale(scale,-scale);canvas.DrawPath(outline.Path,paint);canvas.Restore();
                x+=(outline.Advance-font.Data.Units*.027f)*scale;outline.Dispose();
            }
        }
    }
}
