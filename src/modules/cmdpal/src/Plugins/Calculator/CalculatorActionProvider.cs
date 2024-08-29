// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Windows.CommandPalette.Extensions;
using Microsoft.Windows.CommandPalette.Extensions.Helpers;
using Windows.Foundation;
using System.Data;
using System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Calculator;

public class CalculatorAction : InvokableCommand
{
    private bool success;
    private string result;

    public CalculatorAction()
    {
        Icon = new("\ue8ef");
    }
    public override ICommandResult Invoke()
    {
        if (success)
        {
            ClipboardHelper.SetText(result);
        }

        return ActionResult.KeepOpen();
    }
    internal void SetResult(string result, bool success)
    {
        this.result = result;
        this.success = success;
        if (success)
        {
            this.Name = "Copy";
        }
        else
        {
            this.Name = "";
        }
    }
}

public class CalculatorTopLevelListItem : ListItem, IFallbackHandler
{
    public CalculatorTopLevelListItem() : base(new CalculatorAction())
    {
        // In the case of the calculator, the ListItem itself is the fallback
        // handler, so that it can update it's Title and Subtitle accodingly.
        this._FallbackHandler = this;
        this.Subtitle = "Type an equation";
    }

    public void UpdateQuery(string query) {
        if (query == "")
        {
            this.Title = "=";
        }
        else if (query == "=")
        {
            this.Title = "=";
        }
        else if (query.StartsWith('='))
        {
            this.Title = _ParseQuery(query);
            this.Subtitle = query;
        }
        else this.Title = "";
    }

    private string _ParseQuery(string query)
    {
        var equation = query.Substring(1);
        try
        {
            var result = new DataTable().Compute(equation, null);
            var resultString = result.ToString();
            ((CalculatorAction)this.Command).SetResult(resultString, true);
            return result.ToString();
        }
        catch (Exception e)
        {
            ((CalculatorAction)this.Command).SetResult("", false);
            return $"Error: {e.Message}";
        }
    }
}
public class CalculatorActionProvider : ICommandProvider
{
    public string DisplayName => $"Calculator";

    private readonly CalculatorTopLevelListItem calculatorCommand = new();

    public CalculatorActionProvider()
    {
    }

    public IconDataType Icon => new("");

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public void Dispose() => throw new NotImplementedException();
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize

    public IListItem[] TopLevelCommands()
    {
        return [ calculatorCommand ];
    }

}
