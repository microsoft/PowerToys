// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>What the page does after the user flips the Sync across PCs toggle.</summary>
    public enum TextExpanderSyncToggleAction
    {
        /// <summary>Nothing more: the change only mirrored the current state, or it cancelled a setup.</summary>
        None,

        /// <summary>The setup is now showing under the toggle; move focus into it.</summary>
        ShowSetup,

        /// <summary>Ask before stopping. Syncing continues unless the user confirms.</summary>
        ConfirmStop,
    }
}
