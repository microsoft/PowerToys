// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace PowerToys.DSC.Options;

internal sealed class ModuleOption : Option<string>
{
    public ModuleOption()
        : base("--module", "The module name")
    {
        IsRequired = true;
        AddValidator(OptionValidator);
    }

    private void OptionValidator(OptionResult result)
    {
        var value = result.GetValueOrDefault<string>() ?? string.Empty;
        List<string> validValues = ["Awake"];
        if (!validValues.Contains(value))
        {
            result.ErrorMessage = $"Invalid module name. Valid values are: {string.Join(", ", validValues)}";
        }
    }
}
