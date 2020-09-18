// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.PowerToys.Settings.UI.Lib.Interface;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    // This Singleton class is a wrapper around the settings configurations that are accessed by viewmodels.
    // This class can have only one instance and therefore the settings configurations are common to all.
    public class SettingsRepository<T> : ISettingsRepository<T>
        where T : class, ISettingsConfig, new()
    {
        private static readonly object SettingsRepoLock = new object();

        private static SettingsRepository<T> settingsRepository;

        public static SettingsRepository<T> Instance
        {
            get
            {
                // To ensure that only one instance of Settings Repository is created in a multi-threaded environment.
                lock (SettingsRepoLock)
                {
                    if (settingsRepository == null)
                    {
                        settingsRepository = new SettingsRepository<T>();
                    }

                    return settingsRepository;
                }
            }
        }

        // The Singleton class must have a private constructor so that it cannot be instantiated by any other object other than itself.
        private SettingsRepository()
        {
        }

        private T settingsConfig;

        // Settings configurations shared across all viewmodels
        public T SettingsConfig
        {
            get
            {
                if (settingsConfig == null)
                {
                    if (typeof(T) == typeof(GeneralSettings))
                    {
                        settingsConfig = SettingsUtils.GetSettings<T>();
                    }
                    else
                    {
                        T settingsItem = new T();
                        settingsConfig = SettingsUtils.GetSettings<T>(((BasePTModuleSettings)(object)settingsItem).Name, ((BasePTModuleSettings)(object)settingsItem).GetSettingsFileName());
                    }
                }

                return settingsConfig;
            }

            set
            {
                if (value != null)
                {
                    settingsConfig = value;
                }
            }
        }
    }
}
