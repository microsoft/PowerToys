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

    /// <summary>Triggers Find My Mouse.</summary>
    public const string FindMyMouseTrigger = @"Local\FindMyMouseTriggerEvent-5a9dc5f4-1c74-4f2f-a66f-1b9b6a2f9b23";

    /// <summary>Toggles Mouse Highlighter.</summary>
    public const string MouseHighlighterToggle = @"Local\MouseHighlighterTriggerEvent-1e3c9c3d-3fdf-4f9a-9a52-31c9b3c3a8f4";

    /// <summary>Toggles Mouse Pointer Crosshairs.</summary>
    public const string MouseCrosshairsToggle = @"Local\MouseCrosshairsTriggerEvent-0d4c7f92-0a5c-4f5c-b64b-8a2a2f7e0b21";

    /// <summary>Shows the Mouse Jump preview.</summary>
    public const string MouseJumpShowPreview = @"Local\MouseJumpEvent-aa0be051-3396-4976-b7ba-1a9cc7d236a5";

    /// <summary>Toggles Cursor Wrap.</summary>
    public const string CursorWrapToggle = @"Local\CursorWrapTriggerEvent-1f8452b5-4e6e-45b3-8b09-13f14a5900c9";

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

    /// <summary>Whether a named event currently exists.</summary>
    public static bool Exists(string name)
    {
        try
        {
            if (!EventWaitHandle.TryOpenExisting(name, out var handle))
            {
                return false;
            }

            handle.Dispose();
            return true;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
    }

    /// <summary>Wait until a module creates a named event without signaling it.</summary>
    public static bool WaitUntilAvailable(string name, int timeoutMS = 15_000, int pollIntervalMS = 250) =>
        WaitForAvailability(name, expected: true, timeoutMS, pollIntervalMS);

    /// <summary>Wait until a module closes a named event.</summary>
    public static bool WaitUntilUnavailable(string name, int timeoutMS = 15_000, int pollIntervalMS = 250) =>
        WaitForAvailability(name, expected: false, timeoutMS, pollIntervalMS);

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

    private static bool WaitForAvailability(string name, bool expected, int timeoutMS, int pollIntervalMS)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        while (true)
        {
            if (Exists(name) == expected)
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
