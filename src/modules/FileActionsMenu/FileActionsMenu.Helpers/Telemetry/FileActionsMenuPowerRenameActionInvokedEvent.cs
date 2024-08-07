// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace FileActionsMenu.Helpers.Telemetry
{
    [EventData]
    public class FileActionsMenuPowerRenameActionInvokedEvent : EventBase, IFileActionsMenuItemInvokedEvent
    {
        public int ItemCount { get; set; }

        public bool HasImagesSelected { get; set; }

        public bool HasFilesSelected { get; set; }

        public bool HasFoldersSelected { get; set; }

        public bool HasExecutableFilesSelected { get; set; }

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
