// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Windows.CommandPalette.Extensions;
using Microsoft.Windows.CommandPalette.Extensions.Helpers;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Calculator;

public class CalculatorAction : InvokableCommand
{
    private bool _success;
    private string _result;

    public CalculatorAction()
    {
        Icon = new("\ue8ef");
    }

    public override ICommandResult Invoke()
    {
        if (_success)
        {
            ClipboardHelper.SetText(_result);
        }

        return ActionResult.KeepOpen();
    }

    internal void SetResult(string result, bool success)
    {
        _result = result;
        _success = success;
        if (success)
        {
            Name = "Copy";
        }
        else
        {
            Name = string.Empty;
        }
    }
}
