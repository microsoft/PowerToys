// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common;
using Microsoft.UI.Dispatching;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>Remembers the window under a palette for one activation.</summary>
internal sealed class CommandPaletteInputSession
{
    private readonly HWND _source;
    private readonly HWND _target;
    private readonly uint _processId;
    private readonly DispatcherQueue _dispatcher;
    private readonly Action _hide;
    private bool _ended;

    private CommandPaletteInputSession(HWND source, HWND target, uint processId, DispatcherQueue dispatcher, Action hide)
    {
        _source = source;
        _target = target;
        _processId = processId;
        _dispatcher = dispatcher;
        _hide = hide;
    }

    public CommandPaletteInputTarget GetTarget(Func<bool>? sourceRouteIsOpen = null) =>
        new((nint)_target, _processId, cancellationToken => ReleaseForeground(sourceRouteIsOpen, cancellationToken));

    public static unsafe CommandPaletteInputSession? Capture(HWND source, HWND foreground, DispatcherQueue dispatcher, Action hide)
    {
        uint processId = 0;
        if (foreground == HWND.Null || PInvoke.GetWindowThreadProcessId(foreground, &processId) == 0 || processId == (uint)Environment.ProcessId)
        {
            return null;
        }

        return new(source, foreground, processId, dispatcher, hide);
    }

    public void End() => _ended = true;

    private Task<bool> ReleaseForeground(Func<bool>? sourceRouteIsOpen, CancellationToken cancellationToken)
    {
        if (_dispatcher.HasThreadAccess)
        {
            return Task.FromResult(TryReleaseForeground(sourceRouteIsOpen, cancellationToken));
        }

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatcher.TryEnqueue(() =>
        {
            try
            {
                completion.SetResult(TryReleaseForeground(sourceRouteIsOpen, cancellationToken));
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        }))
        {
            return Task.FromResult(false);
        }

        return completion.Task;
    }

    private bool TryReleaseForeground(Func<bool>? sourceRouteIsOpen, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || _ended || sourceRouteIsOpen?.Invoke() == false || PInvoke.GetForegroundWindow() != _source)
        {
            return false;
        }

        End();
        _hide();
        return true;
    }
}
