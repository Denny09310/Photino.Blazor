namespace Photino.Blazor.Utils;

/// <summary>
/// A simple <see cref="TaskScheduler"/> implementation that executes tasks immediately on the
/// current thread. This scheduler enforces a maximum concurrency level of 1 and does not
/// maintain an internal queue of scheduled tasks. It is used within Photino to execute
/// asynchronous work in a deterministic, synchronous manner.
/// </summary>
internal sealed class SynchronousTaskScheduler : TaskScheduler
{
    /// <inheritdoc />
    public override int MaximumConcurrencyLevel => 1;

    /// <inheritdoc />
    protected override IEnumerable<Task> GetScheduledTasks()
    {
        // This scheduler does not queue tasks, so always return an empty collection.
        return Array.Empty<Task>();
    }

    /// <inheritdoc />
    protected override void QueueTask(Task task)
    {
        // Execute the task immediately on the current thread.
        TryExecuteTask(task);
    }

    /// <inheritdoc />
    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
        // Inline execution is always possible because tasks are never queued.
        return TryExecuteTask(task);
    }
}