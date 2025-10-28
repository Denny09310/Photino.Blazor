using Microsoft.AspNetCore.Components.Web;
using Photino.Blazor;
using System;
using Photino.Blazor.Template.Components;

namespace Photino.Blazor.Template;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var builder = PhotinoBlazorAppBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        var app = builder.Build();

        app.Window.SetIconFile("favicon.ico")
                  .SetTitle("Photino.Blazor.Template")
                  .SetNotificationsEnabled(false);

        AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
        {
            app.Window.ShowMessage("Fatal exception", error.ExceptionObject.ToString());
        };

        app.Run();
    }
}