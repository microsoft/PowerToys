// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;

namespace PowerToys.DSC.Options;

/// <summary>
/// Represents an option for specifying JSON input for the dsc command.
/// </summary>
public sealed class InputOption : Option<string>
{
    public InputOption()
        : base("--input", "The JSON input")
    {
        AddValidator(OptionValidator);
    }

    /// <summary>
    /// Validates the JSON input provided to the option.
    /// </summary>
    /// <param name="result">The option result to validate.</param>
    private void OptionValidator(OptionResult result)
    {
        var value = result.GetValueOrDefault<string>() ?? string.Empty;
        if (string.IsNullOrEmpty(value))
        {
            result.ErrorMessage = "Input cannot be empty.";
        }
        else
        {
            try
            {
                JsonDocument.Parse(value);
            }
            catch (Exception e)
            {
                result.ErrorMessage = $"Invalid JSON input: {e.Message}";
            }
        }
    }
}
