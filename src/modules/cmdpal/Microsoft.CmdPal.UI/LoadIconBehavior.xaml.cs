// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.UI.ExtViews;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Xaml.Interactivity;

namespace Microsoft.CmdPal.UI;

public partial class LoadIconBehavior : DependencyObject, IBehavior
{
    private static readonly IconCacheService IconService = new(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());

    public IconDataType? Source
    {
        get => (IconDataType?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    // Using a DependencyProperty as the backing store for Source.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(IconDataType), typeof(LoadIconBehavior), new PropertyMetadata(null, OnSourcePropertyChanged));

    public DependencyObject? AssociatedObject { get; private set; }

    public void Attach(DependencyObject associatedObject) => AssociatedObject = associatedObject;

    public void Detach() => AssociatedObject = null;

    private static async void OnSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoadIconBehavior @this
            && @this.AssociatedObject is Border border)
        {
            var icoSource = await IconService.GetIconSource(@this.Source ?? new(string.Empty));

            /* This causes a catastrophic failure...
            if (border.Child != null)
            {
                VisualTreeHelper.DisconnectChildrenRecursive(border.Child);
                border.Child = null;
            }*/

            if (icoSource is FontIconSource fontIco)
            {
                fontIco.FontSize = border.Width;

                // For inexplicable reasons, FontIconSource.CreateIconElement
                // doesn't work, so do it ourselves
                IconSourceElement elem = new()
                {
                    IconSource = fontIco,
                };
                border.Child = elem;
            }
            else
            {
                var icoElement = icoSource?.CreateIconElement();
                border.Child = icoElement;
            }
        }
    }
}
