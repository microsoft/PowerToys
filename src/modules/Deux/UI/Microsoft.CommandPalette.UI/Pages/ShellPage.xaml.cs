// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CommandPalette.UI.Helpers;
using Microsoft.CommandPalette.UI.Models;
using Microsoft.CommandPalette.UI.Models.Messages;
using Microsoft.CommandPalette.UI.ViewModels;
using Microsoft.CommandPalette.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace Microsoft.CommandPalette.UI.Pages;

public sealed partial class ShellPage : Microsoft.UI.Xaml.Controls.Page
{
    private readonly ShellViewModel viewModel;
    private readonly SettingsModel _settingsModel;
    private readonly ILogger logger;
    private readonly DispatcherQueue _queue = DispatcherQueue.GetForCurrentThread();
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _debounceTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

    private readonly SlideNavigationTransitionInfo _slideRightTransition = new() { Effect = SlideNavigationTransitionEffect.FromRight };
    private readonly SuppressNavigationTransitionInfo _noAnimation = new();

    private readonly CompositeFormat _pageNavigatedAnnouncement;

    public ShellPage(ShellViewModel viewModel, SettingsModel settingsModel, ILogger logger)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        _settingsModel = settingsModel;
        this.logger = logger;

        // AddHandler(PreviewKeyDownEvent, new KeyEventHandler(ShellPage_OnPreviewKeyDown), true);
        // AddHandler(KeyDownEvent, new KeyEventHandler(ShellPage_OnKeyDown), false);
        // AddHandler(PointerPressedEvent, new PointerEventHandler(ShellPage_OnPointerPressed), true);
        RootFrame.Navigate(typeof(LoadingPage), new AsyncNavigationRequest(viewModel, CancellationToken.None));

        var pageAnnouncementFormat = ResourceLoaderInstance.GetString("ScreenReader_Announcement_NavigatedToPage0");
        _pageNavigatedAnnouncement = CompositeFormat.Parse(pageAnnouncementFormat);
    }

    /// <summary>
    /// Gets the default page animation, depending on the settings
    /// </summary>
    private NavigationTransitionInfo DefaultPageAnimation
    {
        get
        {
            return _settingsModel.DisableAnimations ? _noAnimation : _slideRightTransition;
        }
    }

    private void SummonOnUiThread(HotkeySummonMessage message)
    {
        var commandId = message.CommandId;
        var isRoot = string.IsNullOrEmpty(commandId);
        if (isRoot)
        {
            // If this is the hotkey for the root level, then always show us
            WeakReferenceMessenger.Default.Send<ShowWindowMessage>(new(message.Hwnd));

            // Depending on the settings, either
            // * Go home, or
            // * Select the search text (if we should remain open on this page)
            if (_settingsModel.AutoGoHomeInterval == TimeSpan.Zero)
            {
                // GoHome(false);
            }
            else if (_settingsModel.HighlightSearchOnActivate)
            {
                // SearchBox.SelectSearch();
            }
        }

        // else
        // {
        //    try
        //    {
        //        // For a hotkey bound to a command, first lookup the
        //        // command from our list of toplevel commands.
        //        var tlcManager = App.Current.Services.GetService<TopLevelCommandManager>()!;
        //        var topLevelCommand = tlcManager.LookupCommand(commandId);
        //        if (topLevelCommand is not null)
        //        {
        //            var command = topLevelCommand.CommandViewModel.Model.Unsafe;
        //            var isPage = command is not IInvokableCommand;
        //            // If the bound command is an invokable command, then
        //            // we don't want to open the window at all - we want to
        //            // just do it.
        //            if (isPage)
        //            {
        //                // If we're here, then the bound command was a page
        //                // of some kind. Let's pop the stack, show the window, and navigate to it.
        //                GoHome(false);
        //                WeakReferenceMessenger.Default.Send<ShowWindowMessage>(new(message.Hwnd));
        //            }
        //            var msg = topLevelCommand.GetPerformCommandMessage();
        //            msg.WithAnimation = false;
        //            WeakReferenceMessenger.Default.Send<PerformCommandMessage>(msg);
        //            // we can't necessarily SelectSearch() here, because when the page is loaded,
        //            // we'll fetch the SearchText from the page itself, and that'll stomp the
        //            // selection we start now.
        //            // That's probably okay though.
        //        }
        //    }
        //    catch
        //    {
        //    }
        // }
        WeakReferenceMessenger.Default.Send<FocusSearchBoxMessage>();
    }

    private void BackButton_Clicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => WeakReferenceMessenger.Default.Send<NavigateBackMessage>(new());

    [LoggerMessage(Level = LogLevel.Error)]
    partial void Log_ShowConfirmationMessageError(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid navigation target: AsyncNavigationRequest.{targetViewModel} must be {pageViewModel}")]
    partial void Log_InvalidNavigationTarget(string targetViewModel, string pageViewModel);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unrecognized target for shell navigation: {parameter}")]
    partial void Log_UnrecognizedNavigationTarget(NavigationEventArgs parameter);
}
