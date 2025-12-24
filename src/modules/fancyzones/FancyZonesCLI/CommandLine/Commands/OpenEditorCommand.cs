// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace FancyZonesCLI.CommandLine.Commands;

internal sealed partial class OpenEditorCommand : FancyZonesBaseCommand
{
    public OpenEditorCommand()
        : base("open-editor", "Launch FancyZones layout editor")
    {
        AddAlias("e");
    }

    protected override string Execute(InvocationContext context)
    {
        const string FancyZonesEditorToggleEventName = "Local\\FancyZones-ToggleEditorEvent-1e174338-06a3-472b-874d-073b21c62f14";

        // Check if editor is already running
        var existingProcess = Process.GetProcessesByName("PowerToys.FancyZonesEditor").FirstOrDefault();
        if (existingProcess != null)
        {
            NativeMethods.SetForegroundWindow(existingProcess.MainWindowHandle);
            return string.Empty;
        }

        try
        {
            using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, FancyZonesEditorToggleEventName);
            eventHandle.Set();
            return string.Empty;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to request FancyZones Editor launch. {ex.Message}", ex);
        }
    }
}
