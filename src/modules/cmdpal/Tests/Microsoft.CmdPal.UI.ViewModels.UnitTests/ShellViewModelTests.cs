// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class ShellViewModelTests
{
    private sealed partial class TestAppExtensionHost(string displayName) : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => displayName;
    }

    private sealed class TestCommandProviderContext(string providerId) : ICommandProviderContext
    {
        private readonly Dictionary<string, ICommandItem> _items = [];

        public string ProviderId { get; } = providerId;

        public bool SupportsPinning => true;

        public int GetCommandItemCalls { get; private set; }

        public string? LastRequestedId { get; private set; }

        public void Add(ICommandItem item)
        {
            Assert.IsNotNull(item.Command);
            _items[item.Command.Id] = item;
        }

        public ICommandItem? GetCommandItem(string id)
        {
            GetCommandItemCalls++;
            LastRequestedId = id;

            return _items.TryGetValue(id, out var item) ? item : null;
        }
    }

    private sealed class InitializedPageViewModel : PageViewModel
    {
        public InitializedPageViewModel(IPage model, TaskScheduler scheduler, AppExtensionHost extensionHost, ICommandProviderContext providerContext)
            : base(model, scheduler, extensionHost, providerContext)
        {
            IsInitialized = true;
        }
    }

    public sealed class GoToPageMessageSink : IRecipient<GoToPageMessage>
    {
        public GoToPageMessage? Received { get; private set; }

        public void Receive(GoToPageMessage message)
        {
            Received = message;
        }
    }

    [TestMethod]
    public void PerformCommand_UsesOverrideHostAndProviderContext()
    {
        var defaultHost = new TestAppExtensionHost("Default");
        var overrideHost = new TestAppExtensionHost("Override");
        var overrideProviderContext = new TestCommandProviderContext("override-provider");

        var rootPageService = new Mock<IRootPageService>(MockBehavior.Strict);
        rootPageService
            .Setup(service => service.OnPerformCommand(null, true, overrideHost));

        var pageViewModelFactory = new Mock<IPageViewModelFactoryService>(MockBehavior.Strict);
        AppExtensionHost? capturedHost = null;
        ICommandProviderContext? capturedProviderContext = null;
        pageViewModelFactory
            .Setup(factory => factory.TryCreatePageViewModel(It.IsAny<IPage>(), It.IsAny<bool>(), It.IsAny<AppExtensionHost>(), It.IsAny<ICommandProviderContext>()))
            .Returns((IPage page, bool _, AppExtensionHost host, ICommandProviderContext providerContext) =>
            {
                capturedHost = host;
                capturedProviderContext = providerContext;
                return new InitializedPageViewModel(page, TaskScheduler.Default, host, providerContext);
            });

        var appHostService = new Mock<IAppHostService>(MockBehavior.Strict);
        appHostService.Setup(service => service.GetDefaultHost()).Returns(defaultHost);

        var viewModel = new ShellViewModel(TaskScheduler.Default, rootPageService.Object, pageViewModelFactory.Object, appHostService.Object);
        try
        {
            var targetPage = new ListPage
            {
                Id = "target.page",
                Name = "Target Page",
                Title = "Target Page",
            };

            var message = new PerformCommandMessage(new ExtensionObject<ICommand>(targetPage))
            {
                HostOverride = overrideHost,
                ProviderContextOverride = overrideProviderContext,
            };

            viewModel.Receive(message);

            Assert.AreSame(overrideHost, capturedHost);
            Assert.AreSame(overrideProviderContext, capturedProviderContext);

            pageViewModelFactory.VerifyAll();
            rootPageService.VerifyAll();
            appHostService.Verify(service => service.GetHostForCommand(It.IsAny<object?>(), It.IsAny<AppExtensionHost?>()), Times.Never);
            appHostService.Verify(service => service.GetProviderContextForCommand(It.IsAny<object?>(), It.IsAny<ICommandProviderContext?>()), Times.Never);
        }
        finally
        {
            viewModel.Dispose();
        }
    }

    [TestMethod]
    public void HandleCommandResult_GoToPage_ResolvesTargetAndSendsMessage()
    {
        var defaultHost = new TestAppExtensionHost("Default");
        var currentHost = new TestAppExtensionHost("Current");
        var providerContext = new TestCommandProviderContext("provider");

        var targetPage = new ListPage
        {
            Id = "target.page",
            Name = "Target Page",
            Title = "Target Page",
        };
        var targetItem = new CommandItem(targetPage)
        {
            Title = "Target Page",
        };
        providerContext.Add(targetItem);

        var rootPageService = new Mock<IRootPageService>(MockBehavior.Loose);
        var pageViewModelFactory = new Mock<IPageViewModelFactoryService>(MockBehavior.Loose);
        var appHostService = new Mock<IAppHostService>(MockBehavior.Strict);
        appHostService.Setup(service => service.GetDefaultHost()).Returns(defaultHost);

        var sink = new GoToPageMessageSink();
        WeakReferenceMessenger.Default.Register<GoToPageMessage>(sink);

        var viewModel = new ShellViewModel(TaskScheduler.Default, rootPageService.Object, pageViewModelFactory.Object, appHostService.Object);
        try
        {
            viewModel.CurrentPage = new InitializedPageViewModel(
                new ListPage
                {
                    Id = "current.page",
                    Name = "Current Page",
                    Title = "Current Page",
                },
                TaskScheduler.Default,
                currentHost,
                providerContext)
            {
                IsRootPage = false,
            };

            var result = CommandResult.GoToPage(
                new GoToPageArgs
                {
                    PageId = targetPage.Id,
                    NavigationMode = NavigationMode.GoBack,
                });

            viewModel.Receive(new HandleCommandResultMessage(new ExtensionObject<ICommandResult>(result)));

            Assert.AreEqual(1, providerContext.GetCommandItemCalls);
            Assert.AreEqual(targetPage.Id, providerContext.LastRequestedId);
            Assert.IsNotNull(sink.Received);
            Assert.AreEqual(NavigationMode.GoBack, sink.Received.NavigationMode);
            Assert.AreSame(currentHost, sink.Received.CommandMessage.HostOverride);
            Assert.AreSame(providerContext, sink.Received.CommandMessage.ProviderContextOverride);
            Assert.AreSame(targetPage, sink.Received.CommandMessage.Command.Unsafe);
            Assert.AreSame(targetItem, sink.Received.CommandMessage.Context);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(sink);
            viewModel.Dispose();
        }
    }
}
