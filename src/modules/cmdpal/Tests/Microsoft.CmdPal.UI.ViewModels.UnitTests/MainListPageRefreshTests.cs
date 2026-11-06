// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed class MainListPageRefreshTests
{
    [TestMethod]
    public async Task ProviderUpdates_DoNotPostponePendingUserInput()
    {
        var settingsService = new Mock<ISettingsService>();
        settingsService.SetupGet(service => service.Settings).Returns(new SettingsModel());
        var services = new Mock<IServiceProvider>();
        services.Setup(service => service.GetService(typeof(TaskScheduler))).Returns(TaskScheduler.Default);
        services.Setup(service => service.GetService(typeof(ISettingsService))).Returns(settingsService.Object);
        var appStateService = new Mock<IAppStateService>();
        appStateService.SetupGet(service => service.State).Returns(new AppStateModel());
        var matcherProvider = new Mock<IFuzzyMatcherProvider>();
        matcherProvider.SetupGet(provider => provider.Current).Returns(new PrecomputedFuzzyMatcher());
        using var commands = new TopLevelCommandManager(services.Object, []);
        var aliases = new AliasManager(commands, settingsService.Object);
        using var page = new MainListPage(commands, aliases, matcherProvider.Object, settingsService.Object, appStateService.Object, new(new ListPage()));
        var refreshed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        page.ItemsChanged += (_, _) => refreshed.TrySetResult();

        // Warm up the search path, so the measured keystroke's filtering doesn't use up the typed
        // refresh delay and publish before any fallback refresh arrives.
        page.SearchText = "n";
        await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        refreshed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        page.SearchText = "notepad";
        if (refreshed.Task.IsCompleted)
        {
            Assert.Inconclusive("The typed refresh published before any fallback refresh could arrive.");
        }

        // Fallback completions keep arriving faster than their own debounce while the typed
        // refresh is pending; they must join it rather than push it back.
        var fallbackRefreshes = 0;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        do
        {
            page.Receive(new UpdateFallbackItemsMessage());
            fallbackRefreshes++;
            await Task.Delay(5);
        }
        while (!refreshed.Task.IsCompleted && !timeout.IsCancellationRequested);

        Assert.IsTrue(refreshed.Task.IsCompleted, $"{fallbackRefreshes} fallback refreshes starved the pending search refresh.");
    }
}
