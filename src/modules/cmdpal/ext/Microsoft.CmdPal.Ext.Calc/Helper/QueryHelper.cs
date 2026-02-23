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
    public static ListItem Query(
        string query,
        ISettingsInterface settings,
        bool isFallbackSearch,
        out string displayQuery,
        TypedEventHandler<object, object> handleReplace = null)
    {
        ArgumentNullException.ThrowIfNull(query);

        CultureInfo inputCulture =
            settings.InputUseEnglishFormat ? new CultureInfo("en-us") : CultureInfo.CurrentCulture;
        CultureInfo outputCulture =
            settings.OutputUseEnglishFormat ? new CultureInfo("en-us") : CultureInfo.CurrentCulture;

        // In case the user pastes a query with a leading =
        query = query.TrimStart('=').TrimStart();

        // Enables better looking characters for multiplication and division (e.g., '×' and '÷')
        displayQuery = CalculateHelper.NormalizeCharsForDisplayQuery(query);

        // Happens if the user has only typed the action key so far
        if (string.IsNullOrEmpty(displayQuery))
        {
            return null;
        }

        // Normalize query to engine format (e.g., replace '×' with '*', converts superscripts to functions)
        // This must be done before any further normalization to avoid losing information
        var engineQuery = CalculateHelper.NormalizeCharsToEngine(displayQuery);

        // Cleanup rest of the Unicode characters, whitespace
        var queryForEngine2 = engineQuery.Normalize(NormalizationForm.FormKC);

        // Translate numbers from input culture to en-US culture for the calculation engine
        var translator = NumberTranslator.Create(inputCulture, new CultureInfo("en-US"));

        // Translate the input query
        var input = translator.Translate(queryForEngine2);

        if (string.IsNullOrWhiteSpace(input))
        {
            return ErrorHandler.OnError(isFallbackSearch, query, Properties.Resources.calculator_expression_empty);
        }

        // normalize again to engine chars after translation
        input = CalculateHelper.NormalizeCharsToEngine(input);

        // Auto fix incomplete queries (if enabled)
        if (settings.AutoFixQuery && TryGetIncompleteQuery(input, out var newInput))
        {
            input = newInput;
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

            return isFallbackSearch
                ? ResultHelper.CreateResultForFallback(result.RoundedResult, inputCulture, outputCulture, displayQuery)
                : ResultHelper.CreateResultForPage(result.RoundedResult, inputCulture, outputCulture, displayQuery, settings, handleReplace);
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

    public static bool TryGetIncompleteQuery(string query, out string newQuery)
    {
        newQuery = query;

        var trimmed = query.TrimEnd();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        // 1. Trim trailing operators
        var operators = CalculateHelper.GetQueryOperators();
        while (trimmed.Length > 0 && Array.IndexOf(operators, trimmed[^1]) > -1)
        {
            trimmed = trimmed[..^1].TrimEnd();
        }

        if (trimmed.Length == 0)
        {
            return false;
        }

        // 2. Fix brackets
        newQuery = BracketHelper.BalanceBrackets(trimmed);

        return true;
    }
}
