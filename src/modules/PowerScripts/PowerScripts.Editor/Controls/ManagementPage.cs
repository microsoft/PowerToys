// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PowerScripts.Editor.Controls;

public sealed partial class ManagementPage : ContentControl
{
    public static readonly DependencyProperty ModuleTitleProperty = DependencyProperty.Register(
        nameof(ModuleTitle), typeof(string), typeof(ManagementPage), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ModuleDescriptionProperty = DependencyProperty.Register(
        nameof(ModuleDescription), typeof(string), typeof(ManagementPage), new PropertyMetadata(string.Empty));

    public string ModuleTitle
    {
        get => (string)GetValue(ModuleTitleProperty);
        set => SetValue(ModuleTitleProperty, value);
    }

    public string ModuleDescription
    {
        get => (string)GetValue(ModuleDescriptionProperty);
        set => SetValue(ModuleDescriptionProperty, value);
    }

    public object ModuleContent
    {
        get => Content;
        set => Content = value;
    }
}
