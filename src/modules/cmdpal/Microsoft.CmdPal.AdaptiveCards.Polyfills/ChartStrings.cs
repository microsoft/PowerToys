// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.AdaptiveCards.Polyfills;

/// <summary>Gets the strings that charts show from the host, which owns the resources.</summary>
internal static class ChartStrings
{
    private static Func<string, string> _getString = static name => name;

    public static string Get(string name) => _getString(name);

    public static void SetLookup(Func<string, string> getString) => _getString = getString;
}
