// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class AliasManagerTests
{
    [TestMethod]
    public void CheckAlias_DoesNotRemoveAliasWhileInitialLoadIsStillRunning()
    {
        var manager = CreateTopLevelCommandManager();
        try
        {
            SetIsLoading(manager, true);
            SetPendingBackgroundExtensionLoads(manager, 0);

            var alias = new CommandAlias("test", "test.command", true);
            var settingsService = CreateSettingsService(alias, out var state);
            var aliasManager = new AliasManager(manager, settingsService.Object);

            var handled = aliasManager.CheckAlias(alias.SearchPrefix);

            Assert.IsFalse(handled);
            Assert.IsTrue(state.Current.Aliases.ContainsKey(alias.SearchPrefix));
            settingsService.Verify(s => s.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), false), Times.Never);
        }
        finally
        {
            Cleanup(manager);
        }
    }

    [TestMethod]
    public void CheckAlias_DoesNotRemoveAliasWhileBackgroundExtensionLoadsArePending()
    {
        var manager = CreateTopLevelCommandManager();
        try
        {
            SetIsLoading(manager, false);
            SetPendingBackgroundExtensionLoads(manager, 1);

            var alias = new CommandAlias("test", "test.command", true);
            var settingsService = CreateSettingsService(alias, out var state);
            var aliasManager = new AliasManager(manager, settingsService.Object);

            var handled = aliasManager.CheckAlias(alias.SearchPrefix);

            Assert.IsFalse(handled);
            Assert.IsTrue(state.Current.Aliases.ContainsKey(alias.SearchPrefix));
            settingsService.Verify(s => s.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), false), Times.Never);
        }
        finally
        {
            Cleanup(manager);
        }
    }

    [TestMethod]
    public void CheckAlias_RemovesAliasAfterAllCommandsFinishLoading()
    {
        var manager = CreateTopLevelCommandManager();
        try
        {
            SetIsLoading(manager, false);
            SetPendingBackgroundExtensionLoads(manager, 0);

            var alias = new CommandAlias("test", "test.command", true);
            var settingsService = CreateSettingsService(alias, out var state);
            var aliasManager = new AliasManager(manager, settingsService.Object);

            var handled = aliasManager.CheckAlias(alias.SearchPrefix);

            Assert.IsFalse(handled);
            Assert.IsFalse(state.Current.Aliases.ContainsKey(alias.SearchPrefix));
            settingsService.Verify(s => s.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), false), Times.Once);
        }
        finally
        {
            Cleanup(manager);
        }
    }

    private static TopLevelCommandManager CreateTopLevelCommandManager()
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(p => p.GetService(typeof(TaskScheduler))).Returns(TaskScheduler.Default);

        var commandProviderCache = new Mock<ICommandProviderCache>();
        return new TopLevelCommandManager(serviceProvider.Object, commandProviderCache.Object);
    }

    private static Mock<ISettingsService> CreateSettingsService(CommandAlias alias, out SettingsState state)
    {
        state = new SettingsState
        {
            Current = new SettingsModel
            {
                Aliases = ImmutableDictionary<string, CommandAlias>.Empty.Add(alias.SearchPrefix, alias),
            },
        };

        var settingsService = new Mock<ISettingsService>();
        var settingsState = state;
        settingsService.SetupGet(s => s.Settings).Returns(() => settingsState.Current);
        settingsService
            .Setup(s => s.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()))
            .Callback((Func<SettingsModel, SettingsModel> transform, bool _) => settingsState.Current = transform(settingsState.Current));

        return settingsService;
    }

    private sealed class SettingsState
    {
        public SettingsModel Current { get; set; } = new();
    }

    private static void SetIsLoading(TopLevelCommandManager manager, bool isLoading)
    {
        var setter = typeof(TopLevelCommandManager)
            .GetProperty(nameof(TopLevelCommandManager.IsLoading))!
            .GetSetMethod(nonPublic: true)!;
        setter.Invoke(manager, [isLoading]);
    }

    private static void SetPendingBackgroundExtensionLoads(TopLevelCommandManager manager, int count)
    {
        typeof(TopLevelCommandManager)
            .GetField("_pendingBackgroundExtensionLoads", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(manager, count);
    }

    private static void Cleanup(TopLevelCommandManager manager)
    {
        WeakReferenceMessenger.Default.UnregisterAll(manager);
        manager.Dispose();
    }
}
