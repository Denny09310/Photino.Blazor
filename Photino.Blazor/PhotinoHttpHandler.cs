using System.Net;
using System.Net.Http.Headers;

namespace Photino.Blazor;

/// <summary>
/// An <see cref="HttpMessageHandler"/> that intercepts HTTP requests made by the application and routes
/// them through the <see cref="PhotinoBlazorApp"/> for local resource resolution. If a local resource
/// can be served by the application, a 200 OK response is returned with the appropriate content type.
/// Otherwise, the request is delegated to the inner handler for normal processing.
/// </summary>
public class PhotinoHttpHandler : DelegatingHandler
{
    private readonly PhotinoBlazorApp _app;

    /// <summary>
    /// Initializes a new instance of <see cref="PhotinoHttpHandler"/> for use within DI. The handler
    /// will automatically create an <see cref="HttpClientHandler"/> if one is not provided via
    /// <see cref="InnerHandler"/>.
    /// </summary>
    /// <param name="app">The current Photino Blazor application.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> is <c>null</c>.</exception>
    public PhotinoHttpHandler(PhotinoBlazorApp app) : this(app, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="PhotinoHttpHandler"/>. If an inner handler is provided,
    /// it will be used as the final handler in the chain when a resource cannot be served locally.
    /// </summary>
    /// <param name="app">The current Photino Blazor application.</param>
    /// <param name="innerHandler">The next handler in the pipeline, or <c>null</c> to create a new <see cref="HttpClientHandler"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> is <c>null</c>.</exception>
    public PhotinoHttpHandler(PhotinoBlazorApp app, HttpMessageHandler? innerHandler)
    {
        _app = app ?? throw new ArgumentNullException(nameof(app));
        // The last (inner) handler in the pipeline should be a concrete implementation like
        // HttpClientHandler to perform real HTTP requests if local resolution fails.
        InnerHandler = innerHandler ?? new HttpClientHandler();
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Always check if the requested resource can be served from within the application itself.
        // This avoids unnecessary network calls for resources such as the host page or static assets.
        var content = _app.HandleWebRequest(null, null, request.RequestUri?.AbsoluteUri ?? string.Empty, out var contentType);
        if (content != null)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(content)
            };
            // Only set a content type header if one was provided by the HandleWebRequest call.
            if (!string.IsNullOrEmpty(contentType))
            {
                response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            }
            return response;
        }

        // Delegate to the underlying handler chain when we cannot resolve the resource locally. The base
        // implementation ensures proper disposal of intermediate handlers.
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}