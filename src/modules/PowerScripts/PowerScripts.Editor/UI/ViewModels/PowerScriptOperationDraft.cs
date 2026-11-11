// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PowerScripts.UI.ViewModels;

/// <summary>Configuration generated from one Host operation template.</summary>
public sealed class PowerScriptOperationDraft
{
    public PowerScriptOperationDraft(PowerScriptOperationTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentException.ThrowIfNullOrWhiteSpace(template.CommandId);
        Template = template;
        ProjectName = template.Name;
        Parameters = template.Parameters.Select(definition => new PowerScriptOperationParameter(definition)).ToArray();
        if (Parameters.Select(parameter => parameter.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Parameters.Count)
        {
            throw new ArgumentException("Operation parameters must have unique names.", nameof(template));
        }
    }

    public PowerScriptOperationTemplate Template { get; }

    public string ProjectName { get; set; }

    public IReadOnlyList<PowerScriptOperationParameter> Parameters { get; }

    public bool TryBuildConfiguration(out JsonArray parameters, out JsonObject operation, out string error)
    {
        parameters = new JsonArray();
        operation = null;
        var bindings = new JsonObject();
        foreach (var parameter in Parameters)
        {
            if (!parameter.TryValidateForCreate(out error))
            {
                return false;
            }

            if (parameter.ToBinding() is { } binding)
            {
                bindings.Add(parameter.Name, binding);
            }
        }

        operation = new JsonObject { ["commandId"] = Template.CommandId, ["bindings"] = bindings };
        error = null;
        return true;
    }

    /// <summary>Validates the fixed configuration; previews need no runtime parameter values.</summary>
    public bool TryGetTestValues(out IReadOnlyDictionary<string, JsonElement> values, out string error)
    {
        values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        return TryBuildConfiguration(out _, out _, out error);
    }
}
