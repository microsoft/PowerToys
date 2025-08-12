// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;

namespace PowerToys.DSC.Options;

/// <summary>
/// Represents an option for specifying the output directory for the dsc command.
/// </summary>
internal sealed class OutputDirectoryOption : Option<string>
{
    public OutputDirectoryOption()
        : base("--outputDir", "The output directory")
    {
        AddValidator(OptionValidator);
    }

    /// <summary>
    /// Validates the output directory option.
    /// </summary>
    /// <param name="result">The option result to validate.</param>
    private void OptionValidator(OptionResult result)
    {
        var value = result.GetValueOrDefault<string>() ?? string.Empty;
        if (string.IsNullOrEmpty(value))
        {
            result.ErrorMessage = "Output directory cannot be empty.";
        }
        else if (!Directory.Exists(value))
        {
            result.ErrorMessage = $"Invalid directory: {value}";
        }
    }
}
