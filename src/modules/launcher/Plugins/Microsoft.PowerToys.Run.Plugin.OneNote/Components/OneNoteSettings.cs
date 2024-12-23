// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Controls;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.OneNote.Components
{
    public class OneNoteSettings : ISettingProvider
    {
        private bool coloredIcons;

        internal bool ShowUnreadItems { get; private set; }

        internal bool ShowEncryptedSections { get; private set; }

        internal bool ShowRecycleBins { get; private set; }

        // A timeout value is required as there currently no way to know if the Run window is visible.
        internal double ComObjectTimeout { get; private set; }

        internal bool ColoredIcons
        {
            get => coloredIcons;
            private set
            {
                coloredIcons = value;
                ColoredIconsSettingChanged?.Invoke();
            }
        }

        internal event Action? ColoredIconsSettingChanged;

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>()
        {
            new PluginAdditionalOption()
            {
                Key = nameof(ShowUnreadItems),
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                DisplayLabel = string.Empty,
                DisplayDescription = string.Empty,
                Value = true,
            },
            new PluginAdditionalOption()
            {
                Key = nameof(ShowEncryptedSections),
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                DisplayLabel = string.Empty,
                DisplayDescription = string.Empty,
                Value = true,
            },
            new PluginAdditionalOption()
            {
                Key = nameof(ShowRecycleBins),
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                DisplayLabel = string.Empty,
                DisplayDescription = string.Empty,
                Value = true,
            },
            new PluginAdditionalOption()
            {
                Key = nameof(ComObjectTimeout),
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Numberbox,
                DisplayLabel = "Test",
                DisplayDescription = "Test",
                NumberValue = 10000,
                NumberBoxMin = 1000,
                NumberBoxMax = 120000,
                NumberBoxSmallChange = 1000,
                NumberBoxLargeChange = 50000,
            },
            new PluginAdditionalOption()
            {
                Key = nameof(ColoredIcons),
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                DisplayLabel = string.Empty,
                DisplayDescription = string.Empty,
                Value = true,
            },
        };

        public Control CreateSettingPanel() => throw new NotImplementedException();

        public void UpdateSettings(PowerLauncherPluginSettings? settings)
        {
            if (settings?.AdditionalOptions == null)
            {
                return;
            }

            ShowUnreadItems = GetBoolSettingOrDefault(settings, nameof(ShowUnreadItems));
            ShowEncryptedSections = GetBoolSettingOrDefault(settings, nameof(ShowEncryptedSections));
            ShowRecycleBins = GetBoolSettingOrDefault(settings, nameof(ShowRecycleBins));

            var comObjectTimeout = settings.AdditionalOptions.FirstOrDefault(x => x.Key == nameof(ComObjectTimeout))?.NumberValue;
            ComObjectTimeout = comObjectTimeout ?? AdditionalOptions.First(x => x.Key == nameof(ComObjectTimeout)).NumberValue;

            ColoredIcons = GetBoolSettingOrDefault(settings, nameof(ColoredIcons));
        }

        private bool GetBoolSettingOrDefault(PowerLauncherPluginSettings settings, string settingName)
        {
            var value = settings.AdditionalOptions.FirstOrDefault(x => x.Key == settingName)?.Value;
            return value ?? AdditionalOptions.First(x => x.Key == settingName).Value;
        }
    }
}
