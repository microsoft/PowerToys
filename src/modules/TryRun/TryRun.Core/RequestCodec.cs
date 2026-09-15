// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace PowerToys.TryRun.Core;

public static class RequestCodec
{
    public const int MaximumMessageLength = 100000;

    public static ExecutionRequest Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > MaximumMessageLength)
        {
            throw new InvalidDataException("The execution request is too large.");
        }

        var root = JsonSerializer.Deserialize<JsonElement>(json);
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Missing execution request object.");
        }

        var enveloped = root.TryGetProperty("PolicyProtocol", out var version);
        if (enveloped)
        {
            if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var protocol) || protocol != 1 || !root.TryGetProperty("Request", out var nested) || nested.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("Unsupported policy protocol. Rebuild the matching Try Run UI and worker.");
            }

            root = nested;
        }

        var request = root.Deserialize<ExecutionRequest>() ?? throw new InvalidDataException("Missing execution request.");
        if (request.Policy is not null && !enveloped)
        {
            throw new InvalidDataException("Configurable policies require the versioned policy protocol.");
        }

        request.Validate();
        return request;
    }

    public static string Serialize(ExecutionRequest request)
    {
        request.Validate();
        var json = request.Policy is null ? JsonSerializer.Serialize(request) : JsonSerializer.Serialize(new { PolicyProtocol = 1, Request = request });
        if (json.Length > MaximumMessageLength)
        {
            throw new InvalidDataException("The execution request is too large.");
        }

        return json;
    }

    public static async Task<ExecutionRequest> ReadAsync(TextReader reader, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var buffer = new char[MaximumMessageLength + 1];
        var length = 0;
        while (length < buffer.Length)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(length, 1), cancellationToken).ConfigureAwait(false);
            if (count == 0 || buffer[length] == '\n')
            {
                return Parse(new string(buffer, 0, length));
            }

            length += count;
        }

        throw new InvalidDataException("The execution request is too large.");
    }
}
