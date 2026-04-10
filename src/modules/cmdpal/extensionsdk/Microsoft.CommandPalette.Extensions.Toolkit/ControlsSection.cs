// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class ControlsSection : IControlsSection
{
    public string Title { get; set; } = string.Empty;

    private IControlItem[] _items = [];

    public ControlsSection()
    {
    }

    public ControlsSection(string title, IControlItem[] items)
    {
        Title = title;
        _items = items;
    }

    public IControlItem[] GetItems() => _items;
}
