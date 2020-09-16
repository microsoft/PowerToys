// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Lib.Interface;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class GeneralSettingsCache<T> : IGeneralSettingsCache<T>
    {
        private static GeneralSettingsCache<T> settingsCache;

        public static GeneralSettingsCache<T> Instance
        {
            get
            {
                if (settingsCache == null)
                {
                    settingsCache = new GeneralSettingsCache<T>();
                }

                return settingsCache;
            }
        }

        private GeneralSettingsCache()
        {
        }

        public T CommonSettingsConfig { get; set; }
    }
}
