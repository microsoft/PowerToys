// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.CmdPal.UI;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ContentPage : Page,
     IRecipient<ActivateSelectedListItemMessage>,
     IRecipient<ActivateSecondaryCommandMessage>
{
    private readonly DispatcherQueue _queue = DispatcherQueue.GetForCurrentThread();

    public ContentPageViewModel? ViewModel
    {
        get => (ContentPageViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    // Using a DependencyProperty as the backing store for ViewModel.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(ContentPageViewModel), typeof(ContentPage), new PropertyMetadata(null));

    public static readonly DependencyProperty ContentMarginProperty =
        DependencyProperty.Register(
            nameof(ContentMargin),
            typeof(Thickness),
            typeof(ContentPage),
            new PropertyMetadata(new Thickness(0, 4, 4, 4)));

    public Thickness ContentMargin
    {
        get => (Thickness)GetValue(ContentMarginProperty);
        set => SetValue(ContentMarginProperty, value);
    }

    public static readonly DependencyProperty ContentPaddingProperty =
        DependencyProperty.Register(
            nameof(ContentPadding),
            typeof(Thickness),
            typeof(ContentPage),
            new PropertyMetadata(new Thickness(12, 8, 8, 8)));

    public Thickness ContentPadding
    {
        get => (Thickness)GetValue(ContentPaddingProperty);
        set => SetValue(ContentPaddingProperty, value);
    }

    public static readonly DependencyProperty ItemSpacingProperty =
        DependencyProperty.Register(
            nameof(ItemSpacing),
            typeof(int),
            typeof(ContentPage),
            new PropertyMetadata(8, OnItemSpacingChanged));

    public int ItemSpacing
    {
        get => (int)GetValue(ItemSpacingProperty);
        set => SetValue(ItemSpacingProperty, value);
    }

    private static void OnItemSpacingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ContentPage page)
        {
            page.UpdateItemsRepeaterLayout();
        }
    }

    private void UpdateItemsRepeaterLayout()
    {
        if (ContentItemsRepeater != null)
        {
            ContentItemsRepeater.Layout = new StackLayout
            {
                Orientation = Orientation.Vertical,
                Spacing = (double)ItemSpacing,
            };
        }
    }

    public ContentPage()
    {
        this.InitializeComponent();
        this.Loaded += OnLoaded;
        this.Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Apply the layout in Loaded to ensure ContentItemsRepeater is available
        UpdateItemsRepeaterLayout();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Unhook from everything to ensure nothing can reach us
        // between this point and our complete and utter destruction.
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is not AsyncNavigationRequest navigationRequest)
        {
            throw new InvalidOperationException($"Invalid navigation parameter: {nameof(e.Parameter)} must be {nameof(AsyncNavigationRequest)}");
        }

        if (navigationRequest.TargetViewModel is not ContentPageViewModel contentPageViewModel)
        {
            throw new InvalidOperationException($"Invalid navigation target: AsyncNavigationRequest.{nameof(AsyncNavigationRequest.TargetViewModel)} must be {nameof(ContentPageViewModel)}");
        }

        ViewModel = contentPageViewModel;

        if (!WeakReferenceMessenger.Default.IsRegistered<ActivateSelectedListItemMessage>(this))
        {
            WeakReferenceMessenger.Default.Register<ActivateSelectedListItemMessage>(this);
        }

        if (!WeakReferenceMessenger.Default.IsRegistered<ActivateSecondaryCommandMessage>(this))
        {
            WeakReferenceMessenger.Default.Register<ActivateSecondaryCommandMessage>(this);
        }

        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);
        WeakReferenceMessenger.Default.Unregister<ActivateSelectedListItemMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ActivateSecondaryCommandMessage>(this);

        // Clean-up event listeners
        if (e.NavigationMode != NavigationMode.New)
        {
            ViewModel?.SafeCleanup();
            CleanupHelper.Cleanup(this);
        }

        ViewModel = null;
    }

    // this comes in on Enter keypresses in the SearchBox
    public void Receive(ActivateSelectedListItemMessage message)
    {
        ViewModel?.InvokePrimaryCommandCommand?.Execute(ViewModel);
    }

    // this comes in on Ctrl+Enter keypresses in the SearchBox
    public void Receive(ActivateSecondaryCommandMessage message)
    {
        ViewModel?.InvokeSecondaryCommandCommand?.Execute(ViewModel);
    }

    private void ContentItemsRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        // Each template wraps content in a Grid or StackPanel - apply margin/padding to the root element
        if (args.Element is FrameworkElement element)
        {
            if (element is Grid grid)
            {
                grid.Margin = ContentMargin;
                grid.Padding = ContentPadding;

                // Find and configure ContentFormControl within the grid
                foreach (var child in grid.Children)
                {
                    if (child is Controls.ContentFormControl formControl)
                    {
                        formControl.ItemSpacing = ItemSpacing;
                        break;
                    }
                }
            }
            else if (element is StackPanel panel)
            {
                panel.Margin = ContentMargin;
                panel.Padding = ContentPadding;

                // Find and configure ContentFormControl within the panel
                foreach (var child in panel.Children)
                {
                    if (child is Controls.ContentFormControl formControl)
                    {
                        formControl.ItemSpacing = ItemSpacing;
                        break;
                    }
                }
            }
        }
    }
}
