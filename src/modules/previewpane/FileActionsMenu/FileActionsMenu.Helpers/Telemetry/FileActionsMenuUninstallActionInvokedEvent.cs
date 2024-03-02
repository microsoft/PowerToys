// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace FileActionsMenu.Helpers.Telemetry
{
    [EventData]
    public sealed class FileActionsMenuUninstallActionInvokedEvent : FileActionsMenuItemInvokedEvent
    {
        public bool IsCalledFromDesktop { get; set; }
    }
}
