using Uno.UI.Hosting;

namespace TypePressure;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--self-test")) { Rendering.FontRegression.Run(); return; }
        App.PortraitDemo = args.Contains("--portrait-demo");
        App.InitializeLogging();

        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseX11()
            .UseLinuxFrameBuffer()
            .UseMacOS()
            .UseWin32()
            .Build();

        host.Run();
    }
}
