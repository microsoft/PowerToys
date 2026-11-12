// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Read-only text element (UIA ControlType <c>Text</c>, e.g. a WinUI <c>TextBlock</c>). The
/// rendered text is read via <c>winapp ui get-value</c>, which falls back to the UIA Name.
/// </summary>
public class TextBlock : Element
{
    public TextBlock()
    {
        TargetControlType = "Text";
    }

    /// <summary>The displayed text via <c>winapp ui get-value</c>.</summary>
    public string Text => GetValue();
}
