// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Rendering.WinUI3;
using Microsoft.UI.Xaml;

#pragma warning disable SA1402 // File may only contain a single type

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards;

internal sealed partial class AdaptiveCustomInputValue : IAdaptiveInputValue
{
    private readonly IAdaptiveCustomInputControl _control;

    public AdaptiveCustomInputValue(IAdaptiveInputElement element, IAdaptiveCustomInputControl control)
    {
        InputElement = element;
        _control = control;
        ErrorMessage = control.ValidationErrorElement;
    }

    public bool Validate() => _control.ValidateInput();

    public void SetFocus() => _control.FocusInput();

    public UIElement? ErrorMessage { get; set; }

    public IAdaptiveInputElement InputElement { get; set; }

    public string CurrentValue => _control.CurrentValue;
}

internal interface IAdaptiveCustomInputControl
{
    string CurrentValue { get; }

    UIElement ValidationErrorElement { get; }

    bool ValidateInput();

    void FocusInput();
}

#pragma warning restore SA1402 // File may only contain a single type
