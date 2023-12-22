// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace Microsoft.MouseJumpUI.UnitTests.TestUtils;

internal static class SerializationUtils
{
    public static string SerializeAnonymousType<T>(T value)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        return JsonSerializer.Serialize(value, options);
    }
}
