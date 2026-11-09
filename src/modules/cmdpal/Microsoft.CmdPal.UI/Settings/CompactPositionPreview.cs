// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class CompactPositionPreview : RangeBase
{
    private static readonly CompositeFormat PositionFormat = CompositeFormat.Parse(RS_.GetString("CompactPositionPickerWindow_PositionFormat"));

    public string PositionText => FormatPosition(Value);

    public CompactPositionPreview()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
    }

    internal bool HandleMouseWheel(int delta, bool horizontal)
    {
        if (horizontal || delta == 0)
        {
            return false;
        }

        Value = Math.Clamp(Value + (delta / 120.0 * LargeChange), Minimum, Maximum);
        return true;
    }

    internal bool HandleKey(VirtualKey key)
    {
        var value = key switch
        {
            VirtualKey.Up => Value + SmallChange,
            VirtualKey.Down => Value - SmallChange,
            VirtualKey.PageUp => Value + LargeChange,
            VirtualKey.PageDown => Value - LargeChange,
            VirtualKey.Home => Maximum,
            VirtualKey.End => Minimum,
            _ => (double?)null,
        };

        if (value is not { } position
            || IsModifierDown(VirtualKey.Control)
            || IsModifierDown(VirtualKey.Menu)
            || IsModifierDown(VirtualKey.LeftWindows)
            || IsModifierDown(VirtualKey.RightWindows))
        {
            return false;
        }

        Value = Math.Clamp(position, Minimum, Maximum);
        return true;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CompactPositionPreviewAutomationPeer(this);

    protected override void OnPointerEntered(PointerRoutedEventArgs e)
    {
        base.OnPointerEntered(e);
        VisualStateManager.GoToState(this, "PointerOver", true);
    }

    protected override void OnPointerExited(PointerRoutedEventArgs e)
    {
        base.OnPointerExited(e);
        VisualStateManager.GoToState(this, "Normal", true);
    }

    protected override void OnValueChanged(double oldValue, double newValue)
    {
        base.OnValueChanged(oldValue, newValue);

        var oldText = FormatPosition(oldValue);
        var newText = PositionText;
        if (oldText != newText && FrameworkElementAutomationPeer.FromElement(this) is { } peer)
        {
            peer.RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, oldText, newText);
        }
    }

    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e);

        if (!e.Handled)
        {
            e.Handled = HandleKey(e.Key);
        }
    }

    private static bool IsModifierDown(VirtualKey key) =>
        (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) != 0;

    private static string FormatPosition(double value) => string.Format(
        CultureInfo.CurrentCulture,
        PositionFormat,
        Math.Round(value, MidpointRounding.AwayFromZero));

    private sealed partial class CompactPositionPreviewAutomationPeer(CompactPositionPreview owner) : RangeBaseAutomationPeer(owner), IValueProvider
    {
        string IValueProvider.Value => owner.PositionText;

        void IValueProvider.SetValue(string value)
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var percentage)
                && !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out percentage))
            {
                throw new ArgumentException("The value must be a number.", nameof(value));
            }

            SetValue(percentage);
        }

        protected override object GetPatternCore(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.Value ? this : base.GetPatternCore(patternInterface);

        protected override string GetClassNameCore() => nameof(CompactPositionPreview);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

        protected override AutomationOrientation GetOrientationCore() => AutomationOrientation.Vertical;

        protected override IList<AutomationPeer>? GetChildrenCore() => null;
    }
}
