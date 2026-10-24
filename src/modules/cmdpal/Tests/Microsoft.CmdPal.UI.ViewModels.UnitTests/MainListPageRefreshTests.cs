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
        using var page = new MainListPage(commands, aliases, matcherProvider.Object, settingsService.Object, appStateService.Object, new ListPage());
        var refreshed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        page.ItemsChanged += (_, _) => refreshed.TrySetResult();

        page.SearchText = "notepad";

        // Keep the provider debounce pending until the independent input refresh arrives.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!refreshed.Task.IsCompleted && !timeout.IsCancellationRequested)
        {
            page.Receive(new UpdateFallbackItemsMessage());
            await Task.Delay(5);
        }

        Assert.IsTrue(refreshed.Task.IsCompleted, "Provider notifications starved the pending search refresh.");
    }
}
