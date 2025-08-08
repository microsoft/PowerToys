// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;

namespace PowerToys.DSC.Options;

internal sealed class InputOption : Option<string?>
{
    public InputOption()
        : base("--input", "The JSON input")
    {
        AddValidator(OptionValidator);
    }

    private void OptionValidator(OptionResult result)
    {
        var value = result.GetValueOrDefault<string?>() ?? string.Empty;

        try
        {
            JsonDocument.Parse(value);
        }
        catch (JsonException e)
        {
            result.ErrorMessage = $"Invalid JSON input: {e.Message}";
        }
    }
}
