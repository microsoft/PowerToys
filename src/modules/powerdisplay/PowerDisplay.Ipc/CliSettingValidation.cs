// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace PowerDisplay.Ipc;

/// <summary>
/// Shared validation rules for CLI setting values, so the <c>set</c> command and the
/// <c>apply-profile</c> outcomes path validate identically and cannot drift.
/// </summary>
internal static class CliSettingValidation
{
    /// <summary>
    /// Returns whether a resolved discrete VCP value is acceptable for a monitor: it must be in the
    /// monitor's advertised supported set when one is known. A null/empty set means the monitor did
    /// not advertise its values, so the value is accepted (the hardware write is the final arbiter).
    /// </summary>
    public static bool IsDiscreteValueSupported(int value, IReadOnlyList<int>? supportedValues)
        => supportedValues is not { Count: > 0 } || supportedValues.Contains(value);
}
