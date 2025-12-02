// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToys.Interop;

namespace PowerToysExtension.Commands;

internal sealed partial class ZoomItActionCommand : InvokableCommand
{
    private readonly string _action;
    private readonly string _title;

    public ZoomItActionCommand(string action, string title)
    {
        _action = action;
        _title = title;
        Name = title;
    }

    public override CommandResult Invoke()
    {
        try
        {
            if (!TryGetEventName(_action, out var eventName))
            {
                return CommandResult.ShowToast($"Unknown ZoomIt action: {_action}.");
            }

            using var evt = EventWaitHandle.OpenExisting(eventName);
            evt.Set();
            return CommandResult.Dismiss();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return CommandResult.ShowToast("ZoomIt is not running. Please start it from PowerToys and try again.");
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Failed to invoke ZoomIt ({_title}): {ex.Message}");
        }
    }

    private static bool TryGetEventName(string action, out string eventName)
    {
        switch (action.ToLowerInvariant())
        {
            case "zoom":
                eventName = Constants.ZoomItZoomEvent();
                return true;
            case "draw":
                eventName = Constants.ZoomItDrawEvent();
                return true;
            case "break":
                eventName = Constants.ZoomItBreakEvent();
                return true;
            case "livezoom":
                eventName = Constants.ZoomItLiveZoomEvent();
                return true;
            case "snip":
                eventName = Constants.ZoomItSnipEvent();
                return true;
            case "record":
                eventName = Constants.ZoomItRecordEvent();
                return true;
            default:
                eventName = string.Empty;
                return false;
        }
    }
}
