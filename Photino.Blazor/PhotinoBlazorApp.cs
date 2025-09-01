using Photino.NET;

namespace Photino.Blazor;

public partial class PhotinoBlazorApp(IHost host)
{
    public IHostEnvironment Environment => Services.GetRequiredService<IHostEnvironment>();
    public IHostApplicationLifetime Lifetime => Services.GetRequiredService<IHostApplicationLifetime>();
    public IServiceProvider Services => Host.Services;
    public PhotinoWindow Window => Services.GetRequiredService<PhotinoWindow>();
    public PhotinoWebViewManager WindowManager => Services.GetRequiredService<PhotinoWebViewManager>();

    internal IHost Host { get; } = host;

    public Stream HandleWebRequest(object? sender, string? scheme, string url, out string? contentType)
    {
        var stream = WindowManager.HandleWebRequest(sender, scheme, url, out contentType);
        return stream is null ? throw new InvalidOperationException($"Web request for '{url}' returned no stream.") : stream;
    }

    public void Run()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Window.StartUrl))
            {
                Window.StartUrl = "/";
            }

            WindowManager.Navigate(Window.StartUrl);
            Window.WaitForClose();
        }
        finally
        {
            Host.StopAsync().GetAwaiter().GetResult();

            switch (Host)
            {
                case IAsyncDisposable disposable:
                    disposable.DisposeAsync().GetAwaiter().GetResult();
                    break;

                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }
    }

    internal void Initialize()
    {
        Host.Start();
        Lifetime.ApplicationStopped.Register(Window.Close);

        ConfigureDefaults();
        Window.RegisterCustomSchemeHandler(PhotinoWebViewManager.BlazorAppScheme, HandleWebRequest);
        Window.RegisterWindowClosingHandler(WindowClosingHandler);

        var windowManager = Services.GetRequiredService<PhotinoWebViewManager>();
        var rootComponents = Services.GetRequiredService<PhotinoRootComponentsList>();

        var addRootTasks = rootComponents.Select(component =>
            windowManager.Dispatcher.InvokeAsync(() =>
                windowManager.AddRootComponentAsync(component.ComponentType, component.Selector, component.Parameters)));

        Task.WhenAll(addRootTasks).GetAwaiter().GetResult();
    }

    private void ConfigureDefaults() => Window
        .SetTitle("Photino Blazor App")
        .SetUseOsDefaultSize(false)
        .SetUseOsDefaultLocation(false)
        .SetWidth(1000)
        .SetHeight(900)
        .SetLeft(450)
        .SetTop(100);

    private bool WindowClosingHandler(object sender, EventArgs e)
    {
        Lifetime.StopApplication();
        return false;
    }
}