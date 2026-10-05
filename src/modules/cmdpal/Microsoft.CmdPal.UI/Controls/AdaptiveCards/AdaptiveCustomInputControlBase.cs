// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

#pragma warning disable SA1402 // File may only contain a single type

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards;

internal abstract partial class AdaptiveCustomInputControlBase : Grid, IAdaptiveCustomInputControl
{
    protected AdaptiveCustomInputControlBase(
        string? header,
        string? description,
        bool isRequired)
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;

        RootPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Spacing = 8,
        };

        if (!string.IsNullOrEmpty(header))
        {
            RootPanel.Children.Add(new TextBlock
            {
                Text = isRequired ? $"{header} *" : header,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        if (!string.IsNullOrEmpty(description) &&
            !string.Equals(description, header, StringComparison.OrdinalIgnoreCase))
        {
            RootPanel.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        ValidationError = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
        };
        ApplyCriticalForeground(ValidationError);
    }

    protected StackPanel RootPanel { get; }

    protected TextBlock ValidationError { get; }

    protected bool ValidationWasRequested { get; private set; }

    public abstract string CurrentValue { get; }

    public UIElement ValidationErrorElement => ValidationError;

    public bool ValidateInput()
    {
        ValidationWasRequested = true;
        return UpdateValidation();
    }

    public abstract void FocusInput();

    protected abstract bool UpdateValidation();

    protected void CompleteLayout()
    {
        RootPanel.Children.Add(ValidationError);
        Children.Add(RootPanel);
    }

    protected void UpdateValidationIfRequested()
    {
        if (ValidationWasRequested)
        {
            UpdateValidation();
        }
    }

    protected void ShowValidationError(string message)
    {
        ValidationWasRequested = true;
        ValidationError.Text = message;
        ValidationError.Visibility = Visibility.Visible;
    }

    protected static Button CreateTextButton(string text, string glyph)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
        };
        content.Children.Add(new FontIcon { Glyph = glyph, FontSize = 14 });
        content.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        return new Button { Content = content };
    }

    protected static void ApplyCriticalForeground(TextBlock textBlock)
    {
        if (Application.Current.Resources.TryGetValue("SystemFillColorCriticalBrush", out var resource) &&
            resource is Microsoft.UI.Xaml.Media.Brush brush)
        {
            textBlock.Foreground = brush;
        }
    }
}

internal abstract partial class AdaptiveListInputControlBase : AdaptiveCustomInputControlBase
{
    protected const string AddGlyph = "\uE710";
    protected const string DeleteGlyph = "\uE74D";
    protected const double ListItemMinHeight = 34;

    private const double ListMaxHeight = 240;
    private const int AlwaysVisibleScrollBarItemCount = 7;

    private static readonly CompositeFormat _removeItemLabelFormat =
        CompositeFormat.Parse(RS_.GetString("AdaptiveListInput_RemoveItem"));

    protected AdaptiveListInputControlBase(AdaptiveListInputElement element)
        : base(element.Header, element.Description, element.IsRequired)
    {
        ItemsList = new ListBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            MaxHeight = ListMaxHeight,
            MinHeight = ListItemMinHeight,
            SelectionMode = SelectionMode.Single,
        };

        if (!string.IsNullOrEmpty(element.Header))
        {
            AutomationProperties.SetName(ItemsList, element.Header);
        }

        ScrollViewer.SetHorizontalScrollBarVisibility(ItemsList, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollMode(ItemsList, ScrollMode.Enabled);
        ScrollViewer.SetIsVerticalRailEnabled(ItemsList, true);
        RootPanel.Children.Add(ItemsList);
    }

    protected ListBox ItemsList { get; }

    protected string GetRemoveItemLabel(string item) =>
        string.Format(CultureInfo.CurrentCulture, _removeItemLabelFormat, item);

    protected void RefreshListItems<T>(IReadOnlyCollection<T> items, Func<T, UIElement> createItemRow)
    {
        ItemsList.Items.Clear();
        foreach (var item in items)
        {
            ItemsList.Items.Add(new ListBoxItem
            {
                Content = createItemRow(item),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                MinHeight = ListItemMinHeight,
                Padding = new Thickness(8, 2, 16, 2),
            });
        }

        var scrollBarVisibility = items.Count >= AlwaysVisibleScrollBarItemCount
            ? ScrollBarVisibility.Visible
            : ScrollBarVisibility.Auto;
        ScrollViewer.SetVerticalScrollBarVisibility(ItemsList, scrollBarVisibility);
    }
}

#pragma warning restore SA1402 // File may only contain a single type
