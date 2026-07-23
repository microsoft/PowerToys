// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace PowerDisplay.Models;

/// <summary>
/// Describes the post-adjustment values produced by one complete tray wheel action.
/// </summary>
/// <param name="Mode">The target scope used for the adjustment.</param>
/// <param name="BrightnessValues">Post-clamp brightness values in target enumeration order.</param>
public sealed record TrayWheelAdjustmentFeedback(
    MouseWheelControlMode Mode,
    IReadOnlyList<int> BrightnessValues);
