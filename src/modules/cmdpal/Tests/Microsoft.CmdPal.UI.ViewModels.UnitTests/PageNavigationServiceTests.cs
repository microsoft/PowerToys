// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed partial class PageNavigationServiceTests
{
    private sealed partial class TestPageViewModel : PageViewModel
    {
        private readonly Action _initialize;

        public TestPageViewModel(Action initialize, bool initialized = false)
            : base(new Page(), TaskScheduler.Default, new TestAppExtensionHost(), CommandProviderContext.Empty)
        {
            _initialize = initialize;
            IsInitialized = initialized;
        }

        public override void InitializeProperties() => _initialize();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TryPreparePage_SetsNavigationDefaults(bool nested)
    {
        var page = new Page();
        var host = new TestAppExtensionHost();
        var provider = Mock.Of<ICommandProviderContext>();
        var viewModel = new PageViewModel(page, TaskScheduler.Default, host, provider);
        var factory = new Mock<IPageViewModelFactoryService>();
        factory.Setup(f => f.TryCreatePageViewModel(page, nested, host, provider, null)).Returns(viewModel);
        var service = new PageNavigationService(factory.Object, Mock.Of<IAppHostService>());

        try
        {
            Assert.AreSame(viewModel, service.TryPreparePage(page, nested, host, provider, null));
            Assert.AreEqual(!nested, viewModel.IsRootPage);
            Assert.AreEqual(nested, viewModel.HasBackButton);
            factory.Verify(f => f.TryCreatePageViewModel(page, nested, host, provider, null), Times.Once);
        }
        finally
        {
            viewModel.SafeCleanup();
        }
    }

    [TestMethod]
    public void TryPreparePage_PreservesFallbackContextFromSourcePage()
    {
        var page = new Page();
        var host = new TestAppExtensionHost();
        var provider = Mock.Of<ICommandProviderContext>();
        var fallbackContext = new FallbackQueryContext(host, provider, new object(), CancellationToken.None);
        var sourcePage = new PageViewModel(new Page(), TaskScheduler.Default, new TestAppExtensionHost(), CommandProviderContext.Empty);
        sourcePage.InheritFallbackContext(fallbackContext);
        var message = new PerformCommandMessage(new CommandViewModel(page, new(sourcePage)), sourcePage);
        var viewModel = new PageViewModel(page, TaskScheduler.Default, host, provider);
        var factory = new Mock<IPageViewModelFactoryService>(MockBehavior.Strict);
        factory.Setup(f => f.TryCreatePageViewModel(
            page,
            true,
            host,
            provider,
            It.Is<FallbackQueryContext?>(context => ReferenceEquals(context, fallbackContext)))).Returns(viewModel);
        var appHostService = new Mock<IAppHostService>(MockBehavior.Strict);
        var service = new PageNavigationService(factory.Object, appHostService.Object);

        try
        {
            var resolvedHost = service.ResolveHost(message, sourcePage);
            var resolvedProvider = service.ResolveProviderContext(message, sourcePage);

            Assert.AreSame(host, resolvedHost);
            Assert.AreSame(provider, resolvedProvider);
            Assert.AreSame(fallbackContext, message.FallbackContext);
            Assert.AreSame(fallbackContext.InvocationContext, message.CommandContext);
            Assert.AreSame(viewModel, service.TryPreparePage(page, true, resolvedHost, resolvedProvider, null, message.FallbackContext));
            factory.Verify(
                f => f.TryCreatePageViewModel(
                    page,
                    true,
                    host,
                    provider,
                    It.Is<FallbackQueryContext?>(context => ReferenceEquals(context, fallbackContext))),
                Times.Once);
            appHostService.VerifyNoOtherCalls();
        }
        finally
        {
            viewModel.SafeCleanup();
            sourcePage.SafeCleanup();
        }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void TryPreparePage_InvalidOptions_RejectsBeforeCreatingPage(bool isListPage, bool emptyOptions)
    {
        IPage page = isListPage ? new ListPage() : new Page();
        var factory = new Mock<IPageViewModelFactoryService>(MockBehavior.Strict);
        var service = new PageNavigationService(factory.Object, Mock.Of<IAppHostService>());
        var options = emptyOptions ? new ListPageLaunchOptions() : new ListPageLaunchOptions(Query: "query");

        Assert.ThrowsExactly<NotSupportedException>(() =>
            service.TryPreparePage(page, false, new TestAppExtensionHost(), CommandProviderContext.Empty, options));
        factory.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void TryPreparePage_ListOptionsRequireListViewModel()
    {
        var page = new ListPage();
        var host = new TestAppExtensionHost();
        var viewModel = new PageViewModel(page, TaskScheduler.Default, host, CommandProviderContext.Empty);
        var factory = new Mock<IPageViewModelFactoryService>();
        factory.Setup(f => f.TryCreatePageViewModel(page, false, host, CommandProviderContext.Empty, null)).Returns(viewModel);
        var service = new PageNavigationService(factory.Object, Mock.Of<IAppHostService>());

        try
        {
            Assert.ThrowsExactly<NotSupportedException>(() =>
                service.TryPreparePage(page, false, host, CommandProviderContext.Empty, new(Query: "query")));
        }
        finally
        {
            viewModel.SafeCleanup();
        }
    }

    [TestMethod]
    public async Task InitializePageAsync_InitializedPage_DoesNotRunAgain()
    {
        var initializationCount = 0;
        var page = new TestPageViewModel(() => initializationCount++, initialized: true);
        try
        {
            await PageNavigationService.InitializePageAsync(page, CancellationToken.None);
            Assert.AreEqual(0, initializationCount);
        }
        finally
        {
            page.SafeCleanup();
        }
    }

    [TestMethod]
    public async Task InitializePageAsync_CanceledBeforeScheduling_DoesNotInitialize()
    {
        var initializationCount = 0;
        var page = new TestPageViewModel(() => initializationCount++);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            await Assert.ThrowsExactlyAsync<TaskCanceledException>(() =>
                PageNavigationService.InitializePageAsync(page, cancellation.Token));
            Assert.AreEqual(0, initializationCount);
        }
        finally
        {
            page.SafeCleanup();
        }
    }

    [TestMethod]
    public async Task InitializePageAsync_CanceledDuringInitialization_WaitsForExtensionCall()
    {
        using var release = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = false;
        var page = new TestPageViewModel(() =>
        {
            started.SetResult(Thread.CurrentThread.IsThreadPoolThread);
            if (!release.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("Initialization was not released.");
            }

            finished = true;
        });
        var initialization = PageNavigationService.InitializePageAsync(page, cancellation.Token);

        try
        {
            Assert.IsTrue(await started.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            cancellation.Cancel();
            Assert.IsFalse(initialization.IsCompleted);
            release.Set();
            await initialization.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(finished);
        }
        finally
        {
            release.Set();
            await initialization.WaitAsync(TimeSpan.FromSeconds(5));
            page.SafeCleanup();
        }
    }
}
