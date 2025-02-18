using Photino.Blazor.NativeAOT.Components;
using System;

namespace Photino.Blazor.NativeAOT;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var builder = PhotinoBlazorAppBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("app");
        builder.RootComponents.Add<App>("head::after");

        var app = builder.Build();

        app.Window.SetIconFile("favicon.ico")
                  .SetTitle("Photino Blazor Sample");

        AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
        {
            app.Window.ShowMessage("Fatal exception", error.ExceptionObject.ToString());
        };

        app.Run();
    }
}