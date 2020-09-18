// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.PowerToys.Settings.UI.Lib.Interface;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    // This Singleton class is a wrapper around the Common settings configurations that are to be shared across all the viewmodels.
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
                        T newObject = new T();
                        settingsConfig = SettingsUtils.GetSettings<T>(((BasePTModuleSettings)(object)newObject).Name);
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
