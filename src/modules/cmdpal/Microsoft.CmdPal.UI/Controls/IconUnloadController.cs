// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Controls;

/// <summary>
/// Defers icon cleanup until the control's loaded state has settled.
/// </summary>
/// <remarks>Call on the UI thread and queue <see cref="ProcessUnload"/> asynchronously.</remarks>
internal sealed class IconUnloadController
{
    private readonly Func<bool> _isLoaded;
    private readonly Action _cleanup;
    private readonly Func<bool> _enqueue;
    private readonly Action<Exception> _reportFailure;

    private bool _unloadPending;
    private bool _callbackQueued;

    /// <summary>
    /// Initializes a new instance of the <see cref="IconUnloadController"/> class.
    /// </summary>
    public IconUnloadController(
        Func<bool> isLoaded,
        Action cleanup,
        Func<bool> enqueue,
        Action<Exception> reportFailure)
    {
        _isLoaded = isLoaded;
        _cleanup = cleanup;
        _enqueue = enqueue;
        _reportFailure = reportFailure;
    }

    public void Loaded()
    {
        _unloadPending = false;
    }

    public void Unloaded()
    {
        _unloadPending = true;
        if (_callbackQueued)
        {
            return;
        }

        _callbackQueued = true;
        try
        {
            if (_enqueue())
            {
                return;
            }
        }
        catch (Exception ex)
        {
            ReportFailure(ex);
        }

        _callbackQueued = false;

        // Queue shutdown still requires releasing the current pending demand.
        CompletePendingUnload();
    }

    public void ProcessUnload()
    {
        _callbackQueued = false;
        if (!_unloadPending)
        {
            return;
        }

        if (_isLoaded())
        {
            _unloadPending = false;
            return;
        }

        CompletePendingUnload();
    }

    public void ReportFailure(Exception exception)
    {
        try
        {
            _reportFailure(exception);
        }
        catch (Exception)
        {
            // Error reporting must not escape a WinUI teardown callback.
        }
    }

    private void CompletePendingUnload()
    {
        if (!_unloadPending)
        {
            return;
        }

        _unloadPending = false;
        try
        {
            _cleanup();
        }
        catch (Exception ex)
        {
            ReportFailure(ex);
        }
    }
}
