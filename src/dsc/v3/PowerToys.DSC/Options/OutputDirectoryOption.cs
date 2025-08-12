// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using System.IO;
using System.Text;
using PowerToys.DSC.Properties;

namespace PowerToys.DSC.Options;

/// <summary>
/// Represents an option for specifying the output directory for the dsc command.
/// </summary>
public sealed class OutputDirectoryOption : Option<string>
{
    private static readonly CompositeFormat InvalidOutputDirectoryError = CompositeFormat.Parse(Resources.InvalidOutputDirectoryError);

    public OutputDirectoryOption()
        : base("--outputDir", Resources.OutputDirectoryOptionDescription)
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
            result.ErrorMessage = Resources.OutputDirectoryEmptyOrNullError;
        }
        else if (!Directory.Exists(value))
        {
            result.ErrorMessage = string.Format(CultureInfo.InvariantCulture, InvalidOutputDirectoryError, value);
        }
    }
}
