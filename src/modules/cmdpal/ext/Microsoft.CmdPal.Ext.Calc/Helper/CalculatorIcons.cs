// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

public static class CalculatorIcons
{
    public static IconInfo ResultIcon => new("\uE94E");

    public static IconInfo SaveIcon => new("\uE74E");

    public static IconInfo ErrorIcon => new("\uE783");

    public static IconInfo ProviderIcon => IconHelpers.FromRelativePath("Assets\\Calculator.svg");
}
