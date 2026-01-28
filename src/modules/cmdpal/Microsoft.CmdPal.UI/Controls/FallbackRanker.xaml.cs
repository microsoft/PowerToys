// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Controls;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable - Page lifecycle manages disposal
public sealed partial class FallbackRanker : UserControl
{
    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
    private SettingsViewModel? viewModel;

    public FallbackRanker(SettingsService settingsService, TopLevelCommandManager topLevelCommandManager, IThemeService themeService)
    {
        this.InitializeComponent();

        viewModel = new SettingsViewModel(settingsService, topLevelCommandManager, _mainTaskScheduler, themeService);
    }

    private void ListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        viewModel?.ApplyFallbackSort();
    }
}
