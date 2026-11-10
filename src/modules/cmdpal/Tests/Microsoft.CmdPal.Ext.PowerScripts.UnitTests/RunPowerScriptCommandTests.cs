// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;

namespace Microsoft.CmdPal.Ext.PowerScripts.UnitTests;

[TestClass]
public sealed class RunPowerScriptCommandTests
{
    [TestMethod]
    public void ConstructingTheCommandAndReadingItsPageDoesNotStartExecution()
    {
        var service = new FakePowerScriptsService();
        var script = TestScripts.Create();
        using var session = new PowerScriptRunSession(service, script);
        using var page = new PowerScriptPage(service, script, session);
        var navigations = 0;
        var command = new RunPowerScriptCommand(script, session, page, _ => navigations++);

        page.GetContent();
        page.GetContent();

        Assert.AreEqual(page.Id + ".run", command.Id);
        Assert.AreEqual(0, navigations);
        Assert.AreEqual(0, service.RunCalls);
        Assert.IsNull(session.Completion);
        Assert.AreEqual(PowerScriptRunState.Idle, session.State);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task ExplicitInvocationOpensTheResultPageBeforeStartingExecution()
    {
        var service = new FakePowerScriptsService();
        var script = TestScripts.Create();
        using var session = new PowerScriptRunSession(service, script);
        using var page = new PowerScriptPage(service, script, session);
        IPage? destination = null;
        PowerScriptRunState? stateWhenNavigated = null;
        var command = new RunPowerScriptCommand(script, session, page, target =>
        {
            destination = target;
            stateWhenNavigated = session.State;
        });

        var result = command.Invoke();
        await session.Completion!.WaitAsync(TimeSpan.FromSeconds(4));

        Assert.AreEqual(CommandResultKind.KeepOpen, result.Kind);
        Assert.AreSame(page, destination);
        Assert.AreEqual(PowerScriptRunState.Idle, stateWhenNavigated);
        Assert.AreEqual(1, service.RunCalls);
        Assert.AreSame(script, service.LastScript);
        Assert.HasCount(0, service.LastParameters!);
        Assert.AreEqual(PowerScriptRunState.Succeeded, session.State);
    }

    [TestMethod]
    public void AResultPageNavigationFailureDoesNotStartTheScript()
    {
        var service = new FakePowerScriptsService();
        var script = TestScripts.Create();
        using var session = new PowerScriptRunSession(service, script);
        using var page = new PowerScriptPage(service, script, session);
        var command = new RunPowerScriptCommand(script, session, page, _ => throw new InvalidOperationException("Navigation failed."));

        Assert.ThrowsExactly<InvalidOperationException>(() => command.Invoke());

        Assert.AreEqual(0, service.RunCalls);
        Assert.IsNull(session.Completion);
        Assert.AreEqual(PowerScriptRunState.Idle, session.State);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task RepeatedInvocationWhileRunningOpensTheSamePageWithoutStartingAnotherRun()
    {
        var completion = new TaskCompletionSource<PowerScriptTransformResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakePowerScriptsService { RunHandler = _ => completion.Task };
        var script = TestScripts.Create();
        using var session = new PowerScriptRunSession(service, script);
        using var page = new PowerScriptPage(service, script, session);
        var navigations = 0;
        var command = new RunPowerScriptCommand(script, session, page, _ => navigations++);

        command.Invoke();
        var firstRun = session.Completion;
        command.Invoke();
        page.GetContent();
        page.GetContent();
        await service.RunStarted.WaitAsync(TimeSpan.FromSeconds(4));

        Assert.AreSame(firstRun, session.Completion);
        Assert.AreEqual(2, navigations);
        Assert.AreEqual(1, service.RunCalls);
        Assert.IsTrue(session.IsRunning);

        completion.SetResult(new PowerScriptTransformResult { Text = "finished" });
        await session.Completion!.WaitAsync(TimeSpan.FromSeconds(4));
        page.GetContent();
        page.GetContent();
        Assert.AreEqual(1, service.RunCalls, "Reading the completed result must not rerun the script.");

        command.Invoke();
        await session.Completion!.WaitAsync(TimeSpan.FromSeconds(4));

        Assert.AreNotSame(firstRun, session.Completion);
        Assert.AreEqual(3, navigations);
        Assert.AreEqual(2, service.RunCalls);
        Assert.AreEqual(PowerScriptRunState.Succeeded, session.State);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AStaleNoParameterCommandOnlyOpensTheFormAfterParametersAreAdded(bool required)
    {
        var service = new FakePowerScriptsService();
        var initial = TestScripts.Create();
        var updated = TestScripts.Create(TestScripts.Parameter("value", required: required, defaultValue: required ? null : "default"));
        using var session = new PowerScriptRunSession(service, initial);
        using var page = new PowerScriptPage(service, initial, session);
        IPage? destination = null;
        var command = new RunPowerScriptCommand(initial, session, page, target => destination = target);

        page.UpdateScript(updated);
        command.UpdateScript(updated);
        var result = command.Invoke();
        page.GetContent();

        Assert.AreEqual(CommandResultKind.KeepOpen, result.Kind);
        Assert.AreSame(page, destination);
        Assert.AreEqual(0, service.RunCalls);
        Assert.IsNull(session.Completion);
        Assert.AreEqual(PowerScriptRunState.Idle, session.State);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task ARefreshedNoParameterCommandPassesTheCurrentDescriptorToTheExecutionService()
    {
        var service = new FakePowerScriptsService();
        var initial = TestScripts.Create();
        var updated = new PowerScriptInfo { Id = initial.Id, Name = "Updated action", Description = "Updated metadata" };
        using var session = new PowerScriptRunSession(service, initial);
        using var page = new PowerScriptPage(service, initial, session);
        var command = new RunPowerScriptCommand(initial, session, page, static _ => { });

        command.UpdateScript(updated);
        command.Invoke();
        await session.Completion!.WaitAsync(TimeSpan.FromSeconds(4));

        Assert.AreSame(updated, service.LastScript);
        Assert.HasCount(0, service.LastParameters!);
        Assert.AreEqual(1, service.RunCalls);
    }
}
