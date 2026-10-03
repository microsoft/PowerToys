// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    internal static class MouseWithoutBordersMachineMatrixPersistence
    {
        internal static void Save<TMachine>(
            SettingsUtils settingsUtils,
            MouseWithoutBordersSettings settings,
            IEnumerable<TMachine> machines,
            Func<TMachine, string> nameSelector)
        {
            ArgumentNullException.ThrowIfNull(settingsUtils);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(machines);
            ArgumentNullException.ThrowIfNull(nameSelector);

            var machineNames = machines.Select(nameSelector).ToList();
            settings.Properties.MachineMatrixString.Clear();
            settings.Properties.MachineMatrixString.AddRange(machineNames);
            settingsUtils.SaveSettings(settings.ToJsonString(), MouseWithoutBordersSettings.ModuleName);
        }
    }
}
