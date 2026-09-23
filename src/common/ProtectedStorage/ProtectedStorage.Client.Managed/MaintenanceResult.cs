// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace PowerToys.ProtectedStorage;

/// <summary>A maintenance observation for presentation, never storage authority or a command description.</summary>
public sealed record MaintenanceResult(Guid OperationId, string State, uint NativeCode, string RetryKind, bool CleanupPending, bool DataRetained, string ProductCode = "", string InstalledVersion = "")
{
    public bool Succeeded => NativeCode is 0 or 3010 or 1641;

    public bool RequiresRestart => NativeCode is 3010 or 1641;

    public void ThrowIfFailed()
    {
        if (!Succeeded)
        {
            string code = NativeCode switch
            {
                1618 or 170 => "SetupBusy",
                5 or 1223 or 1602 or 740 => "SetupAuthorizationRequired",
                1638 => "IncompatibleVersion",
                _ => RetryKind == "InspectUnknownOutcome" ? "SetupOutcomeUnknown" : "SetupFailed",
            };
            throw new ProtectedStorageException(code, OperationId, unchecked((int)NativeCode));
        }
    }

    public void ThrowIfRestartRequired()
    {
        if (RequiresRestart)
        {
            throw new ProtectedStorageException("SetupRestartRequired", OperationId, unchecked((int)NativeCode));
        }
    }

    public static MaintenanceResult Parse(int exitCode, string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (output.Length > 65536)
        {
            throw new ProtectedStorageException("InvalidPayload");
        }

        var line = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim();
        if (line == null || line.Length > 65536)
        {
            throw new ProtectedStorageException("InvalidPayload");
        }

        using var document = JsonDocument.Parse(line, new JsonDocumentOptions { MaxDepth = 8 });
        var value = document.RootElement;
        bool validId = Guid.TryParseExact(value.GetProperty("operationId").GetString(), "N", out var id);
        uint code = value.GetProperty("nativeCode").GetUInt32();
        if (!validId || id == Guid.Empty || code != unchecked((uint)exitCode))
        {
            throw new ProtectedStorageException("InvalidPayload");
        }

        return new MaintenanceResult(
            id,
            value.GetProperty("state").GetString() ?? throw new ProtectedStorageException("InvalidPayload"),
            code,
            value.GetProperty("retryKind").GetString() ?? throw new ProtectedStorageException("InvalidPayload"),
            value.GetProperty("cleanupPending").GetBoolean(),
            value.GetProperty("dataRetained").GetBoolean(),
            value.TryGetProperty("productCode", out var product) ? product.GetString() ?? string.Empty : string.Empty,
            value.TryGetProperty("installedVersion", out var version) ? version.GetString() ?? string.Empty : string.Empty);
    }
}
