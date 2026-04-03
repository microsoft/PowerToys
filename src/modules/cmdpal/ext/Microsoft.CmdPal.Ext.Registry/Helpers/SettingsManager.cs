// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Registry.Helpers;

public class SettingsManager : BuiltinJsonSettingsManager, ISettingsInterface
{
    private static readonly string _namespace = "registry";

    private static string Namespaced(string propertyName) => $"{_namespace}.{propertyName}";

    public SettingsManager()
        : base(_namespace)
    {
        // Add settings here when needed
        // Settings.Add(setting);
        LoadSettings();

        Settings.SettingsChanged += (s, a) => this.SaveSettings();
    }
}
