// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Contracts;
using PowerToysExtension.Helpers;

namespace Microsoft.CmdPal.Ext.PowerToys.UnitTests;

[TestClass]
public class PowerDisplayCliServiceTests
{
    private static readonly int[] ExpectedSpecialProfileIds = [4, 9];

    [TestMethod]
    public async Task GetProfilesAsync_EmptyProfileListSucceeds()
    {
        var runner = new FakePowerDisplayProcessRunner(CompletedProfiles([]));
        var service = new PowerDisplayCliService(runner);

        var result = await service.GetProfilesAsync(CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        var value = result.Value;
        Assert.IsNotNull(value);
        Assert.AreEqual(0, value.Profiles.Count);
        CollectionAssert.AreEqual(
            new[] { CliCommandNames.Profiles, "--json" },
            runner.SingleCall().ToArray());
    }

    [TestMethod]
    public async Task GetProfilesAsync_PreservesDuplicateNamesAndSpecialCharacters()
    {
        const string SharedName = "Office | \u4F1A\u8BAE\nFocus";
        var profiles = new List<CliProfileInfo>
        {
            new() { Id = 4, Name = SharedName, MonitorCount = 2, LastModified = "2026-08-28T01:02:03Z" },
            new() { Id = 9, Name = SharedName, MonitorCount = 1 },
        };
        var service = new PowerDisplayCliService(new FakePowerDisplayProcessRunner(CompletedProfiles(profiles)));

        var result = await service.GetProfilesAsync(CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        var value = result.Value;
        Assert.IsNotNull(value);
        Assert.AreEqual(2, value.Profiles.Count);
        Assert.IsTrue(value.Profiles.All(profile => profile.Name == SharedName));
        CollectionAssert.AreEqual(ExpectedSpecialProfileIds, value.Profiles.Select(profile => profile.Id).ToArray());
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("{")]
    [DataRow("null")]
    [DataRow("[]")]
    public async Task GetProfilesAsync_InvalidJsonReturnsInvalidResponse(string standardOutput)
    {
        var runner = new FakePowerDisplayProcessRunner(PowerDisplayProcessResult.Completed(0, standardOutput, string.Empty));
        var service = new PowerDisplayCliService(runner);

        var result = await service.GetProfilesAsync(CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(PowerDisplayCliFailureKind.InvalidResponse, result.FailureKind);
    }

    [DataTestMethod]
    [DataRow("null")]
    [DataRow("[null]")]
    public async Task GetProfilesAsync_NullProfilePayloadReturnsInvalidResponse(string profilesJson)
    {
        const string EnvelopePrefix =
            "{\"isError\":false,\"version\":\"1.0\",\"command\":\"profiles\",\"profiles\":";
        var runner = new FakePowerDisplayProcessRunner(PowerDisplayProcessResult.Completed(
            0,
            EnvelopePrefix + profilesJson + "}",
            string.Empty));
        var service = new PowerDisplayCliService(runner);

        var result = await service.GetProfilesAsync(CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(PowerDisplayCliFailureKind.InvalidResponse, result.FailureKind);
    }

    [DataTestMethod]
    [DataRow("2.0")]
    [DataRow("not-a-version")]
    public async Task GetProfilesAsync_InvalidSchemaReturnsInvalidResponse(string version)
    {
        var response = new CliProfileListResult
        {
            Version = version,
            Profiles = [],
        };
        var runner = new FakePowerDisplayProcessRunner(PowerDisplayProcessResult.Completed(0, Serialize(response), string.Empty));
        var service = new PowerDisplayCliService(runner);

        var result = await service.GetProfilesAsync(CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(PowerDisplayCliFailureKind.InvalidResponse, result.FailureKind);
    }

    [TestMethod]
    public async Task GetProfilesAsync_CompatibleMinorSchemaSucceeds()
    {
        var response = new CliProfileListResult
        {
            Version = "1.99",
            Profiles = [],
        };
        var service = new PowerDisplayCliService(
            new FakePowerDisplayProcessRunner(PowerDisplayProcessResult.Completed(0, Serialize(response), string.Empty)));

        var result = await service.GetProfilesAsync(CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
    }

    [TestMethod]
    public async Task GetProfilesAsync_DuplicateProfileIdsReturnInvalidResponse()
    {
        var profiles = new List<CliProfileInfo>
        {
            new() { Id = 7, Name = "First", MonitorCount = 1 },
            new() { Id = 7, Name = "Second", MonitorCount = 2 },
        };
        var service = new PowerDisplayCliService(new FakePowerDisplayProcessRunner(CompletedProfiles(profiles)));

        var result = await service.GetProfilesAsync(CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(PowerDisplayCliFailureKind.InvalidResponse, result.FailureKind);
    }

    [DataTestMethod]
    [DataRow(CliExitCodes.ArgumentError, CliErrorCodes.ArgumentError, (int)PowerDisplayCliFailureKind.ArgumentError, "structured failure")]
    [DataRow(CliExitCodes.Timeout, CliErrorCodes.Timeout, (int)PowerDisplayCliFailureKind.Timeout, "structured failure")]
    [DataRow(CliExitCodes.InternalError, CliErrorCodes.InternalError, (int)PowerDisplayCliFailureKind.InternalError, "structured failure")]
    [DataRow(CliExitCodes.ProviderUnavailable, CliErrorCodes.ProviderUnavailable, (int)PowerDisplayCliFailureKind.ProviderUnavailable, "structured failure")]
    [DataRow(CliExitCodes.ProviderUnavailable, CliErrorCodes.ProviderUnavailable, (int)PowerDisplayCliFailureKind.ProviderUnavailable, "")]
    public async Task GetProfilesAsync_MapsStructuredCommandFailures(
        int exitCode,
        string errorCode,
        int expectedFailureKind,
        string errorMessage)
    {
        var error = new CliErrorResult
        {
            Command = CliCommandNames.Profiles,
            Error = new CliError
            {
                Code = errorCode,
                Message = errorMessage,
            },
        };
        var runner = new FakePowerDisplayProcessRunner(
            PowerDisplayProcessResult.Completed(exitCode, string.Empty, Serialize(error)));
        var service = new PowerDisplayCliService(runner);

        var result = await service.GetProfilesAsync(CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual((PowerDisplayCliFailureKind)expectedFailureKind, result.FailureKind);
        Assert.AreEqual(errorMessage, result.ErrorMessage);
    }

    [DataTestMethod]
    [DataRow("wrong-command", "1.0", CliErrorCodes.ArgumentError)]
    [DataRow(CliCommandNames.Profiles, "2.0", CliErrorCodes.ArgumentError)]
    [DataRow(CliCommandNames.Profiles, "1.0", CliErrorCodes.InternalError)]
    public async Task GetProfilesAsync_RejectsInconsistentErrorEnvelope(
        string command,
        string version,
        string errorCode)
    {
        var error = new CliErrorResult
        {
            Command = command,
            Version = version,
            Error = new CliError { Code = errorCode },
        };
        var runner = new FakePowerDisplayProcessRunner(
            PowerDisplayProcessResult.Completed(CliExitCodes.ArgumentError, string.Empty, Serialize(error)));
        var service = new PowerDisplayCliService(runner);

        var result = await service.GetProfilesAsync(CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(PowerDisplayCliFailureKind.InvalidResponse, result.FailureKind);
    }

    [DataTestMethod]
    [DataRow(CliErrorCodes.InternalError, CliExitCodes.InternalError, (int)PowerDisplayCliFailureKind.InternalError, "1.0")]
    [DataRow(CliErrorCodes.Timeout, CliExitCodes.Timeout, (int)PowerDisplayCliFailureKind.Timeout, "1.0")]
    [DataRow(CliErrorCodes.InternalError, CliExitCodes.InternalError, (int)PowerDisplayCliFailureKind.InternalError, "1.99")]
    public async Task BothCommands_MapGenericHostFailures(
        string errorCode,
        int exitCode,
        int expectedFailureKind,
        string version)
    {
        const string ErrorMessage = "The host could not process the request.";
        var error = new CliErrorResult
        {
            Command = "unknown",
            Version = version,
            Error = new CliError { Code = errorCode, Message = ErrorMessage },
        };
        var service = new PowerDisplayCliService(new FakePowerDisplayProcessRunner(
            PowerDisplayProcessResult.Completed(exitCode, string.Empty, Serialize(error))));

        AssertFailure(
            await service.GetProfilesAsync(CancellationToken.None),
            (PowerDisplayCliFailureKind)expectedFailureKind,
            ErrorMessage);
        AssertFailure(
            await service.ApplyProfileAsync(42, CancellationToken.None),
            (PowerDisplayCliFailureKind)expectedFailureKind,
            ErrorMessage);
    }

    [DataTestMethod]
    [DataRow("unknown", CliErrorCodes.ArgumentError, CliExitCodes.ArgumentError, "1.0")]
    [DataRow("unknown", CliErrorCodes.ProviderUnavailable, CliExitCodes.ProviderUnavailable, "1.0")]
    [DataRow("unknown", "UNRECOGNIZED_ERROR", CliExitCodes.InternalError, "1.0")]
    [DataRow("other-command", CliErrorCodes.InternalError, CliExitCodes.InternalError, "1.0")]
    [DataRow("other-command", CliErrorCodes.Timeout, CliExitCodes.Timeout, "1.0")]
    [DataRow("UNKNOWN", CliErrorCodes.InternalError, CliExitCodes.InternalError, "1.0")]
    [DataRow("unknown", CliErrorCodes.InternalError, CliExitCodes.Timeout, "1.0")]
    [DataRow("unknown", CliErrorCodes.Timeout, CliExitCodes.InternalError, "1.0")]
    [DataRow("unknown", CliErrorCodes.InternalError, CliExitCodes.InternalError, "2.0")]
    [DataRow("unknown", CliErrorCodes.Timeout, CliExitCodes.Timeout, "not-a-version")]
    public async Task BothCommands_RejectInconsistentGenericHostFailures(
        string command,
        string errorCode,
        int exitCode,
        string version)
    {
        var error = new CliErrorResult
        {
            Command = command,
            Version = version,
            Error = new CliError { Code = errorCode },
        };
        var service = new PowerDisplayCliService(new FakePowerDisplayProcessRunner(
            PowerDisplayProcessResult.Completed(exitCode, string.Empty, Serialize(error))));

        AssertFailure(
            await service.GetProfilesAsync(CancellationToken.None),
            PowerDisplayCliFailureKind.InvalidResponse);
        AssertFailure(
            await service.ApplyProfileAsync(42, CancellationToken.None),
            PowerDisplayCliFailureKind.InvalidResponse);
    }

    [DataTestMethod]
    [DataRow("null")]
    [DataRow("[]")]
    [DataRow("{}")]
    [DataRow("{\"code\":null}")]
    [DataRow("{\"code\":9}")]
    public async Task BothCommands_RejectMalformedGenericHostErrorPayloads(string errorJson)
    {
        const string EnvelopePrefix =
            "{\"isError\":true,\"version\":\"1.0\",\"command\":\"unknown\",\"error\":";
        var service = new PowerDisplayCliService(new FakePowerDisplayProcessRunner(
            PowerDisplayProcessResult.Completed(CliExitCodes.InternalError, string.Empty, EnvelopePrefix + errorJson + "}")));

        AssertFailure(
            await service.GetProfilesAsync(CancellationToken.None),
            PowerDisplayCliFailureKind.InvalidResponse);
        AssertFailure(
            await service.ApplyProfileAsync(42, CancellationToken.None),
            PowerDisplayCliFailureKind.InvalidResponse);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("\"isError\":false,")]
    [DataRow("\"isError\":\"true\",")]
    public async Task BothCommands_RequireExplicitErrorDiscriminatorForGenericHostFailures(string discriminator)
    {
        const string EnvelopeBody =
            "\"version\":\"1.0\",\"command\":\"unknown\",\"error\":{\"code\":\"INTERNAL_ERROR\"}}";
        var service = new PowerDisplayCliService(new FakePowerDisplayProcessRunner(
            PowerDisplayProcessResult.Completed(CliExitCodes.InternalError, string.Empty, "{" + discriminator + EnvelopeBody)));

        AssertFailure(
            await service.GetProfilesAsync(CancellationToken.None),
            PowerDisplayCliFailureKind.InvalidResponse);
        AssertFailure(
            await service.ApplyProfileAsync(42, CancellationToken.None),
            PowerDisplayCliFailureKind.InvalidResponse);
    }

    [DataTestMethod]
    [DataRow("unknown")]
    [DataRow("other-command")]
    public async Task BothCommands_StillRequireExpectedCommandOnSuccess(string command)
    {
        var profilesService = new PowerDisplayCliService(new FakePowerDisplayProcessRunner(
            PowerDisplayProcessResult.Completed(
                CliExitCodes.Ok,
                Serialize(new CliProfileListResult { Command = command }),
                string.Empty)));
        var applyService = new PowerDisplayCliService(new FakePowerDisplayProcessRunner(
            PowerDisplayProcessResult.Completed(
                CliExitCodes.Ok,
                Serialize(new CliApplyProfileResult { Command = command, ProfileId = 42, Profile = "Presentation" }),
                string.Empty)));

        AssertFailure(
            await profilesService.GetProfilesAsync(CancellationToken.None),
            PowerDisplayCliFailureKind.InvalidResponse);
        AssertFailure(
            await applyService.ApplyProfileAsync(42, CancellationToken.None),
            PowerDisplayCliFailureKind.InvalidResponse);
    }

    [TestMethod]
    public async Task GetProfilesAsync_RejectsUnexpectedStderrOnSuccess()
    {
        var completed = CompletedProfiles([]);
        var runner = new FakePowerDisplayProcessRunner(
            PowerDisplayProcessResult.Completed(completed.ExitCode!.Value, completed.StandardOutput, "warning"));
        var service = new PowerDisplayCliService(runner);

        var result = await service.GetProfilesAsync(CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(PowerDisplayCliFailureKind.InvalidResponse, result.FailureKind);
    }

    [TestMethod]
    public async Task ApplyProfileAsync_PassesIntegerIdAndAcceptsMatchingResponse()
    {
        const int ProfileId = 42;
        var response = new CliApplyProfileResult
        {
            ProfileId = ProfileId,
            Profile = "Presentation",
        };
        var runner = new FakePowerDisplayProcessRunner(
            PowerDisplayProcessResult.Completed(0, Serialize(response), string.Empty));
        var service = new PowerDisplayCliService(runner);

        var result = await service.ApplyProfileAsync(ProfileId, CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        var value = result.Value;
        Assert.IsNotNull(value);
        Assert.AreEqual(ProfileId, value.ProfileId);
        CollectionAssert.AreEqual(
            new[] { CliCommandNames.ApplyProfile, "42", "--json" },
            runner.SingleCall().ToArray());
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public async Task ApplyProfileAsync_InvalidIdFailsWithoutStartingProcess(int profileId)
    {
        var runner = new FakePowerDisplayProcessRunner(CompletedProfiles([]));
        var service = new PowerDisplayCliService(runner);

        var result = await service.ApplyProfileAsync(profileId, CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(PowerDisplayCliFailureKind.ArgumentError, result.FailureKind);
        Assert.AreEqual(0, runner.Calls.Count);
    }

    [TestMethod]
    public async Task ApplyProfileAsync_MismatchedResponseIdReturnsInvalidResponse()
    {
        var response = new CliApplyProfileResult
        {
            ProfileId = 8,
            Profile = "Different profile",
        };
        var service = new PowerDisplayCliService(
            new FakePowerDisplayProcessRunner(PowerDisplayProcessResult.Completed(0, Serialize(response), string.Empty)));

        var result = await service.ApplyProfileAsync(7, CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(PowerDisplayCliFailureKind.InvalidResponse, result.FailureKind);
    }

    [TestMethod]
    public async Task ApplyProfileAsync_MapsStructuredProviderUnavailableFailure()
    {
        var error = new CliErrorResult
        {
            Command = CliCommandNames.ApplyProfile,
            Error = new CliError
            {
                Code = CliErrorCodes.ProviderUnavailable,
                Message = "Power Display is not running.",
            },
        };
        var runner = new FakePowerDisplayProcessRunner(PowerDisplayProcessResult.Completed(
            CliExitCodes.ProviderUnavailable,
            string.Empty,
            Serialize(error)));
        var service = new PowerDisplayCliService(runner);

        var result = await service.ApplyProfileAsync(14, CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(PowerDisplayCliFailureKind.ProviderUnavailable, result.FailureKind);
        CollectionAssert.AreEqual(
            new[] { CliCommandNames.ApplyProfile, "14", "--json" },
            runner.SingleCall().ToArray());
    }

    [DataTestMethod]
    [DataRow((int)PowerDisplayProcessFailureKind.MissingExecutable, (int)PowerDisplayCliFailureKind.MissingExecutable)]
    [DataRow((int)PowerDisplayProcessFailureKind.Timeout, (int)PowerDisplayCliFailureKind.Timeout)]
    [DataRow((int)PowerDisplayProcessFailureKind.Cancelled, (int)PowerDisplayCliFailureKind.Cancelled)]
    [DataRow((int)PowerDisplayProcessFailureKind.StartFailure, (int)PowerDisplayCliFailureKind.ProcessFailure)]
    public async Task GetProfilesAsync_MapsProcessFailures(
        int processFailureKind,
        int expectedFailureKind)
    {
        var runner = new FakePowerDisplayProcessRunner(PowerDisplayProcessResult.Failed(
            (PowerDisplayProcessFailureKind)processFailureKind,
            "process failure"));
        var service = new PowerDisplayCliService(runner);

        var result = await service.GetProfilesAsync(CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual((PowerDisplayCliFailureKind)expectedFailureKind, result.FailureKind);
    }

    [TestMethod]
    public async Task GetProfilesAsync_RunnerExceptionReturnsProcessFailure()
    {
        var runner = new FakePowerDisplayProcessRunner(new InvalidOperationException("runner failed"));
        var service = new PowerDisplayCliService(runner);

        var result = await service.GetProfilesAsync(CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(PowerDisplayCliFailureKind.ProcessFailure, result.FailureKind);
        Assert.AreEqual("runner failed", result.ErrorMessage);
    }

    private static void AssertFailure<T>(
        PowerDisplayCliResult<T> result,
        PowerDisplayCliFailureKind expectedFailureKind,
        string? expectedErrorMessage = null)
        where T : class
    {
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(expectedFailureKind, result.FailureKind);
        if (expectedErrorMessage is not null)
        {
            Assert.AreEqual(expectedErrorMessage, result.ErrorMessage);
        }
    }

    private static PowerDisplayProcessResult CompletedProfiles(IReadOnlyList<CliProfileInfo> profiles)
        => PowerDisplayProcessResult.Completed(
            CliExitCodes.Ok,
            Serialize(new CliProfileListResult { Profiles = profiles }),
            string.Empty);

    private static string Serialize(CliProfileListResult result)
        => JsonSerializer.Serialize(result, ContractsJsonContext.Default.CliProfileListResult);

    private static string Serialize(CliApplyProfileResult result)
        => JsonSerializer.Serialize(result, ContractsJsonContext.Default.CliApplyProfileResult);

    private static string Serialize(CliErrorResult result)
        => JsonSerializer.Serialize(result, ContractsJsonContext.Default.CliErrorResult);

    private sealed class FakePowerDisplayProcessRunner : IPowerDisplayProcessRunner
    {
        private readonly Func<IReadOnlyList<string>, CancellationToken, Task<PowerDisplayProcessResult>> _run;

        internal FakePowerDisplayProcessRunner(PowerDisplayProcessResult result)
            : this((_, _) => Task.FromResult(result))
        {
        }

        internal FakePowerDisplayProcessRunner(Exception exception)
            : this((_, _) => Task.FromException<PowerDisplayProcessResult>(exception))
        {
        }

        internal FakePowerDisplayProcessRunner(
            Func<IReadOnlyList<string>, CancellationToken, Task<PowerDisplayProcessResult>> run)
        {
            _run = run;
        }

        internal List<IReadOnlyList<string>> Calls { get; } = [];

        public Task<PowerDisplayProcessResult> RunAsync(
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            Calls.Add(arguments.ToArray());
            return _run(arguments, cancellationToken);
        }

        internal IReadOnlyList<string> SingleCall()
        {
            Assert.AreEqual(1, Calls.Count);
            return Calls[0];
        }
    }
}
