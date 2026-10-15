// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    internal sealed class MouseWithoutBordersPairingState
    {
        private object _context = new();
        private bool _awaitingServiceKeyReload;
        private bool _serviceKeyRequestPending;

        internal string TransientSecurityKey { get; set; }

        internal string ConnectSecurityKey { get; set; }

        internal string ConnectPCName { get; set; }

        internal bool ConnectFieldsVisible { get; set; }

        internal bool HasError { get; private set; }

        internal bool TryGetConnectionInputs(out string pcName, out string securityKey)
        {
            pcName = ConnectPCName?.Trim();
            securityKey = ConnectSecurityKey?.Trim();
            HasError = string.IsNullOrEmpty(pcName) || !MouseWithoutBordersPairingHelper.IsOrdinaryKey(securityKey);
            return !HasError;
        }

        internal async Task<bool> TryUpdateAsync(Func<Task> operation, Action onSuccess, Action<Exception> onFailure, Func<bool> isContextCurrent = null, bool isServiceKeyGeneration = false)
        {
            var context = _context;
            if (isServiceKeyGeneration)
            {
                _awaitingServiceKeyReload = true;
                _serviceKeyRequestPending = true;
            }

            HasError = false;
            try
            {
                await operation();
            }
            catch (Exception ex)
            {
                if (ReferenceEquals(context, _context) && (isContextCurrent?.Invoke() ?? true))
                {
                    if (isServiceKeyGeneration)
                    {
                        _awaitingServiceKeyReload = false;
                        _serviceKeyRequestPending = false;
                    }

                    HasError = true;
                    onFailure(ex);
                }

                return false;
            }

            if (!ReferenceEquals(context, _context) || !(isContextCurrent?.Invoke() ?? true))
            {
                return false;
            }

            if (isServiceKeyGeneration)
            {
                _serviceKeyRequestPending = false;
            }

            onSuccess();
            return true;
        }

        internal bool ShouldClearForSettingsReload(bool previousUseService, string previousSavedValue, bool useService, string savedValue)
        {
            // The service can persist the encrypted value before or after the RPC reply.
            if (_awaitingServiceKeyReload &&
                previousUseService && useService &&
                !string.Equals(previousSavedValue, savedValue, StringComparison.Ordinal) &&
                MouseWithoutBordersPairingHelper.HasValidEncryptedFormat(savedValue))
            {
                _awaitingServiceKeyReload = false;
                if (_serviceKeyRequestPending)
                {
                    TransientSecurityKey = null;
                }

                return false;
            }

            return HasPairingContextChanged(previousUseService, previousSavedValue, useService, savedValue);
        }

        internal string GetDisplayValue(bool isElevated, bool useService, string savedValue) =>
            MouseWithoutBordersPairingHelper.GetDisplayValue(savedValue, isElevated, useService, TransientSecurityKey);

        internal static bool HasPairingContextChanged(bool previousUseService, string previousSavedValue, bool useService, string savedValue)
        {
            return previousUseService != useService ||
                !string.Equals(previousSavedValue, savedValue, StringComparison.Ordinal);
        }

        internal void Clear()
        {
            _context = new object();
            _awaitingServiceKeyReload = false;
            _serviceKeyRequestPending = false;
            HasError = false;
            TransientSecurityKey = null;
            ConnectSecurityKey = string.Empty;
            ConnectPCName = string.Empty;
            ConnectFieldsVisible = false;
        }
    }
}
