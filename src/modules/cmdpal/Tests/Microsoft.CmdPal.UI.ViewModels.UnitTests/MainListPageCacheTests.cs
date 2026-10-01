// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed class MainListPageCacheTests
{
    private const string ProviderId = "test.provider";

    private sealed class TestPageContext : IPageContext
    {
        public TaskScheduler Scheduler => TaskScheduler.Default;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public void ShowException(Exception ex, string? extensionHint = null) =>
            throw new AssertFailedException($"Unexpected exception from view model: {ex}");
    }

    private sealed class BlockingCommandProviderContext : ICommandProviderContext, IDisposable
    {
        private readonly ManualResetEventSlim _readStarted = new();
        private readonly ManualResetEventSlim _continueRead = new();
        private Action? _nextReadAction;
        private int _blockNextRead;
        private int _readCount;

        public string ProviderId
        {
            get
            {
                Interlocked.Increment(ref _readCount);
                Interlocked.Exchange(ref _nextReadAction, null)?.Invoke();
                if (Interlocked.Exchange(ref _blockNextRead, 0) != 0)
                {
                    _readStarted.Set();
                    if (!_continueRead.Wait(TimeSpan.FromSeconds(5)))
                    {
                        throw new TimeoutException("Timed out waiting to continue reading the provider ID.");
                    }
                }

                return MainListPageCacheTests.ProviderId;
            }
        }

        public bool SupportsPinning => true;

        public int ReadCount => Volatile.Read(ref _readCount);

        public void BlockNextRead()
        {
            _readStarted.Reset();
            _continueRead.Reset();
            Volatile.Write(ref _blockNextRead, 1);
        }

        public void RunOnNextRead(Action action) => Interlocked.Exchange(ref _nextReadAction, action);

        public bool WaitForBlockedRead(TimeSpan timeout) => _readStarted.Wait(timeout);

        public void ContinueRead() => _continueRead.Set();

        public void Dispose()
        {
            _readStarted.Dispose();
            _continueRead.Dispose();
        }
    }

    [TestMethod]
    public async Task ConcurrentDefaultViewRebuilds_CannotOverwriteNewerSnapshot()
    {
        var settings = new SettingsModel();
        var settingsService = new Mock<ISettingsService>();
        settingsService.SetupGet(service => service.Settings).Returns(() => settings);

        var services = new Mock<IServiceProvider>();
        services.Setup(service => service.GetService(typeof(TaskScheduler))).Returns(TaskScheduler.Default);
        services.Setup(service => service.GetService(typeof(ISettingsService))).Returns(settingsService.Object);

        var appStateService = new Mock<IAppStateService>();
        appStateService.SetupGet(service => service.State).Returns(new AppStateModel());

        var fuzzyMatcherProvider = new Mock<IFuzzyMatcherProvider>();
        using var providerContext = new BlockingCommandProviderContext();
        using var commandManager = new TopLevelCommandManager(services.Object, []);
        var pageContext = new TestPageContext();
        var first = CreateTopLevelCommand("first", "First", services.Object, pageContext, providerContext);
        var second = CreateTopLevelCommand("second", "Second", services.Object, pageContext, providerContext);
        commandManager.TopLevelCommands.Add(first);
        commandManager.TopLevelCommands.Add(second);

        using var page = new MainListPage(
            commandManager,
            null!,
            fuzzyMatcherProvider.Object,
            settingsService.Object,
            appStateService.Object,
            new ListPage());

        try
        {
            _ = page.GetItems();
            settings = settings.TryPinCommand(ProviderId, first.Id);
            commandManager.RebuildPinnedCache();

            providerContext.BlockNextRead();
            var rebuild = Task.Run(page.GetItems);
            Assert.IsTrue(providerContext.WaitForBlockedRead(TimeSpan.FromSeconds(5)));

            settings = settings.TryPinCommand(ProviderId, second.Id);
            commandManager.RebuildPinnedCache();
            var newerRebuild = await Task.Run(page.GetItems).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.AreEqual(3, newerRebuild.Length);
            Assert.AreSame(first, newerRebuild[1]);
            Assert.AreSame(second, newerRebuild[2]);

            providerContext.ContinueRead();
            var olderRebuild = await rebuild.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.AreEqual(3, olderRebuild.Length);
            Assert.AreSame(first, olderRebuild[1]);
            Assert.AreSame(second, olderRebuild[2]);
        }
        finally
        {
            providerContext.ContinueRead();
            first.Cleanup();
            second.Cleanup();
        }
    }

    [TestMethod]
    public void InvalidatedDefaultViewRebuild_IsRetriedByNextGetItemsCall()
    {
        var settings = new SettingsModel();
        var settingsService = new Mock<ISettingsService>();
        settingsService.SetupGet(service => service.Settings).Returns(() => settings);

        var services = new Mock<IServiceProvider>();
        services.Setup(service => service.GetService(typeof(TaskScheduler))).Returns(TaskScheduler.Default);
        services.Setup(service => service.GetService(typeof(ISettingsService))).Returns(settingsService.Object);

        var appStateService = new Mock<IAppStateService>();
        appStateService.SetupGet(service => service.State).Returns(new AppStateModel());

        var fuzzyMatcherProvider = new Mock<IFuzzyMatcherProvider>();
        using var providerContext = new BlockingCommandProviderContext();
        using var commandManager = new TopLevelCommandManager(services.Object, []);
        var pageContext = new TestPageContext();
        var command = CreateTopLevelCommand("command", "Command", services.Object, pageContext, providerContext);
        commandManager.TopLevelCommands.Add(command);

        using var page = new MainListPage(
            commandManager,
            null!,
            fuzzyMatcherProvider.Object,
            settingsService.Object,
            appStateService.Object,
            new ListPage());

        try
        {
            _ = page.GetItems();
            settings = settings.TryPinCommand("missing.provider", "missing.command");
            commandManager.RebuildPinnedCache();

            var readsBeforeRebuild = providerContext.ReadCount;
            providerContext.RunOnNextRead(() =>
            {
                settings = settings.TryPinCommand(ProviderId, command.Id);
                commandManager.RebuildPinnedCache();
            });

            _ = page.GetItems();
            Assert.AreEqual(readsBeforeRebuild + 1, providerContext.ReadCount);

            _ = page.GetItems();
            Assert.AreEqual(readsBeforeRebuild + 2, providerContext.ReadCount);
        }
        finally
        {
            command.Cleanup();
        }
    }

    private static TopLevelViewModel CreateTopLevelCommand(
        string id,
        string title,
        IServiceProvider services,
        IPageContext pageContext,
        ICommandProviderContext providerContext)
    {
        var model = new CommandItem(new NoOpCommand { Id = id, Name = title }) { Title = title };
        var item = new CommandItemViewModel(new(model), new(pageContext), DefaultContextMenuFactory.Instance);
        var topLevel = new TopLevelViewModel(
            item,
            TopLevelType.Normal,
            CommandPaletteHost.Instance,
            providerContext,
            new ProviderSettings(),
            services,
            model,
            DefaultContextMenuFactory.Instance);
        topLevel.InitializeProperties();
        return topLevel;
    }
}
