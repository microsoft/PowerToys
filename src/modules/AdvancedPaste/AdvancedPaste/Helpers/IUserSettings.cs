// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;

using Microsoft.PowerToys.Settings.UI.Library;

namespace AdvancedPaste.Settings
{
    public interface IUserSettings
    {
        public bool ShowCustomPreview { get; }

        public bool SendPasteKeyCombination { get; }

        public bool CloseAfterLosingFocus { get; }

        public ObservableCollection<AdvancedPasteCustomAction> CustomActions { get; }
    }
}
