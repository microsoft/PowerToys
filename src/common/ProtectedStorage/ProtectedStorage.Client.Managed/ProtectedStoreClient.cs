// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PowerToys.ProtectedStorage;

public sealed class ProtectedStoreClient : IProtectedStoreClient
{
    private readonly IProtectedStorageTransport transport;

    public ProtectedStoreClient()
        : this(new NamedPipeTransport())
    {
    }

    public ProtectedStoreClient(IProtectedStorageTransport transport)
    {
        this.transport = transport;
    }

    public static Version ReleaseVersion => typeof(ProtectedStoreClient).Assembly.GetName().Version ?? throw new ProtectedStorageException("IncompatibleVersion");

    public async Task<StorageCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        var response = await CallAsync(1, new JsonObject(), null, cancellationToken).ConfigureAwait(false);
        if (response.Bytes.Length != 0)
        {
            throw new ProtectedStorageException("InvalidPayload");
        }

        var metadata = response.Metadata;
        if (!Version.TryParse(metadata.GetProperty("release").GetString(), out var release) || release.Build < 0 || release.Revision < 0)
        {
            throw new ProtectedStorageException("InvalidPayload");
        }

        var features = metadata.GetProperty("features").EnumerateArray()
            .Select(value => value.GetString() ?? throw new ProtectedStorageException("InvalidPayload"))
            .ToHashSet(StringComparer.Ordinal);
        return new StorageCapabilities(
            metadata.GetProperty("protocolMajor").GetUInt32(),
            metadata.GetProperty("protocolMinor").GetUInt32(),
            metadata.GetProperty("maxBlobBytes").GetInt32(),
            metadata.GetProperty("maxMetadataBytes").GetInt32(),
            release,
            metadata.GetProperty("maintenance").GetBoolean(),
            metadata.GetProperty("recoveryRequired").GetBoolean(),
            features);
    }

    public async Task<TargetInfo> GetStateAsync(string target, CancellationToken cancellationToken = default)
    {
        var response = await CallAsync(2, new JsonObject { ["target"] = target }, null, cancellationToken).ConfigureAwait(false);
        var metadata = response.Metadata;
        string state = metadata.GetProperty("state").GetString()!;
        bool suppressionKnown = metadata.TryGetProperty("autoImportSuppressed", out var suppression);
        if (!suppressionKnown)
        {
            throw new ProtectedStorageException("IncompatibleVersion");
        }

        return new TargetInfo(
            state,
            metadata.TryGetProperty("revision", out var revision) ? ParseRevision(revision) : null,
            metadata.TryGetProperty("migrationSource", out var source) ? source.Clone() : null,
            metadata.TryGetProperty("cleanupAcknowledged", out var cleanup) && cleanup.GetBoolean(),
            metadata.TryGetProperty("initializationOperationId", out var operation) ? operation.GetGuid() : null,
            suppressionKnown && suppression.GetBoolean());
    }

    public async Task<BlobValue> GetBlobAsync(string target, CancellationToken cancellationToken = default)
    {
        return ParseBlob(await CallAsync(3, new JsonObject { ["target"] = target }, null, cancellationToken).ConfigureAwait(false));
    }

    public async Task<WriteResult> PutBlobAsync(WriteRequest request, CancellationToken cancellationToken = default)
    {
        var condition = new JsonObject { ["kind"] = request.Expected == null ? "IfUninitialized" : "IfRevision" };
        if (request.Expected != null)
        {
            condition["expected"] = new JsonObject
            {
                ["epoch"] = request.Expected.Epoch.ToString("D"),
                ["sequence"] = request.Expected.Sequence.ToString(CultureInfo.InvariantCulture),
            };
        }

        var metadata = new JsonObject
        {
            ["target"] = request.Target,
            ["operationId"] = request.OperationId.ToString("D"),
            ["condition"] = condition,
            ["contentSchema"] = request.ContentSchema,
        };
        if (request.MigrationSource is JsonElement source)
        {
            metadata["migrationSource"] = JsonNode.Parse(source.GetRawText());
        }

        return ParseWrite(await CallAsync(4, metadata, request.Bytes, cancellationToken).ConfigureAwait(false), request.OperationId);
    }

    public async Task<WriteResult> QueryWriteAsync(string target, Guid operationId, CancellationToken cancellationToken = default)
    {
        return ParseWrite(await CallAsync(5, new JsonObject { ["target"] = target, ["operationId"] = operationId.ToString("D") }, null, cancellationToken).ConfigureAwait(false), operationId);
    }

    public async Task AcknowledgeSourceCleanupAsync(string target, Guid operationId, CancellationToken cancellationToken = default)
    {
        await CallAsync(6, new JsonObject { ["target"] = target, ["operationId"] = operationId.ToString("D") }, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Guid> CreateTransientAsync(string target, Guid operationId, byte[] bytes, string contentSchema, CancellationToken cancellationToken = default)
    {
        var response = await CallAsync(7, new JsonObject { ["target"] = target, ["operationId"] = operationId.ToString("D"), ["contentSchema"] = contentSchema }, bytes, cancellationToken).ConfigureAwait(false);
        var id = response.Metadata.GetProperty("id").GetGuid();
        if (id == Guid.Empty)
        {
            throw new ProtectedStorageException("InvalidPayload");
        }

        return id;
    }

    public async Task<BlobValue> GetTransientAsync(string target, Guid id, CancellationToken cancellationToken = default)
    {
        return ParseBlob(await CallAsync(8, new JsonObject { ["target"] = target, ["id"] = id.ToString("D") }, null, cancellationToken).ConfigureAwait(false));
    }

    public async Task DeleteTransientAsync(string target, Guid id, CancellationToken cancellationToken = default)
    {
        await CallAsync(9, new JsonObject { ["target"] = target, ["id"] = id.ToString("D") }, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<WriteResult> UpdateTransientAsync(string target, Guid id, Guid operationId, Revision expected, byte[] bytes, string contentSchema, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (id == Guid.Empty || operationId == Guid.Empty || expected.Epoch != id || expected.Sequence == 0)
        {
            throw new ArgumentException("A transient update requires its object ID and previously read revision.", nameof(expected));
        }

        var metadata = new JsonObject
        {
            ["target"] = target,
            ["id"] = id.ToString("D"),
            ["operationId"] = operationId.ToString("D"),
            ["contentSchema"] = contentSchema,
            ["condition"] = new JsonObject
            {
                ["kind"] = "IfRevision",
                ["expected"] = new JsonObject
                {
                    ["epoch"] = expected.Epoch.ToString("D"),
                    ["sequence"] = expected.Sequence.ToString(CultureInfo.InvariantCulture),
                },
            },
        };
        var response = await CallAsync(10, metadata, bytes, cancellationToken).ConfigureAwait(false);
        if (response.Metadata.GetProperty("id").GetGuid() != id)
        {
            throw new ProtectedStorageException("InvalidPayload", operationId);
        }

        var result = ParseWrite(response, operationId);
        if (result.Revision is not null && result.Revision.Epoch != id)
        {
            throw new ProtectedStorageException("InvalidPayload", operationId);
        }

        return result;
    }

    private async Task<StorageFrame> CallAsync(uint command, JsonObject metadata, byte[]? bytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var document = JsonDocument.Parse(metadata.ToJsonString());
        var request = new StorageFrame(command, Guid.NewGuid(), document.RootElement.Clone(), bytes ?? []);
        var response = await transport.ExchangeAsync(request, cancellationToken).ConfigureAwait(false);
        Guid operationId = request.Metadata.TryGetProperty("operationId", out var operation) && Guid.TryParse(operation.GetString(), out var parsed) ? parsed : Guid.Empty;
        ProtectedStorageException? serverError = null;
        try
        {
            if (response.Command != request.Command || response.RequestId != request.RequestId)
            {
                throw new InvalidDataException("Mismatched protected storage response.");
            }

            string code = response.Metadata.GetProperty("errorCode").GetString() ?? throw new InvalidDataException("Missing error code.");
            if (code != "None")
            {
                if (response.Bytes.Length != 0 || code is not ("Unauthorized" or "TargetDenied" or "NotProvisioned" or "AuthorizationRequired" or
                    "AlreadyInitialized" or "RevisionConflict" or "BusyMaintenance" or "InvalidPayload" or "Timeout" or "OutcomeUnknown" or
                    "RecoveryRequired" or "IncompatibleVersion" or "QuotaExceeded" or "NotFound" or "OperationConflict" or "CleanupPending" or "InternalError" or "OwnerContextRequired"))
                {
                    throw new InvalidDataException("Invalid server rejection.");
                }

                string retry = response.Metadata.GetProperty("retryClass").GetString()!;
                string rejectedOperation = response.Metadata.GetProperty("operationId").GetString()!;
                if (retry is not ("None" or "Retry" or "QueryOutcome") ||
                    string.IsNullOrEmpty(response.Metadata.GetProperty("messageKey").GetString()) ||
                    (rejectedOperation.Length != 0 && (!Guid.TryParseExact(rejectedOperation, "D", out var rejectedId) || rejectedId != operationId)))
                {
                    throw new InvalidDataException("Invalid server rejection metadata.");
                }

                string resultCode = retry == "QueryOutcome" && command is 4 or 6 or 7 or 9 or 10 ? "OutcomeUnknown" : code;
                serverError = new ProtectedStorageException(resultCode, operationId, unchecked((int)response.Metadata.GetProperty("nativeCode").GetUInt32()));
            }
            else
            {
                ValidateMutationResponse(response, request, operationId);
            }
        }
        catch (Exception exception)
        {
            throw new ProtectedStorageException(command is 4 or 6 or 7 or 9 or 10 ? "OutcomeUnknown" : "InvalidPayload", operationId, innerException: exception);
        }

        if (serverError != null)
        {
            throw serverError;
        }

        return response;
    }

    private static void ValidateMutationResponse(StorageFrame response, StorageFrame request, Guid operationId)
    {
        uint command = request.Command;
        if (command is not (4 or 6 or 7 or 9 or 10))
        {
            return;
        }

        if (response.Bytes.Length != 0)
        {
            throw new InvalidDataException("A mutation response cannot contain a blob.");
        }

        if (command is 4 or 10)
        {
            var result = ParseWrite(response, operationId);
            if (result.Outcome != "Committed" || result.Revision == null ||
                (command == 10 && result.Revision.Epoch != request.Metadata.GetProperty("id").GetGuid()))
            {
                throw new InvalidDataException("Invalid committed revision.");
            }
        }

        if (command == 6 && (response.Metadata.GetProperty("operationId").GetGuid() != operationId || !response.Metadata.GetProperty("cleanupAcknowledged").GetBoolean()))
        {
            throw new InvalidDataException("Invalid cleanup acknowledgment.");
        }

        if (command == 7 && (response.Metadata.GetProperty("operationId").GetGuid() != operationId || response.Metadata.GetProperty("id").GetGuid() == Guid.Empty))
        {
            throw new InvalidDataException("Invalid created preview.");
        }

        if (command is 9 or 10 && response.Metadata.GetProperty("id").GetGuid() != request.Metadata.GetProperty("id").GetGuid())
        {
            throw new InvalidDataException("Mismatched preview identity.");
        }
    }

    private static Revision ParseRevision(JsonElement element)
    {
        var epoch = element.GetProperty("epoch").GetGuid();
        string? text = element.GetProperty("sequence").GetString();
        if (epoch == Guid.Empty || !ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence) || sequence == 0 || sequence.ToString(CultureInfo.InvariantCulture) != text)
        {
            throw new ProtectedStorageException("InvalidPayload");
        }

        return new Revision(epoch, sequence);
    }

    private static BlobValue ParseBlob(StorageFrame response)
    {
        return new BlobValue(ParseRevision(response.Metadata.GetProperty("revision")), response.Metadata.GetProperty("contentSchema").GetString()!, response.Bytes);
    }

    private static WriteResult ParseWrite(StorageFrame response, Guid expected)
    {
        if (response.Metadata.GetProperty("operationId").GetGuid() != expected)
        {
            throw new ProtectedStorageException("InvalidPayload");
        }

        return new WriteResult(expected, response.Metadata.TryGetProperty("revision", out var revision) ? ParseRevision(revision) : null, response.Metadata.GetProperty("outcome").GetString()!);
    }
}
