// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using PowerToys.DSC.Resources;

namespace PowerToys.DSC.Options;

/// <summary>
/// Represents an option for specifying the resource name for the dsc command.
/// </summary>
public sealed class ResourceOption : Option<string>
{
    private readonly IList<string> _resources = [];

    public ResourceOption(IList<string> resources)
        : base("--resource", "The resource name")
    {
        _resources = resources;
        IsRequired = true;
        AddValidator(OptionValidator);
    }

    /// <summary>
    /// Validates the resource option to ensure that the specified resource name is valid.
    /// </summary>
    /// <param name="result">The option result to validate.</param>
    private void OptionValidator(OptionResult result)
    {
        var value = result.GetValueOrDefault<string>() ?? string.Empty;
        if (!_resources.Contains(value))
        {
            result.ErrorMessage = $"Invalid resource name. Valid values are: {string.Join(", ", _resources)}";
        }
    }
}
