// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Links this process lifetime to the PowerToys runner: stops when the named exit event is signaled or the
/// host process exits. Either request posts the ordinary shutdown message to our own message window, so the
/// exit unwinds through the normal path that waits for the expansion worker and restores the clipboard.
/// </summary>
internal sealed class HostLink : IDisposable
{
    /// <summary>Overrides <see cref="DefaultExitEventName"/>, for testing or a renamed module.</summary>
    public const string ExitEventSwitch = "--exit-event:";

    /// <summary>Explicit form of the host process id; PowerToys passes a bare pid, which is also accepted.</summary>
    public const string HostPidSwitch = "--host-pid:";

    /// <summary>
    /// Default name of the shutdown event; must match <c>TEXT_EXPANDER_EXIT_EVENT</c> in
    /// <c>src/common/interop/shared_constants.h</c>. The GUID suffix makes the name hard to guess, so another
    /// same-user process can't create it first and own its DACL; <c>Local\</c> keeps one session's shutdown
    /// from stopping another's.
    /// </summary>
    public const string DefaultExitEventName =
        @"Local\PowerToysTextExpanderExitEvent-3f6c1b48-9d02-4a17-8e5b-7c0a2d64f91e";

    /// <summary>
    /// Scopes the single-instance mutex, so an instance run by a host can coexist with a differently scoped one
    /// instead of silently exiting. Two engines both expand the same trigger, so leave it unset unless one of
    /// each is genuinely wanted.
    /// </summary>
    public const string InstanceSwitch = "--instance:";

    /// <summary>
    /// Returns the suffix to append to the single-instance mutex name, or an empty string for the
    /// shared default.
    /// </summary>
    internal static string ParseInstanceSuffix(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        foreach (string arg in args)
        {
            if (arg.StartsWith(InstanceSwitch, StringComparison.OrdinalIgnoreCase))
            {
                string suffix = arg[InstanceSwitch.Length..].Trim();

                // Backslashes would change the kernel namespace (Global\ vs Local\) rather than
                // name an instance within it.
                if (suffix.Length > 0 && !suffix.Contains('\\'))
                {
                    return "." + suffix;
                }
            }
        }

        return string.Empty;
    }

    private readonly EventWaitHandle _exitEvent;
    private readonly ManualResetEvent? _hostExited;
    private readonly ManualResetEvent _disposing = new(false);
    private Thread? _watcher;
    private int _requested;

    public string ExitEventName { get; }

    /// <summary>Gets the host process being watched, or null when running unhosted.</summary>
    public int? HostProcessId { get; }

    private HostLink(string exitEventName, EventWaitHandle exitEvent, int? hostProcessId, ManualResetEvent? hostExited)
    {
        ExitEventName = exitEventName;
        _exitEvent = exitEvent;
        HostProcessId = hostProcessId;
        _hostExited = hostExited;
    }

    /// <summary>
    /// Creates the link, or returns null when the event could not be created — an engine that
    /// cannot be asked to stop is still a working engine, so this never blocks startup.
    /// </summary>
    public static HostLink? Create(string[] args)
    {
        string name = ParseExitEventName(args);

        EventWaitHandle exitEvent;
        try
        {
            // Auto-reset and unset, matching the shim's CreateEvent; either side may create it first.
            exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, name, out _);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or WaitHandleCannotBeOpenedException or ArgumentException)
        {
            return null;
        }

        int? hostPid = ParseHostProcessId(args);
        ManualResetEvent? hostExited = hostPid is int pid ? TryWatchProcess(pid) : null;

        return new HostLink(name, exitEvent, hostPid, hostExited);
    }

    /// <summary>
    /// Starts watching. <paramref name="requestShutdown"/> is invoked at most once, on a
    /// background thread, and must not block.
    /// </summary>
    public void Start(Action requestShutdown)
    {
        ArgumentNullException.ThrowIfNull(requestShutdown);

        _watcher = new Thread(() => Watch(requestShutdown))
        {
            IsBackground = true,
            Name = "GTE Host Link",
        };
        _watcher.Start();
    }

    private void Watch(Action requestShutdown)
    {
        // Index 0 is our own disposal, so Dispose unblocks the wait instead of leaving a thread
        // parked on a handle it is about to close.
        var handles = _hostExited is null
            ? new WaitHandle[] { _disposing, _exitEvent }
            : new WaitHandle[] { _disposing, _exitEvent, _hostExited };

        try
        {
            if (WaitHandle.WaitAny(handles) == 0)
            {
                return; // disposing
            }

            if (Interlocked.Exchange(ref _requested, 1) == 0)
            {
                requestShutdown();
            }
        }
        catch (ObjectDisposedException)
        {
            // Raced with Dispose; nothing left to do.
        }
    }

    // ── Argument parsing ─────────────────────────────────────
    internal static string ParseExitEventName(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        foreach (string arg in args)
        {
            if (arg.StartsWith(ExitEventSwitch, StringComparison.OrdinalIgnoreCase))
            {
                string name = arg[ExitEventSwitch.Length..].Trim();
                if (name.Length > 0)
                {
                    return name;
                }
            }
        }

        return DefaultExitEventName;
    }

    /// <summary>
    /// Reads the host process id from <c>--host-pid:&lt;n&gt;</c> or the bare integer PowerToys passes;
    /// null when there is none.
    /// </summary>
    internal static int? ParseHostProcessId(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        foreach (string arg in args)
        {
            if (arg.StartsWith(HostPidSwitch, StringComparison.OrdinalIgnoreCase) && TryParsePid(arg[HostPidSwitch.Length..], out int explicitPid))
            {
                return explicitPid;
            }
        }

        foreach (string arg in args)
        {
            // Skip switches so "--user:1234" and friends can never be mistaken for a pid.
            if (arg.StartsWith('-') || arg.StartsWith('/'))
            {
                continue;
            }

            if (TryParsePid(arg, out int barePid))
            {
                return barePid;
            }
        }

        return null;
    }

    private static bool TryParsePid(string value, out int pid)
    {
        pid = 0;
        return int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out pid)
            && pid > 0;
    }

    // ── Host process ─────────────────────────────────────────

    /// <summary>
    /// Opens the host for SYNCHRONIZE only and wraps it as a wait handle; null when the process can't be
    /// opened or has gone, which never blocks startup.
    /// </summary>
    private static ManualResetEvent? TryWatchProcess(int pid)
    {
        IntPtr raw = Native.OpenProcess(Native.SYNCHRONIZE, false, (uint)pid);
        if (raw == IntPtr.Zero)
        {
            return null;
        }

        var safe = new SafeWaitHandle(raw, ownsHandle: true);
        if (safe.IsInvalid)
        {
            safe.Dispose();
            return null;
        }

        // Assigning SafeWaitHandle does NOT close the handle the constructor made -- in .NET
        // Core/5+ the setter is a plain field assignment, so the throwaway is only reclaimed by
        // its critical finalizer. Dispose it explicitly rather than leaking a kernel event.
        var wait = new ManualResetEvent(false);
        using (SafeWaitHandle placeholder = wait.SafeWaitHandle)
        {
            wait.SafeWaitHandle = safe;
        }

        return wait;
    }

    public void Dispose()
    {
        try
        {
            _disposing.Set();
        }
        catch (ObjectDisposedException)
        {
        }

        _watcher?.Join(500);
        _watcher = null;

        _exitEvent.Dispose();
        _hostExited?.Dispose();
        _disposing.Dispose();
    }
}
