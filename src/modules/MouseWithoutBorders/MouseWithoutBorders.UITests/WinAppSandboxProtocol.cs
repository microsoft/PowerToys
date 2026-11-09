// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.MouseWithoutBorders.UITests;

internal static class WinAppSandboxProtocol
{
    public const int NoSuchLogonSession = unchecked((int)0x80070520);

    public static void RequireGuestCommandSuccess(string json)
    {
        var root = ReadObject(json);
        if (!root.TryGetProperty("ExitCode", out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var code))
        {
            throw new WinAppSandboxException("guest_exit_code_missing");
        }

        if (code != 0)
        {
            throw new WinAppSandboxException("guest_readiness_probe_failed", code);
        }
    }

    public static Guid[] ReadInventory(string json)
    {
        var root = ReadObject(json);
        if (!root.TryGetProperty("WindowsSandboxEnvironments", out var environments) || environments.ValueKind != JsonValueKind.Array)
        {
            throw new WinAppSandboxException("provider_inventory_invalid");
        }

        var ids = environments.EnumerateArray().Select(item => ReadId(item, "Id")).ToArray();
        if (ids.Distinct().Count() != ids.Length)
        {
            throw new WinAppSandboxException("provider_inventory_ambiguous");
        }

        return ids;
    }

    public static void RequireEmptyInventory(string json)
    {
        if (ReadInventory(json).Length != 0)
        {
            throw new WinAppSandboxException("preexisting_sandbox");
        }
    }

    public static void RequireExclusiveInstance(string json, Guid instanceId)
    {
        var ids = ReadInventory(json);
        if (ids.Length != 1 || ids[0] != instanceId)
        {
            throw new WinAppSandboxException("sandbox_ownership_mismatch");
        }
    }

    public static void RequireExclusiveInstance(Func<string> queryInventory, Guid instanceId, Action ownershipUnconfirmed)
    {
        // A failed query still aborts the operation, but is not evidence that
        // another instance appeared. Cleanup must be able to query again.
        var json = queryInventory();
        try
        {
            RequireExclusiveInstance(json, instanceId);
        }
        catch (WinAppSandboxException)
        {
            ownershipUnconfirmed();
            throw;
        }
    }

    public static void RequireStartResult(string json, Guid instanceId)
    {
        var root = ReadObject(json);
        if (root.TryGetProperty("Id", out _))
        {
            if (ReadId(root, "Id") != instanceId)
            {
                throw new WinAppSandboxException("sandbox_start_identity_mismatch");
            }

            if (!root.TryGetProperty("WindowsSandboxEnvironments", out _))
            {
                return;
            }
        }

        RequireExclusiveInstance(json, instanceId);
    }

    public static void RequireCapabilities(string json)
    {
        var root = ReadObject(json);
        if (!TryObject(root, "subcommands", out var commands) ||
            !TryObject(commands, "target", out var target) ||
            !TryObject(target, "subcommands", out var targets))
        {
            throw new WinAppSandboxException("preview_capabilities_missing");
        }

        foreach (var name in new[] { "exec", "push", "pull", "snapshot", "screenshot", "record" })
        {
            if (!TryObject(targets, name, out var command) ||
                !TryObject(command, "arguments", out var arguments) ||
                !arguments.TryGetProperty("target", out _) ||
                !TryObject(command, "options", out var options) ||
                !options.TryGetProperty("--json", out _))
            {
                throw new WinAppSandboxException("preview_capabilities_missing");
            }

            string[] required = name switch
            {
                "record" => new[] { "--fps", "--max-edge", "--output", "--duration-sec" },
                "screenshot" => new[] { "--output" },
                _ => [],
            };
            if (required.Any(option => !options.TryGetProperty(option, out _)))
            {
                throw new WinAppSandboxException("preview_capabilities_missing");
            }
        }
    }

    public static string RequireAdoptedState(IReadOnlyList<string> states, Guid instanceId, string? expectedEpoch = null)
    {
        if (states.Count != 1)
        {
            throw new WinAppSandboxException("target_state_ambiguous");
        }

        var state = ReadObject(states[0]);
        if (!state.TryGetProperty("schemaVersion", out var schema) || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version) || version != 1 ||
            ReadString(state, "targetKind") != "sandbox" || ReadString(state, "targetId") != "default" ||
            ReadId(state, "instanceId") != instanceId || ReadString(state, "instanceOrigin") != "Adopted" ||
            (state.TryGetProperty("pendingInstanceId", out var pending) && pending.ValueKind != JsonValueKind.Null))
        {
            throw new WinAppSandboxException("target_state_ownership_mismatch");
        }

        // Epochs belong to winapp. In particular, executionTarget.id == "default"
        // and a GUID-looking prefix of an epoch are not provider ownership proofs.
        var epoch = ReadString(state, "bootstrappedEpoch");
        if (string.IsNullOrWhiteSpace(epoch) || (expectedEpoch is not null && epoch != expectedEpoch))
        {
            throw new WinAppSandboxException("target_epoch_mismatch");
        }

        return epoch;
    }

    public static JsonObject ReadSafeSnapshot(string json, string expectedEpoch)
    {
        var snapshot = ReadObject(json);
        if (!TryObject(snapshot, "executionTarget", out var target) ||
            ReadString(target, "kind") != "sandbox" || ReadString(target, "id") != "default" ||
            ReadString(target, "epoch") != expectedEpoch ||
            !ReadBoolean(snapshot, "running") || !ReadBoolean(snapshot, "attached"))
        {
            throw new WinAppSandboxException("target_snapshot_not_attached");
        }

        if (!TryObject(snapshot, "capabilities", out var capabilities) ||
            !ReadBoolean(capabilities, "supportsInteractiveDesktop") ||
            !ReadBoolean(capabilities, "supportsRealInput") ||
            !ReadBoolean(capabilities, "supportsScreenCapture"))
        {
            throw new WinAppSandboxException("target_capabilities_missing");
        }

        long handle = 0;
        var rendered = false;
        var captureReady = false;
        if (snapshot.TryGetProperty("desktop", out var desktop) && desktop.ValueKind == JsonValueKind.Object)
        {
            rendered = ReadBoolean(desktop, "rendered");
            captureReady = ReadBoolean(desktop, "effectiveCaptureReady");
            if (rendered && desktop.TryGetProperty("windowHandle", out var window) && window.ValueKind == JsonValueKind.Number && window.TryGetInt64(out var value) && value > 0)
            {
                handle = value;
            }
        }

        // Never export raw snapshots: guest window titles, deployment metadata, and
        // future provider fields may contain user input or authentication material.
        return new JsonObject
        {
            ["Backend"] = "WinApp",
            ["Running"] = true,
            ["Attached"] = true,
            ["SupportsInteractiveDesktop"] = true,
            ["SupportsRealInput"] = true,
            ["SupportsScreenCapture"] = true,
            ["Rendered"] = rendered,
            ["CaptureReady"] = captureReady,
            ["ViewerHwnd"] = handle,
        };
    }

    public static bool RecordingStarted(string output)
    {
        foreach (var line in output.Split('\n'))
        {
            if (!line.TrimStart().StartsWith('{'))
            {
                continue;
            }

            try
            {
                if (ReadString(ReadObject(line), "event") == "recording-started")
                {
                    return true;
                }
            }
            catch (WinAppSandboxException)
            {
                // A concurrent pipe read may end in the middle of the event.
            }
        }

        return false;
    }

    public static string SafeErrorCode(string json)
    {
        try
        {
            var root = ReadObject(json);
            var code = root.TryGetProperty("error", out var error) ? ReadString(error, "code") : null;
            return code switch
            {
                "sandbox_unsupported" or "sandbox_unmanaged_instance" or "sandbox_start_failed" or
                "sandbox_no_interactive_session" or "sandbox_input_not_ready" or "sandbox_terminated" or
                "sandbox_agent_incompatible" or "sandbox_agent_upgrade_failed" or "sandbox_agent_busy" or
                "sandbox_transport_failed" or "sandbox_transfer_interrupted" or "sandbox_runtime_provision_failed" or
                "sandbox_deployment_dirty" or "sandbox_package_conflict" or "sandbox_provisioned_package_conflict" or
                "sandbox_target_ambiguous" or "sandbox_target_stale" or "sandbox_stale_handle" or
                "sandbox_artifact_failed" or "sandbox_setup_required" or "sandbox_setup_requires_restart" or
                "sandbox_setup_incomplete" or "target_invalid" or "target_invalid_arguments" => code,
                _ => "command_failed",
            };
        }
        catch (WinAppSandboxException)
        {
            return "command_failed";
        }
    }

    private static Guid ReadId(JsonElement element, string name)
    {
        if (!Guid.TryParse(ReadString(element, name), out var id) || id == Guid.Empty)
        {
            throw new WinAppSandboxException("provider_identity_invalid");
        }

        return id;
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private static bool ReadBoolean(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static bool TryObject(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Object;
    }

    private static JsonElement ReadObject(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new WinAppSandboxException("protocol_invalid");
            }

            RequireUniqueProperties(document.RootElement);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new WinAppSandboxException("protocol_invalid");
        }
    }

    private static void RequireUniqueProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new WinAppSandboxException("protocol_duplicate_property");
                }

                RequireUniqueProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                RequireUniqueProperties(item);
            }
        }
    }
}
