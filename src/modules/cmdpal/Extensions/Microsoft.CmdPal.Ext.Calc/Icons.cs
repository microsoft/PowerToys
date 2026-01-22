// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Calc;

internal sealed class Icons
{
    internal static IconInfo CalculatorIcon => IconHelpers.FromRelativePath("Assets\\Calculator.svg");

    internal static IconInfo ResultIcon => new("\uE94E"); // CalculatorEqualTo icon

    internal static IconInfo SaveIcon => new("\uE74E"); // Save icon

    internal static IconInfo ErrorIcon => new("\uE783"); // Error icon
}
