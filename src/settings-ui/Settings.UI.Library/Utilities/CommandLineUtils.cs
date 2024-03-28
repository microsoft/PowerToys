// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Reflection;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library;

public class CommandLineUtils
{
    private static Type GetSettingsConfigType(string moduleName, Assembly settingsLibraryAssembly)
    {
        var settingsClassName = moduleName == "GeneralSettings" ? moduleName : moduleName + "Settings";
        return settingsLibraryAssembly.GetType(typeof(CommandLineUtils).Namespace + "." + settingsClassName);
    }

    public static ISettingsConfig GetSettingsConfigFor(string moduleName, ISettingsUtils settingsUtils, Assembly settingsLibraryAssembly)
    {
        return GetSettingsConfigFor(GetSettingsConfigType(moduleName, settingsLibraryAssembly), settingsUtils);
    }

    /// Executes SettingsRepository<moduleSettingsType>.GetInstance(settingsUtils).SettingsConfig
    public static ISettingsConfig GetSettingsConfigFor(Type moduleSettingsType, ISettingsUtils settingsUtils)
    {
        var genericSettingsRepositoryType = typeof(SettingsRepository<>);
        var moduleSettingsRepositoryType = genericSettingsRepositoryType.MakeGenericType(moduleSettingsType);

        // Note: GeneralSettings is only used here only to satisfy nameof constrains, i.e. the choice of this particular type doesn't have any special significance.
        var getInstanceInfo = moduleSettingsRepositoryType.GetMethod(nameof(SettingsRepository<GeneralSettings>.GetInstance));
        var settingsRepository = getInstanceInfo.Invoke(null, new object[] { settingsUtils });
        var settingsConfigProperty = getInstanceInfo.ReturnType.GetProperty(nameof(SettingsRepository<GeneralSettings>.SettingsConfig));
        return settingsConfigProperty.GetValue(settingsRepository) as ISettingsConfig;
    }

    public static Assembly GetSettingsAssembly()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == "PowerToys.Settings.UI.Lib");
    }

    public static object GetPropertyValue(string propertyName, ISettingsConfig settingsConfig)
    {
        var (settingInfo, properties) = LocateSetting(propertyName, settingsConfig);
        return settingInfo.GetValue(properties);
    }

    public static object GetProperties(ISettingsConfig settingsConfig)
    {
        var settingsType = settingsConfig.GetType();
        if (settingsType == typeof(GeneralSettings))
        {
            return settingsConfig;
        }

        var settingsConfigInfo = settingsType.GetProperty("Properties");
        return settingsConfigInfo.GetValue(settingsConfig);
    }

    public static (PropertyInfo SettingInfo, object Properties) LocateSetting(string propertyName, ISettingsConfig settingsConfig)
    {
        var properties = GetProperties(settingsConfig);
        var propertiesType = properties.GetType();
        if (propertiesType == typeof(GeneralSettings) && propertyName.StartsWith("Enabled.", StringComparison.InvariantCulture))
        {
            var moduleNameToToggle = propertyName.Replace("Enabled.", string.Empty);
            properties = propertiesType.GetProperty("Enabled").GetValue(properties);
            propertiesType = properties.GetType();
            propertyName = moduleNameToToggle;
        }

        return (propertiesType.GetProperty(propertyName), properties);
    }

    public static PropertyInfo GetSettingPropertyInfo(string propertyName, ISettingsConfig settingsConfig)
    {
        return LocateSetting(propertyName, settingsConfig).SettingInfo;
    }
}
