using System.Diagnostics;
using System.Text;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using Windows.System;

namespace TypePressure.Rendering;

public sealed class PressureCanvas : SKCanvasElement
{
    public PressureSimulation Simulation {get;}=new();
    public event Action? Updated;
    private readonly VariableFont _roboto=new("RobotoFlex"),_anybody=new("Anybody");
    private readonly DemoRenderer _demoRenderer;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _timer;
    private readonly Stopwatch _clock=Stopwatch.StartNew();
    private readonly Dictionary<int,FontAxes> _smooth=[];
    private double _lastTime;
    private int _ticks;
    private ForceField? _drag;
    private int _wall;
    private float _wallStart,_initialSqueeze;
    private readonly SKTypeface _ui=SKTypeface.FromFamilyName("Arial");
    private readonly SKTypeface _mono=SKTypeface.FromFamilyName("Consolas");
    private static readonly SKColor Paper=new(242,240,233),Ink=new(32,34,31),Muted=new(120,122,114),Line=new(213,213,203),Accent=new(232,77,39);
    public PressureCanvas()
    {
        _demoRenderer=new DemoRenderer(_roboto,_anybody);
        _timer=DispatcherQueue.CreateTimer();_timer.Interval=TimeSpan.FromMilliseconds(33);
        _timer.Tick+=(_,_)=>{var now=_clock.Elapsed.TotalSeconds;var dt=now-_lastTime;_lastTime=now;if(!Simulation.Paused){Simulation.Step(dt);Invalidate();}if(_ticks++%5==0)Updated?.Invoke();};
        Loaded+=(_,_)=>{_lastTime=_clock.Elapsed.TotalSeconds;_timer.Start();};
        Unloaded+=(_,_)=>_timer.Stop();
        PointerPressed+=OnPressed;PointerMoved+=OnMoved;PointerReleased+=OnReleased;PointerCanceled+=OnReleased;PointerCaptureLost+=(_,_)=>{Simulation.PointerDown=false;_drag=null;_wall=0;};
        PointerExited+=(_,_)=>{Simulation.PointerInside=false;};
        SizeChanged+=(_,_)=>Invalidate();
    }
    public void Refresh(){_smooth.Clear();Invalidate();Updated?.Invoke();}
    public void BeginDemo(bool enabled){Simulation.Demo=enabled;Simulation.PointerDown=false;Simulation.Pressure=0;Simulation.RestartClock();_smooth.Clear();_demoRenderer.Reset();Invalidate();}
    protected override void RenderOverride(SKCanvas canvas,Size area)=>DrawScene(canvas,(float)area.Width,(float)area.Height,false);
    public void DrawScene(SKCanvas canvas,float width,float height,bool exporting)
    {
        if(width<10||height<10)return;
        if(Simulation.Demo){_demoRenderer.Draw(canvas,width,height,Simulation,!exporting&&!Simulation.Paused);return;}
        canvas.Clear(Paper);using var paint=new SKPaint{IsAntialias=true,Color=Ink};
        var demo=Simulation.Demo;var top=demo?height*.08f:140;var bottom=demo?height*.92f:height-130;var left=demo?width*.05f:36;
        var availableHeight=Math.Max(40,bottom-top);var availableWidth=width-2*left;
        if(!demo)
        {
            paint.Color=Line.WithAlpha(90);for(float x=36;x<width-30;x+=22)for(float y=16;y<height;y+=22)canvas.DrawCircle(x,y,.6f,paint);
            paint.Color=Line;paint.StrokeWidth=1;canvas.DrawLine(36,114,width-36,114,paint);
            for(var i=0;i<=40;i++){var x=36+(width-72)*i/40;canvas.DrawLine(x,114,x,118,paint);if(i%10==0)Text(canvas,(i*2.5).ToString("0"),x-(i==40?17:0),132,8,Muted,true);}
            var inset=36+Simulation.Squeeze*width*.19f;
            paint.Color=Line;canvas.DrawLine(inset,151,inset,height-142,paint);canvas.DrawLine(width-inset,151,width-inset,height-142,paint);
            paint.Color=Muted;paint.StrokeWidth=2;foreach(var x in new[]{inset,width-inset}){canvas.DrawLine(x-2,height*.49f-16,x-2,height*.49f+16,paint);canvas.DrawLine(x+2,height*.49f-16,x+2,height*.49f+16,paint);}
        }
        var font=Simulation.Anybody?_anybody:_roboto;
        var text=string.IsNullOrWhiteSpace(Simulation.Text)?" ":Simulation.Text;
        var rows=!demo&&Simulation.Mode==Experiment.Storm?4:1;
        if(rows>1&&text.Length<12)text+=" "+text;
        var characters=text.EnumerateRunes().Select(r=>r.Value).ToArray();
        float reference=0;
        foreach(var c in characters){using var outline=font.CreateOutline(c,750,demo?140:115,0);reference+=outline.Advance-font.Data.Units*.027f;}
        var fontSize=Math.Min(availableWidth/Math.Max(reference,1)*font.Data.Units,availableHeight/(rows==1?.78f:rows*.95f));
        for(var row=0;row<rows;row++)
        {
            var outlines=new List<VariableFont.GlyphOutline>();float advance=0;
            var cy=top+availableHeight*(row+.5f)/rows;
            for(var i=0;i<characters.Length;i++)
            {
                var x=(left+availableWidth*(i+.5f)/characters.Length)/width;
                var axes=Simulation.Axes(i,row,characters.Length,x,cy/height,height/width);
                var key=row*64+i;
                if(!exporting&&!Simulation.Paused&&_smooth.TryGetValue(key,out var previous))axes=new(previous.Weight+(axes.Weight-previous.Weight)*.24f,previous.Width+(axes.Width-previous.Width)*.24f,previous.Slant+(axes.Slant-previous.Slant)*.24f);
                if(!exporting)_smooth[key]=axes;
                else if(_smooth.TryGetValue(key,out previous))axes=previous;
                var outline=font.CreateOutline(characters[i],axes.Weight,axes.Width,axes.Slant);outlines.Add(outline);advance+=outline.Advance-font.Data.Units*.027f;
            }
            var size=Math.Min(fontSize,availableWidth/Math.Max(advance,1)*font.Data.Units);
            var scale=size/font.Data.Units;var xPos=(width-advance*scale)/2;var baseline=cy+size*.35f;
            paint.Color=Ink;
            foreach(var outline in outlines){canvas.Save();canvas.Translate(xPos,baseline);canvas.Scale(scale,-scale);canvas.DrawPath(outline.Path,paint);canvas.Restore();xPos+=(outline.Advance-font.Data.Units*.027f)*scale;outline.Dispose();}
        }
        if(demo)return;
        paint.Style=SKPaintStyle.Stroke;paint.StrokeWidth=1;
        foreach(var wave in Simulation.Waves){var age=(float)(Simulation.Time-wave.Born);paint.Color=Accent.WithAlpha((byte)Math.Max(0,65-age*18));canvas.DrawCircle(wave.X*width,wave.Y*height,age*width*.45f,paint);}
        foreach(var f in Simulation.Fields)
        {
            var x=f.X*width;var y=f.Y*height;paint.Color=Accent.WithAlpha(65);canvas.DrawCircle(x,y,34,paint);
            paint.Color=Accent;canvas.DrawCircle(x,y,22,paint);paint.Style=SKPaintStyle.Fill;paint.Color=Simulation.Selected==f.Id?Accent:Paper.WithAlpha(200);canvas.DrawCircle(x,y,21,paint);
            Text(canvas,f.Kind switch{ForceKind.Inflate=>"+",ForceKind.Compress=>"↔",ForceKind.Lean=>"↗",_=>"~"},x-8,y+8,25,Simulation.Selected==f.Id?Paper:Accent);
            Text(canvas,$"{f.Id:00} / {f.Kind.ToString().ToUpperInvariant()}",x-36,y+48,9,Accent,true);paint.Style=SKPaintStyle.Stroke;
        }
        if(Simulation.PointerDown){paint.Color=Accent;canvas.DrawCircle(Simulation.PointerX*width,Simulation.PointerY*height,54,paint);canvas.DrawLine(Simulation.PointerX*width-6,Simulation.PointerY*height,Simulation.PointerX*width+6,Simulation.PointerY*height,paint);canvas.DrawLine(Simulation.PointerX*width,Simulation.PointerY*height-6,Simulation.PointerX*width,Simulation.PointerY*height+6,paint);}
        paint.Style=SKPaintStyle.Fill;
        Text(canvas,$"EXPERIMENT {(int)Simulation.Mode+1:00} / 04",36,height-83,9,Muted,true);
        var caption=Simulation.Mode switch{Experiment.Breathing=>("A little room to breathe.","Hold anywhere. See what gives."),Experiment.Tug=>("Two forces. One word.","Drag the markers to take a side."),Experiment.Aftershock=>("Every touch leaves an impression.","Click to send a wave through the type."),_=>("A hundred little changes.","One beautifully unstable system.")};
        Text(canvas,caption.Item1,36,height-59,12,Ink);Text(canvas,caption.Item2,36,height-40,12,Ink);
        if(width>1000){paint.Style=SKPaintStyle.Stroke;paint.Color=Accent;canvas.DrawCircle(width/2-85,height-58,18,paint);paint.Style=SKPaintStyle.Fill;Text(canvas,"+",width/2-91,height-52,20,Accent);Text(canvas,"Type is a material.",width/2-55,height-64,11,Muted);Text(canvas,"Put it under pressure.",width/2-55,height-47,11,Ink);}
        if(width>=600)
        {
        Text(canvas,"CHAMBER PRESSURE",width-205,height-83,9,Muted,true);
        var pressure=1+Simulation.Pressure*.7+Simulation.Squeeze*.6;
        Text(canvas,pressure.ToString("F2"),width-205,height-40,28,Ink,true);Text(canvas,"atm",width-129,height-42,10,Muted,true);
        for(var i=0;i<12;i++){paint.Color=i<2+(pressure-1)*8?Accent:Line;canvas.DrawRect(width-102+i*5,height-65,3,25,paint);}
        }
        if(Simulation.Paused)Text(canvas,"TIME, HELD STILL.",width/2-65,height-108,10,Muted,true);
    }
    private void Text(SKCanvas canvas,string text,float x,float y,float size,SKColor color,bool mono=false)
    {using var font=new SKFont(mono?_mono:_ui,size);using var paint=new SKPaint{Color=color,IsAntialias=true};canvas.DrawText(text,x,y,SKTextAlign.Left,font,paint);}
    private void OnPressed(object sender,PointerRoutedEventArgs e)
    {
        if(Simulation.Demo)return;var p=e.GetCurrentPoint(this);var x=(float)p.Position.X;var y=(float)p.Position.Y;
        if(e.Pointer.PointerDeviceType==Microsoft.UI.Input.PointerDeviceType.Mouse&&!p.Properties.IsLeftButtonPressed)return;
        CapturePointer(e.Pointer);e.Handled=true;
        _drag=Simulation.Fields.LastOrDefault(f=>Math.Pow(f.X*ActualWidth-x,2)+Math.Pow(f.Y*ActualHeight-y,2)<34*34);
        if(_drag is not null){Simulation.Selected=_drag.Id;Invalidate();return;}
        var inset=36+Simulation.Squeeze*(float)ActualWidth*.19f;
        if(Math.Abs(y-ActualHeight*.49)<44&&(Math.Abs(x-inset)<18||Math.Abs(x-(ActualWidth-inset))<18)){_wall=x<ActualWidth/2?1:-1;_wallStart=x;_initialSqueeze=Simulation.Squeeze;return;}
        Simulation.Selected=null;Simulation.PointerX=x/(float)ActualWidth;Simulation.PointerY=y/(float)ActualHeight;
        Simulation.PointerInside=true;Simulation.PointerDown=!Simulation.Paused;
        if(Simulation.Mode==Experiment.Aftershock&&!Simulation.Paused)Simulation.Wave(Simulation.PointerX,Simulation.PointerY,1.4f);
    }
    private void OnMoved(object sender,PointerRoutedEventArgs e)
    {
        if(Simulation.Demo||ActualWidth==0||ActualHeight==0)return;var point=e.GetCurrentPoint(this).Position;var x=(float)(point.X/ActualWidth);var y=(float)(point.Y/ActualHeight);
        if(_drag is not null){_drag.X=Math.Clamp(x,.08f,.92f);_drag.Y=Math.Clamp(y,.28f,.76f);Invalidate();return;}
        if(_wall!=0){Simulation.Squeeze=Math.Clamp(_initialSqueeze+((float)point.X-_wallStart)*_wall/((float)ActualWidth*.19f),0,1);Invalidate();return;}
        Simulation.PointerVelocity=Math.Clamp((x-Simulation.PointerX)*45,-3,3);Simulation.PointerX=x;Simulation.PointerY=y;Simulation.PointerInside=true;
    }
    private void OnReleased(object sender,PointerRoutedEventArgs e)
    {
        if(Simulation.PointerDown)Simulation.Wave(Simulation.PointerX,Simulation.PointerY,Math.Max(.5f,Simulation.Pressure));
        Simulation.PointerDown=false;_drag=null;_wall=0;ReleasePointerCapture(e.Pointer);Updated?.Invoke();
    }
    public bool HandleKey(VirtualKey key)
    {
        if(key==VirtualKey.Delete||key==VirtualKey.Back){Simulation.RemoveSelected();Refresh();return true;}
        if(key==VirtualKey.F&&Simulation.Fields.Count>0){var index=Simulation.Fields.FindIndex(f=>f.Id==Simulation.Selected);Simulation.Selected=Simulation.Fields[(index+1)%Simulation.Fields.Count].Id;Invalidate();return true;}
        var field=Simulation.Fields.FirstOrDefault(f=>f.Id==Simulation.Selected);
        if(field is not null){switch(key){case VirtualKey.Left:field.X-=.015f;break;case VirtualKey.Right:field.X+=.015f;break;case VirtualKey.Up:field.Y-=.025f;break;case VirtualKey.Down:field.Y+=.025f;break;default:return false;}field.X=Math.Clamp(field.X,.08f,.92f);field.Y=Math.Clamp(field.Y,.28f,.76f);Invalidate();return true;}
        return false;
    }
    public byte[] Export(bool svg)
    {
        var width=(float)ActualWidth;var height=(float)ActualHeight;
        using var stream=new MemoryStream();
        if(svg){using var writer=new SKManagedWStream(stream);using(var canvas=SKSvgCanvas.Create(new SKRect(0,0,width,height),writer)){DrawScene(canvas,width,height,true);canvas.Flush();}}
        else{using var surface=SKSurface.Create(new SKImageInfo((int)width*2,(int)height*2));surface.Canvas.Scale(2);DrawScene(surface.Canvas,width,height,true);using var image=surface.Snapshot();using var data=image.Encode(SKEncodedImageFormat.Png,100);data.SaveTo(stream);}
        return stream.ToArray();
    }
}
