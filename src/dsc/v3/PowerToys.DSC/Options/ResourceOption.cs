// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using PowerToys.DSC.Resources;

namespace PowerToys.DSC.Options;

internal sealed class ResourceOption : Option<string>
{
    public ResourceOption()
        : base("--resource", "The resource name")
    {
        IsRequired = true;
        AddValidator(OptionValidator);
    }

    private void OptionValidator(OptionResult result)
    {
        var value = result.GetValueOrDefault<string>() ?? string.Empty;
        List<string> validValues = [SettingsResource.ResourceName];
        if (!validValues.Contains(value))
        {
            result.ErrorMessage = $"Invalid resource name. Valid values are: {string.Join(", ", validValues)}";
        }
    }
}
