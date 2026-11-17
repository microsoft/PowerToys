// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace PowerScripts.UI.ViewModels;

/// <summary>Groups CLI actions under one module.</summary>
public sealed class PowerScriptCliModule
{
    private PowerScriptCliModule(string id, string name, IReadOnlyList<PowerScriptOperationTemplate> templates)
    {
        Id = id;
        Name = name;
        Templates = templates;
    }

    public string Id { get; }

    public string Name { get; }

    public IReadOnlyList<PowerScriptOperationTemplate> Templates { get; }

    public static IReadOnlyList<PowerScriptCliModule> FromTemplates(IEnumerable<PowerScriptOperationTemplate> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);
        var actions = templates.ToArray();
        if (actions.Any(template => template is null || string.IsNullOrWhiteSpace(template.ModuleId) ||
            string.IsNullOrWhiteSpace(template.Group) || string.IsNullOrWhiteSpace(template.Id)) ||
            actions.Select(template => template.Id).Distinct(StringComparer.Ordinal).Count() != actions.Length)
        {
            throw new ArgumentException("CLI actions require unique identifiers and module metadata.", nameof(templates));
        }

        return actions.GroupBy(template => template.ModuleId, StringComparer.OrdinalIgnoreCase).Select(group =>
        {
            var grouped = group.ToArray();
            if (grouped.Any(template => !string.Equals(template.Group, grouped[0].Group, StringComparison.Ordinal)))
            {
                throw new ArgumentException("A CLI module must have one consistent display name.", nameof(templates));
            }

            return new PowerScriptCliModule(group.Key, grouped[0].Group, Array.AsReadOnly(grouped));
        }).ToArray();
    }
}
