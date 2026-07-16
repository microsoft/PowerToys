// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class TabbedPageViewModelTests
{
    private sealed partial class TestAppExtensionHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Test Host";
    }

    private sealed partial class TestTabbedPage : TabbedPage
    {
        private ITab[] _tabs;

        public TestTabbedPage(ITab[] tabs)
        {
            _tabs = tabs;
        }

        public override ITab[] GetTabs() => _tabs;

        public void SetTabs(ITab[] tabs)
        {
            _tabs = tabs;
            RaiseItemsChanged(tabs.Length);
        }
    }

    private sealed partial class TestListPage : ListPage
    {
        public TestListPage(string id)
        {
            Id = id;
            Name = id;
            Title = id;
        }

        public override IListItem[] GetItems() => [new ListItem(new NoOpCommand() { Name = "Item" })];
    }

    private sealed partial class TestContentPage : ContentPage
    {
        public TestContentPage(string id)
        {
            Id = id;
            Name = id;
            Title = id;
        }

        public override IContent[] GetContent() => [];
    }

    private static CommandPalettePageViewModelFactory CreateFactory() =>
        new(TaskScheduler.Default, DefaultContextMenuFactory.Instance);

    private static TabbedPageViewModel CreateViewModel(TestTabbedPage page) =>
        new(page, TaskScheduler.Default, new TestAppExtensionHost(), CommandProviderContext.Empty, CreateFactory());

    private static async Task WaitFor(Func<bool> condition, string message, int timeoutMs = 4000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
        {
            await Task.Delay(15);
        }

        Assert.IsTrue(condition(), message);
    }

    [TestMethod]
    public async Task InitializeProperties_BuildsTabsAndSelectsFirst()
    {
        var page = new TestTabbedPage(
        [
            new Tab("Issues", new TestListPage("issues")),
            new Tab("Docs", new TestContentPage("docs")),
        ]);

        var viewModel = CreateViewModel(page);
        viewModel.InitializeProperties();

        await WaitFor(() => viewModel.Tabs.Count == 2 && viewModel.SelectedTab is not null, "Tabs did not populate");

        Assert.IsTrue(viewModel.HasTabs);
        Assert.AreEqual(2, viewModel.Tabs.Count);
        Assert.AreSame(viewModel.Tabs[0], viewModel.SelectedTab);
        Assert.AreEqual("Issues", viewModel.Tabs[0].Title);
        Assert.AreEqual("Docs", viewModel.Tabs[1].Title);

        viewModel.SafeCleanup();
    }

    [TestMethod]
    public async Task HasSearchBox_TrueWhenAnyTabIsListPage()
    {
        var page = new TestTabbedPage(
        [
            new Tab("Docs", new TestContentPage("docs")),
            new Tab("Issues", new TestListPage("issues")),
        ]);

        var viewModel = CreateViewModel(page);
        viewModel.InitializeProperties();

        await WaitFor(() => viewModel.Tabs.Count == 2, "Tabs did not populate");
        Assert.IsTrue(viewModel.HasSearchBox);

        viewModel.SafeCleanup();
    }

    [TestMethod]
    public async Task HasSearchBox_FalseWhenNoTabIsSearchable()
    {
        var page = new TestTabbedPage(
        [
            new Tab("Docs", new TestContentPage("docs")),
            new Tab("About", new TestContentPage("about")),
        ]);

        var viewModel = CreateViewModel(page);
        viewModel.InitializeProperties();

        await WaitFor(() => viewModel.Tabs.Count == 2, "Tabs did not populate");
        Assert.IsFalse(viewModel.HasSearchBox);

        viewModel.SafeCleanup();
    }

    [TestMethod]
    public async Task FirstTab_LazilyCreatesActiveChild()
    {
        var page = new TestTabbedPage(
        [
            new Tab("Issues", new TestListPage("issues")),
            new Tab("Docs", new TestContentPage("docs")),
        ]);

        var viewModel = CreateViewModel(page);
        viewModel.InitializeProperties();

        await WaitFor(() => viewModel.ActiveChild is not null, "Active child was not created");

        Assert.IsInstanceOfType(viewModel.ActiveChild, typeof(ListViewModel));
        Assert.IsFalse(viewModel.ShowUnsupportedPlaceholder);

        viewModel.SafeCleanup();
    }

    [TestMethod]
    public async Task UnsupportedTab_YieldsNoActiveChildAndShowsPlaceholder()
    {
        var page = new TestTabbedPage(
        [
            new Tab("Bare", new Page() { Id = "bare", Name = "Bare", Title = "Bare" }),
        ]);

        var viewModel = CreateViewModel(page);
        viewModel.InitializeProperties();

        await WaitFor(() => viewModel.SelectedTab is not null, "Tab was not selected");

        // Give ActivateTab a chance to resolve the (null) child.
        await WaitFor(() => viewModel.ShowUnsupportedPlaceholder, "Placeholder was not shown for unsupported tab");
        Assert.IsNull(viewModel.ActiveChild);

        viewModel.SafeCleanup();
    }

    [TestMethod]
    public async Task Badge_PropChangedPropagatesToTabViewModel()
    {
        var tab = new Tab("Issues", new TestContentPage("issues"));
        var page = new TestTabbedPage([tab]);

        var viewModel = CreateViewModel(page);
        viewModel.InitializeProperties();

        await WaitFor(() => viewModel.Tabs.Count == 1, "Tabs did not populate");
        Assert.IsFalse(viewModel.Tabs[0].HasBadge);

        tab.Badge = "10";

        await WaitFor(() => viewModel.Tabs[0].Badge == "10", "Badge did not propagate");
        Assert.IsTrue(viewModel.Tabs[0].HasBadge);

        viewModel.SafeCleanup();
    }

    [TestMethod]
    public async Task SearchText_ForwardsToActiveListTab()
    {
        var page = new TestTabbedPage(
        [
            new Tab("Issues", new TestListPage("issues")),
        ]);

        var viewModel = CreateViewModel(page);
        viewModel.InitializeProperties();

        await WaitFor(() => viewModel.ActiveChild is ListViewModel, "List child was not created");

        var listChild = (ListViewModel)viewModel.ActiveChild!;
        viewModel.SearchTextBox = "bug";

        await WaitFor(() => listChild.SearchTextBox == "bug", "Search text was not forwarded");

        viewModel.SafeCleanup();
    }

    [TestMethod]
    public async Task ItemsChanged_PreservesActiveTabById()
    {
        var page = new TestTabbedPage(
        [
            new Tab("Issues", new TestListPage("issues")),
            new Tab("Docs", new TestContentPage("docs")),
        ]);

        var viewModel = CreateViewModel(page);
        viewModel.InitializeProperties();

        await WaitFor(() => viewModel.Tabs.Count == 2 && viewModel.SelectedTab is not null, "Tabs did not populate");

        viewModel.SelectedTab = viewModel.Tabs[1];
        Assert.AreEqual("docs", viewModel.SelectedTab!.TabId);

        // Dynamic update: the extension re-publishes the tabs (new instances) and
        // adds a third. The active tab identity ("docs") must be preserved.
        page.SetTabs(
        [
            new Tab("Issues", new TestListPage("issues")),
            new Tab("Docs", new TestContentPage("docs")),
            new Tab("Actions", new TestContentPage("actions")),
        ]);

        await WaitFor(() => viewModel.Tabs.Count == 3, "Tabs did not update");
        await WaitFor(() => viewModel.SelectedTab?.TabId == "docs", "Active tab was not preserved");

        viewModel.SafeCleanup();
    }
}
