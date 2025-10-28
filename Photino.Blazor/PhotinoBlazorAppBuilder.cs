using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Photino.NET;

namespace Photino.Blazor;

/// <summary>
/// Provides a fluent builder API for configuring and constructing instances of
/// <see cref="PhotinoBlazorApp"/>. The builder encapsulates the creation of
/// a <see cref="HostApplicationBuilder"/>, registration of default services, environment
/// initialization, and root component configuration. Consumers can further customize
/// services, configuration sources, and logging through the exposed properties.
/// </summary>
public sealed class PhotinoBlazorAppBuilder
{
    private readonly HostApplicationBuilder _builder;

    private PhotinoBlazorAppBuilder(string[]? args)
    {
        // Initialize the underlying host builder with the provided command-line arguments
        // and environment variables. The builder is stored so that configuration, services,
        // and logging can be accessed via properties on this class.
        _builder = CreateHostApplicationBuilder(args);

        // Register all of the services required by a default Photino Blazor application.
        RegisterDefaultServices();

        // Initialize RootComponents and Environment after services have been registered.
        RootComponents = CreateRootComponents();
        Environment = CreateEnvironment();
    }

    /// <summary>
    /// Gets a <see cref="ConfigurationManager"/> that can be used to modify configuration sources or
    /// read configuration values during application startup.
    /// </summary>
    public ConfigurationManager Configuration => _builder.Configuration;

    /// <summary>
    /// Gets an <see cref="IWebHostEnvironment"/> representing the hosting environment for the application.
    /// </summary>
    public IWebHostEnvironment Environment { get; }

    /// <summary>
    /// Gets a builder that can be used to configure logging. This builder is scoped to the
    /// application and should be configured before calling <see cref="Build"/>.
    /// </summary>
    public ILoggingBuilder Logging => _builder.Logging;

    /// <summary>
    /// Gets the collection of root components that will be mounted in the main Photino window.
    /// Components can be added to this collection to control what renders when the application starts.
    /// </summary>
    public PhotinoRootComponentsList RootComponents { get; }

    /// <summary>
    /// Gets the service collection used to configure dependency injection for the application.
    /// </summary>
    public IServiceCollection Services => _builder.Services;

    /// <summary>
    /// Creates a new <see cref="PhotinoBlazorAppBuilder"/> instance using default conventions. The
    /// optional <paramref name="args"/> parameter will be passed through to the underlying host builder.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the application's entry point.</param>
    /// <returns>A configured <see cref="PhotinoBlazorAppBuilder"/>.</returns>
    public static PhotinoBlazorAppBuilder CreateDefault(string[]? args = default)
    {
        return new(args);
    }

    /// <summary>
    /// Builds a fully initialized instance of <see cref="PhotinoBlazorApp"/>. After creation, the
    /// app's initialization routine is invoked to start the host and mount all root components.
    /// </summary>
    /// <returns>A ready-to-run <see cref="PhotinoBlazorApp"/>.</returns>
    public PhotinoBlazorApp Build()
    {
        var app = new PhotinoBlazorApp(_builder.Build());
        app.Initialize();
        return app;
    }

    /// <summary>
    /// Creates the underlying <see cref="HostApplicationBuilder"/> and configures it with the provided
    /// command-line arguments and environment variables. Consumers can override this method to further
    /// customize the host builder.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the application's entry point.</param>
    /// <returns>A configured <see cref="HostApplicationBuilder"/>.</returns>
    private static HostApplicationBuilder CreateHostApplicationBuilder(string[]? args)
    {
        var configuration = new ConfigurationManager();
        // Populate the configuration manager with ASP.NET Core-specific environment variables. This mirrors
        // the behavior of WebApplication.CreateBuilder and makes settings like ASPNETCORE_ENVIRONMENT available.
        configuration.AddEnvironmentVariables("ASPNETCORE_");

        return new HostApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            Configuration = configuration
        });
    }

    /// <summary>
    /// Registers the default services required to run a Photino Blazor application. Services are
    /// added as singletons or scoped dependencies as appropriate. If customizing service registrations,
    /// override this method in a derived type.
    /// </summary>
    private void RegisterDefaultServices()
    {
        // Configure Photino-specific options, including the application base URI and host page. These
        // settings are consumed by PhotinoWebViewManager when serving resources.
        Services.AddOptions<PhotinoBlazorAppConfiguration>().Configure(opts =>
        {
            opts.AppBaseUri = new Uri(PhotinoWebViewManager.AppBaseUri);
            opts.HostPage = "index.html";
        });

        // Register an HttpClient that routes requests through the PhotinoHttpHandler so that
        // requests for local resources are intercepted and served by the application instead of
        // performing network requests.
        Services.AddScoped(sp =>
        {
            var handler = sp.GetRequiredService<PhotinoHttpHandler>();
            return new HttpClient(handler) { BaseAddress = new Uri(PhotinoWebViewManager.AppBaseUri) };
        });

        // Register the dispatcher and synchronization context required for Blazor component rendering and
        // Photino's UI thread management.
        Services.AddSingleton<Dispatcher, PhotinoDispatcher>();
        Services.AddSingleton<JSComponentConfigurationStore>();
        Services.AddSingleton<PhotinoBlazorApp>();
        Services.AddSingleton<PhotinoHttpHandler>();
        Services.AddSingleton<PhotinoSynchronizationContext>();
        Services.AddSingleton<PhotinoWebViewManager>();
        Services.AddSingleton(new PhotinoWindow());
        Services.AddBlazorWebView();
    }

    /// <summary>
    /// Initializes the hosting environment for the application. This wraps the underlying
    /// <see cref="IHostEnvironment"/> with a <see cref="PhotinoBlazorAppEnvironment"/> to add web root
    /// support and registers the resulting file provider for dependency injection.
    /// </summary>
    /// <returns>A fully constructed <see cref="PhotinoBlazorAppEnvironment"/>.</returns>
    private PhotinoBlazorAppEnvironment CreateEnvironment()
    {
        var hostEnvironment = new PhotinoBlazorAppEnvironment(_builder.Environment, Configuration);

        Services.AddSingleton<IWebHostEnvironment>(hostEnvironment);
        Services.AddSingleton(hostEnvironment.WebRootFileProvider);

        return hostEnvironment;
    }

    /// <summary>
    /// Creates the list of root components used by the application. These components are registered
    /// as a singleton so that they can be injected and manipulated from other parts of the application.
    /// </summary>
    /// <returns>A new <see cref="PhotinoRootComponentsList"/>.</returns>
    private PhotinoRootComponentsList CreateRootComponents()
    {
        var rootComponents = new PhotinoRootComponentsList();
        Services.AddSingleton(rootComponents);
        return rootComponents;
    }
}