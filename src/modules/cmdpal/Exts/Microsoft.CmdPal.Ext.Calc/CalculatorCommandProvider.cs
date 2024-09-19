// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Extensions;

namespace Microsoft.CmdPal.Ext.Calc;

public partial class CalculatorCommandProvider : ICommandProvider
{
    public string DisplayName => $"Calculator";

    private readonly CalculatorTopLevelListItem calculatorCommand = new();

    public CalculatorCommandProvider()
    {
    }

    public IconDataType Icon => new(string.Empty);

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public void Dispose() => throw new NotImplementedException();
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize

    public IListItem[] TopLevelCommands()
    {
        return [calculatorCommand];
    }
}
