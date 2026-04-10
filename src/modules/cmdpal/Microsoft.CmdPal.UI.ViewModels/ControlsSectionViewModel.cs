// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

public class ControlsSectionViewModel
{
    public string Title { get; }

    public List<ControlItemViewModel> Items { get; }

    public ControlsSectionViewModel(string title, List<ControlItemViewModel> items)
    {
        Title = title;
        Items = items;
    }
}
