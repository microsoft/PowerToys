// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

public static class ResultHelper
{
    public static ListItem CreateResult(
        decimal? roundedResult,
        CultureInfo inputCulture,
        CultureInfo outputCulture,
        string query,
        ISettingsInterface settings,
        TypedEventHandler<object, object> handleSave,
        TypedEventHandler<object, object> handleReplace)
    {
        // Return null when the expression is not a valid calculator query.
        if (roundedResult is null)
        {
            return null;
        }

        var result = roundedResult?.ToString(outputCulture);
        var displayResult = FormatWithSeparators(roundedResult.Value, outputCulture);

        // Create a SaveCommand and subscribe to the SaveRequested event
        // This can append the result to the history list.
        // SaveCommand uses the raw result (no separators) so it can be re-parsed.
        var saveCommand = new SaveCommand(result);
        saveCommand.SaveRequested += handleSave;

        var replaceCommand = new ReplaceQueryCommand();
        replaceCommand.ReplaceRequested += handleReplace;

        var copyCommandItem = CreateResult(roundedResult, inputCulture, outputCulture, query);

        // No TextToSuggest on the main save command item. We don't want to keep suggesting what the result is,
        // as the user is typing it.
        return new ListItem(settings.CloseOnEnter ? copyCommandItem.Command : saveCommand)
        {
            // Title uses formatted display value; data operations use raw result
            Icon = Icons.ResultIcon,
            Title = displayResult,
            Subtitle = query,
            MoreCommands = [
                new CommandContextItem(settings.CloseOnEnter ? saveCommand : copyCommandItem.Command),
                new CommandContextItem(replaceCommand) { RequestedShortcut = KeyChords.CopyResultToSearchBox, },
                ..copyCommandItem.MoreCommands,
            ],
        };
    }

    public static ListItem CreateResult(decimal? roundedResult, CultureInfo inputCulture, CultureInfo outputCulture, string query)
    {
        // Return null when the expression is not a valid calculator query.
        if (roundedResult is null)
        {
            return null;
        }

        var decimalResult = roundedResult?.ToString(outputCulture);
        var decimalDisplay = FormatWithSeparators(roundedResult.Value, outputCulture);
        var decimalValue = (decimal)roundedResult;

        List<IContextItem> context = [];

        try
        {
            if (decimal.IsInteger(decimalValue))
            {
                context.Add(new Separator());

                var i = (BigInteger)decimalValue;

                // hexadecimal
                try
                {
                    var hexResult = BaseConverter.Convert(i, 16);
                    context.Add(new CommandContextItem(new CopyTextCommand(hexResult) { Name = Properties.Resources.calculator_copy_hex })
                    {
                        Title = hexResult,
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error converting to hex format", ex);
                }

                // binary
                try
                {
                    var binaryResult = BaseConverter.Convert(i, 2);
                    context.Add(new CommandContextItem(new CopyTextCommand(binaryResult) { Name = Properties.Resources.calculator_copy_binary })
                    {
                        Title = binaryResult,
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error converting to binary format", ex);
                }

                // octal
                try
                {
                    var octalResult = BaseConverter.Convert(i, 8);
                    context.Add(new CommandContextItem(new CopyTextCommand(octalResult) { Name = Properties.Resources.calculator_copy_octal })
                    {
                        Title = octalResult,
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error converting to octal format", ex);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error creating integer context items", ex);
        }

        return new ListItem(new CopyTextCommand(decimalResult))
        {
            // Title shows formatted display; CopyText and TextToSuggest use raw value
            Title = decimalDisplay,
            Subtitle = query,
            TextToSuggest = decimalResult,
            MoreCommands = context.ToArray(),
        };
    }

    private static string FormatWithSeparators(decimal value, CultureInfo culture)
    {
        var plain = value.ToString(culture);
        var sep = culture.NumberFormat.NumberDecimalSeparator;
        var decIdx = plain.IndexOf(sep, StringComparison.Ordinal);
        var decimals = decIdx >= 0 ? plain.Length - decIdx - sep.Length : 0;
        return value.ToString("N" + decimals, culture);
    }
}
