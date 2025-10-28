using Microsoft.Extensions.FileProviders;

namespace Photino.Blazor;

/// <summary>
/// Provides a concrete implementation of <see cref="IWebHostEnvironment"/> tailored for the Photino
/// Blazor hosting model. It augments the base host environment with a web root and file provider
/// derived from configuration, mirroring the behavior of ASP.NET Core's WebHostEnvironment. If a
/// web root is not explicitly configured, a default of "wwwroot" beneath the content root is used.
/// </summary>
internal sealed class PhotinoBlazorAppEnvironment : IWebHostEnvironment
{
    private readonly IHostEnvironment _hostEnvironment;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotinoBlazorAppEnvironment"/> class.
    /// </summary>
    /// <param name="hostEnvironment">The underlying host environment.</param>
    /// <param name="configuration">Application configuration used to resolve the web root.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="hostEnvironment"/> or
    /// <paramref name="configuration"/> is <c>null</c>.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown if the configured web root does not exist.</exception>
    public PhotinoBlazorAppEnvironment(IHostEnvironment hostEnvironment, IConfiguration configuration)
    {
        _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
        ArgumentNullException.ThrowIfNull(configuration);

        // Attempt to read the configured web root path. If none is present use a default "wwwroot"
        // relative to the content root. Throwing here rather than letting a later file access fail
        // ensures misconfiguration is surfaced early during startup.
        var configuredWebRoot = configuration.GetValue<string?>(WebHostDefaults.WebRootKey);
        var rootPath = string.IsNullOrEmpty(configuredWebRoot)
            ? Path.Combine(ContentRootPath, "wwwroot")
            : configuredWebRoot;

        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException(rootPath);
        }

        WebRootPath = rootPath;
        WebRootFileProvider = new PhysicalFileProvider(WebRootPath);
    }

    /// <inheritdoc />
    public string ApplicationName
    {
        get => _hostEnvironment.ApplicationName;
        set => _hostEnvironment.ApplicationName = value;
    }

    /// <inheritdoc />
    public IFileProvider ContentRootFileProvider
    {
        get => _hostEnvironment.ContentRootFileProvider;
        set => _hostEnvironment.ContentRootFileProvider = value;
    }

    /// <inheritdoc />
    public string ContentRootPath
    {
        get => _hostEnvironment.ContentRootPath;
        set => _hostEnvironment.ContentRootPath = value;
    }

    /// <inheritdoc />
    public string EnvironmentName
    {
        get => _hostEnvironment.EnvironmentName;
        set => _hostEnvironment.EnvironmentName = value;
    }

    /// <summary>
    /// Gets or sets the file provider representing the application's web root. The provider is
    /// initialized to point to <see cref="WebRootPath"/> but can be overridden if necessary.
    /// </summary>
    public IFileProvider WebRootFileProvider { get; set; }

    /// <summary>
    /// Gets or sets the absolute path to the web root directory. This path is resolved during
    /// construction and validated to ensure it exists.
    /// </summary>
    public string WebRootPath { get; set; }
}