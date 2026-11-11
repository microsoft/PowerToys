// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

public partial class PageMenuViewModelTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Factory_AddsDefaultsWithOrWithoutExtensionCommands(bool extensionCommands)
    {
        var page = new TestMenuPage();
        var extensionCommand = Command("Extension action");
        if (extensionCommands)
        {
            page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = new IContextItem[] { extensionCommand };
        }

        var defaultCommand = Command("Host action");
        var factory = new Mock<IContextMenuFactory>();
        factory.Setup(value => value.AddMoreCommandsToPage(It.IsAny<PageViewModel>(), It.IsAny<List<IContextItem>>()))
            .Callback<PageViewModel, List<IContextItem>>((_, items) =>
            {
                if (items.Count > 0)
                {
                    items.Add(new Separator());
                }

                items.Add(defaultCommand);
            });
        var viewModel = CreateViewModel(page, factory.Object);
        CommandContextItemViewModel? ownedDefault = null;
        try
        {
            InitializePageAndMenu(viewModel);
            var commands = viewModel.PageMenu.AllCommands;
            Assert.IsTrue(viewModel.PageMenu.CanOpenContextMenu);
            Assert.AreEqual(extensionCommands ? 3 : 1, commands.Count);
            ownedDefault = (CommandContextItemViewModel)commands[^1];
            Assert.AreSame(defaultCommand, ownedDefault.Model.Unsafe);
            if (extensionCommands)
            {
                Assert.AreSame(extensionCommand, ((CommandContextItemViewModel)commands[0]).Model.Unsafe);
                Assert.IsInstanceOfType<SeparatorViewModel>(commands[1]);
            }

            factory.Verify(value => value.AddMoreCommandsToPage(viewModel, It.IsAny<List<IContextItem>>()), Times.Once);
            Assert.AreEqual(string.Empty, viewModel.ErrorMessage);
        }
        finally
        {
            await viewModel.CleanupAsync();
        }

        Assert.IsNotNull(ownedDefault);
        Assert.IsTrue(ownedDefault.Initialized.HasFlag(InitializedState.CleanedUp));
    }

    [TestMethod]
    [DataRow("separators")]
    [DataRow("unnamed")]
    [DataRow("trailing")]
    [DataRow("mixed")]
    public async Task Factory_MenuHasOnlySeparatorsBetweenVisibleGroups(string shape)
    {
        var visible = Command("Visible action");
        var page = new TestMenuPage();
        page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = shape switch
        {
            "separators" => new IContextItem[] { new Separator(), new Separator() },
            "unnamed" => [Command(string.Empty), new Separator(), Command(string.Empty)],
            "trailing" => [visible, new Separator()],
            _ => [new Separator(), visible, new Separator(), Command(string.Empty), new Separator()],
        };
        var hostCommand = Command("Extension settings");
        var factory = new Mock<IContextMenuFactory>();
        factory.Setup(value => value.AddMoreCommandsToPage(It.IsAny<PageViewModel>(), It.IsAny<List<IContextItem>>()))
            .Callback<PageViewModel, List<IContextItem>>((_, items) =>
            {
                items.Add(new Separator());
                items.Add(hostCommand);
            });
        var viewModel = CreateViewModel(page, factory.Object);
        try
        {
            InitializePageAndMenu(viewModel);

            var commands = viewModel.PageMenu.AllCommands;
            Assert.IsTrue(viewModel.PageMenu.CanOpenContextMenu);
            Assert.HasCount(shape is "trailing" or "mixed" ? 3 : 1, commands);
            Assert.AreSame(hostCommand, ((CommandContextItemViewModel)commands[^1]).Model.Unsafe);
            if (commands.Count > 1)
            {
                Assert.AreSame(visible, ((CommandContextItemViewModel)commands[0]).Model.Unsafe);
                Assert.IsInstanceOfType<SeparatorViewModel>(commands[1]);

                var cached = (CommandContextItemViewModel)commands[0];
                ((Command)visible.Command!).Name = string.Empty;
                cached.Command.ApplyPendingUpdates();
                cached.ApplyPendingUpdates();
                viewModel.PageMenu.ApplyPendingUpdates();
                Assert.HasCount(1, viewModel.PageMenu.AllCommands);
                Assert.AreSame(hostCommand, ((CommandContextItemViewModel)viewModel.PageMenu.AllCommands.Single()).Model.Unsafe);
            }
        }
        finally
        {
            await viewModel.CleanupAsync();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Factory_KeepsDefaultsAvailableWhenExtensionAttributesFail(bool throwingProvider)
    {
        var page = new TestMenuPage();
        if (throwingProvider)
        {
            page.BeforeReadProperties = () => throw new InvalidOperationException("Provider failed");
        }
        else
        {
            page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = "invalid";
        }

        var factory = new Mock<IContextMenuFactory>();
        factory.Setup(value => value.AddMoreCommandsToPage(It.IsAny<PageViewModel>(), It.IsAny<List<IContextItem>>()))
            .Callback<PageViewModel, List<IContextItem>>((_, items) => items.Add(Command("Host action")));
        var viewModel = CreateViewModel(page, factory.Object);
        try
        {
            InitializePageAndMenu(viewModel);

            Assert.IsTrue(viewModel.PageMenu.CanOpenContextMenu);
            Assert.AreEqual("Host action", ((CommandContextItemViewModel)viewModel.PageMenu.AllCommands.Single()).Name);
            Assert.IsFalse(string.IsNullOrEmpty(viewModel.ErrorMessage));
        }
        finally
        {
            await viewModel.CleanupAsync();
        }
    }

    [TestMethod]
    [DataRow(nameof(ICommandItem.Title))]
    [DataRow(nameof(ICommandItem.MoreCommands))]
    public async Task Factory_KeepsDefaultsAvailableWhenExtensionItemInitializationFails(string propertyName)
    {
        var fail = true;
        var failure = new InvalidOperationException($"{propertyName} failed");
        var earlierItem = new Mock<ICommandContextItem>();
        earlierItem.SetupGet(value => value.Command).Returns(new NoOpCommand { Name = "Earlier extension action" });
        var failingItem = new Mock<ICommandContextItem>();
        failingItem.SetupGet(value => value.Command).Returns(new NoOpCommand { Name = "Extension action" });
        failingItem.SetupGet(value => value.Title)
            .Returns(() => fail && propertyName == nameof(ICommandItem.Title) ? throw failure : "Extension action");
        failingItem.SetupGet(value => value.MoreCommands)
            .Returns(() => fail && propertyName == nameof(ICommandItem.MoreCommands) ? throw failure : []);
        var page = new TestMenuPage();
        page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = new IContextItem[] { earlierItem.Object, failingItem.Object };
        var defaultCommand = Command("Extension settings");
        List<int> extensionItemCounts = [];
        var factory = new Mock<IContextMenuFactory>();
        factory.Setup(value => value.AddMoreCommandsToPage(It.IsAny<PageViewModel>(), It.IsAny<List<IContextItem>>()))
            .Callback<PageViewModel, List<IContextItem>>((_, items) =>
            {
                extensionItemCounts.Add(items.Count);
                if (items.Count > 0)
                {
                    items.Add(new Separator());
                }

                items.Add(defaultCommand);
            });
        var viewModel = CreateViewModel(page, factory.Object);
        try
        {
            InitializePageAndMenu(viewModel);

            Assert.IsTrue(viewModel.PageMenu.CanOpenContextMenu);
            var fallback = (CommandContextItemViewModel)viewModel.PageMenu.AllCommands.Single();
            Assert.AreSame(defaultCommand, fallback.Model.Unsafe);
            StringAssert.Contains(viewModel.ErrorMessage, failure.Message);
            StringAssert.Contains(viewModel.ErrorMessage, "An unexpected error occurred in the 'Test Host' extension.");
            earlierItem.VerifyRemove(value => value.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
            Assert.AreEqual(0, extensionItemCounts.Single());

            fail = false;
            viewModel.PageMenu.InitializeProperties();

            Assert.HasCount(4, viewModel.PageMenu.AllCommands);
            Assert.AreSame(earlierItem.Object, ((CommandContextItemViewModel)viewModel.PageMenu.AllCommands[0]).Model.Unsafe);
            Assert.AreSame(failingItem.Object, ((CommandContextItemViewModel)viewModel.PageMenu.AllCommands[1]).Model.Unsafe);
            Assert.IsInstanceOfType<SeparatorViewModel>(viewModel.PageMenu.AllCommands[2]);
            Assert.AreSame(defaultCommand, ((CommandContextItemViewModel)viewModel.PageMenu.AllCommands[3]).Model.Unsafe);
            Assert.IsTrue(fallback.Initialized.HasFlag(InitializedState.CleanedUp));
            Assert.HasCount(2, extensionItemCounts);
            Assert.AreEqual(2, extensionItemCounts[1]);
        }
        finally
        {
            await viewModel.CleanupAsync();
        }
    }

    [TestMethod]
    public async Task Factory_HostItemInitializationFailurePreservesExtensionCommandsAndIsAttributedToHost()
    {
        var extensionCommand = Command("Extension action");
        var page = new TestMenuPage();
        page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = new IContextItem[] { extensionCommand };
        var hostItem = new Mock<ICommandContextItem>();
        hostItem.SetupGet(value => value.Command).Returns(new NoOpCommand { Name = "Host action" });
        hostItem.SetupGet(value => value.Title).Throws(new InvalidOperationException("Host item failed"));
        var factory = new Mock<IContextMenuFactory>();
        factory.Setup(value => value.AddMoreCommandsToPage(It.IsAny<PageViewModel>(), It.IsAny<List<IContextItem>>()))
            .Callback<PageViewModel, List<IContextItem>>((_, items) =>
            {
                items.Add(new Separator());
                items.Add(hostItem.Object);
            });
        var viewModel = CreateViewModel(page, factory.Object);
        try
        {
            InitializePageAndMenu(viewModel);

            Assert.IsTrue(viewModel.PageMenu.CanOpenContextMenu);
            Assert.AreSame(extensionCommand, ((CommandContextItemViewModel)viewModel.PageMenu.AllCommands.Single()).Model.Unsafe);
            StringAssert.Contains(viewModel.ErrorMessage, "Host item failed");
            StringAssert.Contains(viewModel.ErrorMessage, "An unexpected error occurred in the application.");
            Assert.IsFalse(viewModel.ErrorMessage.Contains("'Test Host' extension", StringComparison.Ordinal));

            hostItem.SetupGet(value => value.Title).Returns("Recovered host action");
            viewModel.PageMenu.InitializeProperties();

            Assert.HasCount(3, viewModel.PageMenu.AllCommands);
            Assert.AreEqual("Recovered host action", ((CommandContextItemViewModel)viewModel.PageMenu.AllCommands[^1]).Title);
        }
        finally
        {
            await viewModel.CleanupAsync();
        }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    public async Task Factory_FailuresAreAttributedToHostAndPreserveExtensionCommands(bool invalidExtension, bool extensionCommands)
    {
        var page = new TestMenuPage();
        if (invalidExtension)
        {
            page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = "invalid";
        }
        else if (extensionCommands)
        {
            page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = new IContextItem[] { Command("Extension action") };
        }

        var factory = new Mock<IContextMenuFactory>();
        factory.Setup(value => value.AddMoreCommandsToPage(It.IsAny<PageViewModel>(), It.IsAny<List<IContextItem>>()))
            .Callback<PageViewModel, List<IContextItem>>((_, items) =>
            {
                items.Clear();
                items.Add(Command("Incomplete host action"));
                throw new InvalidOperationException("Factory failed");
            });
        var viewModel = CreateViewModel(page, factory.Object);
        try
        {
            InitializePageAndMenu(viewModel);

            Assert.AreEqual(extensionCommands, viewModel.PageMenu.CanOpenContextMenu);
            Assert.HasCount(extensionCommands ? 1 : 0, viewModel.PageMenu.AllCommands);
            if (extensionCommands)
            {
                Assert.AreEqual("Extension action", ((CommandContextItemViewModel)viewModel.PageMenu.AllCommands.Single()).Name);
            }

            StringAssert.Contains(viewModel.ErrorMessage, "Factory failed");
            StringAssert.Contains(viewModel.ErrorMessage, "An unexpected error occurred in the application.");
            if (invalidExtension)
            {
                StringAssert.Contains(viewModel.ErrorMessage, WellKnownExtensionAttributes.PageMenuCommands);
                StringAssert.Contains(viewModel.ErrorMessage, "An unexpected error occurred in the 'Test Host' extension.");
            }
            else
            {
                Assert.IsFalse(viewModel.ErrorMessage.Contains("'Test Host' extension", StringComparison.Ordinal));
            }

            factory.Setup(value => value.AddMoreCommandsToPage(It.IsAny<PageViewModel>(), It.IsAny<List<IContextItem>>()))
                .Callback<PageViewModel, List<IContextItem>>((_, items) => items.Add(Command("Recovered host action")));
            viewModel.PageMenu.InitializeProperties();

            Assert.IsTrue(viewModel.PageMenu.CanOpenContextMenu);
            Assert.HasCount(extensionCommands ? 2 : 1, viewModel.PageMenu.AllCommands);
            Assert.AreEqual("Recovered host action", ((CommandContextItemViewModel)viewModel.PageMenu.AllCommands[^1]).Name);
        }
        finally
        {
            await viewModel.CleanupAsync();
        }
    }

    [TestMethod]
    public async Task Factory_UnchangedExtensionItemsRetainDefaultsAndReplacementCleansThem()
    {
        var page = new TestMenuPage();
        page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = new IContextItem[] { Command("Original") };
        var factory = new Mock<IContextMenuFactory>();
        factory.Setup(value => value.AddMoreCommandsToPage(It.IsAny<PageViewModel>(), It.IsAny<List<IContextItem>>()))
            .Callback<PageViewModel, List<IContextItem>>((_, items) => items.Add(Command("Host action")));
        var viewModel = CreateViewModel(page, factory.Object);
        try
        {
            InitializePageAndMenu(viewModel);
            var original = viewModel.PageMenu.AllCommands;
            var originalDefault = (CommandContextItemViewModel)original[^1];
            viewModel.PageMenu.InitializeProperties();

            Assert.AreSame(original, viewModel.PageMenu.AllCommands);
            factory.Verify(value => value.AddMoreCommandsToPage(viewModel, It.IsAny<List<IContextItem>>()), Times.Once);

            page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = new IContextItem[] { Command("Replacement") };
            viewModel.PageMenu.InitializeProperties();

            Assert.IsTrue(originalDefault.Initialized.HasFlag(InitializedState.CleanedUp));
            Assert.AreEqual("Host action", ((CommandContextItemViewModel)viewModel.PageMenu.AllCommands[^1]).Name);
            factory.Verify(value => value.AddMoreCommandsToPage(viewModel, It.IsAny<List<IContextItem>>()), Times.Exactly(2));
        }
        finally
        {
            await viewModel.CleanupAsync();
        }
    }

    [TestMethod]
    [DataRow("list")]
    [DataRow("content")]
    [DataRow("parameters")]
    public async Task PageFactory_ForwardsHostMenuFactoryToEveryPageType(string pageType)
    {
        IPage page = pageType switch
        {
            "list" => Mock.Of<IListPage>(),
            "content" => Mock.Of<IContentPage>(),
            _ => Mock.Of<IParametersPage>(),
        };
        var factory = new Mock<IContextMenuFactory>();
        factory.Setup(value => value.AddMoreCommandsToPage(It.IsAny<PageViewModel>(), It.IsAny<List<IContextItem>>()))
            .Callback<PageViewModel, List<IContextItem>>((_, items) => items.Add(Command("Host action")));
        var pageFactory = new CommandPalettePageViewModelFactory(new ImmediateTaskScheduler(), factory.Object);
        var viewModel = pageFactory.TryCreatePageViewModel(page, true, new TestAppExtensionHost(), CommandProviderContext.Empty);
        Assert.IsNotNull(viewModel);
        try
        {
            viewModel.PageMenu.InitializeProperties();

            Assert.IsTrue(viewModel.PageMenu.CanOpenContextMenu);
            factory.Verify(value => value.AddMoreCommandsToPage(viewModel, It.IsAny<List<IContextItem>>()), Times.Once);
        }
        finally
        {
            await viewModel.CleanupAsync();
        }
    }

    private static PageViewModel CreateViewModel(Page page, IContextMenuFactory factory) =>
        new(page, new ImmediateTaskScheduler(), new TestAppExtensionHost(), CommandProviderContext.Empty, factory);
}
