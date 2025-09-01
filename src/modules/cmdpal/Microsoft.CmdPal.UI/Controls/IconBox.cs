// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.UI.Deferred;
using Microsoft.Terminal.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

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

    public IconBox()
    {
        TabFocusNavigation = KeyboardNavigationMode.Once;
        IsTabStop = false;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
    }

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
                    fontIco.FontSize = double.IsNaN(@this.Width) ? @this.Height : @this.Width;

                    // For inexplicable reasons, FontIconSource.CreateIconElement
                    // doesn't work, so do it ourselves
                    // TODO: File platform bug?
                    IconSourceElement elem = new()
                    {
                        IconSource = fontIco,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
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
            if (e.NewValue is null)
            {
                @this.Source = null;
            }
            else
            {
                // TODO GH #239 switch back when using the new MD text block
                // Switching back to EnqueueAsync has broken icons in tags (they don't show)
                // _ = @this._queue.EnqueueAsync(() =>
                @this._queue.TryEnqueue(async void () =>
                {
                    try
                    {
                        if (@this.SourceRequested is null)
                        {
                            return;
                        }

                        var requestedTheme = @this.ActualTheme;
                        var eventArgs = new SourceRequestedEventArgs(e.NewValue, requestedTheme);

                        await @this.SourceRequested.InvokeAsync(@this, eventArgs);

                        // After the await:
                        // Is the icon we're looking up now, the one we still
                        // want to find? Since this IconBox might be used in a
                        // list virtualization situation, it's very possible we
                        // may have already been set to a new icon before we
                        // even got back from the await.
                        if (eventArgs.Key != @this.SourceKey)
                        {
                            // If the requested icon has changed, then just bail
                            return;
                        }

                        @this.Source = eventArgs.Value;

                        // Here's a little lesson in trickery:
                        // Emoji are rendered just a bit bigger than Segoe Icons.
                        // Just enough bigger that they get clipped if you put
                        // them in a box at the same size.
                        //
                        // So, if the icon we get back was a font icon,
                        // and the glyph for that icon is NOT in the range of
                        // Segoe icons, then let's give the icon some extra space
                        var iconData = eventArgs.Key switch
                        {
                            IconDataViewModel key => key,
                            IconInfoViewModel info => requestedTheme == ElementTheme.Light ? info.Light : info.Dark,
                            _ => null,
                        };

                        if (iconData?.Icon is not null && @this.Source is FontIconSource)
                        {
                            var iconSize =
                                !double.IsNaN(@this.Width) ? @this.Width :
                                !double.IsNaN(@this.Height) ? @this.Height :
                                @this.ActualWidth > 0 ? @this.ActualWidth :
                                @this.ActualHeight;

                            @this.Padding = new Thickness(Math.Round(iconSize * -0.2));
                        }
                        else
                        {
                            @this.Padding = default;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Exception from TryEnqueue bypasses the global error handler,
                        // and crashes the app.
                        Logger.LogError("Failed to set icon", ex);
                    }
                });
            }
        }
    }
}
