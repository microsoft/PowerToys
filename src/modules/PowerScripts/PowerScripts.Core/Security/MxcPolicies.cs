// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using PowerScripts.Core.Storage;

namespace PowerScripts.Core.Security;

/// <summary>The stable PowerScripts policy names understood by the MXC integration.</summary>
public static class MxcPolicies
{
    public const string Filesystem = "filesystem";
    public const string Network = "network";
    public const string Ui = "ui";
    public const string LeastPrivilege = "leastPrivilege";

    public static IReadOnlyList<string> All { get; } =
        new[] { Filesystem, Network, Ui, LeastPrivilege };

    public static bool IsKnown(string policy) =>
        All.Contains(policy, StringComparer.OrdinalIgnoreCase);

    internal static List<string> Normalize(IEnumerable<string>? policies) =>
        (policies ?? Array.Empty<string>())
            .Where(IsKnown)
            .Select(p => All.First(known => known.Equals(p, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
