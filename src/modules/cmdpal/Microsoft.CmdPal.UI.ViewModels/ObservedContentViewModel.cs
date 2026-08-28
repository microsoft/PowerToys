// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public abstract partial class ObservedContentViewModel<T>(T model, WeakReference<IPageContext> context)
    : ContentViewModel(context)
    where T : IContent
{
    private const int NotInitialized = 0;
    private const int Initializing = 1;
    private const int Initialized = 2;
    private const int Stopped = 3;

    private readonly Lock _refreshGate = new();

    private int _lifetimeState;
    private bool _refreshInProgress;
    private bool _refreshRequested;

    protected T Model { get; } = model;

    protected bool IsStopped => Volatile.Read(ref _lifetimeState) == Stopped;

    public override sealed void InitializeProperties()
    {
        if (Interlocked.CompareExchange(ref _lifetimeState, Initializing, NotInitialized) != NotInitialized)
        {
            return;
        }

        var ownsSubscriptions = true;
        try
        {
            SubscribeToModel();
            lock (_refreshGate)
            {
                if (Interlocked.CompareExchange(ref _lifetimeState, Initialized, Initializing) == Initializing)
                {
                    ownsSubscriptions = false;

                    // Reserve the first read before admitting notifications so initialization
                    // cannot finish while another callback is still reading the initial values.
                    _refreshInProgress = true;
                    _refreshRequested = true;
                }
            }

            if (!ownsSubscriptions)
            {
                ReadPropertiesLoop();
            }
        }
        catch
        {
            SafeCleanup();
            throw;
        }
        finally
        {
            // Cleanup cannot revoke an event while its add accessor is still running.
            // If initialization was stopped, the initializer owns that revocation.
            if (ownsSubscriptions)
            {
                UnsubscribeFromModel();
            }
        }
    }

    protected virtual void SubscribeToModel() => Model.PropChanged += Model_PropChanged;

    protected virtual void UnsubscribeFromModel() => Model.PropChanged -= Model_PropChanged;

    protected abstract void ReadProperties();

    protected void RefreshProperties()
    {
        lock (_refreshGate)
        {
            if (Volatile.Read(ref _lifetimeState) != Initialized)
            {
                return;
            }

            _refreshRequested = true;
            if (_refreshInProgress)
            {
                return;
            }

            _refreshInProgress = true;
        }

        ReadPropertiesSafely();
    }

    private void Model_PropChanged(object sender, IPropChangedEventArgs args) => RefreshProperties();

    private void ReadPropertiesSafely()
    {
        try
        {
            ReadPropertiesLoop();
        }
        catch (Exception ex)
        {
            ShowException(ex);
        }
    }

    private void ReadPropertiesLoop()
    {
        var reschedule = false;
        try
        {
            while (true)
            {
                lock (_refreshGate)
                {
                    if (IsStopped || !_refreshRequested)
                    {
                        return;
                    }

                    _refreshRequested = false;
                }

                // Extension calls can raise notifications synchronously. Never hold the
                // gate across a read; overlapping requests are coalesced into the next pass.
                ReadProperties();
            }
        }
        finally
        {
            lock (_refreshGate)
            {
                _refreshInProgress = false;
                if (!IsStopped && _refreshRequested)
                {
                    // Preserve requests arriving as the reader exits, including on failure.
                    _refreshInProgress = true;
                    reschedule = true;
                }
            }

            if (reschedule)
            {
                _ = Task.Run(ReadPropertiesSafely);
            }
        }
    }

    protected override void UnsafeCleanup()
    {
        if (Interlocked.Exchange(ref _lifetimeState, Stopped) == Initialized)
        {
            UnsubscribeFromModel();
        }
    }
}
