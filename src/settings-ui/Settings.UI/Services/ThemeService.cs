// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.UI.Xaml;

namespace Microsoft.PowerToys.Settings.UI.Services
{
    public class ThemeService
    {
        private readonly ISettingsRepository<GeneralSettings> _generalSettingsRepository;

        public event EventHandler<ElementTheme> ThemeChanged;

        public ElementTheme Theme { get; private set; } = ElementTheme.Default;

        public ThemeService(ISettingsRepository<GeneralSettings> generalSettingsRepository)
        {
            _generalSettingsRepository = generalSettingsRepository;
            Theme = GetTheme();
        }

        public void ApplyTheme()
        {
            Theme = GetTheme();
            ThemeChanged?.Invoke(null, Theme);
        }

        private ElementTheme GetTheme()
        {
            switch (_generalSettingsRepository.SettingsConfig.Theme.ToUpperInvariant())
            {
                case "LIGHT":
                    return ElementTheme.Light;
                case "DARK":
                    return ElementTheme.Dark;
                case "SYSTEM":
                    return ElementTheme.Default;
                default:
                    ManagedCommon.Logger.LogError($"Unexpected theme name: {_generalSettingsRepository.SettingsConfig.Theme}");
                    return ElementTheme.Default;
            }
        }
    }
}
