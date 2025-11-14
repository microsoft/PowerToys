// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Configuration for Paste AI features (custom action transformations like custom prompt processing)
    /// </summary>
    public class PasteAIConfiguration : INotifyPropertyChanged
    {
        private string _activeProviderId = string.Empty;
        private ObservableCollection<PasteAIProviderDefinition> _providers = new();

        public event PropertyChangedEventHandler PropertyChanged;

        [JsonPropertyName("active-provider-id")]
        public string ActiveProviderId
        {
            get => _activeProviderId;
            set => SetProperty(ref _activeProviderId, value ?? string.Empty);
        }

        [JsonPropertyName("providers")]
        public ObservableCollection<PasteAIProviderDefinition> Providers
        {
            get => _providers;
            set => SetProperty(ref _providers, value ?? new ObservableCollection<PasteAIProviderDefinition>());
        }

        [JsonIgnore]
        public PasteAIProviderDefinition ActiveProvider
        {
            get
            {
                if (_providers is null || _providers.Count == 0)
                {
                    return null;
                }

                if (!string.IsNullOrWhiteSpace(_activeProviderId))
                {
                    var match = _providers.FirstOrDefault(provider => string.Equals(provider.Id, _activeProviderId, StringComparison.OrdinalIgnoreCase));
                    if (match is not null)
                    {
                        return match;
                    }
                }

                return _providers[0];
            }
        }

        [JsonIgnore]
        public AIServiceType ActiveServiceTypeKind => ActiveProvider?.ServiceTypeKind ?? AIServiceType.OpenAI;

        public override string ToString()
            => JsonSerializer.Serialize(this);

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
