// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.Calc;

public partial class CalculatorTopLevelListItem : CommandItem, IFallbackHandler
{
    public CalculatorTopLevelListItem()
        : base(new CalculatorCopyCommand())
    {
        // In the case of the calculator, the ListItem itself is the fallback
        // handler so that it can update its Title and Subtitle accordingly.
        SetDefaultTitle();
    }

    public void UpdateQuery(string query)
    {
        if (string.IsNullOrEmpty(query) || query == "=")
        {
            SetDefaultTitle();
        }
        else if (query.StartsWith('='))
        {
            Title = ParseQuery(query);
            Subtitle = query;
        }
        else
        {
            Title = string.Empty;
        }
    }

    private void SetDefaultTitle()
    {
        Title = "=";
        Subtitle = "Type an equation";
    }

    private string ParseQuery(string query)
    {
        var equation = query.Substring(1);
        try
        {
            var result = new DataTable().Compute(equation, null);
            var resultString = result.ToString() ?? string.Empty;
            ((CalculatorCopyCommand)Command).SetResult(resultString, true);

            return resultString;
        }
        catch (Exception e)
        {
            ((CalculatorCopyCommand)Command).SetResult(string.Empty, false);

            return $"Error: {e.Message}";
        }
    }
}
