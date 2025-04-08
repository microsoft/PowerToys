// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

public static class ResultHelper
{
    public static ListItem CreateResult(CalculateResult result, CultureInfo inputCulture, CultureInfo outputCulture)
    {
        return CreateResult(result.RoundedResult, inputCulture, outputCulture);
    }

    public static ListItem CreateResult(decimal? roundedResult, CultureInfo inputCulture, CultureInfo outputCulture)
    {
        // Return null when the expression is not a valid calculator query.
        if (roundedResult == null)
        {
            return null;
        }

        return new ListItem(new CopyTextCommand(roundedResult?.ToString(outputCulture)))
        {
            // Using CurrentCulture since this is user facing
            Icon = CalculatorIcons.ResultIcon,
            Title = roundedResult?.ToString(outputCulture),
            Subtitle = Properties.Resources.calculator_copy_command_name,
            TextToSuggest = roundedResult?.ToString(inputCulture),
            MoreCommands = [new CommandContextItem(new CopyTextCommand(roundedResult?.ToString(outputCulture)))],
        };
    }
}
