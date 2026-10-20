using SkiaSharp;
using TypePressure.Rendering;

if(args.Length==0)throw new ArgumentException("Provide an output directory, optionally followed by --preview.");
FontRegression.Run();
var output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);
var preview=args.Contains("--preview");
const int width=1080,height=1920,fps=30;
var simulation=new PressureSimulation{Demo=true,Text="PRESSURE"};
var renderer=new DemoRenderer(new VariableFont("RobotoFlex"),new VariableFont("Anybody"));
using var surface=SKSurface.Create(new SKImageInfo(width,height,SKColorType.Bgra8888,SKAlphaType.Opaque));
var frames=(int)(PressureSimulation.DemoDuration*fps);
for(var frame=0;frame<frames;frame++)
{
    if(frame>0)simulation.Step(1d/fps);
    if(preview&&frame%(8*fps)!=4*fps)continue;
    if(preview)renderer.Reset();
    renderer.Draw(surface.Canvas,width,height,simulation);
    using var snapshot=surface.Snapshot();
    using var png=snapshot.Encode(SKEncodedImageFormat.Png,100);
    using(var stream=File.Create(Path.Combine(output,$"frame-{frame:D5}.png")))png.SaveTo(stream);
    if(frame%120==0)Console.WriteLine($"Rendered {frame}/{frames} ({frame/fps}s), scene {simulation.DemoScene+1}/7");
}
Console.WriteLine($"Complete: {(preview?7:frames)} frames at {width}x{height}, {fps} fps, {PressureSimulation.DemoDuration}s.");
