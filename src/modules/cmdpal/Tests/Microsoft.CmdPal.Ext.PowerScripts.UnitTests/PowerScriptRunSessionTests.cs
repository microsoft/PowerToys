// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;

namespace Microsoft.CmdPal.Ext.PowerScripts.UnitTests;

[TestClass]
public sealed class PowerScriptRunSessionTests
{
    [TestMethod]
    [Timeout(5000)]
    public async Task DuplicateRunRequestsAreRejectedUntilTheFirstRunFinishes()
    {
        var result = new TaskCompletionSource<PowerScriptTransformResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakePowerScriptsService { RunHandler = _ => result.Task };
        using var session = new PowerScriptRunSession(service, TestScripts.Create());
        var parameters = new Dictionary<string, JsonElement> { ["count"] = TestScripts.Json("0") };

        Assert.IsTrue(session.TryRun(parameters));
        Assert.IsFalse(session.TryRun(parameters));
        await service.RunStarted.WaitAsync(TimeSpan.FromSeconds(4));
        Assert.AreEqual(1, service.RunCalls);
        Assert.IsTrue(session.IsRunning);
        Assert.AreEqual(PowerScriptRunState.Running, session.State);

        result.SetResult(new PowerScriptTransformResult { Text = "finished" });
        await session.Completion!.WaitAsync(TimeSpan.FromSeconds(4));

        Assert.IsFalse(session.IsRunning);
        Assert.AreEqual(PowerScriptRunState.Succeeded, session.State);
        Assert.IsTrue(session.TryRun(parameters));
        await session.Completion!.WaitAsync(TimeSpan.FromSeconds(4));
        Assert.AreEqual(2, service.RunCalls);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task EmptyTextAndStandardErrorRemainASuccessfulResult()
    {
        var output = new PowerScriptTransformResult { Text = string.Empty, StdErr = "A diagnostic warning.", ExitCode = 0 };
        var service = new FakePowerScriptsService { RunHandler = _ => Task.FromResult(output) };
        using var session = new PowerScriptRunSession(service, TestScripts.Create());

        Assert.IsTrue(session.TryRun(new Dictionary<string, JsonElement>()));
        await session.Completion!.WaitAsync(TimeSpan.FromSeconds(4));

        Assert.AreEqual(PowerScriptRunState.Succeeded, session.State);
        Assert.AreSame(output, session.Result);
        Assert.AreEqual(string.Empty, session.Result!.Text);
        Assert.AreEqual("A diagnostic warning.", session.Result!.StdErr);
        Assert.IsNull(session.Error);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task SubmittedParametersSurviveCallerMutationAndJsonDocumentDisposal()
    {
        var service = new FakePowerScriptsService();
        using var session = new PowerScriptRunSession(service, TestScripts.Create());
        var document = JsonDocument.Parse("0");
        var parameters = new Dictionary<string, JsonElement> { ["count"] = document.RootElement };
        try
        {
            Assert.IsTrue(session.TryRun(parameters));
            parameters["count"] = TestScripts.Json("99");
            parameters["extra"] = TestScripts.Json("true");
        }
        finally
        {
            document.Dispose();
        }

        await session.Completion!.WaitAsync(TimeSpan.FromSeconds(4));

        Assert.AreEqual(PowerScriptRunState.Succeeded, session.State);
        Assert.AreEqual(0, service.LastParameters!["count"].GetInt32());
        Assert.IsFalse(service.LastParameters!.ContainsKey("extra"));
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task ExplicitFormDescriptorIsPreservedWhenTheSessionCatalogHasAlreadyBeenRefreshed()
    {
        var oldDescriptor = TestScripts.Create(TestScripts.Parameter("count", "int", required: true));
        var newDescriptor = TestScripts.Create(TestScripts.Parameter("mode", required: true));
        var service = new FakePowerScriptsService();
        using var session = new PowerScriptRunSession(service, oldDescriptor);
        var oldParameters = new Dictionary<string, JsonElement> { ["count"] = TestScripts.Json("0") };

        session.UpdateScript(newDescriptor);
        Assert.IsTrue(session.TryRun(oldDescriptor, oldParameters));
        oldParameters["count"] = TestScripts.Json("99");
        await session.Completion!.WaitAsync(TimeSpan.FromSeconds(4));

        Assert.AreSame(oldDescriptor, service.LastScript, "The execution service needs the original descriptor to detect a changed form contract.");
        Assert.AreEqual(0, service.LastParameters!["count"].GetInt32());
        Assert.IsFalse(service.LastParameters!.ContainsKey("mode"));
        Assert.AreEqual(1, service.RunCalls);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task StopAfterCompletionDoesNotChangeTheRecordedOutcome()
    {
        var service = new FakePowerScriptsService();
        using var session = new PowerScriptRunSession(service, TestScripts.Create());
        Assert.IsTrue(session.TryRun(new Dictionary<string, JsonElement>()));
        await session.Completion!.WaitAsync(TimeSpan.FromSeconds(4));

        session.RequestStop();

        Assert.AreEqual(PowerScriptRunState.Succeeded, session.State);
        Assert.IsFalse(session.StopWasRequested);
        Assert.AreEqual(1, service.RunCalls);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task RequestStopCancelsTheServiceAndReportsStopped()
    {
        var service = CancelableService();
        using var session = new PowerScriptRunSession(service, TestScripts.Create());
        Assert.IsTrue(session.TryRun(new Dictionary<string, JsonElement>()));
        await service.RunStarted.WaitAsync(TimeSpan.FromSeconds(4));

        session.RequestStop();
        await session.Completion!.WaitAsync(TimeSpan.FromSeconds(4));

        Assert.IsTrue(service.RunCancellationToken.IsCancellationRequested);
        Assert.IsTrue(session.StopWasRequested);
        Assert.AreEqual(PowerScriptRunState.Stopped, session.State);
        Assert.IsFalse(session.IsRunning);
        Assert.IsNull(session.Error);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task DeadlineCancellationIsReportedAsTimeoutRatherThanUserStop()
    {
        var service = CancelableService();
        using var session = new PowerScriptRunSession(service, TestScripts.Create(), TimeSpan.FromMilliseconds(20));

        Assert.IsTrue(session.TryRun(new Dictionary<string, JsonElement>()));
        await session.Completion!.WaitAsync(TimeSpan.FromSeconds(4));

        Assert.AreEqual(PowerScriptRunState.TimedOut, session.State);
        Assert.IsFalse(session.StopWasRequested);
        Assert.IsTrue(service.RunCancellationToken.IsCancellationRequested);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task HostTimeoutExitCodeIsAlsoReportedAsTimeout()
    {
        var service = new FakePowerScriptsService
        {
            RunHandler = _ => Task.FromResult(new PowerScriptTransformResult
            {
                ExitCode = PowerScriptsProtocol.ExitCode.Timeout,
                StdErr = "Host execution deadline reached.",
            }),
        };
        using var session = new PowerScriptRunSession(service, TestScripts.Create());

        Assert.IsTrue(session.TryRun(new Dictionary<string, JsonElement>()));
        await session.Completion!.WaitAsync(TimeSpan.FromSeconds(4));

        Assert.AreEqual(PowerScriptRunState.TimedOut, session.State);
        Assert.AreEqual("Host execution deadline reached.", session.Result!.StdErr);
        Assert.IsFalse(session.StopWasRequested);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task SuccessfulCompletionAfterStopRequestDoesNotClaimTheActionWasUndone()
    {
        var result = new TaskCompletionSource<PowerScriptTransformResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakePowerScriptsService { RunHandler = _ => result.Task };
        using var session = new PowerScriptRunSession(service, TestScripts.Create());
        Assert.IsTrue(session.TryRun(new Dictionary<string, JsonElement>()));
        await service.RunStarted.WaitAsync(TimeSpan.FromSeconds(4));

        session.RequestStop();
        Assert.AreEqual(PowerScriptRunState.StopRequested, session.State);
        Assert.IsTrue(session.IsRunning);
        Assert.IsTrue(service.RunCancellationToken.IsCancellationRequested);

        result.SetResult(new PowerScriptTransformResult { Text = "The action had already finished." });
        await session.Completion!.WaitAsync(TimeSpan.FromSeconds(4));

        Assert.AreEqual(PowerScriptRunState.Succeeded, session.State);
        Assert.IsTrue(session.StopWasRequested);
        Assert.AreEqual("The action had already finished.", session.Result!.Text);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task UnrelatedCancellationIsAnErrorRatherThanAUserStopOrTimeout()
    {
        var service = new FakePowerScriptsService
        {
            RunHandler = _ => Task.FromException<PowerScriptTransformResult>(new OperationCanceledException("Unexpected service cancellation.")),
        };
        using var session = new PowerScriptRunSession(service, TestScripts.Create());

        Assert.IsTrue(session.TryRun(new Dictionary<string, JsonElement>()));
        await session.Completion!.WaitAsync(TimeSpan.FromSeconds(4));

        Assert.AreEqual(PowerScriptRunState.Failed, session.State);
        Assert.AreEqual("Unexpected service cancellation.", session.Error);
        Assert.IsFalse(session.StopWasRequested);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task SynchronousServiceFailureIsObservedAndCanBeRetried()
    {
        var service = new FakePowerScriptsService { RunHandler = _ => throw new InvalidOperationException("Host not found.") };
        using var session = new PowerScriptRunSession(service, TestScripts.Create());

        Assert.IsTrue(session.TryRun(new Dictionary<string, JsonElement>()));
        await session.Completion!.WaitAsync(TimeSpan.FromSeconds(4));
        Assert.AreEqual(PowerScriptRunState.Failed, session.State);
        Assert.AreEqual("Host not found.", session.Error);
        Assert.IsFalse(session.IsRunning);

        service.RunHandler = _ => Task.FromResult(new PowerScriptTransformResult());
        Assert.IsTrue(session.TryRun(new Dictionary<string, JsonElement>()));
        await session.Completion!.WaitAsync(TimeSpan.FromSeconds(4));
        Assert.AreEqual(PowerScriptRunState.Succeeded, session.State);
        Assert.IsNull(session.Error);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task DisposingTheProviderOwnedSessionCancelsExecutionAndPreventsFurtherRuns()
    {
        var service = CancelableService();
        using var session = new PowerScriptRunSession(service, TestScripts.Create());
        Assert.IsTrue(session.TryRun(new Dictionary<string, JsonElement>()));
        await service.RunStarted.WaitAsync(TimeSpan.FromSeconds(4));

        session.Dispose();
        await session.Completion!.WaitAsync(TimeSpan.FromSeconds(4));

        Assert.IsTrue(service.RunCancellationToken.IsCancellationRequested);
        Assert.AreEqual(PowerScriptRunState.Stopped, session.State);
        Assert.IsFalse(session.TryRun(new Dictionary<string, JsonElement>()));
        Assert.AreEqual(1, service.RunCalls);
    }

    private static FakePowerScriptsService CancelableService() => new()
    {
        RunHandler = async cancellationToken =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new PowerScriptTransformResult();
        },
    };
}
