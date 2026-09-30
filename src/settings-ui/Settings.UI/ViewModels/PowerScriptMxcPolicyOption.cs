// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public sealed class PowerScriptMxcPolicyOption : Observable
    {
        private bool _isEnabled;

        public PowerScriptMxcPolicyOption(
            string key,
            string title,
            string description,
            string recommendation,
            bool isEnabled,
            bool isRecommended,
            bool isGloballyEnabled)
        {
            Key = key;
            Title = title;
            Description = description;
            Recommendation = recommendation;
            _isEnabled = isEnabled;
            IsRecommended = isRecommended;
            IsGloballyEnabled = isGloballyEnabled;
        }

        public string Key { get; }

        public string Title { get; }

        public string Description { get; }

        public bool IsRecommended { get; }

        public bool IsGloballyEnabled { get; }

        public string Recommendation { get; }

        public string AutomationId => $"PowerScriptsMxcPolicy_{Key}";

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
