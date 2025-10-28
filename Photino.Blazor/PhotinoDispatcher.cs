// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Components;

namespace Photino.Blazor;

/// <summary>
/// A custom <see cref="Dispatcher"/> implementation that dispatches work onto the Photino UI thread.
/// The dispatcher delegates actual work scheduling to a <see cref="PhotinoSynchronizationContext"/>,
/// ensuring that work items are executed on the correct thread and that unhandled exceptions are
/// surfaced appropriately. This class also overrides <see cref="Dispatcher.CheckAccess"/> to
/// determine if the calling thread matches the Photino UI thread.
/// </summary>
internal class PhotinoDispatcher : Dispatcher
{
    private readonly PhotinoSynchronizationContext _synchronizationContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotinoDispatcher"/> class.
    /// </summary>
    /// <param name="context">The synchronization context used to marshal work onto the Photino UI thread.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="context"/> is <c>null</c>.</exception>
    public PhotinoDispatcher(PhotinoSynchronizationContext context)
    {
        _synchronizationContext = context ?? throw new ArgumentNullException(nameof(context));
        _synchronizationContext.UnhandledException += (sender, e) => OnUnhandledException(e);
    }

    /// <inheritdoc />
    public override bool CheckAccess() => SynchronizationContext.Current == _synchronizationContext;

    /// <inheritdoc />
    public override Task InvokeAsync(Action workItem)
    {
        if (workItem == null) throw new ArgumentNullException(nameof(workItem));
        return CheckAccess()
            ? ExecuteSynchronously(workItem)
            : _synchronizationContext.InvokeAsync(workItem);
    }

    /// <inheritdoc />
    public override Task InvokeAsync(Func<Task> workItem)
    {
        if (workItem == null) throw new ArgumentNullException(nameof(workItem));
        return CheckAccess()
            ? workItem()
            : _synchronizationContext.InvokeAsync(workItem);
    }

    /// <inheritdoc />
    public override Task<TResult> InvokeAsync<TResult>(Func<TResult> workItem)
    {
        if (workItem == null) throw new ArgumentNullException(nameof(workItem));
        return CheckAccess()
            ? Task.FromResult(workItem())
            : _synchronizationContext.InvokeAsync(workItem);
    }

    /// <inheritdoc />
    public override Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> workItem)
    {
        if (workItem == null) throw new ArgumentNullException(nameof(workItem));
        return CheckAccess()
            ? workItem()
            : _synchronizationContext.InvokeAsync(workItem);
    }

    private static Task ExecuteSynchronously(Action workItem)
    {
        workItem();
        return Task.CompletedTask;
    }
}