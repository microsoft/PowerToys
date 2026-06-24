// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>Edit / TextBox control. Drives via <c>winapp ui set-value</c> and <c>get-value</c>.</summary>
public class TextBox : Element
{
    public TextBox()
    {
        TargetControlType = "Edit";
    }

    /// <summary>Set the textbox content via winappcli's <c>set-value</c> (UIA ValuePattern).</summary>
    public TextBox SetText(string value)
    {
        EnsureBound();
        WinappCli.InvokeAssertSuccess("ui", "set-value", Selector, value, Owner!.TargetFlag, Owner!.TargetValue);
        return this;
    }

    /// <summary>Current text content via <c>winapp ui get-value</c>.</summary>
    public string Value
    {
        get
        {
            EnsureBound();
            var r = WinappCli.Invoke("ui", "get-value", Selector, Owner!.TargetFlag, Owner!.TargetValue, "--json");
            if (!r.Success)
            {
                return string.Empty;
            }

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(r.StdOut);
                return doc.RootElement.TryGetProperty("text", out var t) ? (t.GetString() ?? string.Empty) : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
