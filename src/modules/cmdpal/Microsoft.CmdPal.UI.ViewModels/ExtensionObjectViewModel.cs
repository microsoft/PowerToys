// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.UI.ViewModels;

public abstract partial class ExtensionObjectViewModel : ObservableObject, IBatchUpdateTarget, IBackgroundPropertyChangedNotification
{
    private const int InitialPropertyBatchingBufferSize = 16;

    // Raised on the background thread before UI notifications. It's raised on the background thread to prevent
    // blocking the COM proxy.
    public event PropertyChangedEventHandler? PropertyChangedBackground;

    private readonly ILogger _logger;

    private readonly ConcurrentQueue<string> _pendingProps = [];

    private readonly TaskScheduler _uiScheduler;

    private InterlockedBoolean _batchQueued;

    public WeakReference<IPageContext> PageContext { get; private set; } = null!;

    TaskScheduler IBatchUpdateTarget.UIScheduler => _uiScheduler;

    void IBatchUpdateTarget.ApplyPendingUpdates() => ApplyPendingUpdates();

    bool IBatchUpdateTarget.TryMarkBatchQueued() => _batchQueued.Set();

    void IBatchUpdateTarget.ClearBatchQueued() => _batchQueued.Clear();

    private protected ExtensionObjectViewModel(TaskScheduler scheduler, ILogger logger)
    {
        if (this is not IPageContext)
        {
            throw new InvalidOperationException($"Constructor overload without IPageContext can only be used when the derived class implements IPageContext. Type: {GetType().FullName}");
        }

        _uiScheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Defer PageContext assignment - derived constructor MUST call InitializePageContext()
        // or we set it lazily on first access
    }

    private protected ExtensionObjectViewModel(IPageContext context, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        PageContext = new WeakReference<IPageContext>(context);
        _uiScheduler = context.Scheduler;
        _logger = logger;

        LogIfDefaultScheduler();
    }

    private protected ExtensionObjectViewModel(WeakReference<IPageContext> contextRef, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(contextRef);
        ArgumentNullException.ThrowIfNull(logger);

        if (!contextRef.TryGetTarget(out var context))
        {
            throw new ArgumentException("IPageContext must be alive when creating view models.", nameof(contextRef));
        }

        PageContext = contextRef;
        _uiScheduler = context.Scheduler;
        _logger = logger;

        LogIfDefaultScheduler();
    }

    protected void InitializeSelfAsPageContext()
    {
        if (this is not IPageContext self)
        {
            throw new InvalidOperationException("This method can only be called when the class implements IPageContext.");
        }

        PageContext = new WeakReference<IPageContext>(self);
    }

    private void LogIfDefaultScheduler()
    {
        if (_uiScheduler == TaskScheduler.Default)
        {
            Log_ExtensionObjectViewModelCreatedWithDefaultScheduler(GetType().FullName);
        }
    }

    public virtual Task InitializePropertiesAsync()
        => Task.Run(SafeInitializePropertiesSynchronous);

    public void SafeInitializePropertiesSynchronous()
    {
        try
        {
            InitializeProperties();
        }
        catch (Exception ex)
        {
            ShowException(ex);
        }
    }

    public abstract void InitializeProperties();

    protected void UpdateProperty(string propertyName) => MarkPropertyDirty(propertyName);

    protected void UpdateProperty(string propertyName1, string propertyName2)
    {
        MarkPropertyDirty(propertyName1);
        MarkPropertyDirty(propertyName2);
    }

    protected void UpdateProperty(params string[] propertyNames)
    {
        foreach (var p in propertyNames)
        {
            MarkPropertyDirty(p);
        }
    }

    internal void MarkPropertyDirty(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return;
        }

        // We should re-consider if this worth deduping
        _pendingProps.Enqueue(propertyName);
        BatchUpdateManager.Queue(this);
    }

    public void ApplyPendingUpdates()
    {
        ((IBatchUpdateTarget)this).ClearBatchQueued();

        var buffer = ArrayPool<string>.Shared.Rent(InitialPropertyBatchingBufferSize);
        var count = 0;
        var transferred = false;

        try
        {
            while (_pendingProps.TryDequeue(out var name))
            {
                if (count == buffer.Length)
                {
                    var bigger = ArrayPool<string>.Shared.Rent(buffer.Length * 2);
                    Array.Copy(buffer, bigger, buffer.Length);
                    ArrayPool<string>.Shared.Return(buffer, clearArray: true);
                    buffer = bigger;
                }

                buffer[count++] = name;
            }

            if (count == 0)
            {
                return;
            }

            // 1) Background subscribers (must be raised before UI notifications).
            var propertyChangedEventHandler = PropertyChangedBackground;
            if (propertyChangedEventHandler is not null)
            {
                RaiseBackground(propertyChangedEventHandler, this, buffer, count);
            }

            // 2) UI-facing PropertyChanged: ALWAYS marshal to UI scheduler.
            // Hand-off pooled buffer to UI task (UI task returns it).
            //
            // It would be lovely to do nothing if no one is actually listening on PropertyChanged,
            // but ObservableObject doesn't expose that information.
            _ = Task.Factory.StartNew(
                state =>
                {
                    var p = (UiBatch)state!;
                    try
                    {
                        p.Owner.RaiseUi(p.Names, p.Count);
                    }
                    catch (Exception ex)
                    {
                        Log_FailedToRaisePropertyChangedOnUIThread(ex);
                    }
                    finally
                    {
                        ArrayPool<string>.Shared.Return(p.Names, clearArray: true);
                    }
                },
                new UiBatch(this, buffer, count),
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach,
                _uiScheduler);

            transferred = true;
        }
        catch (Exception ex)
        {
            Log_FailedToApplyPendingPropertyUpdates(ex);
        }
        finally
        {
            if (!transferred)
            {
                ArrayPool<string>.Shared.Return(buffer, clearArray: true);
            }
        }
    }

    private void RaiseUi(string[] names, int count)
    {
        for (var i = 0; i < count; i++)
        {
            OnPropertyChanged(Args(names[i]));
        }
    }

    private void RaiseBackground(PropertyChangedEventHandler handlers, object sender, string[] names, int count)
    {
        try
        {
            for (var i = 0; i < count; i++)
            {
                handlers(sender, Args(names[i]));
            }
        }
        catch (Exception ex)
        {
            Log_FailedToRaisePropertyChangedBackground(ex);
        }
    }

    private sealed record UiBatch(ExtensionObjectViewModel Owner, string[] Names, int Count);

    protected void ShowException(Exception ex, string? extensionHint = null)
    {
        if (PageContext.TryGetTarget(out var pageContext))
        {
            pageContext.ShowException(ex, extensionHint);
        }
        else
        {
            Log_ShowExceptionFailedWithNoPageContext(ex);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PropertyChangedEventArgs Args(string name) => new(name);

    protected void DoOnUiThread(Action action)
    {
        if (PageContext.TryGetTarget(out var pageContext))
        {
            Task.Factory.StartNew(
                action,
                CancellationToken.None,
                TaskCreationOptions.None,
                pageContext.Scheduler);
        }
    }

    protected virtual void UnsafeCleanup()
    {
        // base doesn't do anything, but sub-classes should override this.
    }

    public virtual void SafeCleanup()
    {
        try
        {
            UnsafeCleanup();
        }
        catch (Exception ex)
        {
            Log_CleanupException(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Exception during cleanup")]
    partial void Log_CleanupException(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to show exception because PageContext is unavailable.")]
    partial void Log_ShowExceptionFailedWithNoPageContext(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to raise background property changed notifications.")]
    partial void Log_FailedToRaisePropertyChangedBackground(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to apply pending property updates.")]
    partial void Log_FailedToApplyPendingPropertyUpdates(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to raise property changed notifications on UI thread.")]
    partial void Log_FailedToRaisePropertyChangedOnUIThread(Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ExtensionObjectViewModel created with default TaskScheduler. Type: {typeName}")]
    partial void Log_ExtensionObjectViewModelCreatedWithDefaultScheduler(string? typeName);
}
