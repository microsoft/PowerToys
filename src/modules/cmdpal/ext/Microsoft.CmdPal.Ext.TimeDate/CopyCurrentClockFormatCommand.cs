// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TimeDate;

internal sealed partial class CopyCurrentClockFormatCommand : CopyTextCommand
{
    private readonly Func<string> _getCurrentText;

    internal CopyCurrentClockFormatCommand(string name, Func<string> getCurrentText)
        : base(string.Empty)
    {
        Name = name;
        _getCurrentText = getCurrentText;
    }

    internal string GetCurrentText() => _getCurrentText();

    public override ICommandResult Invoke()
    {
        Text = GetCurrentText();
        return base.Invoke();
    }
}
