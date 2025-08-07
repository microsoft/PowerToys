// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace PowerToys.DSC.Helpers;

internal static class ModuleHelper
{
    public static List<string> GetSettingsConfigNames()
    {
        return [.. GetSettingsConfig().Keys.Order()];
    }

    public static Dictionary<string, Type> GetSettingsConfig()
    {
        return Assembly.GetAssembly(typeof(ISettingsConfig))!
            .GetTypes()
            .Where(t => typeof(ISettingsConfig).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .ToDictionary(t => t.Name, t => t);
    }
}
