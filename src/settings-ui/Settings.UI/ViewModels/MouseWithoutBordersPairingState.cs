// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using MouseWithoutBorders.Class;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    internal sealed class MouseWithoutBordersPairingState
    {
        private object _context = new();
        private object _activeUpdate;

        internal bool IsUpdating => _activeUpdate != null;

        internal string TransientSecurityKey { get; set; }

        internal string ConnectSecurityKey { get; set; }

        internal string ConnectPCName { get; set; }

        internal bool ConnectFieldsVisible { get; set; }

        internal bool HasError { get; private set; }

        internal bool TryGetConnectionInputs(out string pcName, out string securityKey)
        {
            pcName = ConnectPCName?.Trim();
            securityKey = ConnectSecurityKey?.Trim();
            HasError = string.IsNullOrEmpty(pcName) || !IsInputKey(securityKey);
            return !HasError;
        }

        internal async Task<bool> TryUpdateAsync(Func<Task> operation, Action onSuccess, Action<Exception> onFailure, Func<bool> isContextCurrent = null)
        {
            if (IsUpdating)
            {
                return false;
            }

            var context = _context;
            var update = new object();
            _activeUpdate = update;
            HasError = false;
            try
            {
                await operation();
                if (!ReferenceEquals(context, _context) || !(isContextCurrent?.Invoke() ?? true))
                {
                    return false;
                }

                onSuccess();
                return true;
            }
            catch (Exception ex)
            {
                if (ReferenceEquals(context, _context) && (isContextCurrent?.Invoke() ?? true))
                {
                    HasError = true;
                    onFailure(ex);
                }

                return false;
            }
            finally
            {
                if (ReferenceEquals(update, _activeUpdate))
                {
                    _activeUpdate = null;
                }
            }
        }

        internal bool ShouldClearForSettingsReload(bool previousUseService, string previousSavedValue, bool useService, string savedValue)
        {
            // A save notification can precede the reply. The owner reads committed settings
            // after acknowledgement before admitting another pairing update.
            if (IsUpdating &&
                previousUseService == useService &&
                !string.Equals(previousSavedValue, savedValue, StringComparison.Ordinal) &&
                (useService ? PairingKeyCodec.HasValidEncryptedFormat(savedValue) : IsInputKey(savedValue)))
            {
                TransientSecurityKey = null;
                return false;
            }

            return HasPairingContextChanged(previousUseService, previousSavedValue, useService, savedValue);
        }

        internal static bool IsInputKey(string value) =>
            PairingKeyCodec.IsOrdinaryKey(value) &&
            value != MouseWithoutBordersIpc.PairingConfiguredIndicator &&
            value != MouseWithoutBordersIpc.PairingSetupRequiredIndicator;

        internal static bool PairingSetupRequired(string savedValue, bool useService) =>
            useService ? !PairingKeyCodec.HasValidEncryptedFormat(savedValue) : !IsInputKey(savedValue);

        internal string GetDisplayValue(bool isElevated, bool useService, string savedValue)
        {
            if (!useService)
            {
                return IsInputKey(savedValue) ? savedValue : string.Empty;
            }

            if (isElevated && IsInputKey(TransientSecurityKey))
            {
                return TransientSecurityKey;
            }

            return PairingKeyCodec.HasValidEncryptedFormat(savedValue) ? MouseWithoutBordersIpc.PairingConfiguredIndicator : string.Empty;
        }

        internal static bool HasPairingContextChanged(bool previousUseService, string previousSavedValue, bool useService, string savedValue)
        {
            return previousUseService != useService ||
                !string.Equals(previousSavedValue, savedValue, StringComparison.Ordinal);
        }

        internal void Clear()
        {
            // Invalidate the display, but keep edits blocked until the pending operation settles.
            _context = new object();
            HasError = false;
            TransientSecurityKey = null;
            ConnectSecurityKey = string.Empty;
            ConnectPCName = string.Empty;
            ConnectFieldsVisible = false;
        }
    }
}
