// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.Common;
using Microsoft.CmdPal.Common.Helpers;

namespace Microsoft.CmdPal.ViewModels;

public abstract partial class ExtensionObjectViewModel : ObservableObject, IBatchUpdateTarget, IBackgroundPropertyChangedNotification
{
    private const int InitialPropertyBatchingBufferSize = 16;

    // Raised on the background thread before UI notifications. It's raised on the background thread to prevent
    // blocking the COM proxy.
    public event PropertyChangedEventHandler? PropertyChangedBackground;

    private readonly ConcurrentQueue<string> _pendingProps = [];

    private readonly TaskScheduler _uiScheduler;

    private InterlockedBoolean _batchQueued;

    public WeakReference<IPageContext> PageContext { get; private set; } = null!;

    TaskScheduler IBatchUpdateTarget.UIScheduler => _uiScheduler;

    void IBatchUpdateTarget.ApplyPendingUpdates() => ApplyPendingUpdates();

    bool IBatchUpdateTarget.TryMarkBatchQueued() => _batchQueued.Set();

    void IBatchUpdateTarget.ClearBatchQueued() => _batchQueued.Clear();

    private protected ExtensionObjectViewModel(TaskScheduler scheduler)
    {
        if (this is not IPageContext)
        {
            throw new InvalidOperationException($"Constructor overload without IPageContext can only be used when the derived class implements IPageContext. Type: {GetType().FullName}");
        }

        _uiScheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));

        // Defer PageContext assignment - derived constructor MUST call InitializePageContext()
        // or we set it lazily on first access
    }

    private protected ExtensionObjectViewModel(IPageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        PageContext = new WeakReference<IPageContext>(context);
        _uiScheduler = context.Scheduler;

        LogIfDefaultScheduler();
    }

    private protected ExtensionObjectViewModel(WeakReference<IPageContext> contextRef)
    {
        ArgumentNullException.ThrowIfNull(contextRef);

        if (!contextRef.TryGetTarget(out var context))
        {
            throw new ArgumentException("IPageContext must be alive when creating view models.", nameof(contextRef));
        }

        PageContext = contextRef;
        _uiScheduler = context.Scheduler;

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
            CoreLogger.LogDebug($"ExtensionObjectViewModel created with TaskScheduler.Default. Type: {GetType().FullName}");
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
                static state =>
                {
                    var p = (UiBatch)state!;
                    try
                    {
                        p.Owner.RaiseUi(p.Names, p.Count);
                    }
                    catch (Exception ex)
                    {
                        CoreLogger.LogError("Failed to raise property change notifications on UI thread.", ex);
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
            CoreLogger.LogError("Failed to apply pending property updates.", ex);
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

    private static void RaiseBackground(PropertyChangedEventHandler handlers, object sender, string[] names, int count)
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
            CoreLogger.LogError("Failed to raise PropertyChangedBackground notifications.", ex);
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
            CoreLogger.LogError("Failed to show exception because PageContext is no longer available.", ex);
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
            CoreLogger.LogDebug(ex.ToString());
        }
    }
}
