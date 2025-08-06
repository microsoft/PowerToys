// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

internal static class ErrorHandler
{
    /// <summary>
    /// Method to handles errors while calculating
    /// </summary>
    /// <param name="isFallbackSearch">Bool to indicate if it is a fallback query.</param>
    /// <param name="queryInput">User input as string including the action keyword.</param>
    /// <param name="errorMessage">Error message if applicable.</param>
    /// <param name="exception">Exception if applicable.</param>
    /// <returns>List of results to show. Either an error message or an empty list.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="errorMessage"/> and <paramref name="exception"/> are both filled with their default values.</exception>
    internal static ListItem OnError(bool isFallbackSearch, string queryInput, string errorMessage, Exception exception = default)
    {
        string userMessage;

        if (errorMessage != default)
        {
            Logger.LogError($"Failed to calculate <{queryInput}>: {errorMessage}");
            userMessage = errorMessage;
        }
        else if (exception != default)
        {
            Logger.LogError($"Exception when query for <{queryInput}>", exception);
            userMessage = exception.Message;
        }
        else
        {
            throw new ArgumentException("The arguments error and exception have default values. One of them has to be filled with valid error data (error message/exception)!");
        }

        return isFallbackSearch ? null : CreateErrorResult(userMessage);
    }

    private static ListItem CreateErrorResult(string errorMessage)
    {
        return new ListItem(new NoOpCommand())
        {
            Title = Properties.Resources.calculator_calculation_failed_title,
            Subtitle = errorMessage,
            Icon = Icons.ErrorIcon,
        };
    }
}
