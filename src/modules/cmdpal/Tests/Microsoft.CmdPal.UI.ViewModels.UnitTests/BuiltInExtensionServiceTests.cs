// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class BuiltInExtensionServiceTests
{
    private Mock<ISettingsService> _mockSettingsService = null!;
    private ILoggerFactory _loggerFactory = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockSettingsService = new Mock<ISettingsService>();
        _mockSettingsService.Setup(s => s.Settings).Returns(CreateMinimalSettingsModel());
        _mockSettingsService.Setup(s => s.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()));
        _loggerFactory = NullLoggerFactory.Instance;
    }

    private static SettingsModel CreateMinimalSettingsModel()
    {
        var minimalJson = "{}";
        return System.Text.Json.JsonSerializer.Deserialize(
            minimalJson,
            JsonSerializationContext.Default.SettingsModel) ?? new SettingsModel();
    }

    private BuiltInExtensionService CreateService(IEnumerable<ICommandProvider>? providers = null)
    {
        return new BuiltInExtensionService(
            providers ?? Array.Empty<ICommandProvider>(),
            TaskScheduler.Default,
            null!,
            null!,
            _mockSettingsService.Object,
            _loggerFactory);
    }

    [TestMethod]
    public async Task SignalStopExtensionsAsync_CompletesImmediately()
    {
        // Arrange — built-in providers live in-proc; stop is a no-op.
        using var service = CreateService();

        // Act
        var task = service.SignalStopExtensionsAsync();

        // Assert
        Assert.IsTrue(task.IsCompleted, "SignalStopExtensionsAsync should return a completed task");
        await task;
    }

    [TestMethod]
    public async Task EnableProviderAsync_UnknownId_DoesNotThrow()
    {
        // Arrange — service with no providers loaded.
        using var service = CreateService();

        // Act & Assert — enabling a nonexistent provider should be a silent no-op.
        await service.EnableProviderAsync("nonexistent.provider.id");
    }

    [TestMethod]
    public async Task DisableProviderAsync_UnknownId_DoesNotThrow()
    {
        // Arrange — service with no providers loaded.
        using var service = CreateService();

        // Act & Assert — disabling a nonexistent provider should be a silent no-op.
        await service.DisableProviderAsync("nonexistent.provider.id");
    }

    [TestMethod]
    public void Dispose_WithoutStart_DoesNotThrow()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert — disposing before any load should not throw.
        service.Dispose();
    }

    [TestMethod]
    public async Task SignalStartExtensionsAsync_NoProviders_DoesNotFireEvents()
    {
        // Arrange
        using var service = CreateService(Array.Empty<ICommandProvider>());
        var providerAddedFired = false;
        var commandsAddedFired = false;

        service.OnCommandProviderAdded += (_, _) => providerAddedFired = true;
        service.OnCommandsAdded += (_, _) => commandsAddedFired = true;

        // Act
        await service.SignalStartExtensionsAsync(new WeakReference<IPageContext>(null!));

        // Assert — with no providers there is nothing to load.
        Assert.IsFalse(providerAddedFired, "OnCommandProviderAdded should not fire when there are no providers");
        Assert.IsFalse(commandsAddedFired, "OnCommandsAdded should not fire when there are no providers");
    }

    [TestMethod]
    public async Task SignalStartExtensionsAsync_CalledTwice_LoadsOnlyOnce()
    {
        // Arrange — use a concrete stub provider to track how many times
        // LoadBuiltInsAsync processes it.
        var stub = new StubCommandProvider("double-load-test");
        using var service = CreateService(new[] { stub });

        var addedCount = 0;
        service.OnCommandProviderAdded += (_, _) => addedCount++;

        var weakContext = new WeakReference<IPageContext>(null!);

        // Act — call twice
        await service.SignalStartExtensionsAsync(weakContext);
        await service.SignalStartExtensionsAsync(weakContext);

        // Assert — second call should skip loading because _isLoaded is true.
        Assert.AreEqual(1, addedCount, "LoadBuiltInsAsync should only run once even if SignalStart is called twice");
    }

    [TestMethod]
    public async Task SignalStartExtensionsAsync_WithProvider_FiresOnCommandProviderAdded()
    {
        // Arrange
        var stub = new StubCommandProvider("test.builtin.provider");
        using var service = CreateService(new ICommandProvider[] { stub });

        CommandProviderWrapper? receivedWrapper = null;
        service.OnCommandProviderAdded += (_, wrappers) =>
        {
            foreach (var w in wrappers)
            {
                receivedWrapper = w;
            }
        };

        // Act
        await service.SignalStartExtensionsAsync(new WeakReference<IPageContext>(null!));

        // Assert
        Assert.IsNotNull(receivedWrapper, "OnCommandProviderAdded should fire with a wrapper");
        Assert.AreEqual("test.builtin.provider", receivedWrapper!.Id);
    }

    [TestMethod]
    public async Task SignalStartExtensionsAsync_WithProvider_FiresOnCommandsAdded()
    {
        // Arrange
        var stub = new StubCommandProvider("commands-test");
        using var service = CreateService(new ICommandProvider[] { stub });

        var commandsAddedFired = false;
        service.OnCommandsAdded += (_, _) => commandsAddedFired = true;

        // Act
        await service.SignalStartExtensionsAsync(new WeakReference<IPageContext>(null!));

        // Assert — even with no actual top-level commands, the event fires with an empty collection.
        Assert.IsTrue(commandsAddedFired, "OnCommandsAdded should fire for each loaded provider");
    }

    [TestMethod]
    public async Task DisableProviderAsync_KnownId_FiresOnCommandsRemoved()
    {
        // Arrange — start the service so the provider is loaded and enabled.
        var stub = new StubCommandProvider("disable-test");
        using var service = CreateService(new ICommandProvider[] { stub });
        await service.SignalStartExtensionsAsync(new WeakReference<IPageContext>(null!));

        var commandsRemovedFired = false;
        service.OnCommandsRemoved += (_, _) => commandsRemovedFired = true;

        // Act
        await service.DisableProviderAsync("disable-test");

        // Assert
        Assert.IsTrue(commandsRemovedFired, "OnCommandsRemoved should fire when disabling a known provider");
    }

    [TestMethod]
    public async Task EnableProviderAsync_AlreadyEnabled_IsNoOp()
    {
        // Arrange — start the service; the provider is enabled by default.
        var stub = new StubCommandProvider("already-enabled");
        using var service = CreateService(new ICommandProvider[] { stub });
        await service.SignalStartExtensionsAsync(new WeakReference<IPageContext>(null!));

        var commandsAddedCount = 0;
        service.OnCommandsAdded += (_, _) => commandsAddedCount++;

        // Act — enabling a provider that's already enabled should bail out.
        await service.EnableProviderAsync("already-enabled");

        // Assert — no additional events fired.
        Assert.AreEqual(0, commandsAddedCount, "Re-enabling an already-enabled provider should not fire OnCommandsAdded");
    }

    [TestMethod]
    public async Task DisableProviderAsync_ThenEnable_FiresOnCommandsAdded()
    {
        // Arrange
        var stub = new StubCommandProvider("toggle-test");
        using var service = CreateService(new ICommandProvider[] { stub });
        await service.SignalStartExtensionsAsync(new WeakReference<IPageContext>(null!));

        // Disable first
        await service.DisableProviderAsync("toggle-test");

        var commandsAddedFired = false;
        service.OnCommandsAdded += (_, _) => commandsAddedFired = true;

        // Act — re-enable
        await service.EnableProviderAsync("toggle-test");

        // Assert
        Assert.IsTrue(commandsAddedFired, "Re-enabling a disabled provider should fire OnCommandsAdded");
    }

    /// <summary>
    /// Minimal in-proc command provider for test isolation.
    /// Returns no commands, no fallbacks, no dock bands.
    /// </summary>
    private sealed partial class StubCommandProvider : CommandProvider
    {
        public StubCommandProvider(string id)
        {
            Id = id;
            DisplayName = $"Stub: {id}";
        }

        public override ICommandItem[] TopLevelCommands() => [];

        public override IFallbackCommandItem[]? FallbackCommands() => null;
    }
}
