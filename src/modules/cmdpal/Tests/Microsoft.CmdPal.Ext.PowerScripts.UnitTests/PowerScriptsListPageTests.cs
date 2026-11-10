// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.PowerScripts.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;

namespace Microsoft.CmdPal.Ext.PowerScripts.UnitTests;

[TestClass]
public sealed class PowerScriptsListPageTests
{
    private static readonly string[] ExpectedScriptTitles = ["Alpha", "Beta"];

    [TestMethod]
    [Timeout(5000)]
    public async Task DiscoveryAndFilteringUseTheCachedCatalogWithoutExecutingScripts()
    {
        var catalog = new PowerScriptsCatalog(
        [
            new PowerScriptInfo { Id = "beta", Name = "Beta", Description = "Clean the downloads folder" },
            new PowerScriptInfo { Id = "alpha", Name = "Alpha", Description = "Unrelated action" },
        ],
        true);
        var service = new FakePowerScriptsService();
        using var page = new PowerScriptsListPage(service, static _ => { });
        await Discover(page, service, catalog);

        var items = page.GetItems();
        CollectionAssert.AreEqual(ExpectedScriptTitles, items.Select(item => item.Title).ToArray());
        Assert.AreEqual(items[0].Command.Id, page.GetItems()[0].Command.Id);

        page.SearchText = "downloads";
        var filtered = page.GetItems();
        Assert.HasCount(1, filtered);
        Assert.AreEqual("Beta", filtered[0].Title);
        page.SearchText = "ALPHA";
        Assert.AreEqual("Alpha", page.GetItems()[0].Title);
        Assert.AreEqual(1, service.DiscoverCalls);
        Assert.AreEqual(0, service.RunCalls);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(5000)]
    public async Task ScriptsWithDeclaredParametersOpenAFormWithoutRunning(bool required)
    {
        var parameter = TestScripts.Parameter("value", required: required, defaultValue: required ? null : "default");
        var service = new FakePowerScriptsService();
        using var page = new PowerScriptsListPage(service, static _ => { });
        await Discover(page, service, new([TestScripts.Create(parameter)], true));

        var items = page.GetItems();

        Assert.HasCount(1, items);
        Assert.IsInstanceOfType<IContentPage>(items[0].Command);
        var scriptPage = (IContentPage)items[0].Command;
        scriptPage.GetContent();
        scriptPage.GetContent();
        Assert.AreSame(scriptPage, page.GetItems()[0].Command);
        Assert.AreEqual(1, service.DiscoverCalls);
        Assert.AreEqual(0, service.RunCalls);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task ParameterlessRowsOnlyRunWhenTheirPrimaryCommandIsInvoked()
    {
        var service = new FakePowerScriptsService();
        var script = TestScripts.Create();
        IPage? destination = null;
        var navigations = 0;
        using var page = new PowerScriptsListPage(service, target =>
        {
            destination = target;
            navigations++;
            ((IContentPage)target).GetContent();
        });
        await Discover(page, service, new([script], true));

        var item = page.GetItems().Single();
        Assert.IsInstanceOfType<RunPowerScriptCommand>(item.Command);
        Assert.AreEqual(0, service.RunCalls);
        Assert.AreEqual(0, navigations);

        var result = ((IInvokableCommand)item.Command).Invoke(page);
        await service.RunStarted.WaitAsync(TimeSpan.FromSeconds(4));

        Assert.AreEqual(CommandResultKind.KeepOpen, result.Kind);
        Assert.IsInstanceOfType<IContentPage>(destination);
        ((IContentPage)destination!).GetContent();
        ((IContentPage)destination!).GetContent();
        page.SearchText = script.Name;
        page.GetItems();
        Assert.AreEqual(1, navigations);
        Assert.AreEqual(1, service.RunCalls);
        Assert.AreSame(script, service.LastScript);
        Assert.HasCount(0, service.LastParameters!);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task EmptyCatalogAndNoMatchingScriptsHaveDifferentEmptyStates()
    {
        var service = new FakePowerScriptsService();
        using var emptyPage = new PowerScriptsListPage(service, static _ => { });
        await Discover(emptyPage, service, new([], true));

        Assert.HasCount(0, emptyPage.GetItems());
        Assert.AreEqual(Resources.NoScripts, emptyPage.EmptyContent!.Title);

        var populatedService = new FakePowerScriptsService();
        using var populatedPage = new PowerScriptsListPage(populatedService, static _ => { });
        await Discover(populatedPage, populatedService, new([TestScripts.Create()], true));
        populatedPage.SearchText = "there is no matching script";

        Assert.HasCount(0, populatedPage.GetItems());
        Assert.AreEqual(Resources.NoMatches, populatedPage.EmptyContent!.Title);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(5000)]
    public async Task DisabledCatalogShowsTheConsumerReasonAndPolicyState(bool policyControlled)
    {
        var service = new FakePowerScriptsService();
        using var page = new PowerScriptsListPage(service, static _ => { });
        await Discover(page, service, new([], false, "command-palette-disabled", policyControlled));

        Assert.HasCount(0, page.GetItems());
        Assert.AreEqual(policyControlled ? Resources.DisabledByPolicy : Resources.Disabled, page.EmptyContent!.Title);
        Assert.AreEqual(policyControlled ? Resources.IntegrationPolicyDisabledHint : Resources.IntegrationDisabledHint, page.EmptyContent!.Subtitle);
        Assert.AreEqual(0, service.RunCalls);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task FailedDiscoveryCanBeRetriedFromTheEmptyState()
    {
        var service = new FakePowerScriptsService();
        using var page = new PowerScriptsListPage(service, static _ => { });
        await Discover(page, service, new([], true), failure: new InvalidOperationException("Host unavailable."));

        Assert.HasCount(0, page.GetItems());
        Assert.AreEqual(Resources.DiscoveryFailed, page.EmptyContent!.Title);
        Assert.AreEqual("Host unavailable.", page.EmptyContent!.Subtitle);
        var retry = (IInvokableCommand)page.EmptyContent!.Command;

        await Discover(page, service, new([TestScripts.Create()], true), () => retry.Invoke(page));

        Assert.HasCount(1, page.GetItems());
        Assert.AreEqual(2, service.DiscoverCalls);
        Assert.IsNull(page.EmptyContent);
        Assert.IsFalse(page.IsLoading);
        Assert.AreEqual(0, service.RunCalls);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task RefreshUpdatesStableScriptPagesAndRemovesDeletedScriptsFromTheList()
    {
        var service = new FakePowerScriptsService();
        using var page = new PowerScriptsListPage(service, static _ => { });
        var parameter = TestScripts.Parameter("value");
        await Discover(page, service, new([TestScripts.Create(parameter)], true));
        var originalItems = page.GetItems();
        var scriptPage = originalItems[0].Command;
        var refreshItem = originalItems[0].MoreCommands.OfType<ICommandContextItem>().Single(item => item.Title == Resources.Refresh);
        var refresh = (IInvokableCommand)refreshItem.Command;
        var updated = new PowerScriptInfo { Id = "test.script", Name = "Renamed script", Description = "Updated metadata", Parameters = [parameter] };

        Assert.HasCount(1, originalItems);
        Assert.IsFalse(originalItems.Any(item => item.Title == Resources.Refresh));

        await Discover(page, service, new([updated], true), () => refresh.Invoke(page));
        var updatedItems = page.GetItems();
        Assert.AreSame(scriptPage, updatedItems[0].Command);
        Assert.AreEqual("Renamed script", updatedItems[0].Title);
        Assert.AreEqual("Updated metadata", updatedItems[0].Subtitle);

        await Discover(page, service, new([], true), () => refresh.Invoke(page));
        Assert.HasCount(0, page.GetItems());
        Assert.AreEqual(Resources.NoScripts, page.EmptyContent!.Title);
        Assert.AreEqual(0, service.RunCalls);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task RefreshingAParameterlessScriptMakesItsCachedRunActionOpenTheNewFormWithoutRunning()
    {
        var service = new FakePowerScriptsService();
        IPage? destination = null;
        using var page = new PowerScriptsListPage(service, target => destination = target);
        await Discover(page, service, new([TestScripts.Create()], true));
        var originalItem = page.GetItems().Single();
        var originalAction = (RunPowerScriptCommand)originalItem.Command;
        var refresh = (IInvokableCommand)originalItem.MoreCommands.OfType<ICommandContextItem>().Single(item => item.Title == Resources.Refresh).Command;
        var updated = TestScripts.Create(TestScripts.Parameter("value", required: true));

        await Discover(page, service, new([updated], true), () => refresh.Invoke(page));
        var updatedItem = page.GetItems().Single();
        Assert.IsInstanceOfType<IContentPage>(updatedItem.Command);

        originalAction.Invoke();

        Assert.AreSame(updatedItem.Command, destination);
        ((IContentPage)destination!).GetContent();
        Assert.AreEqual(0, service.RunCalls);
    }

    private static async Task Discover(
        PowerScriptsListPage page,
        FakePowerScriptsService service,
        PowerScriptsCatalog catalog,
        Action? trigger = null,
        Exception? failure = null)
    {
        var result = new TaskCompletionSource<PowerScriptsCatalog>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var previousCalls = service.DiscoverCalls;
        service.DiscoverHandler = cancellationToken =>
        {
            started.TrySetResult();
            return result.Task.WaitAsync(cancellationToken);
        };

        void ItemsChanged(object sender, IItemsChangedEventArgs args)
        {
            if (!page.IsLoading && service.DiscoverCalls > previousCalls)
            {
                completed.TrySetResult();
            }
        }

        page.ItemsChanged += ItemsChanged;
        try
        {
            if (trigger is null)
            {
                page.GetItems();
            }
            else
            {
                trigger();
            }

            await started.Task.WaitAsync(TimeSpan.FromSeconds(4));
            if (failure is null)
            {
                result.SetResult(catalog);
            }
            else
            {
                result.SetException(failure);
            }

            await completed.Task.WaitAsync(TimeSpan.FromSeconds(4));
        }
        finally
        {
            page.ItemsChanged -= ItemsChanged;
        }
    }
}
