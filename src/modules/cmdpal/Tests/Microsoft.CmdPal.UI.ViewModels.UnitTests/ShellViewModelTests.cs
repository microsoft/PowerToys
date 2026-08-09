// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
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
[DoNotParallelize]
public partial class ShellViewModelTests
{
    private SynchronizationContext? _originalSynchronizationContext;

    private sealed class ImmediateSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) => callback(state);
    }

    private sealed partial class TestAppExtensionHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Test Host";
    }

    private sealed class TestPageViewModel : PageViewModel
    {
        public TestPageViewModel(IPage page, AppExtensionHost host)
            : base(page, TaskScheduler.Default, host, CommandProviderContext.Empty)
        {
            IsInitialized = true;
            ModelIsLoading = false;
        }
    }

    private static Mock<IAppHostService> CreateAppHostService(AppExtensionHost host)
    {
        var appHostService = new Mock<IAppHostService>();
        appHostService.Setup(service => service.GetDefaultHost()).Returns(host);
        appHostService
            .Setup(service => service.GetHostForCommand(It.IsAny<object?>(), It.IsAny<AppExtensionHost?>()))
            .Returns(host);
        appHostService
            .Setup(service => service.GetProviderContextForCommand(It.IsAny<object?>(), It.IsAny<ICommandProviderContext?>()))
            .Returns(CommandProviderContext.Empty);
        return appHostService;
    }

    [TestInitialize]
    public void TestInitialize()
    {
        _originalSynchronizationContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new ImmediateSynchronizationContext());
    }

    [TestCleanup]
    public void TestCleanup()
    {
        SynchronizationContext.SetSynchronizationContext(_originalSynchronizationContext);
    }

    [TestMethod]
    public void PerformCommand_InvalidListPageOptions_DoesNotMutateNavigationState()
    {
        var host = new TestAppExtensionHost();
        var rootPageService = new Mock<IRootPageService>();
        var pageViewModelFactory = new Mock<IPageViewModelFactoryService>();
        var appHostService = CreateAppHostService(host);

        var shell = new ShellViewModel(
            TaskScheduler.Default,
            rootPageService.Object,
            pageViewModelFactory.Object,
            appHostService.Object);
        var windowMessageRecipient = new object();
        var showWindowCount = 0;
        WeakReferenceMessenger.Default.Register<ShowWindowMessage>(
            windowMessageRecipient,
            (_, _) => showWindowCount++);

        try
        {
            var contentPage = new Page
            {
                Id = "content-page",
                Name = "Content page",
            };
            var message = new PerformCommandMessage(new ExtensionObject<ICommand>(contentPage))
            {
                ListPageOptions = new(Query: "ssh"),
                ShowWindowIfPage = true,
                TransientPage = true,
            };

            shell.Receive(message);

            Assert.IsFalse(shell.IsNested);
            Assert.IsFalse(shell.IsTransient);
            Assert.AreEqual(0, showWindowCount);
            pageViewModelFactory.Verify(
                factory => factory.TryCreatePageViewModel(
                    It.IsAny<IPage>(),
                    It.IsAny<bool>(),
                    It.IsAny<AppExtensionHost>(),
                    It.IsAny<ICommandProviderContext>()),
                Times.Never);
            rootPageService.Verify(
                service => service.OnPerformCommand(
                    It.IsAny<object?>(),
                    It.IsAny<bool>(),
                    It.IsAny<AppExtensionHost?>()),
                Times.Never);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(windowMessageRecipient);
            WeakReferenceMessenger.Default.UnregisterAll(shell);
            shell.Dispose();
        }
    }

    [TestMethod]
    public void PerformCommand_TransientNestedPage_HidesBackButton()
    {
        var host = new TestAppExtensionHost();
        var rootPageService = new Mock<IRootPageService>();
        var pageViewModelFactory = new Mock<IPageViewModelFactoryService>();
        var appHostService = CreateAppHostService(host);
        var page = new Page
        {
            Id = "transient-page",
            Name = "Transient page",
        };
        var pageViewModel = new TestPageViewModel(page, host);
        pageViewModelFactory
            .Setup(factory => factory.TryCreatePageViewModel(
                page,
                true,
                host,
                CommandProviderContext.Empty))
            .Returns(pageViewModel);
        var shell = new ShellViewModel(
            TaskScheduler.Default,
            rootPageService.Object,
            pageViewModelFactory.Object,
            appHostService.Object);

        try
        {
            var message = new PerformCommandMessage(new ExtensionObject<ICommand>(page))
            {
                TransientPage = true,
            };

            shell.Receive(message);

            Assert.IsTrue(shell.IsTransient);
            Assert.IsFalse(shell.IsNested);
            Assert.IsFalse(pageViewModel.HasBackButton);
            rootPageService.Verify(
                service => service.OnPerformCommand(null, true, host),
                Times.Once);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(shell);
            shell.Dispose();
        }
    }

    [TestMethod]
    public void PerformCommand_UnsupportedCommand_StillNotifiesRootPageService()
    {
        var host = new TestAppExtensionHost();
        var rootPageService = new Mock<IRootPageService>();
        var pageViewModelFactory = new Mock<IPageViewModelFactoryService>();
        var appHostService = CreateAppHostService(host);
        var command = new Mock<ICommand>();
        var shell = new ShellViewModel(
            TaskScheduler.Default,
            rootPageService.Object,
            pageViewModelFactory.Object,
            appHostService.Object);

        try
        {
            shell.Receive(new PerformCommandMessage(new ExtensionObject<ICommand>(command.Object)));

            rootPageService.Verify(
                service => service.OnPerformCommand(null, true, host),
                Times.Once);
            pageViewModelFactory.VerifyNoOtherCalls();
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(shell);
            shell.Dispose();
        }
    }
}
