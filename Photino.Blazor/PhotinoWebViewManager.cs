// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Photino.NET;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace Photino.Blazor;

/// <summary>
/// Extends the Blazor <see cref="WebViewManager"/> to integrate with the Photino runtime. This manager
/// handles dispatching messages to and from the embedded web view, intercepting resource requests
/// using a custom scheme, and pumping messages from .NET to JavaScript via a channel. Platform
/// differences are abstracted away such that Windows uses the <c>http</c> scheme while other
/// operating systems use a custom <c>app</c> scheme.
/// </summary>
public class PhotinoWebViewManager : WebViewManager
{
    /// <summary>
    /// Gets the URI scheme used to serve the application. Windows does not permit top-level navigation
    /// to a custom scheme using WebView2, so the <c>http</c> scheme is used there. Other platforms
    /// support a custom <c>app</c> scheme which enables request interception.
    /// </summary>
    public static readonly string BlazorAppScheme = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? "http"
        : "app";

    /// <summary>
    /// Gets the base URI used by the application. All relative requests are resolved against this
    /// base URI.
    /// </summary>
    public static readonly string AppBaseUri = $"{BlazorAppScheme}://localhost/";

    private readonly Channel<string> _messageChannel;
    private readonly PhotinoWindow _window;

    /// <summary>
    /// Initializes a new instance of <see cref="PhotinoWebViewManager"/>.
    /// </summary>
    /// <param name="window">The Photino window hosting the web view.</param>
    /// <param name="provider">The dependency injection provider.</param>
    /// <param name="dispatcher">The dispatcher used to marshal calls back to the UI thread.</param>
    /// <param name="fileProvider">The file provider used to resolve static assets.</param>
    /// <param name="jsComponents">The JS component configuration store.</param>
    /// <param name="config">Application configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required argument is null.</exception>
    public PhotinoWebViewManager(
        PhotinoWindow window,
        IServiceProvider provider,
        Dispatcher dispatcher,
        IFileProvider fileProvider,
        JSComponentConfigurationStore jsComponents,
        IOptions<PhotinoBlazorAppConfiguration> config)
        : base(provider, dispatcher, config?.Value?.AppBaseUri ?? throw new ArgumentNullException(nameof(config)), fileProvider, jsComponents, config.Value.HostPage)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));

        // Use a synchronous task scheduler to dispatch messages off the browser UI thread when necessary.
        var synchronousScheduler = new Utils.SynchronousTaskScheduler();

        _window.WebMessageReceived += (sender, message) =>
        {
            // Move processing off the browser UI thread. The origin URL is unknown, so always trust the
            // message as coming from our own application. Future versions could include origin
            // information to make this more robust.
            Task.Factory.StartNew(
                (msg) =>
                {
                    var origin = new Uri(AppBaseUri);
                    MessageReceived(origin, (string)msg!);
                },
                message,
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach,
                synchronousScheduler);
        };

        // Initialize the message channel used for sending messages from .NET to the browser and
        // start a background reader to pump messages into the web view. We use a single reader but
        // allow multiple writers to enqueue messages concurrently.
        _messageChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        _ = Task.Run(MessagePump);
    }

    /// <summary>
    /// Handles resource requests by attempting to serve them from the application's file provider.
    /// If the resource is not found locally, the request is ignored and falls back to network
    /// handling (resulting in a 404 from the browser).
    /// </summary>
    /// <param name="_">Unused sender argument.</param>
    /// <param name="__">Unused scheme argument.</param>
    /// <param name="url">The absolute URL of the requested resource.</param>
    /// <param name="contentType">When this method returns, contains the resolved content type if available.</param>
    /// <returns>A stream containing the resource content, or <c>null</c> if the resource cannot be served locally.</returns>
    public Stream? HandleWebRequest(object? _, string? __, string url, out string? contentType)
    {
        // Determine whether the request is for a page (no file extension) or a static asset. Without
        // explicit knowledge of the request type we guess based on the local path.
        var localPath = new Uri(url).LocalPath;
        var hasFileExtension = localPath.LastIndexOf('.') > localPath.LastIndexOf('/');

        // Remove query string parameters before attempting to resolve the file. For example, request
        // to /_content/Blazorise/button.js?v=1.0.7.0 should resolve to button.js.
        if (url.Contains('?'))
        {
            url = url[..url.IndexOf('?')];
        }
        if (url.StartsWith(AppBaseUri, StringComparison.Ordinal) &&
            TryGetResponseContent(url, !hasFileExtension, out var _, out var _, out var content, out var headers))
        {
            headers.TryGetValue("Content-Type", out contentType);
            return content;
        }

        contentType = default;
        return null;
    }

    /// <inheritdoc />
    protected override ValueTask DisposeAsyncCore()
    {
        // Signal completion of the message channel so that the message pump can exit. Exceptions
        // here can be ignored as they simply indicate the channel has already been closed.
        try
        {
            _messageChannel.Writer.Complete();
        }
        catch
        {
            // ignored
        }
        return base.DisposeAsyncCore();
    }

    /// <inheritdoc />
    protected override void NavigateCore(Uri absoluteUri)
    {
        // Delegate navigation to the underlying Photino window. Photino handles ensuring that the
        // correct page is loaded and displayed.
        _window.Load(absoluteUri);
    }

    /// <inheritdoc />
    protected override void SendMessage(string message)
    {
        // Try to synchronously write to the channel. If the channel cannot accept the message
        // synchronously, enqueue it asynchronously to avoid blocking the calling thread.
        if (!_messageChannel.Writer.TryWrite(message))
        {
            _ = EnqueueMessageAsync(message);
        }
    }

    private async Task EnqueueMessageAsync(string message)
    {
        try
        {
            await _messageChannel.Writer.WriteAsync(message).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            // The channel has been completed; ignore further messages.
        }
    }

    private async Task MessagePump()
    {
        var reader = _messageChannel.Reader;
        try
        {
            while (await reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (reader.TryRead(out var message))
                {
                    await _window.SendWebMessageAsync(message).ConfigureAwait(false);
                }
            }
        }
        catch (ChannelClosedException)
        {
            // The channel was closed. This is expected during shutdown.
        }
    }
}