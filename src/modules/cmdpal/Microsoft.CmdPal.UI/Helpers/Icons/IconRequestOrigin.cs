// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Controls;

namespace Microsoft.CmdPal.UI.Helpers;

internal readonly record struct IconRequestOrigin
{
    private const int MaximumDiagnosticScopeLength = 64;

    public long IconBoxId { get; }

    public IconRequestSite RequestSite { get; }

    public string DiagnosticScope { get; }

    public IconRequestOrigin(long iconBoxId, IconRequestSite requestSite, string? diagnosticScope)
    {
        IconBoxId = Math.Max(0, iconBoxId);
        RequestSite = Enum.IsDefined(typeof(IconRequestSite), requestSite) ? requestSite : IconRequestSite.Unknown;
        DiagnosticScope = NormalizeDiagnosticScope(diagnosticScope);
    }

    public IconRequestOrigin Normalize() => new(IconBoxId, RequestSite, DiagnosticScope);

    private static string NormalizeDiagnosticScope(string? diagnosticScope)
    {
        if (string.IsNullOrWhiteSpace(diagnosticScope))
        {
            return string.Empty;
        }

        var scope = diagnosticScope.AsSpan().Trim();
        if (scope.Length > MaximumDiagnosticScopeLength)
        {
            return string.Empty;
        }

        foreach (var character in scope)
        {
            var isAsciiLetter = character is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
            var isAsciiDigit = character is >= '0' and <= '9';
            if (!isAsciiLetter && !isAsciiDigit && character is not '.' and not '-' and not '_')
            {
                return string.Empty;
            }
        }

        return scope.Length == diagnosticScope.Length ? diagnosticScope : scope.ToString();
    }
}
