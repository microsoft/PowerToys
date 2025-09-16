// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;

namespace TopToolbar.Models;

public class ButtonGroup
{
    public string Id { get; set; } = System.Guid.NewGuid().ToString();

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; }

    public bool IsEnabled { get; set; } = true;

    public ObservableCollection<ToolbarButton> Buttons { get; set; } = new();
}
