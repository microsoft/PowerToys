// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Text;
using Microsoft.CommandPalette.ViewModels;
using Windows.System;

namespace Microsoft.CommandPalette.UI.Pages;

public sealed partial class ShellPage : Page
{
    private readonly ShellViewModel viewModel;
    private readonly DispatcherQueue _queue = DispatcherQueue.GetForCurrentThread();
    private readonly DispatcherQueueTimer _debounceTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

    private readonly SlideNavigationTransitionInfo _slideRightTransition = new() { Effect = SlideNavigationTransitionEffect.FromRight };
    private readonly SuppressNavigationTransitionInfo _noAnimation = new();

    private readonly ToastWindow _toast = new();

    private readonly CompositeFormat _pageNavigatedAnnouncement;


    private CancellationTokenSource? _focusAfterLoadedCts;

    public event PropertyChangedEventHandler? PropertyChanged;

    private WeakReference<Page>? _lastNavigatedPageRef;
    private SettingsWindow? _settingsWindow;

    public ShellPage(ShellViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
    }
}
