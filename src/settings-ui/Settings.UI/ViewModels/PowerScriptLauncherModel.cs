// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using PowerScripts.Client;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

public static class PowerScriptLauncherModel
{
    public static IReadOnlyList<PowerScriptInfo> FilterScripts(IEnumerable<PowerScriptInfo> scripts, string query) =>
        scripts.Where(script => script.IsAction &&
            (script.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
             script.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             script.Description.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
        .OrderBy(script => script.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToArray();

    public static bool TryCollectParameters(
        IEnumerable<PowerScriptParameter> declarations,
        IReadOnlyDictionary<string, string> values,
        out Dictionary<string, string> parameters,
        out string missingParameter)
    {
        parameters = new(StringComparer.OrdinalIgnoreCase);
        missingParameter = null;
        foreach (var declaration in declarations)
        {
            values.TryGetValue(declaration.Name, out var value);
            if (declaration.IsRequired && string.IsNullOrEmpty(value ?? declaration.Default))
            {
                missingParameter = declaration.Label ?? declaration.Name;
                parameters.Clear();
                return false;
            }

            if (value is not null)
            {
                parameters.Add(declaration.Name, value);
            }
        }

        return true;
    }
}
