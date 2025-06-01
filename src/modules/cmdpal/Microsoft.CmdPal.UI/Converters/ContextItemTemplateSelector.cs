﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI;

internal sealed partial class ContextItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Default { get; set; }

    public DataTemplate? Critical { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        return ((CommandContextItemViewModel)item).IsCritical ? Critical : Default;
    }
}
