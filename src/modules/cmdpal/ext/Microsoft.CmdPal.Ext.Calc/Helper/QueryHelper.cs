// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

public static partial class QueryHelper
{
    public static ListItem Query(string query, ISettingsInterface settings, bool isFallbackSearch, TypedEventHandler<object, object> handleSave = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!isFallbackSearch)
        {
            ArgumentNullException.ThrowIfNull(handleSave);
        }

        CultureInfo inputCulture = settings.InputUseEnglishFormat ? new CultureInfo("en-us") : CultureInfo.CurrentCulture;
        CultureInfo outputCulture = settings.OutputUseEnglishFormat ? new CultureInfo("en-us") : CultureInfo.CurrentCulture;

        // In case the user pastes a query with a leading =
        query = query.TrimStart('=');

        // Happens if the user has only typed the action key so far
        if (string.IsNullOrEmpty(query))
        {
            return null;
        }

        NumberTranslator translator = NumberTranslator.Create(inputCulture, new CultureInfo("en-US"));
        var input = translator.Translate(query.Normalize(NormalizationForm.FormKC));

        if (string.IsNullOrWhiteSpace(input))
        {
            return ErrorHandler.OnError(isFallbackSearch, query, Properties.Resources.calculator_expression_empty);
        }

        if (!CalculateHelper.InputValid(input))
        {
            return null;
        }

        try
        {
            // Using CurrentUICulture since this is user facing
            var result = CalculateEngine.Interpret(settings, input, outputCulture, out var errorMessage);

            // This could happen for some incorrect queries, like pi(2)
            if (result.Equals(default(CalculateResult)))
            {
                // If errorMessage is not default then do error handling
                return errorMessage == default ? null : ErrorHandler.OnError(isFallbackSearch, query, errorMessage);
            }

            if (isFallbackSearch)
            {
                // Fallback search
                return ResultHelper.CreateResult(result.RoundedResult, inputCulture, outputCulture, query);
            }

            return ResultHelper.CreateResult(result.RoundedResult, inputCulture, outputCulture, query, settings, handleSave);
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
