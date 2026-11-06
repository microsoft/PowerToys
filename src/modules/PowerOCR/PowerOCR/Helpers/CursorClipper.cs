// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;

using ManagedCommon;
using PowerOCR.Core.Models;

namespace PowerOCR.Helpers;

internal static partial class CursorClipper
{
    private static readonly object SyncRoot = new();
    private static long _nextGeneration;
    private static ClipState? _activeClip;

    /// <summary>
    /// Attempts to constrain the cursor for one selection gesture. The returned lease owns
    /// only the clip installed by this call and restores the previous global cursor state
    /// when disposed.
    /// </summary>
    internal static IDisposable? TryAcquire(DisplayBounds bounds)
    {
        var requestedRect = new OSInterop.RECT
        {
            Left = bounds.X,
            Top = bounds.Y,
            Right = bounds.X + bounds.Width,
            Bottom = bounds.Y + bounds.Height,
        };

        lock (SyncRoot)
        {
            // ClipCursor is global to the input desktop. Never let two overlay pages believe
            // they independently own it, otherwise a stale cleanup can release a newer clip.
            if (_activeClip is ClipState activeClip)
            {
                if (!activeClip.RestorePending || !TryRestoreActiveClip())
                {
                    Logger.LogWarning("Cursor clipping is already owned by another PowerOCR selection.");
                    return null;
                }
            }

            if (!OSInterop.GetClipCursor(out var previousRect))
            {
                Logger.LogWarning($"GetClipCursor failed with error {Marshal.GetLastWin32Error()}.");
                return null;
            }

            // If the requested restriction is already active, do not claim ownership of a
            // global state that may have been installed by another application.
            if (RectsEqual(previousRect, requestedRect))
            {
                return null;
            }

            if (!OSInterop.ClipCursor(ref requestedRect))
            {
                Logger.LogWarning($"ClipCursor failed with error {Marshal.GetLastWin32Error()}.");
                return null;
            }

            long generation = ++_nextGeneration;
            _activeClip = new ClipState(
                generation,
                requestedRect,
                previousRect,
                IsVirtualScreen(previousRect),
                RestorePending: false);
            return new CursorClipLease(generation);
        }
    }

    /// <summary>
    /// Retries restoration of a PowerOCR-owned clip after window cleanup. This method never
    /// changes cursor state unless the current clip still matches the one PowerOCR installed.
    /// </summary>
    internal static void ReleaseOwnedClip()
    {
        lock (SyncRoot)
        {
            if (_activeClip is ClipState activeClip && !activeClip.RestorePending)
            {
                _activeClip = activeClip with { RestorePending = true };
            }

            _ = TryRestoreActiveClip();
        }
    }

    private static void Release(long generation)
    {
        lock (SyncRoot)
        {
            if (_activeClip is not ClipState activeClip || activeClip.Generation != generation)
            {
                return;
            }

            _activeClip = activeClip with { RestorePending = true };
            _ = TryRestoreActiveClip();
        }
    }

    private static bool TryRestoreActiveClip()
    {
        if (_activeClip is not ClipState activeClip)
        {
            return true;
        }

        if (!OSInterop.GetClipCursor(out var currentRect))
        {
            Logger.LogWarning($"GetClipCursor failed during restore with error {Marshal.GetLastWin32Error()}.");
            return false;
        }

        // Another process may have replaced the global clip while OCR was running. In that
        // case, relinquish local ownership without overwriting the newer owner's state.
        if (!RectsEqual(currentRect, activeClip.InstalledRect))
        {
            _activeClip = null;
            return true;
        }

        bool restored = activeClip.PreviousWasUnrestricted
            ? OSInterop.ClipCursor(IntPtr.Zero)
            : RestorePreviousRect(activeClip.PreviousRect);
        if (!restored)
        {
            Logger.LogWarning($"Failed to restore cursor clipping with error {Marshal.GetLastWin32Error()}.");
            return false;
        }

        _activeClip = null;
        return true;
    }

    private static bool RestorePreviousRect(OSInterop.RECT previousRect)
    {
        return OSInterop.ClipCursor(ref previousRect);
    }

    private static bool IsVirtualScreen(OSInterop.RECT rect)
    {
        int left = OSInterop.GetSystemMetrics(OSInterop.SM_XVIRTUALSCREEN);
        int top = OSInterop.GetSystemMetrics(OSInterop.SM_YVIRTUALSCREEN);
        int width = OSInterop.GetSystemMetrics(OSInterop.SM_CXVIRTUALSCREEN);
        int height = OSInterop.GetSystemMetrics(OSInterop.SM_CYVIRTUALSCREEN);

        return width > 0
               && height > 0
               && rect.Left == left
               && rect.Top == top
               && rect.Right == left + width
               && rect.Bottom == top + height;
    }

    private static bool RectsEqual(OSInterop.RECT left, OSInterop.RECT right)
        => left.Left == right.Left
           && left.Top == right.Top
           && left.Right == right.Right
           && left.Bottom == right.Bottom;

    private readonly record struct ClipState(
        long Generation,
        OSInterop.RECT InstalledRect,
        OSInterop.RECT PreviousRect,
        bool PreviousWasUnrestricted,
        bool RestorePending);

    private sealed partial class CursorClipLease : IDisposable
    {
        private readonly long _generation;
        private int _disposed;

        public CursorClipLease(long generation)
        {
            _generation = generation;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                Release(_generation);
            }
        }
    }
}
