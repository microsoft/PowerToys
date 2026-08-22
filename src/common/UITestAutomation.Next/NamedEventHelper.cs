// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Signals the named events PowerToys modules listen on (see <c>common/interop/shared_constants.h</c>).
/// </summary>
/// <remarks>
/// Many module actions that a test would otherwise drive through the Settings UI are also exposed as
/// a named event the runner or module waits on. Signalling it is a single kernel call, where the UI
/// route costs several <c>winapp.exe</c> invocations, each of which walks the Settings UIA tree and
/// can take tens of seconds on a loaded machine. Use this when the Settings interaction is a means to
/// an end rather than the behaviour under test.
/// </remarks>
public static class NamedEventHelper
{
    /// <summary>Toggles the FancyZones layout editor open/closed.</summary>
    public const string FancyZonesEditorToggle = @"Local\FancyZones-ToggleEditorEvent-1e174338-06a3-472b-874d-073b21c62f14";

    /// <summary>Set an existing named event. Returns false when no module currently owns it.</summary>
    public static bool TrySignal(string name)
    {
        try
        {
            if (!EventWaitHandle.TryOpenExisting(name, out var handle))
            {
                return false;
            }

            using (handle)
            {
                return handle.Set();
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Wait until a module has created the named event, then signal it.</summary>
    public static bool WaitAndSignal(string name, int timeoutMS = 15_000, int pollIntervalMS = 250)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        while (true)
        {
            if (TrySignal(name))
            {
                return true;
            }

            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            Thread.Sleep(pollIntervalMS);
        }
    }
}
