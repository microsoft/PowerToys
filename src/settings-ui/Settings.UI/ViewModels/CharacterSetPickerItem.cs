// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// A character set entry in the "Choose character sets" dialog. Tracks its own
    /// selected state so that selections survive search filtering.
    /// </summary>
    public partial class CharacterSetPickerItem : Observable
    {
        private bool _isChecked;

        public CharacterSetPickerItem(PowerAccentLanguageModel language)
        {
            Language = language;
        }

        public PowerAccentLanguageModel Language { get; }

        public bool IsChecked
        {
            get => _isChecked;
            set => Set(ref _isChecked, value);
        }
    }
}
