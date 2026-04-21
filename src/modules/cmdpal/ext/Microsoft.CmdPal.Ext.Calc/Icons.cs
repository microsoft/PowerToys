// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Calc;

internal static class Icons
{
    internal static IconInfo CalculatorIcon => IconHelpers.FromRelativePath("Assets\\Calculator.svg");

    internal static IconInfo ResultIcon => new("\uE94E"); // CalculatorEqualTo icon

    internal static IconInfo SaveIcon => new("\uE74E"); // Save icon

    internal static IconInfo DeleteIcon => new("\uE74D"); // Delete icon

    internal static IconInfo HistoryIcon => new("\uE81C"); // History icon

    internal static IconInfo PasteIcon => new("\uE77F"); // Paste icon

    internal static IconInfo ErrorIcon => new("\uE783"); // Error icon
}
