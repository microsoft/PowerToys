// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

public static class ResultHelper
{
    public static ListItem CreateResult(decimal? roundedResult, CultureInfo inputCulture, CultureInfo outputCulture, string query, TypedEventHandler<object, object> handleSave)
    {
        // Return null when the expression is not a valid calculator query.
        if (roundedResult == null)
        {
            return null;
        }

        var result = roundedResult?.ToString(outputCulture);

        // Create a SaveCommand and subscribe to the SaveRequested event
        // This can append the result to the history list.
        var saveCommand = new SaveCommand(result);
        saveCommand.SaveRequested += handleSave;

        return new ListItem(saveCommand)
        {
            // Using CurrentCulture since this is user facing
            Icon = CalculatorIcons.ResultIcon,
            Title = result,
            Subtitle = query,
            TextToSuggest = result,
            MoreCommands = [new CommandContextItem(new CopyTextCommand(result))],
        };
    }

    public static ListItem CreateResult(decimal? roundedResult, CultureInfo inputCulture, CultureInfo outputCulture, string query)
    {
        // Return null when the expression is not a valid calculator query.
        if (roundedResult == null)
        {
            return null;
        }

        var result = roundedResult?.ToString(outputCulture);

        return new ListItem(new CopyTextCommand(result))
        {
            // Using CurrentCulture since this is user facing
            Icon = CalculatorIcons.ResultIcon,
            Title = result,
            Subtitle = query,
            TextToSuggest = result,
        };
    }
}
