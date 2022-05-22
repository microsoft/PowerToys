// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.Calculator
{
    internal class ErrorHandler
    {
        internal static List<Result> OnError(string icon, bool isGlobalQuery, string queryInput, string errorMessage, Exception exception = default)
        {
            string userMessage;

            if (errorMessage != default)
            {
                Log.Error($"Error when query for <{queryInput}>: {errorMessage}", typeof(Calculator.Main));
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
