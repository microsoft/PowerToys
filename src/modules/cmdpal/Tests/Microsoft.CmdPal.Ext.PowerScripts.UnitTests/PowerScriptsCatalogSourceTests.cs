// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.PowerScripts.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;

namespace Microsoft.CmdPal.Ext.PowerScripts.UnitTests;

[TestClass]
public sealed class PowerScriptsCatalogSourceTests
{
    private static readonly string[] ExpectedScriptTitles = ["Alpha", "Alpha", "Beta"];
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(4);

    [TestMethod]
    [Timeout(5000)]
    public async Task ConcurrentQueriesShareDiscoveryAndCancelOnlyTheirOwnWait()
    {
        var result = new TaskCompletionSource<PowerScriptsCatalog>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakePowerScriptsService
        {
            DiscoverHandler = cancellationToken =>
            {
                started.TrySetResult(cancellationToken);
                return result.Task.WaitAsync(cancellationToken);
            },
        };
        using var source = new PowerScriptsCatalogSource(service, static _ => { });
        using var queryCancellation = new CancellationTokenSource();
        Assert.AreEqual(0, service.DiscoverCalls);

        source.EnsureDiscovery();
        var canceledQuery = source.GetCatalogAsync(queryCancellation.Token);
        var survivingQuery = source.GetCatalogAsync(CancellationToken.None);
        source.EnsureDiscovery();
        var discoveryCancellation = await started.Task.WaitAsync(WaitTimeout);

        queryCancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await canceledQuery.WaitAsync(WaitTimeout));

        Assert.IsFalse(discoveryCancellation.IsCancellationRequested);
        Assert.IsFalse(survivingQuery.IsCompleted);
        Assert.IsTrue(source.IsDiscovering);
        Assert.AreEqual(1, service.DiscoverCalls);

        var catalog = new PowerScriptsCatalog([TestScripts.Create()], true);
        result.SetResult(catalog);

        Assert.AreSame(catalog, await survivingQuery.WaitAsync(WaitTimeout));
        Assert.AreSame(catalog, source.Catalog);
        Assert.IsFalse(source.IsDiscovering);
        Assert.AreEqual(1, service.DiscoverCalls);
        Assert.AreEqual(0, service.RunCalls);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task CancelingAQueryDoesNotCancelAnExistingRunOrSharedRefresh()
    {
        var runResult = new TaskCompletionSource<PowerScriptTransformResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakePowerScriptsService
        {
            Catalog = new([TestScripts.Create()], true),
            RunHandler = cancellationToken => runResult.Task.WaitAsync(cancellationToken),
        };
        PowerScriptPage? destination = null;
        using var source = new PowerScriptsCatalogSource(service, target =>
        {
            destination = (PowerScriptPage)target;
            destination.GetContent();
        });
        var catalog = await source.GetCatalogAsync(CancellationToken.None).WaitAsync(WaitTimeout);
        ((IInvokableCommand)source.GetItems(catalog, string.Empty).Single().Command).Invoke(source);
        await service.RunStarted.WaitAsync(WaitTimeout);
        var page = destination!;

        var discoveryResult = new TaskCompletionSource<PowerScriptsCatalog>(TaskCreationOptions.RunContinuationsAsynchronously);
        var discoveryStarted = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.DiscoverHandler = cancellationToken =>
        {
            discoveryStarted.TrySetResult(cancellationToken);
            return discoveryResult.Task.WaitAsync(cancellationToken);
        };
        source.Refresh();
        using var queryCancellation = new CancellationTokenSource();
        var canceledQuery = source.GetCatalogAsync(queryCancellation.Token);
        var survivingQuery = source.GetCatalogAsync(CancellationToken.None);
        var discoveryCancellation = await discoveryStarted.Task.WaitAsync(WaitTimeout);

        queryCancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await canceledQuery.WaitAsync(WaitTimeout));

        Assert.IsFalse(discoveryCancellation.IsCancellationRequested);
        Assert.IsFalse(service.RunCancellationToken.IsCancellationRequested);
        Assert.IsTrue(page.IsLoading);
        Assert.AreEqual(1, service.RunCalls);

        discoveryResult.SetResult(catalog);
        Assert.AreSame(catalog, await survivingQuery.WaitAsync(WaitTimeout));
        await CompleteRun(page, () => runResult.SetResult(new PowerScriptTransformResult { Text = "finished" }));
        AssertStatus(page, Resources.Completed);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task CatalogLifetimeStartsAtDiscoveryCompletion()
    {
        var clock = new ManualTimeProvider();
        var result = new TaskCompletionSource<PowerScriptsCatalog>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakePowerScriptsService
        {
            DiscoverHandler = cancellationToken =>
            {
                started.TrySetResult();
                return result.Task.WaitAsync(cancellationToken);
            },
        };
        using var source = new PowerScriptsCatalogSource(service, static _ => { }, clock);
        var refreshCompletions = 0;
        source.RefreshCompleted += (_, _) => Interlocked.Increment(ref refreshCompletions);

        var firstQuery = source.GetCatalogAsync(CancellationToken.None);
        await started.Task.WaitAsync(WaitTimeout);
        clock.Advance(TimeSpan.FromMinutes(1));
        var catalog = new PowerScriptsCatalog([TestScripts.Create()], true);
        result.SetResult(catalog);
        Assert.AreSame(catalog, await firstQuery.WaitAsync(WaitTimeout));

        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.AreSame(catalog, await source.GetCatalogAsync(CancellationToken.None).WaitAsync(WaitTimeout));
        Assert.AreEqual(1, service.DiscoverCalls);

        var updated = new PowerScriptsCatalog([], true);
        service.DiscoverHandler = _ => Task.FromResult(updated);
        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.AreSame(updated, await source.GetCatalogAsync(CancellationToken.None).WaitAsync(WaitTimeout));
        Assert.AreEqual(2, service.DiscoverCalls);
        Assert.AreEqual(0, Volatile.Read(ref refreshCompletions));
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task FailedDiscoveryIsThrottledUntilItsCatalogLifetimeExpires()
    {
        var clock = new ManualTimeProvider();
        var failure = new InvalidOperationException("Host unavailable.");
        var service = new FakePowerScriptsService
        {
            DiscoverHandler = _ => Task.FromException<PowerScriptsCatalog>(failure),
        };
        using var source = new PowerScriptsCatalogSource(service, static _ => { }, clock);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await source.GetCatalogAsync(CancellationToken.None).WaitAsync(WaitTimeout));
        Assert.AreEqual(failure.Message, source.Error);
        Assert.IsFalse(source.IsDiscovering);
        Assert.AreEqual(1, service.DiscoverCalls);

        clock.Advance(TimeSpan.FromSeconds(29));
        source.EnsureDiscovery();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await source.GetCatalogAsync(CancellationToken.None).WaitAsync(WaitTimeout));
        Assert.AreEqual(1, service.DiscoverCalls);

        var catalog = new PowerScriptsCatalog([TestScripts.Create()], true);
        service.DiscoverHandler = _ => Task.FromResult(catalog);
        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.AreSame(catalog, await source.GetCatalogAsync(CancellationToken.None).WaitAsync(WaitTimeout));
        Assert.IsNull(source.Error);
        Assert.AreEqual(2, service.DiscoverCalls);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task ManualRefreshRetriesImmediatelyAndPublishesOneCompletion()
    {
        var service = new FakePowerScriptsService
        {
            DiscoverHandler = _ => Task.FromException<PowerScriptsCatalog>(new InvalidOperationException("Host unavailable.")),
        };
        using var source = new PowerScriptsCatalogSource(service, static _ => { }, new ManualTimeProvider());
        var refreshed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshCompletions = 0;
        source.RefreshCompleted += (_, _) =>
        {
            Interlocked.Increment(ref refreshCompletions);
            refreshed.TrySetResult();
        };
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await source.GetCatalogAsync(CancellationToken.None).WaitAsync(WaitTimeout));
        Assert.AreEqual(0, Volatile.Read(ref refreshCompletions));

        var result = new TaskCompletionSource<PowerScriptsCatalog>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.DiscoverHandler = cancellationToken =>
        {
            started.TrySetResult();
            return result.Task.WaitAsync(cancellationToken);
        };
        source.Refresh();
        await started.Task.WaitAsync(WaitTimeout);
        source.Refresh();
        source.Refresh();
        var catalog = new PowerScriptsCatalog([TestScripts.Create()], true);
        result.SetResult(catalog);
        await refreshed.Task.WaitAsync(WaitTimeout);

        Assert.AreSame(catalog, source.Catalog);
        Assert.IsNull(source.Error);
        Assert.IsFalse(source.IsDiscovering);
        Assert.AreEqual(2, service.DiscoverCalls);
        Assert.AreEqual(1, Volatile.Read(ref refreshCompletions));
        Assert.AreSame(catalog, await source.GetCatalogAsync(CancellationToken.None).WaitAsync(WaitTimeout));
        Assert.AreEqual(1, Volatile.Read(ref refreshCompletions));
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task MatchingAndLimitsUseTheCachedCatalogWithoutExecutingScripts()
    {
        var service = new FakePowerScriptsService
        {
            Catalog = new(
            [
                new PowerScriptInfo { Id = "beta", Name = "Beta", Description = "Clean the downloads folder" },
                new PowerScriptInfo { Id = "alpha-z", Name = "Alpha", Description = "Another action" },
                new PowerScriptInfo { Id = "alpha-a", Name = "Alpha", Description = "First action" },
            ],
            true),
        };
        using var source = new PowerScriptsCatalogSource(service, static _ => { });
        var catalog = await source.GetCatalogAsync(CancellationToken.None).WaitAsync(WaitTimeout);

        var limited = source.GetItems(catalog, "ALPHA", maximumItems: 1);
        var all = source.GetItems(catalog, string.Empty);
        CollectionAssert.AreEqual(ExpectedScriptTitles, all.Select(item => item.Title).ToArray());
        Assert.HasCount(1, limited);
        Assert.AreEqual("com.microsoft.cmdpal.builtin.powerscripts.script.alpha-a.run", limited[0].Command.Id);
        Assert.AreSame(limited[0].Command, all[0].Command);
        Assert.AreEqual("Beta", source.GetItems(catalog, "DOWNLOADS").Single().Title);
        Assert.HasCount(0, source.GetItems(catalog, string.Empty, maximumItems: 0));
        Assert.AreEqual(1, service.DiscoverCalls);
        Assert.AreEqual(0, service.RunCalls);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task ViewsShareTheirPagesAndCommandsAndRejectDuplicateRuns()
    {
        var result = new TaskCompletionSource<PowerScriptTransformResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakePowerScriptsService
        {
            Catalog = new(
            [
                TestScripts.Create(),
                new PowerScriptInfo { Id = "form", Name = "Form script", Parameters = [TestScripts.Parameter("value")] },
            ],
            true),
            RunHandler = cancellationToken => result.Task.WaitAsync(cancellationToken),
        };
        var destinations = new List<IPage>();
        using var source = new PowerScriptsCatalogSource(service, target =>
        {
            destinations.Add(target);
            ((IContentPage)target).GetContent();
        });
        var catalog = await source.GetCatalogAsync(CancellationToken.None).WaitAsync(WaitTimeout);
        var firstView = source.GetItems(catalog, string.Empty);
        var secondView = source.GetItems(catalog, "script");
        var firstForm = firstView.Single(item => item.Title == "Form script").Command;
        Assert.IsInstanceOfType<IContentPage>(firstForm);
        Assert.AreSame(firstForm, secondView.Single(item => item.Title == "Form script").Command);
        var firstAction = (IInvokableCommand)firstView.Single(item => item.Title == "Test script").Command;
        var secondAction = (IInvokableCommand)secondView.Single(item => item.Title == "Test script").Command;
        Assert.AreSame(firstAction, secondAction);

        firstAction.Invoke(source);
        await service.RunStarted.WaitAsync(WaitTimeout);
        secondAction.Invoke(source);

        Assert.HasCount(2, destinations);
        Assert.AreSame(destinations[0], destinations[1]);
        Assert.AreEqual(1, service.RunCalls);
        var page = (PowerScriptPage)destinations[0];
        Assert.IsTrue(page.IsLoading);
        await CompleteRun(page, () => result.SetResult(new PowerScriptTransformResult { Text = "finished" }));
        AssertStatus(page, Resources.Completed);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(5000)]
    public async Task RefreshMakesAnOldParameterlessActionOpenTheSharedFormWithoutRunning(bool required)
    {
        var initial = TestScripts.Create();
        var service = new FakePowerScriptsService { Catalog = new([initial], true) };
        IPage? destination = null;
        using var source = new PowerScriptsCatalogSource(service, target =>
        {
            destination = target;
            ((IContentPage)target).GetContent();
        });
        var catalog = await source.GetCatalogAsync(CancellationToken.None).WaitAsync(WaitTimeout);
        var originalAction = (IInvokableCommand)source.GetItems(catalog, string.Empty).Single().Command;
        var updated = TestScripts.Create(TestScripts.Parameter("value", required: required, defaultValue: required ? null : "default"));
        service.Catalog = new([updated], true);

        await Refresh(source);
        var updatedPage = source.GetItems(source.Catalog!, string.Empty).Single().Command;
        Assert.IsInstanceOfType<IContentPage>(updatedPage);
        originalAction.Invoke(source);

        Assert.AreSame(updatedPage, destination);
        Assert.AreSame(updatedPage, source.GetItems(source.Catalog!, "script").Single().Command);
        Assert.AreEqual(0, service.RunCalls);

        service.Catalog = new([initial], true);
        await Refresh(source);
        Assert.AreSame(originalAction, source.GetItems(source.Catalog!, string.Empty).Single().Command);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    [Timeout(5000)]
    public async Task AnOldSnapshotMaterializesTheLatestParameterContract(bool required, bool materializeCurrentFirst)
    {
        var initial = TestScripts.Create();
        var service = new FakePowerScriptsService { Catalog = new([initial], true) };
        using var source = new PowerScriptsCatalogSource(service, static _ => { });
        var oldCatalog = await source.GetCatalogAsync(CancellationToken.None).WaitAsync(WaitTimeout);
        var parameter = TestScripts.Parameter("currentValue", required: required, defaultValue: "current default") with
        {
            Description = "Current parameter help",
        };
        var updated = new PowerScriptInfo
        {
            Id = initial.Id,
            Name = "Updated action",
            Description = "Updated metadata",
            Parameters = [parameter],
            Trusted = true,
        };
        service.Catalog = new([updated], true);

        await Refresh(source);
        var currentCatalog = source.Catalog!;
        var firstCurrentCommand = materializeCurrentFirst
            ? source.GetItems(currentCatalog, updated.Name).Single().Command
            : null;
        var oldItem = source.GetItems(oldCatalog, initial.Name).Single();

        Assert.IsInstanceOfType<PowerScriptPage>(oldItem.Command);
        var page = (PowerScriptPage)oldItem.Command;
        page.GetContent();
        Assert.AreSame(page, source.GetItems(currentCatalog, updated.Name).Single().Command);
        if (firstCurrentCommand is not null)
        {
            Assert.AreSame(firstCurrentCommand, page);
        }

        Assert.AreEqual(initial.Name, oldItem.Title);
        Assert.AreEqual(initial.Description, oldItem.Subtitle);
        Assert.AreEqual(updated.Name, page.Title);
        using var data = JsonDocument.Parse(page.ParameterForm.DataJson);
        Assert.AreEqual(updated.Description, data.RootElement.GetProperty("description").GetString());
        var fields = data.RootElement.GetProperty("fields");
        Assert.AreEqual(1, fields.GetArrayLength());
        Assert.AreEqual(parameter.Description, fields[0].GetProperty("description").GetString());
        Assert.AreEqual(parameter.Default, fields[0].GetProperty("value").GetString());
        Assert.AreEqual(0, service.RunCalls);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(5000)]
    public async Task OldSnapshotsFilterDeletedScriptsBeforeLimitingAndHideUnavailableCatalogs(bool discoveryFails)
    {
        var alpha = new PowerScriptInfo { Id = "alpha", Name = "Alpha" };
        var beta = new PowerScriptInfo { Id = "beta", Name = "Beta", Parameters = [TestScripts.Parameter("value")] };
        var service = new FakePowerScriptsService { Catalog = new([beta, alpha], true) };
        using var source = new PowerScriptsCatalogSource(service, static _ => { });
        var oldCatalog = await source.GetCatalogAsync(CancellationToken.None).WaitAsync(WaitTimeout);
        var originalPage = source.GetItems(oldCatalog, beta.Name).Single().Command;
        service.Catalog = new([beta], true);

        await Refresh(source);
        var limited = source.GetItems(oldCatalog, string.Empty, maximumItems: 1);

        Assert.HasCount(1, limited);
        Assert.AreEqual(beta.Name, limited[0].Title);
        Assert.AreSame(originalPage, limited[0].Command);

        if (discoveryFails)
        {
            service.DiscoverHandler = _ => Task.FromException<PowerScriptsCatalog>(new InvalidOperationException("Host unavailable."));
        }
        else
        {
            service.Catalog = new([], false, "integration-disabled");
        }

        await Refresh(source);

        Assert.HasCount(0, source.GetItems(oldCatalog, string.Empty));
        Assert.HasCount(0, source.GetItems(source.Catalog!, string.Empty));
        if (discoveryFails)
        {
            Assert.AreEqual("Host unavailable.", source.Error);
        }
        else
        {
            Assert.IsFalse(source.Catalog!.IsEnabled);
        }

        service.DiscoverHandler = null;
        service.Catalog = new([beta], true);
        await Refresh(source);

        Assert.AreSame(originalPage, source.GetItems(oldCatalog, string.Empty, maximumItems: 1).Single().Command);
        Assert.AreSame(originalPage, source.GetItems(source.Catalog!, beta.Name).Single().Command);
        Assert.AreEqual(0, service.RunCalls);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task DeletingAScriptKeepsItsRunningPageAndReusesItsCommandWhenItReturns()
    {
        var result = new TaskCompletionSource<PowerScriptTransformResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var originalCatalog = new PowerScriptsCatalog([TestScripts.Create()], true);
        var service = new FakePowerScriptsService
        {
            Catalog = originalCatalog,
            RunHandler = cancellationToken => result.Task.WaitAsync(cancellationToken),
        };
        PowerScriptPage? destination = null;
        using var source = new PowerScriptsCatalogSource(service, target =>
        {
            destination = (PowerScriptPage)target;
            destination.GetContent();
        });
        var catalog = await source.GetCatalogAsync(CancellationToken.None).WaitAsync(WaitTimeout);
        var originalAction = (IInvokableCommand)source.GetItems(catalog, string.Empty).Single().Command;
        originalAction.Invoke(source);
        await service.RunStarted.WaitAsync(WaitTimeout);
        var page = destination!;
        service.Catalog = new([], true);

        await Refresh(source);

        Assert.HasCount(0, source.GetItems(source.Catalog!, string.Empty));
        Assert.IsFalse(service.RunCancellationToken.IsCancellationRequested);
        Assert.IsTrue(page.IsLoading);
        await CompleteRun(page, () => result.SetResult(new PowerScriptTransformResult { Text = "finished after deletion" }));
        AssertStatus(page, Resources.Completed);

        service.Catalog = originalCatalog;
        await Refresh(source);

        Assert.AreSame(originalAction, source.GetItems(source.Catalog!, string.Empty).Single().Command);
        Assert.AreEqual(1, service.RunCalls);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task DeletingAnIdleScriptKeepsItsPageUntilTheSourceIsDisposed()
    {
        var originalCatalog = new PowerScriptsCatalog([TestScripts.Create(TestScripts.Parameter("value"))], true);
        var service = new FakePowerScriptsService { Catalog = originalCatalog };
        using var source = new PowerScriptsCatalogSource(service, static _ => { });
        var catalog = await source.GetCatalogAsync(CancellationToken.None).WaitAsync(WaitTimeout);
        var originalPage = source.GetItems(catalog, string.Empty).Single().Command;
        service.Catalog = new([], true);

        await Refresh(source);
        Assert.HasCount(0, source.GetItems(source.Catalog!, string.Empty));
        service.Catalog = originalCatalog;
        await Refresh(source);

        Assert.AreSame(originalPage, source.GetItems(source.Catalog!, string.Empty).Single().Command);
        Assert.AreEqual(0, service.RunCalls);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task DisposingTheSourceCancelsDiscoveryAndRejectsItsLateResult()
    {
        var result = new TaskCompletionSource<PowerScriptsCatalog>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakePowerScriptsService
        {
            DiscoverHandler = cancellationToken =>
            {
                started.TrySetResult(cancellationToken);
                return result.Task;
            },
        };
        using var source = new PowerScriptsCatalogSource(service, static _ => { });
        var query = source.GetCatalogAsync(CancellationToken.None);
        var discoveryCancellation = await started.Task.WaitAsync(WaitTimeout);

        source.Dispose();

        Assert.IsTrue(discoveryCancellation.IsCancellationRequested);
        result.SetResult(new([TestScripts.Create()], true));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () => await query.WaitAsync(WaitTimeout));
        Assert.IsNull(source.Catalog);
        Assert.AreEqual(1, service.DiscoverCalls);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task DisposingTheSourcePreventsAnOldIdleActionFromStartingExecution()
    {
        var result = new TaskCompletionSource<PowerScriptTransformResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakePowerScriptsService
        {
            Catalog = new([TestScripts.Create()], true),
            RunHandler = cancellationToken => result.Task.WaitAsync(cancellationToken),
        };
        PowerScriptPage? destination = null;
        using var source = new PowerScriptsCatalogSource(service, target =>
        {
            destination = (PowerScriptPage)target;
            destination.GetContent();
        });
        var catalog = await source.GetCatalogAsync(CancellationToken.None).WaitAsync(WaitTimeout);
        var action = (IInvokableCommand)source.GetItems(catalog, string.Empty).Single().Command;

        source.Dispose();
        action.Invoke(source);

        Assert.IsNotNull(destination);
        var page = destination!;
        page.ParameterForm.Refresh();
        using var data = JsonDocument.Parse(page.ParameterForm.DataJson);
        Assert.AreEqual(string.Empty, data.RootElement.GetProperty("status").GetString());
        Assert.AreEqual(Resources.Run, data.RootElement.GetProperty("actionTitle").GetString());
        Assert.AreEqual(0, service.RunCalls);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task DisposingTheSourceCancelsItsExistingRun()
    {
        var result = new TaskCompletionSource<PowerScriptTransformResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakePowerScriptsService
        {
            Catalog = new([TestScripts.Create()], true),
            RunHandler = async cancellationToken =>
            {
                using var registration = cancellationToken.Register(() => canceled.TrySetResult());
                return await result.Task.WaitAsync(cancellationToken);
            },
        };
        using var source = new PowerScriptsCatalogSource(service, target => ((IContentPage)target).GetContent());
        var catalog = await source.GetCatalogAsync(CancellationToken.None).WaitAsync(WaitTimeout);
        ((IInvokableCommand)source.GetItems(catalog, string.Empty).Single().Command).Invoke(source);
        await service.RunStarted.WaitAsync(WaitTimeout);

        source.Dispose();
        await canceled.Task.WaitAsync(WaitTimeout);

        Assert.IsTrue(service.RunCancellationToken.IsCancellationRequested);
        Assert.AreEqual(1, service.RunCalls);
    }

    private static async Task Refresh(PowerScriptsCatalogSource source)
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void RefreshCompleted(object? sender, EventArgs args) => completed.TrySetResult();

        source.RefreshCompleted += RefreshCompleted;
        try
        {
            source.Refresh();
            await completed.Task.WaitAsync(WaitTimeout);
        }
        finally
        {
            source.RefreshCompleted -= RefreshCompleted;
        }
    }

    private static async Task CompleteRun(PowerScriptPage page, Action complete)
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void ItemsChanged(object sender, IItemsChangedEventArgs args)
        {
            if (!page.IsLoading)
            {
                completed.TrySetResult();
            }
        }

        page.ItemsChanged += ItemsChanged;
        try
        {
            complete();
            await completed.Task.WaitAsync(WaitTimeout);
        }
        finally
        {
            page.ItemsChanged -= ItemsChanged;
        }
    }

    private static void AssertStatus(PowerScriptPage page, string expected)
    {
        using var data = JsonDocument.Parse(page.ParameterForm.DataJson);
        Assert.AreEqual(expected, data.RootElement.GetProperty("status").GetString());
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _ticks = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).Ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);

        public override long GetTimestamp() => Interlocked.Read(ref _ticks);

        internal void Advance(TimeSpan elapsed) => Interlocked.Add(ref _ticks, elapsed.Ticks);
    }
}
