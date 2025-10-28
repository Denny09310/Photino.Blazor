using Photino.NET;

namespace Photino.Blazor;

/// <summary>
/// Represents the core application class for a Photino Blazor app. This class is responsible for
/// bootstrapping the host, initializing the window manager and root components, handling web requests,
/// and managing the application lifetime. A partial declaration is used so that consumers can extend
/// functionality without modifying the generated code.
/// </summary>
public partial class PhotinoBlazorApp
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PhotinoBlazorApp"/> class.
    /// </summary>
    /// <param name="host">The host used to drive the application's services.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="host"/> is <c>null</c>.</exception>
    public PhotinoBlazorApp(IHost host)
    {
        Host = host ?? throw new ArgumentNullException(nameof(host));
    }

    /// <summary>
    /// Gets the current host environment. This is a convenience shortcut that resolves the
    /// <see cref="IHostEnvironment"/> from <see cref="Services"/>.
    /// </summary>
    public IHostEnvironment Environment => Services.GetRequiredService<IHostEnvironment>();

    /// <summary>
    /// Gets the application lifetime. This resolves the <see cref="IHostApplicationLifetime"/> from
    /// <see cref="Services"/>.
    /// </summary>
    public IHostApplicationLifetime Lifetime => Services.GetRequiredService<IHostApplicationLifetime>();

    /// <summary>
    /// Gets the service provider associated with this application. The service provider is backed by
    /// the <see cref="Host"/> and is used for resolving dependencies.
    /// </summary>
    public IServiceProvider Services => Host.Services;

    /// <summary>
    /// Gets the Photino window associated with this application. The window instance is resolved
    /// via the service container.
    /// </summary>
    public PhotinoWindow Window => Services.GetRequiredService<PhotinoWindow>();

    /// <summary>
    /// Gets the web view manager responsible for navigating and handling communication between
    /// the .NET code and the embedded web view.
    /// </summary>
    public PhotinoWebViewManager WindowManager => Services.GetRequiredService<PhotinoWebViewManager>();

    /// <summary>
    /// Gets the underlying host instance. This property is internal as consumers typically should
    /// not interact with the host directly.
    /// </summary>
    internal IHost Host { get; }

    /// <summary>
    /// Handles a web request triggered by the Photino runtime. If a stream cannot be produced
    /// the method throws instead of returning <c>null</c> so that callers always get a valid
    /// <see cref="Stream"/> instance when this method returns.
    /// </summary>
    /// <param name="sender">The sender of the request.</param>
    /// <param name="scheme">The scheme of the request (e.g., http, app).</param>
    /// <param name="url">The absolute url of the resource being requested.</param>
    /// <param name="contentType">The detected content type, if any.</param>
    /// <returns>A <see cref="Stream"/> containing the response content.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no stream can be returned for the given URL.</exception>
    public Stream HandleWebRequest(object? sender, string? scheme, string url, out string? contentType)
    {
        var stream = WindowManager.HandleWebRequest(sender, scheme, url, out contentType);
        return stream is null
            ? throw new InvalidOperationException($"Web request for '{url}' returned no stream.")
            : stream;
    }

    /// <summary>
    /// Runs the application synchronously, blocking the calling thread until the window closes. Once the window
    /// is closed, this method will gracefully stop and dispose the underlying host. This method should only
    /// be called once during application startup.
    /// </summary>
    public void Run()
    {
        try
        {
            // Ensure the window has a start URL. Without this the application will not navigate anywhere.
            if (string.IsNullOrWhiteSpace(Window.StartUrl))
            {
                Window.StartUrl = "/";
            }

            // Begin navigation and block until the window is closed.
            WindowManager.Navigate(Window.StartUrl);
            Window.WaitForClose();
        }
        finally
        {
            // Shut down the host and dispose resources. Always wait on StopAsync() to ensure all hosted
            // services have a chance to shut down gracefully before disposing.
            Host.StopAsync().GetAwaiter().GetResult();

            if (Host is IDisposable disposable1)
            {
                disposable1.Dispose();
            }
            
            if (Host is IAsyncDisposable disposable2)
            {
                disposable2.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
    }

    /// <summary>
    /// Performs asynchronous initialization of the application. This method is intended to be called
    /// immediately after constructing a new <see cref="PhotinoBlazorApp"/> instance. It starts the
    /// host, configures default window properties, registers the custom scheme and closing handlers,
    /// and mounts all configured root components.
    /// </summary>
    internal void Initialize()
    {
        // Start the host; this registers the server environment and makes services available.
        Host.Start();
        Lifetime.ApplicationStopping.Register(Window.Close);

        ConfigureDefaultWindowProperties();
        Window.RegisterCustomSchemeHandler(PhotinoWebViewManager.BlazorAppScheme, HandleWebRequest);
        Window.RegisterWindowClosingHandler(WindowClosingHandler);

        var windowManager = Services.GetRequiredService<PhotinoWebViewManager>();
        var rootComponents = Services.GetRequiredService<PhotinoRootComponentsList>();

        // Mount all root components to the web view in a deterministic order. The use of
        // Dispatcher.InvokeAsync ensures that each addition happens on the correct thread.
        var addRootTasks = rootComponents.Select(component =>
            windowManager.Dispatcher.InvokeAsync(() =>
                windowManager.AddRootComponentAsync(component.ComponentType, component.Selector, component.Parameters)));

        Task.WhenAll(addRootTasks).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Configures a sensible default appearance and size for the Photino window. Developers can
    /// extend the partial class declaration to override these defaults.
    /// </summary>
    private void ConfigureDefaultWindowProperties() => Window
        .SetTitle("Photino Blazor App")
        .SetNotificationsEnabled(false);

    private bool WindowClosingHandler(object sender, EventArgs e)
    {
        // When the window is closing, signal the application lifetime to stop. Returning false
        // indicates that the Photino framework should continue closing the window as normal.
        Lifetime.StopApplication();
        return false;
    }
}