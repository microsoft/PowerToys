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
        public static void LogEvent<T>(string[] selectedItems)
            where T : FileActionsMenuItemInvokedEvent, new()
        {
            PowerToysTelemetry.Log.WriteEvent(new T
            {
                HasFilesSelected = selectedItems.Any(File.Exists),
                HasFoldersSelected = selectedItems.Any(Directory.Exists),
                HasExecutableFilesSelected = selectedItems.Any(item => item.EndsWith(".exe", System.StringComparison.InvariantCulture) || item.EndsWith(".dll", System.StringComparison.InvariantCulture)),
                HasImagesSelected = selectedItems.Any(item => item.IsImage()),
            });
        }
    }
}
