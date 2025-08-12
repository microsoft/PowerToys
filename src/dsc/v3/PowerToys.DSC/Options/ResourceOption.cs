// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Text;
using PowerToys.DSC.Properties;

namespace PowerToys.DSC.Options;

/// <summary>
/// Represents an option for specifying the resource name for the dsc command.
/// </summary>
public sealed class ResourceOption : Option<string>
{
    private static readonly CompositeFormat InvalidResourceNameError = CompositeFormat.Parse(Resources.InvalidResourceNameError);

    private readonly IList<string> _resources = [];

    public ResourceOption(IList<string> resources)
        : base("--resource", Resources.ResourceOptionDescription)
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
            result.ErrorMessage = string.Format(CultureInfo.InvariantCulture, InvalidResourceNameError, string.Join(", ", _resources));
        }
    }
}
