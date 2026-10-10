// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Input;

namespace PowerDisplay.ViewModels;

public sealed class RefreshRateOption
{
    public RefreshRateOption(int rate, bool isSelected, ICommand selectCommand)
    {
        Rate = rate;
        DisplayText = $"{rate} Hz";
        IsSelected = isSelected;
        SelectCommand = selectCommand;
    }

    public int Rate { get; }

    public string DisplayText { get; }

    public bool IsSelected { get; }

    public ICommand SelectCommand { get; }
}
