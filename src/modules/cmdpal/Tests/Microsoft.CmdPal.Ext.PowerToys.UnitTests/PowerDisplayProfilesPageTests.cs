// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Contracts;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using PowerToysExtension.Pages;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.PowerToys.UnitTests;

[TestClass]
public class PowerDisplayProfilesPageTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [TestMethod]
    public async Task GetItems_BeforeSubscriptionDoesNotLoad_AndObservedReadsShareSnapshot()
    {
        var pendingLoad = PendingLoad();
        var service = new FakePowerDisplayCliService
        {
            GetProfilesHandler = _ => pendingLoad.Task,
        };
        var page = new PowerDisplayProfilesPage(service);
        INotifyItemsChanged observable = page;
        var itemsChanged = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        TypedEventHandler<object, IItemsChangedEventArgs> handler = (_, _) =>
        {
            // The host fetches again in response to this event. That must not start another CLI.
            itemsChanged.TrySetResult(page.GetItems().Length);
        };

        Assert.IsTrue(page.IsLoading);
        Assert.AreEqual(0, page.GetItems().Length);
        Assert.AreEqual(0, page.GetItems().Length);
        Assert.AreEqual(0, service.GetProfilesCallCount, "the host fetches before subscribing");

        observable.ItemsChanged += handler;
        await service.GetProfilesObserved.Task.WaitAsync(TestTimeout);
        Assert.AreEqual(0, page.GetItems().Length);
        Assert.IsTrue(page.IsLoading);

        const string DuplicateName = "Office | \u4F1A\u8BAE";
        pendingLoad.SetResult(SuccessfulProfiles(
            new() { Id = 3, Name = DuplicateName, MonitorCount = 2 },
            new() { Id = 8, Name = DuplicateName, MonitorCount = 1, LastModified = "invalid" }));

        await page.LoadingTask.WaitAsync(TestTimeout);
        Assert.AreEqual(2, await itemsChanged.Task.WaitAsync(TestTimeout));
        Assert.IsFalse(page.IsLoading);

        var items = page.GetItems();
        Assert.AreSame(items, page.GetItems());
        Assert.AreEqual($"{DuplicateName} (#3)", items[0].Title);
        Assert.AreEqual($"{DuplicateName} (#8)", items[1].Title);
        Assert.IsTrue(items[0].Subtitle.StartsWith("2 monitors", StringComparison.Ordinal));
        Assert.IsTrue(items[1].Subtitle.StartsWith("1 monitor", StringComparison.Ordinal));
        Assert.IsInstanceOfType<ApplyPowerDisplayProfileCommand>(items[0].Command);
        Assert.AreEqual("com.microsoft.powertoys.powerDisplay.applyProfile.3", items[0].Command!.Id);
        Assert.AreEqual("com.microsoft.powertoys.powerDisplay.applyProfile.8", items[1].Command!.Id);
        Assert.AreEqual(1, service.GetProfilesCallCount);
        observable.ItemsChanged -= handler;
    }

    [TestMethod]
    public async Task Reentry_LoadsFreshProfilesAfterLastObserverLeaves()
    {
        var service = new FakePowerDisplayCliService
        {
            GetProfilesHandler = _ => Task.FromResult(SuccessfulProfiles(new CliProfileInfo { Id = 3, Name = "Before" })),
        };
        var page = new PowerDisplayProfilesPage(service);
        INotifyItemsChanged observable = page;
        TypedEventHandler<object, IItemsChangedEventArgs> handler = (_, _) => { };

        observable.ItemsChanged += handler;
        await page.LoadingTask.WaitAsync(TestTimeout);
        Assert.AreEqual("Before (#3)", page.GetItems()[0].Title);

        observable.ItemsChanged -= handler;
        Assert.IsTrue(page.IsLoading);
        Assert.AreEqual(0, page.GetItems().Length);
        Assert.AreEqual(1, service.GetProfilesCallCount);

        service.GetProfilesHandler = _ => Task.FromResult(SuccessfulProfiles(new CliProfileInfo { Id = 4, Name = "After" }));
        observable.ItemsChanged += handler;
        await page.LoadingTask.WaitAsync(TestTimeout);

        Assert.AreEqual("After (#4)", page.GetItems()[0].Title);
        Assert.AreEqual(2, service.GetProfilesCallCount);
        observable.ItemsChanged -= handler;
    }

    [TestMethod]
    public async Task UnknownAndDuplicateSubscriptions_CancelOnlyAfterLastActualRemoval()
    {
        var pendingLoad = PendingLoad();
        var tokenObserved = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakePowerDisplayCliService
        {
            GetProfilesHandler = token =>
            {
                tokenObserved.TrySetResult(token);
                return pendingLoad.Task;
            },
        };
        var page = new PowerDisplayProfilesPage(service);
        INotifyItemsChanged observable = page;
        TypedEventHandler<object, IItemsChangedEventArgs> handler = (_, _) => { };
        TypedEventHandler<object, IItemsChangedEventArgs> unknown = (_, _) => { };

        observable.ItemsChanged -= unknown;
        Assert.AreEqual(0, service.GetProfilesCallCount);
        observable.ItemsChanged += handler;
        observable.ItemsChanged += handler;
        var loadingTask = page.LoadingTask;
        var token = await tokenObserved.Task.WaitAsync(TestTimeout);

        observable.ItemsChanged -= unknown;
        observable.ItemsChanged -= handler;
        Assert.IsFalse(token.IsCancellationRequested);
        Assert.AreEqual(1, service.GetProfilesCallCount);

        observable.ItemsChanged -= handler;
        Assert.IsTrue(token.IsCancellationRequested);
        observable.ItemsChanged -= handler;
        Assert.AreEqual(0, page.GetItems().Length);
        pendingLoad.SetResult(SuccessfulProfiles());
        await loadingTask.WaitAsync(TestTimeout);
    }

    [TestMethod]
    public async Task CancelledOldLoad_CannotOverwriteReenteredPageEvenWhenServiceIgnoresCancellation()
    {
        var oldLoad = PendingLoad();
        var newLoad = PendingLoad();
        var oldTokenObserved = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var newCallObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var service = new FakePowerDisplayCliService
        {
            GetProfilesHandler = token =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    oldTokenObserved.TrySetResult(token);
                    return oldLoad.Task;
                }

                newCallObserved.TrySetResult(true);
                return newLoad.Task;
            },
        };
        var page = new PowerDisplayProfilesPage(service);
        INotifyItemsChanged observable = page;
        var notifications = 0;
        TypedEventHandler<object, IItemsChangedEventArgs> handler = (_, _) => Interlocked.Increment(ref notifications);

        observable.ItemsChanged += handler;
        var oldTask = page.LoadingTask;
        var oldToken = await oldTokenObserved.Task.WaitAsync(TestTimeout);
        observable.ItemsChanged -= handler;
        Assert.IsTrue(oldToken.IsCancellationRequested);

        observable.ItemsChanged += handler;
        await newCallObserved.Task.WaitAsync(TestTimeout);
        newLoad.SetResult(SuccessfulProfiles(new CliProfileInfo { Id = 8, Name = "Current" }));
        await page.LoadingTask.WaitAsync(TestTimeout);
        var currentNotifications = Volatile.Read(ref notifications);

        oldLoad.SetResult(SuccessfulProfiles(new CliProfileInfo { Id = 3, Name = "Stale" }));
        await oldTask.WaitAsync(TestTimeout);

        Assert.AreEqual("Current (#8)", page.GetItems()[0].Title);
        Assert.IsFalse(page.IsLoading);
        Assert.AreEqual(currentNotifications, Volatile.Read(ref notifications));
        Assert.AreEqual(2, service.GetProfilesCallCount);
        observable.ItemsChanged -= handler;
    }

    [DataTestMethod]
    [DataRow("", "Power Display isn't running. Select to try again.")]
    [DataRow("PowerDisplay is not running. Enable it in PowerToys settings.", "PowerDisplay is not running. Enable it in PowerToys settings.")]
    public async Task FailedLoad_ShowsRetryThatLoadsProfilesAgain(string errorMessage, string expectedSubtitle)
    {
        var service = new FakePowerDisplayCliService
        {
            GetProfilesHandler = _ => Task.FromResult(
                PowerDisplayCliResult<CliProfileListResult>.Failure(
                    PowerDisplayCliFailureKind.ProviderUnavailable,
                    CliExitCodes.ProviderUnavailable,
                    errorMessage)),
        };
        var page = new PowerDisplayProfilesPage(service);
        INotifyItemsChanged observable = page;
        TypedEventHandler<object, IItemsChangedEventArgs> handler = (_, _) => { };
        observable.ItemsChanged += handler;
        await page.LoadingTask.WaitAsync(TestTimeout);

        Assert.IsFalse(page.IsLoading);
        Assert.AreEqual(0, page.GetItems().Length);
        var emptyContent = page.EmptyContent;
        Assert.IsNotNull(emptyContent);
        Assert.AreEqual("Couldn't load Power Display profiles", emptyContent.Title);
        Assert.AreEqual(expectedSubtitle, emptyContent.Subtitle);
        var retry = emptyContent.Command as AnonymousCommand;
        Assert.IsNotNull(retry);
        Assert.IsNotNull(GetRefreshCommand(emptyContent));

        service.GetProfilesHandler = _ => Task.FromResult(SuccessfulProfiles(new CliProfileInfo { Id = 11, Name = "Recovered" }));
        Assert.AreEqual(CommandResultKind.KeepOpen, retry.Invoke().Kind);
        await page.LoadingTask.WaitAsync(TestTimeout);

        Assert.AreEqual(2, service.GetProfilesCallCount);
        Assert.AreEqual("Recovered (#11)", page.GetItems()[0].Title);
        observable.ItemsChanged -= handler;
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Refresh_IsAvailableForSuccessfulAndEmptyLists_AndCoalescesClicks(bool initiallyEmpty)
    {
        var service = new FakePowerDisplayCliService
        {
            GetProfilesHandler = _ => Task.FromResult(initiallyEmpty
                ? SuccessfulProfiles()
                : SuccessfulProfiles(new CliProfileInfo { Id = 5, Name = "Before" })),
        };
        var page = new PowerDisplayProfilesPage(service);
        INotifyItemsChanged observable = page;
        TypedEventHandler<object, IItemsChangedEventArgs> handler = (_, _) => { };
        observable.ItemsChanged += handler;
        await page.LoadingTask.WaitAsync(TestTimeout);
        var source = initiallyEmpty ? page.EmptyContent! : page.GetItems()[0];
        var refresh = GetRefreshCommand(source);

        var pendingRefresh = PendingLoad();
        var refreshObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.GetProfilesHandler = _ =>
        {
            refreshObserved.TrySetResult(true);
            return pendingRefresh.Task;
        };

        Assert.AreEqual(CommandResultKind.KeepOpen, refresh.Invoke().Kind);
        await refreshObserved.Task.WaitAsync(TestTimeout);
        Assert.IsTrue(page.IsLoading);
        Assert.AreEqual(0, page.GetItems().Length);
        refresh.Invoke();
        Assert.AreEqual(2, service.GetProfilesCallCount);

        pendingRefresh.SetResult(SuccessfulProfiles(new CliProfileInfo { Id = 9, Name = "After refresh" }));
        await page.LoadingTask.WaitAsync(TestTimeout);
        Assert.AreEqual("After refresh (#9)", page.GetItems()[0].Title);
        Assert.AreEqual(2, service.GetProfilesCallCount);
        observable.ItemsChanged -= handler;
    }

    private static AnonymousCommand GetRefreshCommand(ICommandItem item)
    {
        Assert.AreEqual(1, item.MoreCommands.Length);
        var context = item.MoreCommands[0] as ICommandContextItem;
        Assert.IsNotNull(context);
        var refresh = context.Command as AnonymousCommand;
        Assert.IsNotNull(refresh);
        Assert.AreEqual("com.microsoft.powertoys.powerDisplay.profiles.refresh", refresh.Id);
        return refresh;
    }

    private static TaskCompletionSource<PowerDisplayCliResult<CliProfileListResult>> PendingLoad()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static PowerDisplayCliResult<CliProfileListResult> SuccessfulProfiles(params CliProfileInfo[] profiles)
        => PowerDisplayCliResult<CliProfileListResult>.Success(new CliProfileListResult { Profiles = [.. profiles] });
}
