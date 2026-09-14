// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.Core;

public sealed record OriginalFilesCheck(int Unchanged, int Changed, int Unavailable)
{
    public override string ToString() => $"Selected original files (host hash check): {Unchanged} unchanged, {Changed} changed, {Unavailable} unavailable. This checks selected file contents, not the entire host.";
}
