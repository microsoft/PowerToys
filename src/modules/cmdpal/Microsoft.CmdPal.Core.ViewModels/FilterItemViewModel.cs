// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Core.ViewModels;

public partial class FilterItemViewModel : IFilterItemViewModel
{
    public string Id { get; set; }

    public string Name { get; set; }

    public IconInfoViewModel Icon { get; set; }

    public bool IsSelected { get; set; }

    public FilterItemViewModel(IFilter filter)
    {
        Id = filter.Id;
        Name = filter.Name;
        Icon = new(filter.Icon);
    }
}
