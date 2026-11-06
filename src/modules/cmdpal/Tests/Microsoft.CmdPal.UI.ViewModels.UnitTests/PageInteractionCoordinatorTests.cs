// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.System;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed partial class PageInteractionCoordinatorTests
{
    private readonly List<PageViewModel> _pages = [];
    private readonly List<CommandItemViewModel> _selections = [];

    [TestCleanup]
    public async Task CleanupAsync()
    {
        foreach (var selection in _selections)
        {
            selection.SafeCleanup();
        }

        foreach (var page in _pages)
        {
            await page.CleanupAsync();
        }
    }

    private sealed partial class TestAppExtensionHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Test Host";
    }

    private sealed class TestCommandBar : ICommandBarInteractionTarget
    {
        public List<ICommandBarContext?> Contexts { get; } = [];

        public int OpenCount { get; private set; }

        public int CloseCount { get; private set; }

        public List<(IContextMenuContext Context, CommandContextItemViewModel Command, Func<bool> IsContextCurrent)> Invocations { get; } = [];

        public void SetCommandContext(ICommandBarContext? context) => Contexts.Add(context);

        public void OpenContextMenu() => OpenCount++;

        public void CloseContextMenu() => CloseCount++;

        public void InvokeContextCommand(IContextMenuContext context, CommandContextItemViewModel command, Func<bool> isContextCurrent) => Invocations.Add((context, command, isContextCurrent));
    }

    private sealed partial class TestMenuPage : Page, IExtendedAttributesProvider
    {
        public TestMenuPage(IContextItem[] commands) => MenuCommands = commands;
    }

    private sealed class TestPageTarget : IPageInteractionTarget, IPageInteractionEventSource
    {
        public event EventHandler? ContextMenuCloseRequested;

        public event EventHandler? FocusSearchRequested;

        public event EventHandler<PageDragStateChangedEventArgs>? DragStateChanged;

        public int PreviousCount { get; private set; }

        public int NextCount { get; private set; }

        public int LeftCount { get; private set; }

        public int RightCount { get; private set; }

        public int PageUpCount { get; private set; }

        public int PageDownCount { get; private set; }

        public int PrimaryCount { get; private set; }

        public int SecondaryCount { get; private set; }

        public void NavigatePrevious() => PreviousCount++;

        public void NavigateNext() => NextCount++;

        public void NavigateLeft() => LeftCount++;

        public void NavigateRight() => RightCount++;

        public void NavigatePageUp() => PageUpCount++;

        public void NavigatePageDown() => PageDownCount++;

        public void ActivatePrimary() => PrimaryCount++;

        public void ActivateSecondary() => SecondaryCount++;

        public void RequestContextMenuClose() => ContextMenuCloseRequested?.Invoke(this, EventArgs.Empty);

        public void RequestSearchFocus() => FocusSearchRequested?.Invoke(this, EventArgs.Empty);

        public void SetDragging(bool isDragging) => DragStateChanged?.Invoke(this, new(isDragging));
    }

    [TestMethod]
    public void TwoPageHosts_UpdateOnlyTheirOwningCommandBar()
    {
        var pageA = CreatePage();
        var pageB = CreatePage();
        var barA = new TestCommandBar();
        var barB = new TestCommandBar();
        using var hostA = new PageInteractionCoordinator(barA);
        using var hostB = new PageInteractionCoordinator(barB);
        hostA.AttachPage(pageA);
        hostB.AttachPage(pageB);
        barA.Contexts.Clear();
        barB.Contexts.Clear();
        var contextA = Mock.Of<ICommandBarContext>();
        var contextB = Mock.Of<ICommandBarContext>();

        pageA.SetCommandBarContext(contextA);
        pageB.SetCommandBarContext(contextB);

        CollectionAssert.AreEqual(new[] { contextA }, barA.Contexts);
        CollectionAssert.AreEqual(new[] { contextB }, barB.Contexts);
    }

    [TestMethod]
    public void SuggestionsAndParameterFocus_StayWithTheirOwningPage()
    {
        var pageA = CreatePage();
        var pageB = CreatePage();
        using var hostA = new PageInteractionCoordinator(new TestCommandBar());
        using var hostB = new PageInteractionCoordinator(new TestCommandBar());
        hostA.AttachPage(pageA);
        hostB.AttachPage(pageB);
        var suggestionsA = new List<string>();
        var suggestionsB = new List<string>();
        var focusA = 0;
        var focusB = 0;
        hostA.SearchSuggestionChanged += (_, e) => suggestionsA.Add(e.Suggestion);
        hostB.SearchSuggestionChanged += (_, e) => suggestionsB.Add(e.Suggestion);
        hostA.ParameterFocusRequested += (_, _) => focusA++;
        hostB.ParameterFocusRequested += (_, _) => focusB++;

        pageA.SetSearchSuggestion("alpha");
        pageA.RequestParameterFocus(null!);

        Assert.AreEqual(1, suggestionsA.Count);
        Assert.AreEqual("alpha", suggestionsA[0]);
        Assert.AreEqual(1, focusA);
        Assert.AreEqual(0, suggestionsB.Count);
        Assert.AreEqual(0, focusB);
    }

    [TestMethod]
    public void KeyboardNavigationAndActivation_ReachOnlyTheActivePage()
    {
        using var host = new PageInteractionCoordinator(new TestCommandBar());
        var oldTarget = new TestPageTarget();
        var activeTarget = new TestPageTarget();
        host.AttachTarget(oldTarget);
        host.AttachTarget(activeTarget);

        host.NavigatePrevious();
        host.NavigateNext();
        host.NavigateLeft();
        host.NavigateRight();
        host.NavigatePageUp();
        host.NavigatePageDown();
        host.ActivatePrimary();
        host.ActivateSecondary();

        Assert.AreEqual(0, oldTarget.NextCount);
        Assert.AreEqual(1, activeTarget.PreviousCount);
        Assert.AreEqual(1, activeTarget.NextCount);
        Assert.AreEqual(1, activeTarget.LeftCount);
        Assert.AreEqual(1, activeTarget.RightCount);
        Assert.AreEqual(1, activeTarget.PageUpCount);
        Assert.AreEqual(1, activeTarget.PageDownCount);
        Assert.AreEqual(1, activeTarget.PrimaryCount);
        Assert.AreEqual(1, activeTarget.SecondaryCount);
    }

    [TestMethod]
    public void ContextMenuOperations_ReachOnlyTheOwningCommandBar()
    {
        var barA = new TestCommandBar();
        var barB = new TestCommandBar();
        using var hostA = new PageInteractionCoordinator(barA);
        using var hostB = new PageInteractionCoordinator(barB);
        var targetA = new TestPageTarget();
        var targetB = new TestPageTarget();
        hostA.AttachPage(CreatePageWithMenu(CreateMenuCommand("Page A")));
        hostB.AttachPage(CreatePageWithMenu(CreateMenuCommand("Page B")));
        hostA.AttachTarget(targetA);
        hostB.AttachTarget(targetB);

        hostA.OpenContextMenu();
        targetA.RequestContextMenuClose();
        Assert.IsTrue(TryShortcut(hostA, VirtualKey.S, VirtualKeyModifiers.Control));

        Assert.AreEqual(1, barA.OpenCount);
        Assert.AreEqual(1, barA.CloseCount);
        Assert.AreEqual(0, barB.OpenCount);
        Assert.AreEqual(0, barB.CloseCount);
        Assert.AreEqual(1, barA.Invocations.Count);
        Assert.AreEqual("Page A", barA.Invocations[0].Command.Title);
        Assert.AreEqual(0, barB.Invocations.Count);

        Assert.IsTrue(TryShortcut(hostB, VirtualKey.S, VirtualKeyModifiers.Control));
        Assert.AreEqual(1, barA.Invocations.Count);
        Assert.AreEqual(1, barB.Invocations.Count);
        Assert.AreEqual("Page B", barB.Invocations[0].Command.Title);
    }

    [TestMethod]
    public void Shortcut_SelectedItemWinsOverPageMenuWithoutChangingCommandContext()
    {
        var page = CreatePageWithMenu(CreateMenuCommand("Page settings"));
        var selection = CreateSelection(page, CreateMenuCommand("Item settings"));
        var bar = new TestCommandBar();
        using var host = new PageInteractionCoordinator(bar);
        host.AttachPage(page);
        page.SetCommandBarContext(selection);
        bar.Contexts.Clear();

        Assert.IsTrue(TryShortcut(host, VirtualKey.S, VirtualKeyModifiers.Control));

        Assert.AreEqual(1, bar.Invocations.Count);
        Assert.AreSame(selection, bar.Invocations[0].Context);
        Assert.AreEqual("Item settings", bar.Invocations[0].Command.Title);
        Assert.AreSame(selection, host.CurrentCommandContext);
        Assert.AreEqual(0, bar.Contexts.Count);
    }

    [TestMethod]
    public void Shortcut_PageMenuRemainsAvailableWithNoSelection()
    {
        var page = CreatePageWithMenu(CreateMenuCommand("Page settings"));
        var bar = new TestCommandBar();
        using var host = new PageInteractionCoordinator(bar);
        host.AttachPage(page);
        bar.Contexts.Clear();

        Assert.IsTrue(TryShortcut(host, VirtualKey.S, VirtualKeyModifiers.Control));

        Assert.AreEqual(1, bar.Invocations.Count);
        Assert.AreSame(page.PageMenu, bar.Invocations[0].Context);
        Assert.AreSame(page.PageMenu.AllCommands[0], bar.Invocations[0].Command);
        Assert.IsNull(host.CurrentCommandContext);
        Assert.AreEqual(0, bar.Contexts.Count);
    }

    [TestMethod]
    public void Shortcut_PageMenuIsFallbackWhenSelectedItemDoesNotMatch()
    {
        var page = CreatePageWithMenu(CreateMenuCommand("Page settings"));
        var selection = CreateSelection(page, CreateMenuCommand("Other action", VirtualKey.O));
        var bar = new TestCommandBar();
        using var host = new PageInteractionCoordinator(bar);
        host.AttachPage(page);
        page.SetCommandBarContext(selection);

        Assert.IsTrue(TryShortcut(host, VirtualKey.S, VirtualKeyModifiers.Control));

        Assert.AreEqual(1, bar.Invocations.Count);
        Assert.AreSame(page.PageMenu, bar.Invocations[0].Context);
        Assert.AreEqual("Page settings", bar.Invocations[0].Command.Title);
        Assert.AreSame(selection, host.CurrentCommandContext);
    }

    [DataTestMethod]
    [DataRow(VirtualKey.K, VirtualKeyModifiers.Control)]
    [DataRow(VirtualKey.Shift, VirtualKeyModifiers.Shift)]
    [DataRow(VirtualKey.LeftShift, VirtualKeyModifiers.Shift | VirtualKeyModifiers.Control)]
    [DataRow(VirtualKey.RightShift, VirtualKeyModifiers.Shift | VirtualKeyModifiers.Control)]
    [DataRow(VirtualKey.Control, VirtualKeyModifiers.Control)]
    [DataRow(VirtualKey.LeftControl, VirtualKeyModifiers.Control)]
    [DataRow(VirtualKey.RightControl, VirtualKeyModifiers.Control)]
    [DataRow(VirtualKey.Menu, VirtualKeyModifiers.Menu)]
    [DataRow(VirtualKey.LeftMenu, VirtualKeyModifiers.Menu)]
    [DataRow(VirtualKey.RightMenu, VirtualKeyModifiers.Menu)]
    [DataRow(VirtualKey.LeftWindows, VirtualKeyModifiers.Windows)]
    [DataRow(VirtualKey.RightWindows, VirtualKeyModifiers.Windows)]
    [DataRow(VirtualKey.Enter, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.Enter, VirtualKeyModifiers.Control)]
    [DataRow(VirtualKey.Escape, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.Back, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.Tab, VirtualKeyModifiers.Shift)]
    [DataRow(VirtualKey.Up, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.Down, VirtualKeyModifiers.Menu)]
    [DataRow(VirtualKey.Left, VirtualKeyModifiers.Control)]
    [DataRow(VirtualKey.Right, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.Home, VirtualKeyModifiers.Menu)]
    [DataRow(VirtualKey.End, VirtualKeyModifiers.Control)]
    [DataRow(VirtualKey.PageUp, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.PageDown, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.Insert, VirtualKeyModifiers.Control)]
    [DataRow(VirtualKey.Delete, VirtualKeyModifiers.Shift)]
    [DataRow(VirtualKey.A, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.A, VirtualKeyModifiers.Shift)]
    [DataRow(VirtualKey.A, VirtualKeyModifiers.Control)]
    [DataRow(VirtualKey.C, VirtualKeyModifiers.Control)]
    [DataRow(VirtualKey.V, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift)]
    [DataRow(VirtualKey.X, VirtualKeyModifiers.Control)]
    [DataRow(VirtualKey.Y, VirtualKeyModifiers.Control)]
    [DataRow(VirtualKey.Z, VirtualKeyModifiers.Control)]
    [DataRow(VirtualKey.F, VirtualKeyModifiers.Menu)]
    [DataRow(VirtualKeyExtensions.OemCommaKey, VirtualKeyModifiers.Control)]
    [DataRow(VirtualKey.Q, VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu)]
    [DataRow(VirtualKey.Q, VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu | VirtualKeyModifiers.Shift)]
    [DataRow(VirtualKey.Application, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.Application, VirtualKeyModifiers.Control)]
    [DataRow(VirtualKey.F10, VirtualKeyModifiers.Shift)]
    [DataRow(VirtualKey.F4, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.F4, VirtualKeyModifiers.Menu)]
    [DataRow(VirtualKey.F4, VirtualKeyModifiers.Control)]
    [DataRow(VirtualKey.Space, VirtualKeyModifiers.Menu)]
    [DataRow(VirtualKey.NumberPad0, VirtualKeyModifiers.Menu)]
    [DataRow(VirtualKey.NumberPad5, VirtualKeyModifiers.Menu)]
    [DataRow(VirtualKey.NumberPad9, VirtualKeyModifiers.Menu)]
    [DataRow(VirtualKey.NumberPad5, VirtualKeyModifiers.Menu | VirtualKeyModifiers.Shift)]
    public void Shortcut_PageMenuCannotOverrideHostNavigationOrTextInput(VirtualKey key, VirtualKeyModifiers modifiers)
    {
        var page = CreatePageWithMenu(CreateMenuCommand("Page action", key, modifiers));
        var bar = new TestCommandBar();
        using var host = new PageInteractionCoordinator(bar);
        host.AttachPage(page);

        Assert.IsFalse(TryShortcut(host, key, modifiers));

        Assert.AreEqual(0, bar.Invocations.Count);
    }

    [DataTestMethod]
    [DataRow(VirtualKey.S, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift)]
    [DataRow(VirtualKey.K, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift)]
    [DataRow(VirtualKeyExtensions.OemCommaKey, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift)]
    [DataRow(VirtualKey.S, VirtualKeyModifiers.Menu)]
    [DataRow(VirtualKey.S, VirtualKeyModifiers.Windows)]
    [DataRow(VirtualKey.F, VirtualKeyModifiers.Menu | VirtualKeyModifiers.Shift)]
    [DataRow(VirtualKey.Space, VirtualKeyModifiers.Menu | VirtualKeyModifiers.Shift)]
    [DataRow(VirtualKey.F1, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.F5, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.F5, VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu)]
    [DataRow(VirtualKey.F10, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.F10, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift)]
    [DataRow(VirtualKey.F24, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.NumberPad5, VirtualKeyModifiers.Control)]
    [DataRow(VirtualKey.NumberPad5, VirtualKeyModifiers.Menu | VirtualKeyModifiers.Windows)]
    public void Shortcut_PageMenuAcceptsCommandChords(VirtualKey key, VirtualKeyModifiers modifiers)
    {
        var page = CreatePageWithMenu(CreateMenuCommand("Page action", key, modifiers));
        var bar = new TestCommandBar();
        using var host = new PageInteractionCoordinator(bar);
        host.AttachPage(page);

        Assert.IsTrue(TryShortcut(host, key, modifiers));
        Assert.AreEqual(1, bar.Invocations.Count);
        Assert.AreSame(page.PageMenu, bar.Invocations[0].Context);
    }

    [DataTestMethod]
    [DataRow(VirtualKey.Escape, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.C, VirtualKeyModifiers.Control)]
    [DataRow(VirtualKey.Application, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.F10, VirtualKeyModifiers.Shift)]
    [DataRow(VirtualKey.F4, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.F4, VirtualKeyModifiers.Menu)]
    [DataRow(VirtualKey.Space, VirtualKeyModifiers.Menu)]
    [DataRow(VirtualKey.NumberPad5, VirtualKeyModifiers.Menu)]
    public void Shortcut_PageScopeRestrictionsDoNotChangeSelectedItemBindings(VirtualKey key, VirtualKeyModifiers modifiers)
    {
        var page = CreatePageWithMenu(CreateMenuCommand("Page action", key, modifiers));
        var selection = CreateSelection(page, CreateMenuCommand("Item action", key, modifiers));
        var bar = new TestCommandBar();
        using var host = new PageInteractionCoordinator(bar);
        host.AttachPage(page);
        page.SetCommandBarContext(selection);

        Assert.IsTrue(TryShortcut(host, key, modifiers));
        Assert.AreEqual(1, bar.Invocations.Count);
        Assert.AreSame(selection, bar.Invocations[0].Context);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Shortcut_DeferredInvocationChecksLiveContext(bool pageMenu)
    {
        var page = CreatePageWithMenu(CreateMenuCommand("Page action"));
        var selection = CreateSelection(page, CreateMenuCommand("Item action", pageMenu ? VirtualKey.O : VirtualKey.S));
        var bar = new TestCommandBar();
        using var host = new PageInteractionCoordinator(bar);
        host.AttachPage(page);
        page.SetCommandBarContext(selection);

        Assert.IsTrue(TryShortcut(host, VirtualKey.S, VirtualKeyModifiers.Control));
        var invocation = bar.Invocations[0];
        Assert.IsTrue(invocation.IsContextCurrent());

        page.SetCommandBarContext(CreateSelection(page, CreateMenuCommand("Replacement")));
        Assert.AreEqual(pageMenu, invocation.IsContextCurrent());

        host.AttachPage(CreatePage());
        Assert.IsFalse(invocation.IsContextCurrent());
    }

    [TestMethod]
    public void Shortcut_SelectedItemCtrlKRemainsAvailable()
    {
        var page = CreatePageWithMenu(CreateMenuCommand("Page action", VirtualKey.K));
        var selection = CreateSelection(page, CreateMenuCommand("Item action", VirtualKey.K));
        var bar = new TestCommandBar();
        using var host = new PageInteractionCoordinator(bar);
        host.AttachPage(page);
        page.SetCommandBarContext(selection);

        Assert.IsTrue(TryShortcut(host, VirtualKey.K, VirtualKeyModifiers.Control));

        Assert.AreEqual(1, bar.Invocations.Count);
        Assert.AreSame(selection, bar.Invocations[0].Context);
        Assert.AreEqual("Item action", bar.Invocations[0].Command.Title);
    }

    [TestMethod]
    public void Shortcut_PageSubmenuIsDeliveredWithItsPageMenuContext()
    {
        var submenu = CreateMenuCommand("Page tools");
        submenu.MoreCommands = [CreateMenuCommand("Nested action", VirtualKey.N)];
        var page = CreatePageWithMenu(submenu);
        var bar = new TestCommandBar();
        using var host = new PageInteractionCoordinator(bar);
        host.AttachPage(page);

        Assert.IsTrue(TryShortcut(host, VirtualKey.S, VirtualKeyModifiers.Control));

        Assert.AreEqual(1, bar.Invocations.Count);
        Assert.AreSame(page.PageMenu, bar.Invocations[0].Context);
        Assert.IsTrue(bar.Invocations[0].Command.HasSubmenu);
        Assert.IsFalse(TryShortcut(host, VirtualKey.N, VirtualKeyModifiers.Control));
        Assert.AreEqual(1, bar.Invocations.Count);
    }

    [TestMethod]
    public void Shortcut_DetachedPageAndSelectionAreNoLongerAvailable()
    {
        var oldPage = CreatePageWithMenu(CreateMenuCommand("Old page settings"));
        var oldSelection = CreateSelection(oldPage, CreateMenuCommand("Old item action", VirtualKey.O));
        var bar = new TestCommandBar();
        using var host = new PageInteractionCoordinator(bar);
        host.AttachPage(oldPage);
        oldPage.SetCommandBarContext(oldSelection);
        host.AttachPage(CreatePage());

        Assert.IsFalse(TryShortcut(host, VirtualKey.S, VirtualKeyModifiers.Control));
        Assert.IsFalse(TryShortcut(host, VirtualKey.O, VirtualKeyModifiers.Control));

        host.AttachPage(null);
        oldPage.SetCommandBarContext(oldSelection);

        Assert.IsFalse(TryShortcut(host, VirtualKey.S, VirtualKeyModifiers.Control));
        Assert.AreEqual(0, bar.Invocations.Count);
    }

    [TestMethod]
    public void StalePageCallbacks_AreIgnoredAfterNavigation()
    {
        var oldPage = CreatePage();
        var currentPage = CreatePage();
        var bar = new TestCommandBar();
        using var host = new PageInteractionCoordinator(bar);
        var suggestions = new List<string>();
        var detailsChanges = 0;
        host.SearchSuggestionChanged += (_, e) => suggestions.Add(e.Suggestion);
        host.DetailsChanged += (_, _) => detailsChanges++;
        host.AttachPage(oldPage);
        host.AttachPage(currentPage);
        bar.Contexts.Clear();
        suggestions.Clear();
        detailsChanges = 0;

        oldPage.SetCommandBarContext(Mock.Of<ICommandBarContext>());
        oldPage.SetSearchSuggestion("stale");
        oldPage.SetDetails(null);

        Assert.AreEqual(0, bar.Contexts.Count);
        Assert.AreEqual(0, suggestions.Count);
        Assert.AreEqual(0, detailsChanges);
    }

    [TestMethod]
    public void CurrentCommandContext_FollowsTheActivePageImmediately()
    {
        var oldPage = CreatePage();
        var currentPage = CreatePage();
        using var host = new PageInteractionCoordinator(new TestCommandBar());
        var oldContext = Mock.Of<ICommandBarContext>();
        var currentContext = Mock.Of<ICommandBarContext>();

        host.AttachPage(oldPage);
        oldPage.SetCommandBarContext(oldContext);
        Assert.AreSame(oldContext, host.CurrentCommandContext);

        host.AttachPage(currentPage);
        Assert.IsNull(host.CurrentCommandContext);

        oldPage.SetCommandBarContext(Mock.Of<ICommandBarContext>());
        Assert.IsNull(host.CurrentCommandContext);

        currentPage.SetCommandBarContext(currentContext);
        Assert.AreSame(currentContext, host.CurrentCommandContext);

        host.AttachPage(null);
        Assert.IsNull(host.CurrentCommandContext);
    }

    [TestMethod]
    public void DragState_AffectsOnlyTheOwningHost()
    {
        using var hostA = new PageInteractionCoordinator(new TestCommandBar());
        using var hostB = new PageInteractionCoordinator(new TestCommandBar());
        var targetA = new TestPageTarget();
        var targetB = new TestPageTarget();
        hostA.AttachTarget(targetA);
        hostB.AttachTarget(targetB);
        var dragA = new List<bool>();
        var dragB = new List<bool>();
        hostA.DragStateChanged += (_, e) => dragA.Add(e.IsDragging);
        hostB.DragStateChanged += (_, e) => dragB.Add(e.IsDragging);

        targetA.SetDragging(true);
        targetA.SetDragging(false);

        Assert.AreEqual(2, dragA.Count);
        Assert.IsTrue(dragA[0]);
        Assert.IsFalse(dragA[1]);
        Assert.AreEqual(0, dragB.Count);
    }

    [TestMethod]
    public void AttachDetach_DoesNotDuplicateHandlersOrRetainOldPages()
    {
        var bar = new TestCommandBar();
        using var host = new PageInteractionCoordinator(bar);
        var page = CreatePage();
        host.AttachPage(page);
        host.AttachPage(page);
        bar.Contexts.Clear();

        page.SetCommandBarContext(Mock.Of<ICommandBarContext>());

        Assert.AreEqual(1, bar.Contexts.Count);

        var oldPageReference = AttachAndReplacePage(host);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.IsFalse(oldPageReference.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private WeakReference AttachAndReplacePage(PageInteractionCoordinator host)
    {
        var oldPage = CreatePage();
        try
        {
            host.AttachPage(oldPage);
            var reference = new WeakReference(oldPage);
            host.AttachPage(CreatePage());
            return reference;
        }
        finally
        {
            _pages.Remove(oldPage);
            oldPage.CleanupAsync().GetAwaiter().GetResult();
        }
    }

    private PageViewModel CreatePage()
    {
        var page = new PageViewModel(new Page(), TaskScheduler.Default, new TestAppExtensionHost(), CommandProviderContext.Empty);
        _pages.Add(page);
        return page;
    }

    private static bool TryShortcut(PageInteractionCoordinator host, VirtualKey key, VirtualKeyModifiers modifiers) =>
        host.TryCommandKeybinding(new(modifiers, (int)key, 0));

    private PageViewModel CreatePageWithMenu(params IContextItem[] commands)
    {
        var page = new PageViewModel(new TestMenuPage(commands), TaskScheduler.Default, new TestAppExtensionHost(), CommandProviderContext.Empty);
        _pages.Add(page);
        page.InitializeProperties();
        page.PageMenu.InitializeProperties();
        return page;
    }

    private static CommandContextItem CreateMenuCommand(string title, VirtualKey key = VirtualKey.S, VirtualKeyModifiers modifiers = VirtualKeyModifiers.Control) =>
        new(new NoOpCommand { Name = title })
        {
            RequestedShortcut = new(modifiers, (int)key, 0),
        };

    private CommandItemViewModel CreateSelection(PageViewModel page, params IContextItem[] commands)
    {
        var model = new ListItem(new NoOpCommand { Name = "Run" }) { MoreCommands = commands };
        var selection = new CommandItemViewModel(new ExtensionObject<ICommandItem>(model), new(page), DefaultContextMenuFactory.Instance);
        _selections.Add(selection);
        selection.SlowInitializeProperties();
        return selection;
    }
}
