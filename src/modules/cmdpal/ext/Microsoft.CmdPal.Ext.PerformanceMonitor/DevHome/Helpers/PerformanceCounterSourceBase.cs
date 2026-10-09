// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.CmdPal.Common;

namespace CoreWidgetProvider.Helpers;

internal abstract class PerformanceCounterSourceBase
{
    protected PerformanceCounter? CreatePerformanceCounter(string categoryName, string counterName, string instanceName = "", bool readOnly = true, bool logFailure = true)
    {
        try
        {
            return new PerformanceCounter(categoryName, counterName, instanceName, readOnly);
        }
        catch (Exception ex)
        {
            if (logFailure)
            {
                var suffix = string.IsNullOrEmpty(instanceName) ? string.Empty : $@"\{instanceName}";
                CoreLogger.LogError($@"Failed to initialize performance counter '{categoryName}\{counterName}{suffix}'.", ex);
            }

            return null;
        }
    }

    protected PerformanceCounterCategory? CreatePerformanceCounterCategory(string categoryName, bool logFailure = true)
    {
        try
        {
            if (!PerformanceCounterCategory.Exists(categoryName))
            {
                if (logFailure)
                {
                    CoreLogger.LogError($@"Performance counter category '{categoryName}' does not exist on this system.");
                }

                return null;
            }

            return new PerformanceCounterCategory(categoryName);
        }
        catch (Exception ex)
        {
            if (logFailure)
            {
                CoreLogger.LogError($@"Failed to initialize performance counter category '{categoryName}'.", ex);
            }

            return null;
        }
    }

    protected void LogFailureOnce(ref bool hasLoggedFailure, string message, Exception ex)
    {
        if (!hasLoggedFailure)
        {
            hasLoggedFailure = true;
            CoreLogger.LogError(message, ex);
        }
    }
}
