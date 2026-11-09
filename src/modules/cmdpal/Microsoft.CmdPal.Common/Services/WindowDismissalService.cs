// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Win32;
using Windows.Win32.Foundation;

namespace Microsoft.CmdPal.Common.Services;

/// <summary>
/// Keeps a host window open while an owned operation is active.
/// </summary>
public sealed partial class WindowDismissalService
{
    private readonly Lock _lock = new();
    private readonly Dictionary<nint, HashSet<Hold>> _holds = [];

    /// <summary>
    /// Raised when the final hold for a window is released.
    /// </summary>
    /// <remarks>
    /// Handlers run on the releasing thread, outside the service lock.
    /// Resetting a closed session does not raise this event.
    /// </remarks>
    public event EventHandler<nint>? LastHoldReleased;

    /// <summary>
    /// Suppresses automatic dismissal until the returned hold is disposed.
    /// </summary>
    /// <remarks>
    /// Acquire before transferring focus. Holds may overlap and may be released on any thread.
    /// Explicitly closing the host ends its holds without waiting for the operations to finish.
    /// </remarks>
    public IDisposable Acquire(nint ownerHwnd)
    {
        ArgumentOutOfRangeException.ThrowIfZero(ownerHwnd);
        lock (_lock)
        {
            if (!_holds.TryGetValue(ownerHwnd, out var holds))
            {
                holds = [];
                _holds.Add(ownerHwnd, holds);
            }

            var hold = new Hold(this, ownerHwnd);
            holds.Add(hold);
            return hold;
        }
    }

    /// <summary>
    /// Checks for an explicit hold or a native modal dialog that has disabled its owner.
    /// </summary>
    public bool IsSuppressed(nint ownerHwnd)
    {
        lock (_lock)
        {
            if (_holds.ContainsKey(ownerHwnd))
            {
                return true;
            }
        }

        var owner = (HWND)ownerHwnd;
        return PInvoke.IsWindow(owner) && PInvoke.IsWindowEnabled(owner) == 0;
    }

    /// <summary>
    /// Ends all holds for a closed window or page session.
    /// </summary>
    public void Reset(nint ownerHwnd)
    {
        lock (_lock)
        {
            _holds.Remove(ownerHwnd);
        }
    }

    private void Release(nint ownerHwnd, Hold hold)
    {
        lock (_lock)
        {
            if (!_holds.TryGetValue(ownerHwnd, out var holds) || !holds.Remove(hold) || holds.Count != 0)
            {
                return;
            }

            _holds.Remove(ownerHwnd);
        }

        LastHoldReleased?.Invoke(this, ownerHwnd);
    }

    private sealed partial class Hold(WindowDismissalService service, nint ownerHwnd) : IDisposable
    {
        public void Dispose() => service.Release(ownerHwnd, this);
    }
}
