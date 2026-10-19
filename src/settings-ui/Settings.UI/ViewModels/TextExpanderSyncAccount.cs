// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>A OneDrive account Text Expander can sync snippets with.</summary>
    public sealed class TextExpanderSyncAccount
    {
        public TextExpanderSyncAccount(string variableName, string root, string displayName)
        {
            VariableName = variableName;
            Root = root;
            DisplayName = displayName;
        }

        /// <summary>Gets the environment variable that points at the account, such as OneDriveCommercial.</summary>
        public string VariableName { get; }

        /// <summary>Gets the account's folder on this PC.</summary>
        public string Root { get; }

        /// <summary>Gets the name shown to the user, such as "OneDrive – Personal".</summary>
        public string DisplayName { get; }

        /// <summary>Gets the variable form saved in settings, so the same setting works on every PC.</summary>
        public string VariablePath => "%" + VariableName + "%";

        // RadioButtons uses this as the accessible name of the generated item.
        public override string ToString() => DisplayName;
    }
}
