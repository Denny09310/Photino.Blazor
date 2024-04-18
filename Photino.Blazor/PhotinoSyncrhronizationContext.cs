﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using PhotinoNET;
using System.Reflection;

namespace Photino.Blazor;

// Most UI platforms have a built-in SyncContext/Dispatcher, e.g., Windows Forms and WPF, which WebView
// can normally use directly. However, Photino currently doesn't.
//
// This is a duplicate of Microsoft.AspNetCore.Components.Rendering.RendererSynchronizationContextDispatcher,
// except that it also uses Photino's "Invoke" to ensure we're running on the correct thread to be able to
// interact with the unmanaged resources (the window and WebView).
//
// It might be that a simpler variant of this would work, for example purely using Photino's "Invoke" and
// relying on that for single-threadedness. Maybe also in the future Photino could consider having its own
// built-in SyncContext/Dispatcher like other UI platforms.

internal class PhotinoSynchronizationContext : SynchronizationContext
{
    private static readonly Action<Task, object?> BackgroundWorkThunk = (task, state) =>
    {
        if (state is not WorkItem item) return;
        item.SynchronizationContext?.ExecuteBackground(item);
    };

    private static readonly ContextCallback ExecutionContextThunk = (state) =>
    {
        if (state is not WorkItem item) return;
        item.SynchronizationContext?.ExecuteSynchronously(default!, item.Callback, item.State);
    };

    private readonly MethodInfo _invokeMethodInfo;
    private readonly State _state;
    private readonly PhotinoWindow _window;

#pragma warning disable IDE0052
    private readonly int _uiThreadId;
#pragma warning restore IDE0052

    public PhotinoSynchronizationContext(PhotinoWindow window) : this(window, new State())
    {
    }

    private PhotinoSynchronizationContext(PhotinoWindow window, State state)
    {
        _state = state;

        _window = window ?? throw new ArgumentNullException(nameof(window));

        _uiThreadId = (int)typeof(PhotinoWindow).GetField("_managedThreadId", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(_window)!;

        _invokeMethodInfo = typeof(PhotinoWindow).GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance)!;
    }

    public event UnhandledExceptionEventHandler? UnhandledException;

    // shallow copy
    public override SynchronizationContext CreateCopy()
    {
        return new PhotinoSynchronizationContext(_window, _state);
    }

    public Task InvokeAsync(Action action)
    {
        var completion = new PhotinoSynchronizationTaskCompletionSource<Action, object?>(action);
        ExecuteSynchronouslyIfPossible((state) =>
        {
            if (state is not PhotinoSynchronizationTaskCompletionSource<Action, object?> completion)
                throw new ArgumentException("state is not of type PhotinoSynchronizationTaskCompletionSource<Action, object?>", nameof(state));

            try
            {
                completion.Callback();
                completion.SetResult(null);
            }
            catch (OperationCanceledException)
            {
                completion.SetCanceled();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }, completion);

        return completion.Task;
    }

    public Task InvokeAsync(Func<Task> asyncAction)
    {
        var completion = new PhotinoSynchronizationTaskCompletionSource<Func<Task>, object?>(asyncAction);
        ExecuteSynchronouslyIfPossible(async (state) =>
        {
            if (state is not PhotinoSynchronizationTaskCompletionSource<Func<Task>, object?> completion)
                throw new ArgumentException("state is not of type PhotinoSynchronizationTaskCompletionSource<Func<Task>, object>", nameof(state));

            try
            {
                await completion.Callback();
                completion.SetResult(null);
            }
            catch (OperationCanceledException)
            {
                completion.SetCanceled();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }, completion);

        return completion.Task;
    }

    public Task<TResult> InvokeAsync<TResult>(Func<TResult> function)
    {
        var completion = new PhotinoSynchronizationTaskCompletionSource<Func<TResult>, TResult>(function);
        ExecuteSynchronouslyIfPossible((state) =>
        {
            if (state is not PhotinoSynchronizationTaskCompletionSource<Func<Task>, object?> completion)
                throw new ArgumentException("state is not of type PhotinoSynchronizationTaskCompletionSource<Func<Task>, object>", nameof(state));

            try
            {
                var result = completion.Callback();
                completion.SetResult(result);
            }
            catch (OperationCanceledException)
            {
                completion.SetCanceled();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }, completion);

        return completion.Task;
    }

    public Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> asyncFunction)
    {
        var completion = new PhotinoSynchronizationTaskCompletionSource<Func<Task<TResult>>, TResult>(asyncFunction);
        ExecuteSynchronouslyIfPossible(async (state) =>
        {
            if (state is not PhotinoSynchronizationTaskCompletionSource<Func<Task<TResult>>, TResult> completion)
                throw new ArgumentException("state is not of type PhotinoSynchronizationTaskCompletionSource<Func<Task<TResult>>, TResult>", nameof(state));

            try
            {
                var result = await completion.Callback();
                completion.SetResult(result);
            }
            catch (OperationCanceledException)
            {
                completion.SetCanceled();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }, completion);

        return completion.Task;
    }

    // asynchronously runs the callback
    //
    // NOTE: this must always run async. It's not legal here to execute the work item synchronously.
    public override void Post(SendOrPostCallback d, object? state)
    {
        lock (_state.Lock)
        {
            _state.Task = Enqueue(_state.Task, d, state, forceAsync: true);
        }
    }

    // synchronously runs the callback
    public override void Send(SendOrPostCallback d, object? state)
    {
        Task antecedent;
        var completion = new TaskCompletionSource<object?>();

        lock (_state.Lock)
        {
            antecedent = _state.Task;
            _state.Task = completion.Task;
        }

        // We have to block. That's the contract of Send - we don't expect this to be used
        // in many scenarios in Components.
        //
        // Using Wait here is ok because the antecedent task will never throw.
        antecedent.Wait();

        ExecuteSynchronously(completion, d, state);
    }

    private void DispatchException(Exception ex)
    {
        UnhandledException?.Invoke(this, new UnhandledExceptionEventArgs(ex, isTerminating: false));
    }

    private Task Enqueue(Task antecedent, SendOrPostCallback d, object? state, bool forceAsync = false)
    {
        // If we get here is means that a callback is being explicitly queued. Let's instead add it to the queue and yield.
        //
        // We use our own queue here to maintain the execution order of the callbacks scheduled here. Also
        // we need a queue rather than just scheduling an item in the thread pool - those items would immediately
        // block and hurt scalability.
        //
        // We need to capture the execution context so we can restore it later. This code is similar to
        // the call path of ThreadPool.QueueUserWorkItem and System.Threading.QueueUserWorkItemCallback.
        ExecutionContext? executionContext = null;
        if (!ExecutionContext.IsFlowSuppressed())
        {
            executionContext = ExecutionContext.Capture();
        }

        var flags = forceAsync ? TaskContinuationOptions.RunContinuationsAsynchronously : TaskContinuationOptions.None;
        return antecedent.ContinueWith(BackgroundWorkThunk, new WorkItem()
        {
            SynchronizationContext = this,
            ExecutionContext = executionContext!,
            Callback = d,
            State = state,
        }, CancellationToken.None, flags, TaskScheduler.Current);
    }

    private void ExecuteBackground(WorkItem item)
    {
        if (item.ExecutionContext == null)
        {
            try
            {
                ExecuteSynchronously(null, item.Callback, item.State);
            }
            catch (Exception ex)
            {
                DispatchException(ex);
            }

            return;
        }

        // Perf - using a static thunk here to avoid a delegate allocation.
        try
        {
            ExecutionContext.Run(item.ExecutionContext, ExecutionContextThunk, item);
        }
        catch (Exception ex)
        {
            DispatchException(ex);
        }
    }

    private void ExecuteSynchronously(TaskCompletionSource<object?>? completion, SendOrPostCallback? d, object? state)
    {
        // Anything run on the sync context should actually be dispatched as far as Photino
        // is concerned, so that it's safe to interact with the native window/WebView.
        _invokeMethodInfo.Invoke(_window, new Action[] { () =>
        {
            var original = Current;
            try
            {
                _state.IsBusy = true;
                SetSynchronizationContext(this);
                d?.Invoke(state);
            }
            finally
            {
                _state.IsBusy = false;
                SetSynchronizationContext(original);

                completion?.SetResult(null);
            }
        }});
    }

    // Similar to Post, but it can runs the work item synchronously if the context is not busy.
    //
    // This is the main code path used by components, we want to be able to run async work but only dispatch
    // if necessary.
    private void ExecuteSynchronouslyIfPossible(SendOrPostCallback d, object state)
    {
        TaskCompletionSource<object?> completion;
        lock (_state.Lock)
        {
            if (!_state.Task.IsCompleted)
            {
                _state.Task = Enqueue(_state.Task, d, state);
                return;
            }

            // We can execute this synchronously because nothing is currently running
            // or queued.
            completion = new();
            _state.Task = completion.Task;
        }

        ExecuteSynchronously(completion, d, state);
    }

    private class PhotinoSynchronizationTaskCompletionSource<TCallback, TResult>(TCallback callback) : TaskCompletionSource<TResult>
    {
        public TCallback Callback { get; } = callback;
    }

    private class State
    {
        public bool IsBusy; // Just for debugging
        public object Lock = new();
        public Task Task = Task.CompletedTask;

        public override string ToString()
        {
            return $"{{ Busy: {IsBusy}, Pending Task: {Task} }}";
        }
    }

    private class WorkItem
    {
        public SendOrPostCallback? Callback;
        public ExecutionContext? ExecutionContext;
        public object? State;
        public PhotinoSynchronizationContext? SynchronizationContext;
    }
}