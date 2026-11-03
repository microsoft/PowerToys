// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.CmdPal.Common;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
[DoNotParallelize]
public sealed partial class AppExtensionHostTests
{
    private readonly Mock<ILogger> _logger = new();
    private ILogger? _originalLogger;
    private SynchronizationContext? _originalSynchronizationContext;

    private sealed partial class TestHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Test extension";
    }

    [TestInitialize]
    public void Initialize()
    {
        _originalLogger = CoreLogger.Instance;
        _originalSynchronizationContext = SynchronizationContext.Current;
        CoreLogger.InitializeLogger(_logger.Object);
        SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
    }

    [TestCleanup]
    public void Cleanup()
    {
        CoreLogger.InitializeLogger(_originalLogger!);
        SynchronizationContext.SetSynchronizationContext(_originalSynchronizationContext);
    }

    [TestMethod]
    [DataRow(MessageState.Info, nameof(ILogger.LogInfo))]
    [DataRow(MessageState.Success, nameof(ILogger.LogInfo))]
    [DataRow(MessageState.Warning, nameof(ILogger.LogWarning))]
    [DataRow(MessageState.Error, nameof(ILogger.LogError))]
    [DataRow((MessageState)1234, nameof(ILogger.LogInfo))]
    public void LogMessage_WritesToFileLoggerWithSeverity(MessageState state, string loggerMethod)
    {
        var result = new TestHost().LogMessage(new LogMessage("Extension diagnostic") { State = state });

        Assert.AreEqual(AsyncStatus.Completed, result.Status);
        Assert.AreEqual(1, _logger.Invocations.Count);
        Assert.AreEqual(loggerMethod, _logger.Invocations[0].Method.Name);
        Assert.AreEqual("Extension diagnostic", _logger.Invocations[0].Arguments[0]);
    }

    [TestMethod]
    public void LogMessage_NullDoesNotLog()
    {
        var result = new TestHost().LogMessage(null);

        Assert.AreEqual(AsyncStatus.Completed, result.Status);
        Assert.AreEqual(0, _logger.Invocations.Count);
    }

    [TestMethod]
    public void DebugLog_PreservesCallerInformationAndDebugBuildOnlyDiagnostics()
    {
        var sourceLineNumber = GetCurrentLineNumber() + 1;
        new TestHost().DebugLog("Debug diagnostic");

#if DEBUG
        _logger.Verify(
            logger => logger.LogDebug(
                "[Host message] Debug diagnostic",
                nameof(DebugLog_PreservesCallerInformationAndDebugBuildOnlyDiagnostics),
                It.Is<string>(path => path.EndsWith("AppExtensionHostTests.cs", StringComparison.Ordinal)),
                sourceLineNumber),
            Times.Once);
#else
        Assert.AreEqual(0, _logger.Invocations.Count);
#endif
    }

    [TestMethod]
    public void LogMessage_DoesNotRetainExtensionObject()
    {
        var message = LogAndReleaseMessage(new TestHost());

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.IsFalse(message.IsAlive, "Logging must not retain the extension's log object after the call completes.");
    }

    private static int GetCurrentLineNumber([CallerLineNumber] int line = 0) => line;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference LogAndReleaseMessage(AppExtensionHost host)
    {
        var message = new LogMessage("Collectible diagnostic");
        _ = host.LogMessage(message);
        return new WeakReference(message);
    }
}
