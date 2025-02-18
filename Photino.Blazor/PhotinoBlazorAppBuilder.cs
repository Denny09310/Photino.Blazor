using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Photino.NET;

namespace Photino.Blazor;

public sealed class PhotinoBlazorAppBuilder
{
    private readonly HostApplicationBuilder _builder;

    private PhotinoBlazorAppBuilder(string[]? args)
    {
        _builder = InitializeHostApplicationBuilder(args);

        InitializeDefaultServices();

        RootComponents = InitializeRootComponents();
        Environment = InitializeEnvironment();
    }

    /// <summary>
    /// Gets an <see cref="ConfigurationManager"/> that can be used to customize the application's
    /// configuration sources and read configuration attributes.
    /// </summary>
    public ConfigurationManager Configuration => _builder.Configuration;

    /// <summary>
    /// Gets information about the app's host environment.
    /// </summary>
    public IWebHostEnvironment Environment { get; }

    /// <summary>
    /// Gets the logging builder for configuring logging services.
    /// </summary>
    public ILoggingBuilder Logging => _builder.Logging;

    /// <summary>
    /// Gets the collection of root component mappings configured for the application.
    /// </summary>
    public PhotinoRootComponentsList RootComponents { get; }

    /// <summary>
    /// Gets the service collection.
    /// </summary>
    public IServiceCollection Services => _builder.Services;

    /// <summary>
    /// Creates an instance of <see cref="BlazorDesktopHostBuilder"/> using the most common
    /// conventions and settings.
    /// </summary>
    /// <param name="args">The arguments passed to the application's main method.</param>
    /// <returns>A <see cref="BlazorDesktopHostBuilder"/>.</returns>
    public static PhotinoBlazorAppBuilder CreateDefault(string[]? args = default)
    {
        return new(args);
    }

    /// <summary>
    /// Builds a <see cref="BlazorDesktopHost"/> instance based on the configuration of this builder.
    /// </summary>
    /// <returns>A <see cref="BlazorDesktopHost"/> object.</returns>
    public PhotinoBlazorApp Build()
    {
        return new(_builder.Build());
    }

    private static HostApplicationBuilder InitializeHostApplicationBuilder(string[]? args)
    {
        var configuration = new ConfigurationManager();

        configuration.AddEnvironmentVariables("ASPNETCORE_");

        return new(new HostApplicationBuilderSettings
        {
            Args = args,
            Configuration = configuration
        });
    }

    private void InitializeDefaultServices()
    {
        Services.AddOptions<PhotinoBlazorAppConfiguration>().Configure(opts =>
        {
            opts.AppBaseUri = new Uri(PhotinoWebViewManager.AppBaseUri);
            opts.HostPage = "index.html";
        });

        Services.AddScoped(sp =>
        {
            var handler = sp.GetRequiredService<PhotinoHttpHandler>();
            return new HttpClient(handler) { BaseAddress = new Uri(PhotinoWebViewManager.AppBaseUri) };
        });

        Services.AddSingleton<Dispatcher, PhotinoDispatcher>();
        Services.AddSingleton<JSComponentConfigurationStore>();
        Services.AddSingleton<PhotinoBlazorApp>();
        Services.AddSingleton<PhotinoHttpHandler>();
        Services.AddSingleton<PhotinoSynchronizationContext>();
        Services.AddSingleton<PhotinoWebViewManager>();
        Services.AddSingleton(new PhotinoWindow());
        Services.AddBlazorWebView();
    }

    private PhotinoBlazorAppEnvironment InitializeEnvironment()
    {
        var hostEnvironment = new PhotinoBlazorAppEnvironment(_builder.Environment, Configuration);

        Services.AddSingleton<IWebHostEnvironment>(hostEnvironment);
        Services.AddSingleton(hostEnvironment.WebRootFileProvider);

        return hostEnvironment;
    }

    private PhotinoRootComponentsList InitializeRootComponents()
    {
        var rootComponents = new PhotinoRootComponentsList();

        Services.AddSingleton(rootComponents);

        return rootComponents;
    }
}