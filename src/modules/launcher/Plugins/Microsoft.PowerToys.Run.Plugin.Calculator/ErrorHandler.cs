// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.Calculator
{
    internal static class ErrorHandler
    {
        /// <summary>
        /// Method to handles errors while calculating
        /// </summary>
        /// <param name="icon">Path to result icon.</param>
        /// <param name="isGlobalQuery">Bool to indicate if it is a global query.</param>
        /// <param name="queryInput">User input as string including the action keyword.</param>
        /// <param name="errorMessage">Error message if applicable.</param>
        /// <param name="exception">Exception if applicable.</param>
        /// <returns>List of results to show. Either an error message or an empty list.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="errorMessage"/> and <paramref name="exception"/> are both filled with their default values.</exception>
        internal static List<Result> OnError(string icon, bool isGlobalQuery, string queryInput, string errorMessage, Exception exception = default)
        {
            string userMessage;

            if (errorMessage != default)
            {
                Log.Error($"Failed to calculate <{queryInput}>: {errorMessage}", typeof(Calculator.Main));
                userMessage = errorMessage;
            }
            else if (exception != default)
            {
                Log.Exception($"Exception when query for <{queryInput}>", exception, exception.GetType());
                userMessage = exception.Message;
            }
            else
            {
                throw new ArgumentException("The arguments error and exception have default values. One of them has to be filled with valid error data (error message/exception)!");
            }

            return isGlobalQuery ? new List<Result>() : new List<Result> { CreateErrorResult(userMessage, icon) };
        }

        private static Result CreateErrorResult(string errorMessage, string iconPath)
        {
            return new Result
            {
                Title = Properties.Resources.wox_plugin_calculator_calculation_failed,
                SubTitle = errorMessage,
                IcoPath = iconPath,
                Score = 300,
            };
        }
    }
}
