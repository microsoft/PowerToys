// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using EnvironmentVariablesUILib.Models;

namespace EnvironmentVariablesUILib.Telemetry
{
    public interface ITelemetry
    {
        abstract void LogEnvironmentVariablesProfileEnabledEvent(bool enabled);

        abstract void LogEnvironmentVariablesVariableChangedEvent(VariablesSetType type);
    }
}
