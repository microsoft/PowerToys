// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Awake.Core.Models;

namespace Awake.Core;

internal static class AwakeStateCalculator
{
    internal static ExecutionState ComputeAwakeState(bool keepDisplayOn, bool isScreenLocked)
    {
        return keepDisplayOn && !isScreenLocked
            ? ExecutionState.ES_SYSTEM_REQUIRED | ExecutionState.ES_DISPLAY_REQUIRED | ExecutionState.ES_CONTINUOUS
            : ExecutionState.ES_SYSTEM_REQUIRED | ExecutionState.ES_CONTINUOUS;
    }
}
