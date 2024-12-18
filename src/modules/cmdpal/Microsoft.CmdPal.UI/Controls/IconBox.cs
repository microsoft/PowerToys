// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Controls;

/// <summary>
/// A helper control which takes an <see cref="IconSource"/> and creates the corresponding <see cref="IconElement"/>.
/// </summary>
public partial class IconBox : ContentControl
{
    private readonly DispatcherQueue _queue = DispatcherQueue.GetForCurrentThread();

    /// <summary>
    /// Gets or sets the <see cref="IconSource"/> to display within the <see cref="IconBox"/>. Overwritten, if <see cref="SourceKey"/> is used instead.
    /// </summary>
    public IconSource? Source
    {
        get => (IconSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    // Using a DependencyProperty as the backing store for Source.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(IconSource), typeof(IconBox), new PropertyMetadata(null, OnSourcePropertyChanged));

    /// <summary>
    /// Gets or sets a value to use as the <see cref="SourceKey"/> to retrieve an <see cref="IconSource"/> to set as the <see cref="Source"/>.
    /// </summary>
    public object? SourceKey
    {
        get => (object?)GetValue(SourceKeyProperty);
        set => SetValue(SourceKeyProperty, value);
    }

    // Using a DependencyProperty as the backing store for SourceKey.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty SourceKeyProperty =
        DependencyProperty.Register(nameof(SourceKey), typeof(object), typeof(IconBox), new PropertyMetadata(null, OnSourceKeyPropertyChanged));

    /// <summary>
    /// Gets or sets the <see cref="SourceRequested"/> event handler to provide the value of the <see cref="IconSource"/> for the <see cref="Source"/> property from the provided <see cref="SourceKey"/>.
    /// </summary>
    public event TypedEventHandler<IconBox, SourceRequestedEventArgs>? SourceRequested;

    private static void OnSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is IconBox @this)
        {
            switch (e.NewValue)
            {
                case null:
                    @this.Content = null;
                    break;
                case FontIconSource fontIco:
                    fontIco.FontSize = @this.Width;

                    // For inexplicable reasons, FontIconSource.CreateIconElement
                    // doesn't work, so do it ourselves
                    // TODO: File platform bug?
                    IconSourceElement elem = new()
                    {
                        IconSource = fontIco,
                    };
                    @this.Content = elem;
                    break;
                case IconSource source:
                    @this.Content = source.CreateIconElement();
                    break;
                default:
                    throw new InvalidOperationException($"New value of {e.NewValue} is not of type IconSource.");
            }
        }
    }

    private static void OnSourceKeyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is IconBox @this)
        {
            if (e.NewValue == null)
            {
                @this.Source = null;
            }
            else
            {
                // TODO GH #239 switch back when using the new MD text block
                // _ = @this._queue.EnqueueAsync(() =>
                @this._queue.TryEnqueue(new(() =>
                {
                    var eventArgs = new SourceRequestedEventArgs(e.NewValue);

                    if (@this.SourceRequested != null)
                    {
                        @this.SourceRequested.Invoke(@this, eventArgs);

                        @this.Source = eventArgs.Value;
                    }
                }));
            }
        }
    }
}
