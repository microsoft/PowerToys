// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.ModuleContracts;
using PowerToysExtension.Commands;

namespace Microsoft.CmdPal.Ext.PowerToys.UnitTests;

[TestClass]
public class AwakeCommandResultTests
{
    [TestMethod]
    public void StartAwakeCommandWithoutToastDismisses()
    {
        var onSuccessInvoked = false;
        var command = new StartAwakeCommand(
            "Start Awake",
            () => Task.FromResult(OperationResult.Ok()),
            onSuccess: () => onSuccessInvoked = true);

        var result = command.Invoke();

        Assert.AreEqual(CommandResultKind.Dismiss, result.Kind);
        Assert.IsTrue(onSuccessInvoked);
    }

    [TestMethod]
    public void StartAwakeCommandToastDismisses()
    {
        var command = new StartAwakeCommand(
            "Start Awake",
            () => Task.FromResult(OperationResult.Ok()),
            successToast: "Awake started");

        AssertToastDismisses(command.Invoke());
    }

    [TestMethod]
    public void StartAwakeCommandFailureDismissesAfterToast()
    {
        var command = new StartAwakeCommand(
            "Start Awake",
            () => Task.FromResult(OperationResult.Fail("Failed to start")));

        AssertToastDismisses(command.Invoke());
    }

    [TestMethod]
    public void RefreshAwakeStatusCommandDismisses()
    {
        var refreshInvoked = false;
        var command = new RefreshAwakeStatusCommand(() => refreshInvoked = true);

        var result = command.Invoke();

        Assert.AreEqual(CommandResultKind.Dismiss, result.Kind);
        Assert.IsTrue(refreshInvoked);
    }

    private static void AssertToastDismisses(CommandResult result)
    {
        Assert.AreEqual(CommandResultKind.ShowToast, result.Kind);
        var args = result.Args as ToastArgs;
        Assert.IsNotNull(args);
        var toastResult = args.Result as CommandResult;
        Assert.IsNotNull(toastResult);
        Assert.AreEqual(CommandResultKind.Dismiss, toastResult.Kind);
    }
}
