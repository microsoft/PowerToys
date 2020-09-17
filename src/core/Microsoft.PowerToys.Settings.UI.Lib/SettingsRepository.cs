// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.PowerToys.Settings.UI.Lib.Interface;
using Microsoft.PowerToys.Settings.UI.Lib.Utilities;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    // This Singleton class is a wrapper around the Common settings configurations that are to be shared across all the viewmodels.
    // This class can have only one instance and therefore the settings configurations are common to all.
    public class SettingsRepository : ISettingsRepository
    {
        private static SettingsRepository settingsRepository;

        public static SettingsRepository Instance
        {
            get
            {
                if (settingsRepository == null)
                {
                    settingsRepository = new SettingsRepository();
                }

                return settingsRepository;
            }
        }

        private SettingsRepository()
        {
        }

        public bool IsGeneralSettingsInitialized { get; set; }

        private GeneralSettings generalSettingsConfig;

        // Settings configurations shared across all viewmodels
        public GeneralSettings GeneralSettingsConfig
        {
            get
            {
                if (generalSettingsConfig == null)
                {
                    try
                    {
                        generalSettingsConfig = SettingsUtils.GetSettings<GeneralSettings>(string.Empty);

                        if (Helper.CompareVersions(generalSettingsConfig.PowertoysVersion, Helper.GetProductVersion()) < 0)
                        {
                            // Update settings
                            generalSettingsConfig.PowertoysVersion = Helper.GetProductVersion();
                            SettingsUtils.SaveSettings(generalSettingsConfig.ToJsonString(), string.Empty);
                        }
                    }
                    catch (FormatException e)
                    {
                        // If there is an issue with the version number format, don't migrate settings.
                        Debug.WriteLine(e.Message);
                    }
                    catch
                    {
                        SettingsUtils.SaveSettings(generalSettingsConfig.ToJsonString(), string.Empty);
                    }

                    IsGeneralSettingsInitialized = true;
                }

                return generalSettingsConfig;
            }

            set
            {
                if (value != null)
                {
                    generalSettingsConfig = value;
                }
            }
        }
    }
}
