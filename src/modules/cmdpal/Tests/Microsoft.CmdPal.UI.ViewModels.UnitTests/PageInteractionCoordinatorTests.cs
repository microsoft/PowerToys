// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.System;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed partial class PageInteractionCoordinatorTests
{
    private sealed partial class TestAppExtensionHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Test Host";
    }

    private sealed class TestCommandBar : ICommandBarInteractionTarget
    {
        public List<ICommandBarContext?> Contexts { get; } = [];

        public int OpenCount { get; private set; }

        public int CloseCount { get; private set; }

        public int KeybindingCount { get; private set; }

        public bool KeybindingResult { get; set; }

        public void SetCommandContext(ICommandBarContext? context) => Contexts.Add(context);

        public void OpenContextMenu() => OpenCount++;

        public void CloseContextMenu() => CloseCount++;

        public bool TryCommandKeybinding(bool ctrl, bool alt, bool shift, bool win, VirtualKey key)
        {
            KeybindingCount++;
            return KeybindingResult;
        }
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

    private static PageViewModel CreatePage() =>
        new(new Page(), TaskScheduler.Default, new TestAppExtensionHost(), CommandProviderContext.Empty);
}