// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
[DoNotParallelize]
public class PowerToysRootPageServiceTests
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(5);

    [TestMethod]
    public async Task PostLoadRootPageAsync_WaitsForExternalLoad()
    {
        using var services = CreateManagerServices();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<IEnumerable<CommandProviderWrapper>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var extensionService = new Mock<IExtensionService>();
        extensionService.Setup(s => s.LoadProvidersAsync(It.IsAny<CancellationToken>())).Returns(() =>
        {
            started.TrySetResult();
            return release.Task;
        });
        var manager = new TopLevelCommandManager(services, [extensionService.Object]);
        var service = CreateService(manager);
        try
        {
            var postLoad = Task.Run(service.PostLoadRootPageAsync);
            await started.Task.WaitAsync(LoadTimeout);
            Assert.IsFalse(postLoad.IsCompleted);

            release.SetResult([]);
            await postLoad.WaitAsync(LoadTimeout);

            Assert.IsNotNull(manager.LoadExternalProvidersCommand.ExecutionTask);
            Assert.IsTrue(manager.LoadExternalProvidersCommand.ExecutionTask.IsCompletedSuccessfully);
            extensionService.Verify(s => s.LoadProvidersAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            release.TrySetResult([]);
            await manager.DisposeAsync().AsTask().WaitAsync(LoadTimeout);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PostLoadRootPageAsync_UnsuccessfulLoad_IsObservedAndPreservesCommandOutcome(bool canceled)
    {
        using var services = CreateManagerServices();
        using var output = new StringWriter();
        using var listener = new TextWriterTraceListener(output);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<IEnumerable<CommandProviderWrapper>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var extensionService = new Mock<IExtensionService>();
        extensionService.Setup(s => s.LoadProvidersAsync(It.IsAny<CancellationToken>())).Returns(() =>
        {
            started.TrySetResult();
            return release.Task;
        });
        var manager = new TopLevelCommandManager(services, [extensionService.Object]);
        var service = CreateService(manager);
        var failure = new InvalidOperationException("External provider startup failed.");
        Trace.Listeners.Add(listener);
        try
        {
            var postLoad = Task.Run(service.PostLoadRootPageAsync);
            await started.Task.WaitAsync(LoadTimeout);
            var disposal = canceled ? manager.DisposeAsync().AsTask() : Task.CompletedTask;
            if (canceled)
            {
                release.SetResult([]);
            }
            else
            {
                release.SetException(failure);
            }

            await postLoad.WaitAsync(LoadTimeout);
            await disposal.WaitAsync(LoadTimeout);

            var executionTask = manager.LoadExternalProvidersCommand.ExecutionTask;
            Assert.IsNotNull(executionTask);
            Assert.AreEqual(canceled, executionTask.IsCanceled);
            Assert.AreEqual(!canceled, executionTask.IsFaulted);
            var log = output.ToString();
            if (canceled)
            {
                Assert.AreEqual(string.Empty, log);
            }
            else
            {
                Assert.AreSame(failure, executionTask.Exception!.InnerException);
                StringAssert.Contains(log, "Failed to load external command providers");
                Assert.AreEqual(1, log.Split(failure.Message, StringSplitOptions.None).Length - 1);
            }
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            release.TrySetResult([]);
            await manager.DisposeAsync().AsTask().WaitAsync(LoadTimeout);
        }
    }

    [TestMethod]
    [DataRow(false, true)]
    [DataRow(true, true)]
    [DataRow(false, false)]
    [DataRow(true, false)]
    public void OnPerformCommand_RequestsForegroundGrantForEveryCommand(bool topLevel, bool grantSucceeds)
    {
        var grantRequests = new List<bool>();
        var host = new CommandPaletteHost(CreateExtension(grantRequests, grantSucceeds));
        var service = CreateService();

        service.OnPerformCommand(null, topLevel, host);
        service.OnPerformCommand(null, topLevel, host);
        service.OnPerformCommand(null, topLevel, host);

        bool[] expectedGrantRequests = [true, false, false];
        CollectionAssert.AreEqual(expectedGrantRequests, grantRequests);
    }

    [TestMethod]
    public void OnPerformCommand_RequestsForegroundGrantForTheCommandOwner()
    {
        var firstGrantRequests = new List<bool>();
        var secondGrantRequests = new List<bool>();
        var firstHost = new CommandPaletteHost(CreateExtension(firstGrantRequests));
        var secondHost = new CommandPaletteHost(CreateExtension(secondGrantRequests));
        var service = CreateService();

        service.OnPerformCommand(null, false, firstHost);
        service.OnPerformCommand(null, false, firstHost);
        service.OnPerformCommand(null, false, secondHost);
        service.OnPerformCommand(null, false, secondHost);
        service.OnPerformCommand(null, false, firstHost);

        bool[] expectedFirstGrantRequests = [true, false, true];
        bool[] expectedSecondGrantRequests = [true, false];
        CollectionAssert.AreEqual(expectedFirstGrantRequests, firstGrantRequests);
        CollectionAssert.AreEqual(expectedSecondGrantRequests, secondGrantRequests);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void GoHome_AndBuiltInCommandsDoNotGrantForegroundToPreviousExtension(bool builtInCommand)
    {
        var grantRequests = new List<bool>();
        var host = new CommandPaletteHost(CreateExtension(grantRequests));
        var service = CreateService();

        service.GoHome();
        service.OnPerformCommand(null, true, CommandPaletteHost.Instance);
        service.OnPerformCommand(null, false, host);
        if (builtInCommand)
        {
            service.OnPerformCommand(null, true, CommandPaletteHost.Instance);
        }
        else
        {
            service.GoHome();
            service.GoHome();
        }

        bool[] expectedBeforeReturning = [true];
        CollectionAssert.AreEqual(expectedBeforeReturning, grantRequests);

        service.OnPerformCommand(null, false, host);

        bool[] expectedAfterReturning = [true, true];
        CollectionAssert.AreEqual(expectedAfterReturning, grantRequests);
    }

    private static IExtensionWrapper CreateExtension(List<bool> grantRequests, bool grantSucceeds = true)
    {
        var extension = new Mock<IExtensionWrapper>(MockBehavior.Strict);
        extension.Setup(static e => e.TryAllowSetForeground(It.IsAny<bool>()))
            .Callback<bool>(grantRequests.Add)
            .Returns(grantSucceeds);
        return extension.Object;
    }

    private static ServiceProvider CreateManagerServices()
    {
        var settingsService = new Mock<ISettingsService>();
        settingsService.SetupGet(s => s.Settings).Returns(new SettingsModel());
        return new ServiceCollection()
            .AddSingleton(TaskScheduler.Default)
            .AddSingleton(settingsService.Object)
            .BuildServiceProvider();
    }

    private static PowerToysRootPageService CreateService(TopLevelCommandManager? manager = null)
    {
        return new(manager!, null!, null!, null!, null!);
    }
}
