// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

public static partial class QueryHelper
{
    public static List<ListItem> Query(string query, SettingsManager settings, bool isFallbackSearch)
    {
        ArgumentNullException.ThrowIfNull(query);

        var replaceInput = settings.ReplaceInputIfQueryEndsWithEqualSign && query.EndsWith('=');
        CultureInfo inputCulture = settings.InputUseEnglishFormat ? new CultureInfo("en-us") : CultureInfo.CurrentCulture;
        CultureInfo outputCulture = settings.OutputUseEnglishFormat ? new CultureInfo("en-us") : CultureInfo.CurrentCulture;

        // Happens if the user has only typed the action key so far
        if (string.IsNullOrEmpty(query))
        {
            return new List<ListItem>();
        }

        NumberTranslator translator = NumberTranslator.Create(inputCulture, new CultureInfo("en-US"));
        var input = translator.Translate(query.Normalize(NormalizationForm.FormKC));

        if (replaceInput)
        {
            input = input[..^1];
        }

        if (!CalculateHelper.InputValid(input))
        {
            return new List<ListItem>();
        }

        try
        {
            // Using CurrentUICulture since this is user facing
            var result = CalculateEngine.Interpret(settings, input, outputCulture, out var errorMessage);

            // This could happen for some incorrect queries, like pi(2)
            if (result.Equals(default(CalculateResult)))
            {
                // If errorMessage is not default then do error handling
                return errorMessage == default ? new List<ListItem>() : ErrorHandler.OnError(isFallbackSearch, query, errorMessage);
            }
            else if (replaceInput)
            {
                // TODO: need to implement a way to replace the input in the search box
                return new List<ListItem>();
            }

            return new List<ListItem>
            {
                ResultHelper.CreateResult(result.RoundedResult, inputCulture, outputCulture),
            };
        }
        catch (Mages.Core.ParseException)
        {
            // Invalid input
            return ErrorHandler.OnError(isFallbackSearch, query, Properties.Resources.calculator_expression_not_complete);
        }
        catch (OverflowException)
        {
            // Result to big to convert to decimal
            return ErrorHandler.OnError(isFallbackSearch, query, Properties.Resources.calculator_not_covert_to_decimal);
        }
        catch (Exception e)
        {
            // Any other crash occurred
            // We want to keep the process alive if any the mages library throws any exceptions.
            return ErrorHandler.OnError(isFallbackSearch, query, default, e);
        }
    }
}
