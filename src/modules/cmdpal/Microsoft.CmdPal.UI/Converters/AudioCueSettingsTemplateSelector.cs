// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI;

internal sealed partial class AudioCueSettingsTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Volume { get; set; }

    public DataTemplate? Effect { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) =>
        item is AudioCueEffectSettingsViewModel ? Effect : Volume;

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) =>
        SelectTemplateCore(item);
}
