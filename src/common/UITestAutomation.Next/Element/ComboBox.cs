// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// WinUI/WPF <c>ComboBox</c> (UIA ControlType <c>ComboBox</c>). Selection is driven CLI-first:
/// <see cref="Select"/> expands via <c>winapp ui invoke</c> then clicks the chosen item, while
/// editable combo boxes can be set directly with <see cref="SelectByText"/>
/// (<c>winapp ui set-value</c>).
/// </summary>
/// <remarks>
/// The dropdown items live in a popup that the owning process surfaces as a separate window
/// (e.g. Settings' <c>PopupHost</c>). Process-scoped sessions (<see cref="Session.FromProcess"/>)
/// see those items because every search re-resolves via <c>-a</c>; a window-scoped (<c>-w</c>)
/// session may not, in which case prefer <see cref="SelectByText"/>.
/// </remarks>
public class ComboBox : Element
{
    public ComboBox()
    {
        TargetControlType = "ComboBox";
    }

    /// <summary>Currently selected item text via <c>winapp ui get-value</c> (SelectionPattern fallback).</summary>
    public string SelectedText => GetValue();

    /// <summary>
    /// Expand the combo box (CLI <c>invoke</c> toggles ExpandCollapse) and click the item whose
    /// Name matches <paramref name="itemName"/>.
    /// </summary>
    public ComboBox Select(string itemName, int timeoutMS = 5000)
    {
        EnsureBound();
        Click();
        Thread.Sleep(150);
        Owner!.Find<Element>(By.Name(itemName), timeoutMS).Click();
        return this;
    }

    /// <summary>
    /// Set the combo box value directly via <c>winapp ui set-value</c> (UIA ValuePattern). Works
    /// for editable combo boxes; for non-editable combos use <see cref="Select"/>.
    /// </summary>
    public ComboBox SelectByText(string text)
    {
        EnsureBound();
        WinappCli.InvokeAssertSuccess("ui", "set-value", Selector, text, Owner!.TargetFlag, Owner!.TargetValue);
        return this;
    }
}
