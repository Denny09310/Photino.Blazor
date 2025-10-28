namespace Photino.Blazor;

/// <summary>
/// Represents configuration options for a Photino Blazor application. These settings are consumed
/// by <see cref="PhotinoWebViewManager"/> and related infrastructure to determine the base URI
/// used for serving resources and the name of the host page to load.
/// </summary>
public class PhotinoBlazorAppConfiguration
{
    /// <summary>
    /// Gets or sets the base URI used by the application. This URI is typically derived from
    /// <see cref="PhotinoWebViewManager.AppBaseUri"/> and defines the root for resolving
    /// relative resource paths.
    /// </summary>
    public Uri AppBaseUri { get; set; } = default!;

    /// <summary>
    /// Gets or sets the name of the host HTML page that bootstraps the Blazor application.
    /// The default value is <c>index.html</c>.
    /// </summary>
    public string HostPage { get; set; } = default!;
}