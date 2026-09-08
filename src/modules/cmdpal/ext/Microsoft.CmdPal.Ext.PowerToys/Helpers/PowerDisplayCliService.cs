// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using PowerDisplay.Contracts;

namespace PowerToysExtension.Helpers;

internal sealed class PowerDisplayCliService : IPowerDisplayCliService
{
    private static readonly string[] ProfilesArguments = [CliCommandNames.Profiles, "--json"];

    private readonly IPowerDisplayProcessRunner _processRunner;

    internal PowerDisplayCliService()
        : this(new PowerDisplayProcessRunner())
    {
    }

    internal PowerDisplayCliService(IPowerDisplayProcessRunner processRunner)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public async Task<PowerDisplayCliResult<CliProfileListResult>> GetProfilesAsync(CancellationToken cancellationToken)
    {
        PowerDisplayProcessResult processResult;
        try
        {
            processResult = await _processRunner.RunAsync(ProfilesArguments, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return PowerDisplayCliResult<CliProfileListResult>.Failure(PowerDisplayCliFailureKind.Cancelled);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"PowerDisplay CLI profiles invocation failed: {ex}");
            return PowerDisplayCliResult<CliProfileListResult>.Failure(
                PowerDisplayCliFailureKind.ProcessFailure,
                errorMessage: ex.Message);
        }

        var processFailure = MapProcessFailure<CliProfileListResult>(processResult);
        if (processFailure is not null)
        {
            return processFailure;
        }

        if (processResult.ExitCode != CliExitCodes.Ok)
        {
            return ParseCommandFailure<CliProfileListResult>(processResult, CliCommandNames.Profiles);
        }

        if (!string.IsNullOrWhiteSpace(processResult.StandardError))
        {
            return InvalidResponse<CliProfileListResult>(processResult);
        }

        try
        {
            var json = processResult.StandardOutput.Trim();
            if (!HasExpectedEnvelopeShape(json, false, "profiles"))
            {
                return InvalidResponse<CliProfileListResult>(processResult);
            }

            var result = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliProfileListResult);
            if (result is null || result.IsError ||
                !HasCompatibleSchemaVersion(result.Version) ||
                !string.Equals(result.Command, CliCommandNames.Profiles, StringComparison.Ordinal) ||
                result.Profiles is null ||
                result.Profiles.Any(profile => profile is null ||
                                               profile.Id <= 0 ||
                                               profile.MonitorCount < 0 ||
                                               string.IsNullOrWhiteSpace(profile.Name)) ||
                result.Profiles.Select(profile => profile.Id).Distinct().Count() != result.Profiles.Count)
            {
                return InvalidResponse<CliProfileListResult>(processResult);
            }

            return PowerDisplayCliResult<CliProfileListResult>.Success(result);
        }
        catch (JsonException ex)
        {
            return InvalidResponse<CliProfileListResult>(processResult, ex.Message);
        }
    }

    public async Task<PowerDisplayCliResult<CliApplyProfileResult>> ApplyProfileAsync(
        int profileId,
        CancellationToken cancellationToken)
    {
        if (profileId <= 0)
        {
            return PowerDisplayCliResult<CliApplyProfileResult>.Failure(
                PowerDisplayCliFailureKind.ArgumentError,
                CliExitCodes.ArgumentError);
        }

        string[] arguments =
        [
            CliCommandNames.ApplyProfile,
            profileId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--json",
        ];

        PowerDisplayProcessResult processResult;
        try
        {
            processResult = await _processRunner.RunAsync(arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return PowerDisplayCliResult<CliApplyProfileResult>.Failure(PowerDisplayCliFailureKind.Cancelled);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"PowerDisplay CLI apply-profile invocation failed: {ex}");
            return PowerDisplayCliResult<CliApplyProfileResult>.Failure(
                PowerDisplayCliFailureKind.ProcessFailure,
                errorMessage: ex.Message);
        }

        var processFailure = MapProcessFailure<CliApplyProfileResult>(processResult);
        if (processFailure is not null)
        {
            return processFailure;
        }

        if (processResult.ExitCode != CliExitCodes.Ok)
        {
            return ParseCommandFailure<CliApplyProfileResult>(processResult, CliCommandNames.ApplyProfile);
        }

        if (!string.IsNullOrWhiteSpace(processResult.StandardError))
        {
            return InvalidResponse<CliApplyProfileResult>(processResult);
        }

        try
        {
            var json = processResult.StandardOutput.Trim();
            if (!HasExpectedEnvelopeShape(json, false))
            {
                return InvalidResponse<CliApplyProfileResult>(processResult);
            }

            var result = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliApplyProfileResult);
            if (result is null || result.IsError ||
                !HasCompatibleSchemaVersion(result.Version) ||
                !string.Equals(result.Command, CliCommandNames.ApplyProfile, StringComparison.Ordinal) ||
                result.ProfileId <= 0 || result.ProfileId != profileId ||
                string.IsNullOrWhiteSpace(result.Profile))
            {
                return InvalidResponse<CliApplyProfileResult>(processResult);
            }

            return PowerDisplayCliResult<CliApplyProfileResult>.Success(result);
        }
        catch (JsonException ex)
        {
            return InvalidResponse<CliApplyProfileResult>(processResult, ex.Message);
        }
    }

    private static PowerDisplayCliResult<T>? MapProcessFailure<T>(PowerDisplayProcessResult processResult)
        where T : class
    {
        return processResult.FailureKind switch
        {
            PowerDisplayProcessFailureKind.None => null,
            PowerDisplayProcessFailureKind.MissingExecutable => PowerDisplayCliResult<T>.Failure(
                PowerDisplayCliFailureKind.MissingExecutable,
                errorMessage: processResult.ErrorMessage),
            PowerDisplayProcessFailureKind.Timeout => PowerDisplayCliResult<T>.Failure(
                PowerDisplayCliFailureKind.Timeout,
                CliExitCodes.Timeout),
            PowerDisplayProcessFailureKind.Cancelled => PowerDisplayCliResult<T>.Failure(
                PowerDisplayCliFailureKind.Cancelled),
            _ => PowerDisplayCliResult<T>.Failure(
                PowerDisplayCliFailureKind.ProcessFailure,
                errorMessage: processResult.ErrorMessage),
        };
    }

    private static PowerDisplayCliResult<T> ParseCommandFailure<T>(
        PowerDisplayProcessResult processResult,
        string expectedCommand)
        where T : class
    {
        if (!string.IsNullOrWhiteSpace(processResult.StandardOutput))
        {
            return InvalidResponse<T>(processResult);
        }

        try
        {
            var json = processResult.StandardError.Trim();
            if (!HasExpectedEnvelopeShape(json, true, "error"))
            {
                return InvalidResponse<T>(processResult);
            }

            var error = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliErrorResult);
            if (error is null || !error.IsError ||
                !HasCompatibleSchemaVersion(error.Version) ||
                error.Error is null ||
                string.IsNullOrWhiteSpace(error.Error.Code) ||
                !HasExpectedErrorCommand(error.Command, expectedCommand, error.Error.Code) ||
                error.Error.ExitCode != processResult.ExitCode)
            {
                return InvalidResponse<T>(processResult);
            }

            return PowerDisplayCliResult<T>.Failure(
                MapExitCode(processResult.ExitCode!.Value),
                processResult.ExitCode,
                error.Error.Message ?? string.Empty);
        }
        catch (JsonException ex)
        {
            return InvalidResponse<T>(processResult, ex.Message);
        }
    }

    private static PowerDisplayCliResult<T> InvalidResponse<T>(
        PowerDisplayProcessResult processResult,
        string errorMessage = "")
        where T : class
    {
        Logger.LogWarning(
            $"PowerDisplay CLI returned an invalid JSON response. ExitCode={processResult.ExitCode}; Detail={errorMessage}");
        return PowerDisplayCliResult<T>.Failure(
            PowerDisplayCliFailureKind.InvalidResponse,
            processResult.ExitCode,
            errorMessage);
    }

    private static PowerDisplayCliFailureKind MapExitCode(int exitCode) => exitCode switch
    {
        CliExitCodes.ArgumentError => PowerDisplayCliFailureKind.ArgumentError,
        CliExitCodes.Timeout => PowerDisplayCliFailureKind.Timeout,
        CliExitCodes.InternalError => PowerDisplayCliFailureKind.InternalError,
        CliExitCodes.ProviderUnavailable => PowerDisplayCliFailureKind.ProviderUnavailable,
        _ => PowerDisplayCliFailureKind.ProcessFailure,
    };

    private static bool HasCompatibleSchemaVersion(string? schemaVersion)
    {
        return System.Version.TryParse(CliSchema.Version, out var supportedVersion) &&
               System.Version.TryParse(schemaVersion, out var actualVersion) &&
               supportedVersion.Major == actualVersion.Major;
    }

    private static bool HasExpectedErrorCommand(string actualCommand, string expectedCommand, string errorCode)
    {
        // The app uses "unknown" for host failures caught outside its command dispatcher.
        // Accept only those generic failures; successful and command-specific responses still
        // have to identify the requested command.
        return string.Equals(actualCommand, expectedCommand, StringComparison.Ordinal) ||
               (string.Equals(actualCommand, "unknown", StringComparison.Ordinal) &&
                errorCode is CliErrorCodes.InternalError or CliErrorCodes.Timeout);
    }

    private static bool HasExpectedEnvelopeShape(
        string json,
        bool expectedIsError,
        string? payloadProperty = null)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("isError", out var isErrorProperty) ||
            isErrorProperty.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
            isErrorProperty.GetBoolean() != expectedIsError ||
            !root.TryGetProperty("version", out var versionProperty) ||
            versionProperty.ValueKind != JsonValueKind.String ||
            !HasCompatibleSchemaVersion(versionProperty.GetString()) ||
            !root.TryGetProperty("command", out var commandProperty) ||
            commandProperty.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        if (payloadProperty is null || !root.TryGetProperty(payloadProperty, out var payload))
        {
            return payloadProperty is null;
        }

        return expectedIsError
            ? payload.ValueKind == JsonValueKind.Object &&
              payload.TryGetProperty("code", out var code) &&
              code.ValueKind == JsonValueKind.String &&
              !string.IsNullOrWhiteSpace(code.GetString())
            : payload.ValueKind == JsonValueKind.Array;
    }
}
