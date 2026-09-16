// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.FuzzTests;

public static class CmdPalQueryFuzzer
{
    public static void FuzzTarget(ReadOnlySpan<byte> input)
    {
        if (input.Length > 16384)
        {
            return;
        }

        try
        {
            var paths = CmdPalLaunch.ParseQuery(Encoding.UTF8.GetString(input));
            if (paths.Length > 1 || (paths.Length == 1 && !paths.SequenceEqual(SelectionPayload.Decode(SelectionPayload.Encode(paths)))))
            {
                throw new InvalidOperationException("A CmdPal query changed while crossing the selection protocol.");
            }
        }
        catch (ArgumentException)
        {
            // Invalid queries produce no command and never touch the filesystem.
        }
    }
}
