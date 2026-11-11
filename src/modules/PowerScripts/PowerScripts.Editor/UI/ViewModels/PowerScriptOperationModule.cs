// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace PowerScripts.UI.ViewModels;

/// <summary>One module and its operations in their Host catalogue order.</summary>
public sealed class PowerScriptOperationModule
{
    private readonly List<PowerScriptOperationTemplate> _operations = new();

    private PowerScriptOperationModule(string id, string name)
    {
        Id = id;
        Name = name;
        Operations = _operations.AsReadOnly();
    }

    public string Id { get; }

    public string Name { get; }

    public IReadOnlyList<PowerScriptOperationTemplate> Operations { get; }

    public static IReadOnlyList<PowerScriptOperationModule> FromTemplates(IEnumerable<PowerScriptOperationTemplate> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);
        var modules = new List<PowerScriptOperationModule>();
        var byId = new Dictionary<string, PowerScriptOperationModule>(StringComparer.OrdinalIgnoreCase);
        var templateIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var operationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var template in templates)
        {
            if (template is null || string.IsNullOrWhiteSpace(template.ModuleId) || string.IsNullOrWhiteSpace(template.Group))
            {
                throw new ArgumentException("Operation templates require a module identifier and display name.", nameof(templates));
            }

            if (string.IsNullOrWhiteSpace(template.Id) || string.IsNullOrWhiteSpace(template.CommandId))
            {
                throw new ArgumentException("Operation templates require template and operation identifiers.", nameof(templates));
            }

            if (!templateIds.Add(template.Id))
            {
                throw new ArgumentException("Operation template identifiers must be unique.", nameof(templates));
            }

            if (!operationIds.Add(template.CommandId))
            {
                throw new ArgumentException("Operation identifiers must be unique across modules.", nameof(templates));
            }

            if (!byId.TryGetValue(template.ModuleId, out var module))
            {
                module = new PowerScriptOperationModule(template.ModuleId, template.Group);
                byId.Add(module.Id, module);
                modules.Add(module);
            }
            else if (!string.Equals(module.Name, template.Group, StringComparison.Ordinal))
            {
                throw new ArgumentException("A module identifier must have one consistent display name.", nameof(templates));
            }

            module._operations.Add(template);
        }

        return modules.AsReadOnly();
    }
}
