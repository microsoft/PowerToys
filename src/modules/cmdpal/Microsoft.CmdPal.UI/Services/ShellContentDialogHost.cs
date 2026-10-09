// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.UI.Xaml;

namespace Microsoft.CmdPal.UI.Services;

internal sealed partial class ShellContentDialogHost : IDisposable
{
    private static readonly TimeSpan XamlRootWaitTimeout = TimeSpan.FromSeconds(5);

    private readonly FrameworkElement _owner;
    private readonly Action<bool> _setDialogMode;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly CancellationToken _lifetimeToken;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _isDisposed;

    internal bool IsDialogActive => !_isDisposed && _gate.CurrentCount == 0;

    internal XamlRoot? XamlRoot => _owner.XamlRoot;

    internal ShellContentDialogHost(FrameworkElement owner, Action<bool> setDialogMode)
    {
        _owner = owner;
        _setDialogMode = setDialogMode;
        _lifetimeToken = _lifetimeCts.Token;
    }

    internal T? TryGetResource<T>(object key)
        where T : class
    {
        return _owner.Resources.TryGetValue(key, out var value) ? value as T : null;
    }

    internal async Task<ContentDialogLease?> AcquireAsync(bool enterDialogMode = false)
    {
        if (_isDisposed)
        {
            return null;
        }

        try
        {
            await _gate.WaitAsync(_lifetimeToken);
        }
        catch (OperationCanceledException) when (_isDisposed)
        {
            return null;
        }
        catch (ObjectDisposedException) when (_isDisposed)
        {
            return null;
        }

        if (_isDisposed)
        {
            _gate.Release();
            return null;
        }

        var lease = new ContentDialogLease(_gate, _setDialogMode);
        if (enterDialogMode)
        {
            try
            {
                lease.EnterDialogMode();
            }
            catch
            {
                lease.Dispose();
                throw;
            }
        }

        return lease;
    }

    internal async Task<bool> WaitForXamlRootAsync()
    {
        if (XamlRoot is not null)
        {
            return true;
        }

        if (_isDisposed)
        {
            return false;
        }

        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnLoaded(object sender, RoutedEventArgs args)
        {
            if (XamlRoot is not null)
            {
                loaded.TrySetResult();
            }
        }

        _owner.Loaded += OnLoaded;
        try
        {
            // Loaded may have fired between the initial check and subscribing.
            if (XamlRoot is not null)
            {
                return true;
            }

            try
            {
                await loaded.Task.WaitAsync(XamlRootWaitTimeout, _lifetimeToken);
            }
            catch (TimeoutException)
            {
                Logger.LogError("Could not display external command link consent because the shell was not loaded.");
                return false;
            }
            catch (OperationCanceledException) when (_isDisposed)
            {
                return false;
            }
            catch (ObjectDisposedException) when (_isDisposed)
            {
                return false;
            }

            return XamlRoot is not null;
        }
        finally
        {
            _owner.Loaded -= OnLoaded;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _lifetimeCts.Cancel();
        _ = DisposeGateWhenIdleAsync();
        _lifetimeCts.Dispose();
    }

    private async Task DisposeGateWhenIdleAsync()
    {
        try
        {
            // Active leases and canceled waiters finish before the gate is disposed.
            await _gate.WaitAsync().ConfigureAwait(false);
            _gate.Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to dispose the content-dialog gate.", ex);
        }
    }

    internal sealed partial class ContentDialogLease : IDisposable
    {
        private readonly Action<bool> _setDialogMode;
        private SemaphoreSlim? _gate;
        private bool _isInDialogMode;

        internal ContentDialogLease(SemaphoreSlim gate, Action<bool> setDialogMode)
        {
            _gate = gate;
            _setDialogMode = setDialogMode;
        }

        internal void EnterDialogMode()
        {
            if (_isInDialogMode)
            {
                return;
            }

            _setDialogMode(true);
            _isInDialogMode = true;
        }

        public void Dispose()
        {
            var gate = Interlocked.Exchange(ref _gate, null);
            if (gate is null)
            {
                return;
            }

            try
            {
                if (_isInDialogMode)
                {
                    _setDialogMode(false);
                }
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
