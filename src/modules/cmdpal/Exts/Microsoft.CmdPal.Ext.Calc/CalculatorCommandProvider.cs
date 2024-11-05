// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.Calc;

public partial class CalculatorCommandProvider : CommandProvider
{
    private readonly CalculatorTopLevelListItem calculatorCommand = new();

    public CalculatorCommandProvider()
    {
        DisplayName = "Calculator";
    }

    public override IListItem[] TopLevelCommands()
    {
        return [calculatorCommand];
    }
}
