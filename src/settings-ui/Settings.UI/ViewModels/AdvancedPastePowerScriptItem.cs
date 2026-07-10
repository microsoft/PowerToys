// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// A PowerScript row shown in the Advanced Paste settings. UI-only: the persisted state is just the
    /// set of enabled ids on <c>AdvancedPasteProperties.EnabledPowerScripts</c>; this projection carries
    /// the script's display metadata plus a two-way <see cref="IsEnabled"/> toggle.
    /// </summary>
    public sealed class AdvancedPastePowerScriptItem : Observable
    {
        private bool _isEnabled;

        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public bool IsEnabled
        {
            get => _isEnabled;
            set => Set(ref _isEnabled, value);
        }
    }
}
