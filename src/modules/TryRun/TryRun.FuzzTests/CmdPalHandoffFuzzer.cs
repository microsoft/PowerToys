// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Text.Json;
using PowerToys.TryRun.Launching;

namespace PowerToys.TryRun.FuzzTests;

public static class CmdPalHandoffFuzzer
{
    public static void FuzzTarget(ReadOnlySpan<byte> input)
    {
        if (input.Length > CmdPalHandoff.MaximumCharacters * 4)
        {
            return;
        }

        try
        {
            var decoded = CmdPalHandoff.Decode(Encoding.UTF8.GetString(input));
            var copy = CmdPalHandoff.Decode(CmdPalHandoff.Encode(decoded));
            if (decoded.Path != copy.Path || !decoded.Arguments.SequenceEqual(copy.Arguments))
            {
                throw new InvalidOperationException("Command Palette selection changed during handoff.");
            }
        }
        catch (ArgumentException)
        {
            // Invalid selections are rejected without reading or running the target.
        }
        catch (JsonException)
        {
            // Malformed JSON is rejected at the UI boundary.
        }
    }
}
