// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.WinUI;
using Microsoft.CmdPal.UI.Deferred;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Controls;

/// <summary>
/// A helper control which takes an <see cref="IconSource"/> and creates the corresponding <see cref="IconElement"/>.
/// </summary>
public partial class ContentIcon : FontIcon
{
    public UIElement Content
    {
        get => (UIElement)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(
            nameof(Content),
            typeof(UIElement),
            typeof(ContentIcon),
            new PropertyMetadata(null));

    public ContentIcon()
    {
        Loaded += IconBoxElement_Loaded;
    }

    private void IconBoxElement_Loaded(object sender, RoutedEventArgs e)
    {
        if (this.FindDescendants().OfType<Grid>().FirstOrDefault() is Grid grid && Content is not null)
        {
            grid.Children.Add(Content);
        }
    }
}
