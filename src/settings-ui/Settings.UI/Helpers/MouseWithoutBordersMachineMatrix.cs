// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.PowerToys.Settings.UI.Helpers;

public static class MouseWithoutBordersMachineMatrix
{
    public static bool Reconcile(List<string> matrix, List<string> availableMachines)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(availableMachines);

        var changed = false;
        for (var index = 0; index < matrix.Count; index++)
        {
            // Saving an already-empty slot would retrigger the settings watcher indefinitely.
            if (!string.IsNullOrEmpty(matrix[index]) && !availableMachines.Contains(matrix[index]))
            {
                matrix[index] = string.Empty;
                changed = true;
            }
        }

        foreach (var machine in availableMachines)
        {
            if (matrix.Contains(machine))
            {
                continue;
            }

            var index = matrix.FindIndex(string.IsNullOrEmpty);
            if (index >= 0)
            {
                matrix[index] = machine;
                changed = true;
            }
        }

        return changed;
    }
}
