// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Microsoft.MouseWithoutBorders.UITests;

internal sealed class WinAppSandboxPrerequisiteReport
{
    public const string FileName = "winapp-prerequisites.json";
    public const string CollectorFileName = "prerequisite-user.json";
    private static readonly Regex VersionLine = new(
        @"^[ \t]*(?:(?:Windows Sandbox(?: CLI)?|wsb(?:\.exe)?)[ \t]+)?(?:version[ \t]*:?[ \t]*)?v?([0-9]{1,5}(?:\.[0-9]{1,5}){2,3})[ \t]*\r?$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private readonly JsonObject stages = new()
    {
        ["OperatingSystem"] = new JsonObject { ["Status"] = "NotChecked", ["Version"] = null },
        [nameof(WinAppSandboxPrerequisite.UserPackageRegistration)] = new JsonObject
        {
            ["Status"] = "NotChecked",
            ["QueryStatus"] = "NotChecked",
            ["Present"] = null,
            ["Count"] = null,
            ["Packages"] = null,
        },
        [nameof(WinAppSandboxPrerequisite.UserExecutionAlias)] = new JsonObject
        {
            ["Status"] = "NotChecked",
            ["Exists"] = null,
            ["IsReparsePoint"] = null,
        },
        [nameof(WinAppSandboxPrerequisite.PackageExecutable)] = new JsonObject
        {
            ["Status"] = "NotChecked",
            ["InstalledLocation"] = null,
            ["Exists"] = null,
            ["Machine"] = null,
        },
        ["PrivateToolAndState"] = new JsonObject { ["Status"] = "NotChecked" },
        ["CliSchema"] = new JsonObject { ["Status"] = "NotChecked" },
        [nameof(WinAppSandboxPrerequisite.ProviderVersion)] = new JsonObject
        {
            ["Status"] = "NotChecked",
            ["VersionLine"] = null,
            ["ExitCode"] = null,
            ["TimedOut"] = null,
            ["TimeoutSeconds"] = null,
        },
    };

    private readonly JsonObject report;
    private string? persistentRoot;
    private string? collectorPath;

    private WinAppSandboxPrerequisiteReport(string? runId, string? invocationId = null)
    {
        foreach (var stage in stages)
        {
            stage.Value!["FailureCode"] = null;
            stage.Value["QueryErrorHResult"] = null;
        }

        using var identity = WindowsIdentity.GetCurrent();
        using var process = Process.GetCurrentProcess();
        report = new JsonObject
        {
            ["SchemaVersion"] = 1,
            ["RunId"] = runId,
            ["InvocationId"] = invocationId,
            ["Scope"] = "CurrentInteractiveUser",
            ["UserAccount"] = identity.Name,
            ["UserSid"] = identity.User?.Value,
            ["SessionId"] = process.SessionId,
            ["ProcessId"] = process.Id,
            ["IsElevated"] = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator),
            ["IsSystem"] = identity.IsSystem,
            ["ProcessArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
            ["OsArchitecture"] = RuntimeInformation.OSArchitecture.ToString(),
            ["Stages"] = stages,
        };
    }

    public static void Capture(string runRoot, string runId, Action<WinAppSandboxPrerequisiteReport> checks)
    {
        var evidence = new WinAppSandboxPrerequisiteReport(runId);
        Capture(runRoot, evidence, checks);
    }

    public static void CapturePersistent(string? testRunDirectory, Action<string> attach, Action<WinAppSandboxPrerequisiteReport> checks, string? provisionedRunRoot = null)
    {
        var invocationId = Guid.NewGuid().ToString("N");
        var root = Path.Combine(RunFiles.PersistentResultsRoot(testRunDirectory), "mwb-preflight-" + invocationId);
        Directory.CreateDirectory(root);
        var evidence = new WinAppSandboxPrerequisiteReport(null, invocationId);
        evidence.persistentRoot = root;
        Capture(
            root,
            evidence,
            report =>
            {
                report.Persist();
                if (!string.IsNullOrWhiteSpace(provisionedRunRoot))
                {
                    var parent = Path.GetDirectoryName(Path.GetFullPath(provisionedRunRoot))
                        ?? throw new WinAppSandboxException("prerequisite_report_parent_missing");
                    report.collectorPath = Path.Combine(parent, CollectorFileName);
                    report.Persist();
                }

                checks(report);
            },
            attach);
    }

    public void SetRunId(string runId)
    {
        var id = Guid.Parse(runId);
        if (id == Guid.Empty)
        {
            throw new WinAppSandboxException("run_identity_invalid");
        }

        report["RunId"] = id.ToString("D");
        Persist();
    }

    public void CheckProviderVersion(TimeSpan timeout, Func<Action<WinAppSandboxCommand.Result>, string> execute)
    {
        Check(nameof(WinAppSandboxPrerequisite.ProviderVersion), stage =>
        {
            stage["TimeoutSeconds"] = timeout.TotalSeconds;
            stage["TimedOut"] = false;
            try
            {
                var version = execute(result =>
                {
                    stage["ExitCode"] = result.ExitCode;
                    stage["VersionLine"] = SanitizeVersionLine(result.StandardOutput);
                });
                if (string.IsNullOrWhiteSpace(version))
                {
                    throw new WinAppSandboxException(WinAppSandboxPrerequisite.ProviderVersion);
                }
            }
            catch (Exception error)
            {
                throw new AggregateException(new WinAppSandboxException(WinAppSandboxPrerequisite.ProviderVersion), error);
            }
        });
    }

    public static string? SanitizeVersionLine(string output)
    {
        var match = VersionLine.Match(output);
        return match.Success ? "wsb " + match.Groups[1].Value : null;
    }

    public T Check<T>(string name, Func<JsonObject, T> check)
    {
        var stage = stages[name]!.AsObject();
        stage["Status"] = "Failed";
        try
        {
            var result = check(stage);
            stage["Status"] = "Passed";
            Persist();
            return result;
        }
        catch (Exception error)
        {
            var errors = error is AggregateException aggregate ? aggregate.Flatten().InnerExceptions.ToArray() : [error];
            var codes = errors.Select(SafeErrorCode).Distinct().ToArray();
            stage["FailureCode"] = codes.FirstOrDefault() ?? "prerequisite_check_failed";
            stage["ErrorCodes"] = JsonSerializer.SerializeToNode(codes);
            stage["ErrorHResults"] = JsonSerializer.SerializeToNode(errors.Select(item => $"0x{item.HResult:X8}").Distinct().ToArray());
            var queryError = errors.FirstOrDefault(item => item is COMException or Win32Exception or IOException or UnauthorizedAccessException);
            stage["QueryErrorHResult"] = queryError is null ? null : $"0x{queryError.HResult:X8}";
            var win32 = errors.OfType<Win32Exception>().FirstOrDefault();
            if (win32 is not null)
            {
                stage["Win32ErrorCode"] = win32.NativeErrorCode;
            }

            if (name == nameof(WinAppSandboxPrerequisite.ProviderVersion))
            {
                stage["TimedOut"] = codes.Contains("command_timeout", StringComparer.Ordinal);
            }

            Persist(error);
            throw;
        }
    }

    public void Check(string name, Action<JsonObject> check) => Check(name, stage =>
    {
        check(stage);
        return true;
    });

    private static void Capture(string runRoot, WinAppSandboxPrerequisiteReport evidence, Action<WinAppSandboxPrerequisiteReport> checks, Action<string>? attach = null)
    {
        try
        {
            checks(evidence);
        }
        catch (Exception failure)
        {
            try
            {
                evidence.Write(runRoot, attach);
            }
            catch (WinAppSandboxException writeFailure)
            {
                throw new AggregateException("Modern Sandbox prerequisites failed and their evidence could not be written.", failure, writeFailure);
            }

            throw;
        }

        evidence.Write(runRoot, attach);
    }

    private void Persist(Exception? failure = null)
    {
        if (persistentRoot is null)
        {
            return;
        }

        try
        {
            Write(persistentRoot, null);
        }
        catch (WinAppSandboxException error) when (failure is not null)
        {
            throw new AggregateException("Modern Sandbox prerequisites failed and their evidence could not be written.", failure, error);
        }
    }

    private void Write(string runRoot, Action<string>? attach)
    {
        try
        {
            var path = Path.Combine(runRoot, FileName);
            RunFiles.Write(path, report);
            attach?.Invoke(path);
            if (collectorPath is not null)
            {
                RunFiles.Write(collectorPath, report);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new WinAppSandboxException("prerequisite_report_write_failed");
        }
    }

    private static string SafeErrorCode(Exception error) => error switch
    {
        WinAppSandboxException sandbox when sandbox.Code is
            "trusted_provider_unavailable" or "sandbox_unsupported" or
            "command_failed" or "command_timeout" or "command_stream_incomplete" or "command_start_failed" or
            "private_path_reparse_point" or "target_state_must_remain_private" or "target_state_preexisting" or
            "preview_binary_changed" or "preview_capabilities_missing" => sandbox.Code,
        COMException => "com_query_failed",
        Win32Exception => "win32_query_failed",
        UnauthorizedAccessException => "access_denied",
        IOException => "io_failed",
        _ => "prerequisite_check_failed",
    };
}
