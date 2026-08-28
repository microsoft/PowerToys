// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class ContentExpansionButton : Button
{
    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(ContentExpansionButton), new PropertyMetadata(false, StateChanged));

    public static readonly DependencyProperty HiddenItemCountProperty =
        DependencyProperty.Register(nameof(HiddenItemCount), typeof(int), typeof(ContentExpansionButton), new PropertyMetadata(0, StateChanged));

    private static readonly CompositeFormat _showMoreFormat =
        CompositeFormat.Parse(ResourceLoaderInstance.GetString("ContentSection_ShowMoreFormat"));

    private readonly TextBlock _label = new();
    private readonly FontIcon _chevron = new() { FontSize = 12 };

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public int HiddenItemCount
    {
        get => (int)GetValue(HiddenItemCountProperty);
        set => SetValue(HiddenItemCountProperty, value);
    }

    public ContentExpansionButton()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        panel.Children.Add(_label);
        panel.Children.Add(_chevron);
        Content = panel;
        Click += Button_Click;
        UpdateLabel();
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        IsExpanded = !IsExpanded;
    }

    private static void StateChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var button = (ContentExpansionButton)sender;
        button.UpdateLabel();
        if (args.Property == IsExpandedProperty && FrameworkElementAutomationPeer.FromElement(button) is { } peer)
        {
            peer.RaisePropertyChangedEvent(
                ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                (bool)args.OldValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
                button.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
        }
    }

    private void UpdateLabel()
    {
        _label.Text = IsExpanded
            ? ResourceLoaderInstance.GetString("ContentSection_ShowFewer")
            : string.Format(CultureInfo.CurrentCulture, _showMoreFormat, HiddenItemCount);
        _chevron.Glyph = IsExpanded ? "\uE70E" : "\uE70D";
        AutomationProperties.SetName(this, _label.Text);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ExpansionPeer(this);

    private sealed partial class ExpansionPeer(ContentExpansionButton owner) : ButtonAutomationPeer(owner), IExpandCollapseProvider
    {
        public ExpandCollapseState ExpandCollapseState => owner.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public void Expand() => owner.IsExpanded = true;

        public void Collapse() => owner.IsExpanded = false;

        protected override object GetPatternCore(PatternInterface patternInterface)
            => patternInterface == PatternInterface.ExpandCollapse ? this : base.GetPatternCore(patternInterface);
    }
}
