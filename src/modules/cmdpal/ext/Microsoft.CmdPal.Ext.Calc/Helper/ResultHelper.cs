// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

public static class ResultHelper
{
    internal static CommandResult CreateCopyCommandResult(bool hideOnCopy)
    {
        return CommandResult.ShowToast(new ToastArgs
        {
            Message = Properties.Resources.calculator_copy_toast_text,
            Result = hideOnCopy ? CommandResult.Hide() : CommandResult.KeepOpen(),
        });
    }

    public static ListItem CreateResultForPage(
        decimal? roundedResult,
        CultureInfo inputCulture,
        CultureInfo outputCulture,
        string query,
        ISettingsInterface settings,
        TypedEventHandler<object, object> handleReplace)
    {
        // Return null when the expression is not a valid calculator query.
        if (roundedResult is null)
        {
            return null;
        }

        var result = roundedResult?.ToString(outputCulture);

        var replaceCommand = new ReplaceQueryCommand();
        replaceCommand.ReplaceRequested += handleReplace;

        var copyCommand = new CalculatorCopyCommand(result, query, settings);
        copyCommand.ReplaceRequested += ReplaceOnAction;

        var pasteCommand = new CalculatorPasteCommand(result, query, settings);
        pasteCommand.ReplaceRequested += ReplaceOnAction;

        var usePaste = settings.PrimaryAction == PrimaryAction.Paste;
        var primaryCommand = usePaste ? (ICommand)pasteCommand : copyCommand;
        var secondaryCommand = usePaste ? (ICommand)copyCommand : pasteCommand;
        var copyCommandItem = CreateResultItem(roundedResult, inputCulture, outputCulture, query, primaryCommand, settings.CloseOnEnter);

        // No TextToSuggest on the main save command item. We don't want to keep suggesting what the result is,
        // as the user is typing it.
        return new ListItem(primaryCommand)
        {
            Icon = Icons.ResultIcon,
            Title = result,
            Subtitle = query,
            MoreCommands = [
                new CommandContextItem(secondaryCommand),
                new CommandContextItem(replaceCommand) { RequestedShortcut = KeyChords.CopyResultToSearchBox, },
                ..copyCommandItem.MoreCommands,
            ],
        };

        void ReplaceOnAction(object sender, object args)
        {
            if (settings.ReplaceQueryOnEnter)
            {
                handleReplace(sender, args);
            }
        }
    }

    public static ListItem CreateResultForFallback(decimal? roundedResult, CultureInfo inputCulture, CultureInfo outputCulture, string query)
    {
        // Return null when the expression is not a valid calculator query.
        if (roundedResult is null)
        {
            return null;
        }

        var decimalResult = roundedResult?.ToString(outputCulture);
        var copyCommand = CreateCopyCommand(decimalResult, Properties.Resources.calculator_copy_command_name, hideOnCopy: true);
        return CreateResultItem(roundedResult, inputCulture, outputCulture, query, copyCommand, hideOnCopy: true);
    }

    private static ListItem CreateResultItem(decimal? roundedResult, CultureInfo inputCulture, CultureInfo outputCulture, string query, ICommand copyCommand, bool hideOnCopy)
    {
        var decimalResult = roundedResult?.ToString(outputCulture);

        List<IContextItem> context = [];

        if (decimal.IsInteger((decimal)roundedResult))
        {
            context.Add(new Separator());

            var i = decimal.ToInt64((decimal)roundedResult);

            // hexadecimal
            try
            {
                var hexResult = "0x" + i.ToString("X", outputCulture);
                context.Add(new CommandContextItem(CreateCopyCommand(hexResult, Properties.Resources.calculator_copy_hex, hideOnCopy))
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
                var binaryResult = "0b" + i.ToString("B", outputCulture);
                context.Add(new CommandContextItem(CreateCopyCommand(binaryResult, Properties.Resources.calculator_copy_binary, hideOnCopy))
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
                var octalResult = "0o" + Convert.ToString(i, 8);
                context.Add(new CommandContextItem(CreateCopyCommand(octalResult, Properties.Resources.calculator_copy_octal, hideOnCopy))
                {
                    Title = octalResult,
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Error converting to octal format", ex);
            }
        }

        return new ListItem(copyCommand)
        {
            // Using CurrentCulture since this is user facing
            Title = decimalResult,
            Subtitle = query,
            TextToSuggest = decimalResult,
            MoreCommands = context.ToArray(),
        };
    }

    private static CopyTextCommand CreateCopyCommand(string text, string name, bool hideOnCopy)
    {
        var command = new CopyTextCommand(text)
        {
            Name = name,
            Result = CreateCopyCommandResult(hideOnCopy),
        };

        return command;
    }
}
