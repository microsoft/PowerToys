// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.WinUI;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Rectangle = Microsoft.UI.Xaml.Shapes.Rectangle;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Attached property to color internal caret/overlay rectangles inside a TextBox
/// so they follow the TextBox's actual Foreground brush.
/// </summary>
public static class TextBoxCaretColor
{
    public static readonly DependencyProperty SyncWithForegroundProperty =
        DependencyProperty.RegisterAttached("SyncWithForeground", typeof(bool), typeof(TextBoxCaretColor), new PropertyMetadata(false, OnSyncCaretRectanglesChanged))!;

    private static readonly ConditionalWeakTable<TextBox, State> States = [];

    public static void SetSyncWithForeground(DependencyObject obj, bool value)
    {
        obj.SetValue(SyncWithForegroundProperty, value);
    }

    public static bool GetSyncWithForeground(DependencyObject obj)
    {
        return (bool)obj.GetValue(SyncWithForegroundProperty);
    }

    private static void OnSyncCaretRectanglesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox tb)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            Attach(tb);
        }
        else
        {
            Detach(tb);
        }
    }

    private static void Attach(TextBox tb)
    {
        if (States.TryGetValue(tb, out var st) && st.IsHooked)
        {
            return;
        }

        st ??= new State();
        st.IsHooked = true;
        States.Remove(tb);
        States.Add(tb, st);

        tb.Loaded += TbOnLoaded;
        tb.Unloaded += TbOnUnloaded;
        tb.GotFocus += TbOnGotFocus;

        st.ForegroundToken = tb.RegisterPropertyChangedCallback(Control.ForegroundProperty!, (_, _) => Apply(tb));

        if (tb.IsLoaded)
        {
            Apply(tb);
        }
    }

    private static void Detach(TextBox tb)
    {
        if (!States.TryGetValue(tb, out var st))
        {
            return;
        }

        tb.Loaded -= TbOnLoaded;
        tb.Unloaded -= TbOnUnloaded;
        tb.GotFocus -= TbOnGotFocus;

        if (st.ForegroundToken != 0)
        {
            tb.UnregisterPropertyChangedCallback(Control.ForegroundProperty!, st.ForegroundToken);
            st.ForegroundToken = 0;
        }

        st.IsHooked = false;
    }

    private static void TbOnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            Apply(tb);
        }
    }

    private static void TbOnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            Detach(tb);
        }
    }

    private static void TbOnGotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            Apply(tb);
        }
    }

    private static void Apply(TextBox tb)
    {
        try
        {
            ApplyCore(tb);
        }
        catch (COMException)
        {
            // ignore
        }
    }

    private static void ApplyCore(TextBox tb)
    {
        // Ensure template is realized
        tb.ApplyTemplate();

        // Find the internal ScrollContentPresenter within the TextBox template
        var scp = tb.FindDescendant<ScrollContentPresenter>(s => s.Name == "ScrollContentPresenter");
        if (scp is null)
        {
            return;
        }

        var brush = tb.Foreground; // use the actual current foreground brush
        if (brush == null)
        {
            brush = new SolidColorBrush(Colors.Black);
        }

        foreach (var rect in scp.FindDescendants().OfType<Rectangle>())
        {
            try
            {
                rect.Fill = brush;
                rect.CompositeMode = ElementCompositeMode.SourceOver;
                rect.Opacity = 0.9;
            }
            catch
            {
                // best-effort; some rectangles might be template-owned
            }
        }
    }

    private sealed class State
    {
        public long ForegroundToken { get; set; }

        public bool IsHooked { get; set; }
    }
}
