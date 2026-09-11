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

        var request = JsonSerializer.Deserialize<ExecutionRequest>(json) ?? throw new InvalidDataException("Missing execution request.");
        request.Validate();
        return request;
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
