// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Linq;
using FileActionsMenu.Ui.Helpers;
using Microsoft.PowerToys.Telemetry;

namespace FileActionsMenu.Helpers.Telemetry
{
    public sealed class TelemetryHelper
    {
        public static void LogEvent(FileActionsMenuItemInvokedEvent e, string[] selectedItems)
        {
            e.HasFilesSelected = selectedItems.Any(File.Exists);
            e.HasFoldersSelected = selectedItems.Any(Directory.Exists);
            e.HasExecutableFilesSelected = selectedItems.Any(item => item.EndsWith(".exe", System.StringComparison.InvariantCulture) || item.EndsWith(".dll", System.StringComparison.InvariantCulture));
            e.HasImagesSelected = selectedItems.Any(item => item.IsImage());
            e.ItemCount = selectedItems.Length;

            PowerToysTelemetry.Log.WriteEvent(e);
        }
    }
}
