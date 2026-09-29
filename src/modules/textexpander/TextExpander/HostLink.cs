// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Links this process lifetime to the PowerToys runner.
///
/// <para>
/// There are two ways to be asked to stop. A <b>named auto-reset event</b> is the convention every
/// PowerToys C# module uses: the module's C++ shim does <c>SetEvent</c> →
/// <c>WaitForSingleObject(process, ~1500ms)</c> → <c>TerminateProcess</c> as a hard fallback, so a
/// module that does not listen for its event is always killed instead of asked. Separately, the
/// <b>host process exiting</b> must take us with it: an orphaned engine would sit there holding a
/// system-wide keyboard hook with nothing left to turn it off.
/// </para>
///
/// <para>
/// Both funnel into the same request, and that request deliberately does <em>not</em> tear the
/// process down here. It posts the ordinary shutdown message to our own message window so the exit
/// unwinds through the normal path, which waits for the expansion worker. That matters: every
/// paste borrows the user's clipboard — emptied, overwritten, handed back — and only the normal
/// path reaches the restore. Exiting from this thread would destroy whatever they had copied,
/// which is precisely what being asked to stop is meant to avoid.
/// </para>
/// </summary>
internal sealed class HostLink : IDisposable
{
    /// <summary>Overrides <see cref="DefaultExitEventName"/>, for testing or a renamed module.</summary>
    public const string ExitEventSwitch = "--exit-event:";

    /// <summary>
    /// Explicit form of the host process id. PowerToys passes its pid as a bare argument, which
    /// <see cref="ParseHostProcessId"/> also accepts; this spelling exists so the intent is
    /// readable in a shortcut or a test script.
    /// </summary>
    public const string HostPidSwitch = "--host-pid:";

    /// <summary>
    /// Default name of the shutdown event.
    ///
    /// <para>
    /// <b>Must match the constant the C++ module shim uses</b>, which for a PowerToys module lives
    /// in <c>src/common/interop/shared_constants.h</c> alongside <c>POWERACCENT_EXIT_EVENT</c> and
    /// friends. PowerToys suffixes these names with a GUID to avoid collisions; adopt whatever the
    /// final module registration settles on and change it here and in the shim together.
    /// </para>
    ///
    /// <para>
    /// The GUID suffix matches what every PowerToys shared event name does
    /// (<c>POWERACCENT_EXIT_EVENT</c> and friends in <c>shared_constants.h</c>). It is not
    /// decoration: a fixed, guessable name can be created first by any same-user process, which
    /// then owns its DACL. That matters most when this engine runs elevated from its logon task,
    /// because a medium-integrity process cannot otherwise reach it — UIPI blocks the window
    /// message, and the elevated token's default DACL grants a filtered token only
    /// <c>EVENT_QUERY_STATE</c>, not <c>EVENT_MODIFY_STATE</c>. Squatting is the way round that,
    /// so the name should not be easy to guess.
    /// </para>
    ///
    /// <para>
    /// <c>Local\</c> rather than <c>Global\</c> on purpose: the engine is a per-user, per-session
    /// process, and a global name would let one session's shutdown stop another's.
    /// </para>
    /// </summary>
    public const string DefaultExitEventName =
        @"Local\PowerToysTextExpanderExitEvent-3f6c1b48-9d02-4a17-8e5b-7c0a2d64f91e";

    /// <summary>
    /// Scopes the single-instance mutex, so an instance run by a host can coexist with a
    /// differently scoped one instead of silently exiting.
    ///
    /// <para>
    /// Without this, enabling a PowerToys module while another engine instance is already running
    /// makes the module's process hit the shared mutex and <c>return 0</c> immediately. PowerToys
    /// would show the module enabled, the shim would see its child vanish, and nothing would
    /// expand — with nothing on screen to explain why.
    /// </para>
    ///
    /// <para>
    /// <b>The tradeoff is real:</b> two engines watching the same keyboard both expand the same
    /// trigger, so separating them is only correct when the user genuinely wants one of each.
    /// Leaving this unset keeps today's behaviour, where the second instance defers.
    /// </para>
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
            // Auto-reset and initially unset, matching CreateEvent(nullptr, false, false, name)
            // on the shim side. Opening an existing one is fine: the shim may well have created
            // it first, and either side may legitimately win that race.
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
    /// Reads the host process id. Accepts the explicit <c>--host-pid:&lt;n&gt;</c> spelling and the
    /// bare positional integer PowerToys actually passes (<c>PowerToys.PowerAccent.exe 1234</c>).
    /// Returns null when there is no host, when no host pid was provided.
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
    /// Opens the host for SYNCHRONIZE only — the least that allows a wait — and wraps it as a
    /// wait handle. Returns null when the process cannot be opened or has already gone, rather
    /// than refusing to start: losing the parent watch is survivable, failing to launch is not.
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
