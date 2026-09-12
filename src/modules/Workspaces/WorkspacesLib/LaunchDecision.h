// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

enum class LaunchDecision
{
    Approved,
    Skipped,
    Canceled,
    UiUnavailable,
    TimedOut,
    InvalidResponse,
    TargetChanged,
};
