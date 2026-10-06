// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class ListItemDoubleTapTargetTests
{
    private readonly List<ListItemViewModel> _items = [];

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var item in _items)
        {
            item.SafeCleanup();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ContainerOrNestedContent_ResolvesClickedItemInsteadOfSelection(bool nestedContent)
    {
        var clickedItem = CreateItem(new ListItem(new NoOpCommand()));
        var view = new TestView { SelectedItem = CreateItem(new ListItem(new NoOpCommand())) };
        var container = view.Add(clickedItem);
        TestElement source = nestedContent ? new TestElement { Parent = new TestElement { Parent = container } } : container;

        Assert.AreSame(clickedItem, Resolve(source, view));
    }

    [TestMethod]
    public void SelectionResetAndReordering_StillResolvesClickedContainer()
    {
        var firstItem = CreateItem(new ListItem(new NoOpCommand()));
        var clickedItem = CreateItem(new ListItem(new NoOpCommand()));
        var view = new TestView { SelectedItem = clickedItem };
        view.Add(firstItem);
        var container = view.Add(clickedItem);

        view.Containers.Reverse();
        view.SelectedItem = firstItem;

        Assert.AreSame(clickedItem, Resolve(container, view));
    }

    [TestMethod]
    public void RecycledContainer_ResolvesItsCurrentItem()
    {
        var previousItem = CreateItem(new ListItem(new NoOpCommand()));
        var currentItem = CreateItem(new ListItem(new NoOpCommand()));
        var view = new TestView { SelectedItem = previousItem };
        var container = view.Add(previousItem);
        var source = new TestElement { Parent = container };

        container.Item = currentItem;

        Assert.AreSame(currentItem, Resolve(source, view));
    }

    [TestMethod]
    public void RemovedContainer_DoesNotFallBackToSelection()
    {
        var view = new TestView { SelectedItem = CreateItem(new ListItem(new NoOpCommand())) };
        var container = view.Add(CreateItem(new ListItem(new NoOpCommand())));
        view.Containers.Remove(container);

        Assert.IsNull(Resolve(container, view));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BackgroundOrView_DoesNotActivateSelection(bool background)
    {
        var view = new TestView { SelectedItem = CreateItem(new ListItem(new NoOpCommand())) };
        TestElement source = background ? new TestElement { Parent = view } : view;

        Assert.IsNull(Resolve(source, view));
    }

    [TestMethod]
    public void MissingSource_DoesNotFallBackToSelection()
    {
        var view = new TestView { SelectedItem = CreateItem(new ListItem(new NoOpCommand())) };

        Assert.IsNull(Resolve(null, view));
    }

    [TestMethod]
    public void NestedViewContainer_DoesNotActivateOuterItem()
    {
        var view = new TestView();
        var outerContainer = view.Add(CreateItem(new ListItem(new NoOpCommand())));
        var nestedView = new TestView { Parent = outerContainer };
        var nestedContainer = nestedView.Add(CreateItem(new ListItem(new NoOpCommand())));

        Assert.IsNull(Resolve(new TestElement { Parent = nestedContainer }, view));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("Section")]
    public void StructuralRow_IsNotAnActivationTarget(string section)
    {
        var view = new TestView();
        var item = CreateItem(new Separator(section));
        var container = view.Add(item);

        Assert.IsFalse(item.IsInteractive);
        Assert.IsNull(Resolve(container, view));
    }

    [TestMethod]
    public void SingleClickActivation_DisablesDoubleTapResolution()
    {
        var view = new TestView();
        var container = view.Add(CreateItem(new ListItem(new NoOpCommand())));

        var result = ListItemDoubleTapTarget.Resolve<TestElement, TestContainer>(
            container,
            view,
            singleClickActivates: true,
            _ => throw new AssertFailedException("Single-click mode must not resolve a double-tap target."),
            _ => throw new AssertFailedException("Single-click mode must not resolve a double-tap target."));

        Assert.IsNull(result);
    }

    private static ListItemViewModel? Resolve(TestElement? source, TestView view)
        => ListItemDoubleTapTarget.Resolve<TestElement, TestContainer>(
            source,
            view,
            singleClickActivates: false,
            static element => element.Parent,
            container => view.Containers.Contains(container) ? container.Item : null);

    private ListItemViewModel CreateItem(IListItem model)
    {
        var item = new ListItemViewModel(model, new(new TestPageContext()), DefaultContextMenuFactory.Instance);
        _items.Add(item);
        item.SlowInitializeProperties();
        return item;
    }

    private sealed class TestPageContext : IPageContext
    {
        public TaskScheduler Scheduler => TaskScheduler.Default;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public void ShowException(Exception ex, string? extensionHint = null)
            => throw new AssertFailedException($"Unexpected exception from view model: {ex}");
    }

    private class TestElement
    {
        public TestElement? Parent { get; set; }
    }

    private sealed class TestContainer : TestElement
    {
        public ListItemViewModel? Item { get; set; }
    }

    private sealed class TestView : TestElement
    {
        public ListItemViewModel? SelectedItem { get; set; }

        public List<TestContainer> Containers { get; } = [];

        public TestContainer Add(ListItemViewModel item)
        {
            var container = new TestContainer { Parent = this, Item = item };
            Containers.Add(container);
            return container;
        }
    }
}
