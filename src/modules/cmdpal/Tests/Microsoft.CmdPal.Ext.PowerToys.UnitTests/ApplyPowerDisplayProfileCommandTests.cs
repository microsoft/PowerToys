// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Contracts;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;

namespace Microsoft.CmdPal.Ext.PowerToys.UnitTests;

[TestClass]
public class ApplyPowerDisplayProfileCommandTests
{
    [TestMethod]
    public void Invoke_SuccessPassesProfileIdAndDismissesAfterToast()
    {
        const int ProfileId = 37;
        var service = new FakePowerDisplayCliService
        {
            ApplyProfileHandler = (profileId, _) => Task.FromResult(
                PowerDisplayCliResult<CliApplyProfileResult>.Success(new CliApplyProfileResult
                {
                    ProfileId = profileId,
                    Profile = "CLI profile name",
                })),
        };
        var command = new ApplyPowerDisplayProfileCommand(ProfileId, "Palette profile name", service);

        var result = command.Invoke();

        Assert.AreEqual(ProfileId, service.LastAppliedProfileId);
        Assert.AreEqual("com.microsoft.powertoys.powerDisplay.applyProfile.37", command.Id);
        Assert.AreEqual("Palette profile name (#37)", command.Name);
        Assert.AreEqual(CommandResultKind.ShowToast, result.Kind);
        var toast = GetToast(result);
        StringAssert.Contains(toast.Message!, "CLI profile name");
        Assert.AreEqual(CommandResultKind.Dismiss, toast.Result!.Kind);
    }

    [DataTestMethod]
    [DataRow((int)PowerDisplayCliFailureKind.ArgumentError, "no longer available")]
    [DataRow((int)PowerDisplayCliFailureKind.Timeout, "in time")]
    [DataRow((int)PowerDisplayCliFailureKind.InternalError, "couldn't process")]
    [DataRow((int)PowerDisplayCliFailureKind.ProviderUnavailable, "isn't running")]
    [DataRow((int)PowerDisplayCliFailureKind.MissingExecutable, "isn't available")]
    [DataRow((int)PowerDisplayCliFailureKind.InvalidResponse, "invalid response")]
    [DataRow((int)PowerDisplayCliFailureKind.Cancelled, "cancelled")]
    [DataRow((int)PowerDisplayCliFailureKind.ProcessFailure, "couldn't process")]
    public void Invoke_FailureKeepsPaletteOpen(
        int failureKind,
        string expectedMessageFragment)
    {
        var service = new FakePowerDisplayCliService
        {
            ApplyProfileHandler = (_, _) => Task.FromResult(
                PowerDisplayCliResult<CliApplyProfileResult>.Failure((PowerDisplayCliFailureKind)failureKind)),
        };
        var command = new ApplyPowerDisplayProfileCommand(12, "Profile", service);

        var result = command.Invoke();

        Assert.AreEqual(12, service.LastAppliedProfileId);
        Assert.AreEqual(CommandResultKind.ShowToast, result.Kind);
        var toast = GetToast(result);
        StringAssert.Contains(toast.Message!, expectedMessageFragment);
        Assert.AreEqual(CommandResultKind.KeepOpen, toast.Result!.Kind);
    }

    [TestMethod]
    public void Invoke_ServiceExceptionReturnsFailureToast()
    {
        var service = new FakePowerDisplayCliService
        {
            ApplyProfileHandler = (_, _) => Task.FromException<PowerDisplayCliResult<CliApplyProfileResult>>(
                new InvalidOperationException("service failed")),
        };
        var command = new ApplyPowerDisplayProfileCommand(8, "Profile", service);

        var result = command.Invoke();

        var toast = GetToast(result);
        Assert.AreEqual(CommandResultKind.KeepOpen, toast.Result!.Kind);
        StringAssert.Contains(toast.Message!, "couldn't process");
    }

    [TestMethod]
    public void Constructor_RejectsNonPositiveProfileId()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => _ = new ApplyPowerDisplayProfileCommand(0, "Profile", new FakePowerDisplayCliService()));
    }

    private static ToastArgs GetToast(CommandResult result)
    {
        var toast = result.Args as ToastArgs;
        Assert.IsNotNull(toast);
        Assert.IsNotNull(toast.Message);
        Assert.IsNotNull(toast.Result);
        return toast;
    }
}
