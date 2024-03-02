// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;

namespace FileActionsMenu.Helpers.Telemetry
{
    [EventData]
    public sealed class FileActionsMenuVerifyHashActionInvokedEvent : FileActionsMenuItemInvokedEvent
    {
        public TelemetryHashEnums.HashType HashType { get; set; }

        public TelemetryHashEnums.HashGenerateOrVerifyType VerifyType { get; set; }
    }
}
