// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Run.Plugin.System.Properties;
using Microsoft.PowerToys.Settings.UI.Library;

[assembly: InternalsVisibleTo("Microsoft.PowerToys.Run.Plugin.System.UnitTests")]

namespace Microsoft.PowerToys.Run.Plugin.System.Components
{
    /// <summary>
    /// Additional settings for the WindowWalker plugin.
    /// </summary>
    /// <remarks>Some code parts reused from TimeZone plugin.</remarks>
    internal sealed class SystemPluginSettings
    {
        /// <summary>
        /// Are the class properties initialized with default values
        /// </summary>
        private readonly bool _initialized;

        /// <summary>
        /// An instance of the class <see cref="SystemPluginSettings"></see>
        /// </summary>
        private static SystemPluginSettings instance;

        /// <summary>
        /// Gets a value indicating whether the user has to confirm the execution of system commands
        /// </summary>
        internal bool ConfirmSystemCommands { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the commands are localized or not
        /// </summary>
        internal bool LocalizeSystemCommands { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SystemPluginSettings"/> class.
        /// Private constructor to make sure there is never more than one instance of this class
        /// </summary>
        private SystemPluginSettings()
        {
            // Init class properties with default values
            UpdateSettings(null);
            _initialized = true;
        }

        /// <summary>
        /// Gets an instance property of this class that makes sure that the first instance gets created
        /// and that all the requests end up at that one instance.
        /// The benefit of this is that we don't need additional variables/parameters
        /// to communicate the settings between plugin's classes/methods.
        /// We can simply access this one instance, whenever we need the actual settings.
        /// </summary>
        internal static SystemPluginSettings Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new SystemPluginSettings();
                }

                return instance;
            }
        }

        /// <summary>
        /// Return a list with all additional plugin options.
        /// </summary>
        /// <returns>A list with all additional plugin options.</returns>
        internal static List<PluginAdditionalOption> GetAdditionalOptions()
        {
            var optionList = new List<PluginAdditionalOption>
            {
                new PluginAdditionalOption()
                {
                    Key = nameof(ConfirmSystemCommands),
                    DisplayLabel = Resources.confirm_system_commands,
                    Value = false,
                },
                new PluginAdditionalOption()
                {
                    Key = nameof(LocalizeSystemCommands),
                    DisplayLabel = Resources.Use_localized_system_commands,
                    Value = true,
                },
            };

            return optionList;
        }

        /// <summary>
        /// Update this settings.
        /// </summary>
        /// <param name="settings">The settings for all power launcher plugins.</param>
        internal void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            if ((settings is null || settings.AdditionalOptions is null) & _initialized)
            {
                return;
            }

            ConfirmSystemCommands = GetSettingOrDefault(settings, nameof(ConfirmSystemCommands));
            LocalizeSystemCommands = GetSettingOrDefault(settings, nameof(LocalizeSystemCommands));
        }

        /// <summary>
        /// Return one <see cref="bool"/> setting of the given settings list with the given name.
        /// </summary>
        /// <param name="settings">The object that contain all settings.</param>
        /// <param name="name">The name of the setting.</param>
        /// <returns>A settings value.</returns>
        private static bool GetSettingOrDefault(PowerLauncherPluginSettings settings, string name)
        {
            var option = settings?.AdditionalOptions?.FirstOrDefault(x => x.Key == name);

            // If a setting isn't available, we use the value defined in the method GetAdditionalOptions() as fallback.
            // We can use First() instead of FirstOrDefault() because the values must exist. Otherwise, we made a mistake when defining the settings.
            return option?.Value ?? GetAdditionalOptions().First(x => x.Key == name).Value;
        }
    }
}
