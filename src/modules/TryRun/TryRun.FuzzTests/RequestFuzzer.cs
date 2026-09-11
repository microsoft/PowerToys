// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Text.Json;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.FuzzTests;

public static class RequestFuzzer
{
    public static void FuzzTarget(ReadOnlySpan<byte> data)
    {
        if (data.Length > RequestCodec.MaximumMessageLength)
        {
            return;
        }

        try
        {
            var text = Encoding.UTF8.GetString(data);
            PowerShellErrorFormatter.Format(text);
            RequestCodec.Parse(text);
        }
        catch (JsonException)
        {
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidDataException)
        {
        }
    }
}
