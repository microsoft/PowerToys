// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.Core;

public sealed record PolicyField(string Key, string Title, string Group, string Default, string Help, string Kind = "text", string[]? Choices = null, string Backend = "Both", string? LinuxDefault = null)
{
    public string DefaultFor(bool linux) => linux ? LinuxDefault ?? Default : Default;

    public bool AppliesTo(bool linux) => Backend == "Both" || Backend == (linux ? "Linux" : "Windows");
}
