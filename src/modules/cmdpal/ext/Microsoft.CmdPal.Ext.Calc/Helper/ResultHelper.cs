﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using ManagedCommon;
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

        var copyCommandItem = CreateResult(roundedResult, inputCulture, outputCulture, query);

        return new ListItem(saveCommand)
        {
            // Using CurrentCulture since this is user facing
            Icon = CalculatorIcons.ResultIcon,
            Title = result,
            Subtitle = query,
            TextToSuggest = result,
            MoreCommands = [
                new CommandContextItem(copyCommandItem.Command)
                {
                    Icon = copyCommandItem.Icon,
                    Title = copyCommandItem.Title,
                    Subtitle = copyCommandItem.Subtitle,
                },
                ..copyCommandItem.MoreCommands,
            ],
        };
    }

    public static ListItem CreateResult(decimal? roundedResult, CultureInfo inputCulture, CultureInfo outputCulture, string query)
    {
        // Return null when the expression is not a valid calculator query.
        if (roundedResult == null)
        {
            return null;
        }

        var decimalResult = roundedResult?.ToString(outputCulture);

        List<CommandContextItem> context = [];

        if (decimal.IsInteger((decimal)roundedResult))
        {
            var i = decimal.ToInt64((decimal)roundedResult);
            try
            {
                var hexResult = "0x" + i.ToString("X", outputCulture);
                context.Add(new CommandContextItem(new CopyTextCommand(hexResult) { Name = Properties.Resources.calculator_copy_hex })
                {
                    Title = hexResult,
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Error parsing hex format", ex);
            }

            try
            {
                var binaryResult = "0b" + i.ToString("B", outputCulture);
                context.Add(new CommandContextItem(new CopyTextCommand(binaryResult) { Name = Properties.Resources.calculator_copy_binary })
                {
                    Title = binaryResult,
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Error parsing binary format", ex);
            }
        }

        return new ListItem(new CopyTextCommand(decimalResult))
        {
            // Using CurrentCulture since this is user facing
            Title = decimalResult,
            Subtitle = query,
            TextToSuggest = decimalResult,
            MoreCommands = context.ToArray(),
        };
    }
}
