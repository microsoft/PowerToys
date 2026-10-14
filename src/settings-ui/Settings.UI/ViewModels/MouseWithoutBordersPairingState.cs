// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    internal sealed class MouseWithoutBordersPairingState
    {
        internal string TransientSecurityKey { get; set; }

        internal string ConnectSecurityKey { get; set; }

        internal string ConnectPCName { get; set; }

        internal bool ConnectFieldsVisible { get; set; }

        internal string GetDisplayValue(bool useService, string savedValue)
        {
            if (!useService)
            {
                return savedValue;
            }

            if (TransientSecurityKey != null)
            {
                return TransientSecurityKey;
            }

            return IsSetupRequired(useService, savedValue) ? string.Empty : savedValue;
        }

        internal bool ShouldPreserveTransientSecurityKey(bool useService, string savedValue)
        {
            return TransientSecurityKey != null &&
                useService &&
                string.Equals(
                    savedValue,
                    MouseWithoutBordersIpc.PairingConfiguredIndicator,
                    StringComparison.Ordinal);
        }

        internal static string GetSavedValueAfterGeneratedKey(bool useService, string generatedKey)
        {
            return useService ? MouseWithoutBordersIpc.PairingConfiguredIndicator : generatedKey;
        }

        internal static bool IsSetupRequired(bool useService, string savedValue)
        {
            return useService &&
                string.Equals(
                    savedValue,
                    MouseWithoutBordersIpc.PairingSetupRequiredIndicator,
                    StringComparison.Ordinal);
        }

        internal void Clear()
        {
            TransientSecurityKey = null;
            ConnectSecurityKey = string.Empty;
            ConnectPCName = string.Empty;
            ConnectFieldsVisible = false;
        }
    }
}
