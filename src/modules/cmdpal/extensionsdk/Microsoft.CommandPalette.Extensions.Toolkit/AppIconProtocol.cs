// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Creates and parses Command Palette app-icon protocol strings for use in
/// <see cref="IconData.Icon"/> and <see cref="IconInfo"/>.
/// </summary>
/// <remarks>
/// Candidates are tried in order by a supporting Command Palette host. The
/// versioned, length-prefixed payload permits any UTF-16 file path, indexed icon
/// reference, or URI without reserving a separator character inside candidates.
/// </remarks>
public static class AppIconProtocol
{
    private const string AppIconPrefix = "|AppIcon|";
    private const string JumboAppIconPrefix = "|JumboAppIcon|";
    private const string WireVersion = "v1;";
    private const int MaximumCandidateCount = 8;

    private static readonly string[] ProtocolPrefixValues = [AppIconPrefix, JumboAppIconPrefix];

    /// <summary>
    /// Gets the protocol prefixes claimed by app-icon requests.
    /// </summary>
    public static ReadOnlySpan<string> ProtocolPrefixes => ProtocolPrefixValues;

    /// <summary>
    /// Creates a standard app-icon request with an optional fallback candidate.
    /// </summary>
    /// <param name="primary">The first icon candidate to try.</param>
    /// <param name="fallback">An optional candidate to try if the primary candidate cannot provide an icon.</param>
    /// <returns>A protocol string that can be passed to <see cref="IconData"/> or <see cref="IconInfo"/>.</returns>
    public static string Create(string primary, string? fallback = null) =>
        CreateCore(AppIconPrefix, primary, fallback, null);

    /// <summary>
    /// Creates a large app-icon request with up to two fallback candidates.
    /// </summary>
    /// <param name="primary">The first icon candidate to try.</param>
    /// <param name="fallback">An optional candidate to try if the primary candidate cannot provide an icon.</param>
    /// <param name="finalFallback">An optional final candidate.</param>
    /// <returns>A protocol string that can be passed to <see cref="IconData"/> or <see cref="IconInfo"/>.</returns>
    public static string CreateJumbo(
        string primary,
        string? fallback = null,
        string? finalFallback = null) =>
        CreateCore(JumboAppIconPrefix, primary, fallback, finalFallback);

    /// <summary>
    /// Determines whether a value is claimed by the app-icon protocol.
    /// </summary>
    /// <remarks>
    /// This identifies the protocol prefix even when the payload is malformed, so
    /// a supporting host will not reinterpret a malformed request as another icon kind.
    /// </remarks>
    public static bool IsProtocol(string? value) =>
        value?.StartsWith(AppIconPrefix, StringComparison.Ordinal) == true
        || value?.StartsWith(JumboAppIconPrefix, StringComparison.Ordinal) == true;

    /// <summary>
    /// Parses a versioned app-icon request.
    /// </summary>
    /// <param name="value">The protocol string to parse.</param>
    /// <param name="candidates">The unique icon candidates in resolution order.</param>
    /// <param name="jumbo"><see langword="true"/> when the request asks for a large icon.</param>
    /// <returns><see langword="true"/> when the request is well formed and uses a supported version.</returns>
    /// <remarks>Candidate lengths are encoded as UTF-16 code-unit counts.</remarks>
    public static bool TryParse(string? value, out string[] candidates, out bool jumbo)
    {
        candidates = [];
        jumbo = false;

        if (!TryGetPayload(value, out var payload, out var parsedJumbo)
            || !payload.StartsWith(WireVersion, StringComparison.Ordinal))
        {
            return false;
        }

        payload = payload[WireVersion.Length..];
        if (payload.IsEmpty)
        {
            return false;
        }

        var validationPayload = payload;
        var candidateCount = 0;
        while (!validationPayload.IsEmpty)
        {
            if (candidateCount == MaximumCandidateCount
                || !TryReadCandidate(ref validationPayload, out _))
            {
                return false;
            }

            candidateCount++;
        }

        var parsedCandidates = new string[candidateCount];
        for (var candidateIndex = 0; candidateIndex < parsedCandidates.Length; candidateIndex++)
        {
            if (!TryReadCandidate(ref payload, out var candidate))
            {
                return false;
            }

            for (var previousIndex = 0; previousIndex < candidateIndex; previousIndex++)
            {
                if (candidate.Equals(parsedCandidates[previousIndex].AsSpan(), StringComparison.Ordinal))
                {
                    return false;
                }
            }

            parsedCandidates[candidateIndex] = candidate.ToString();
        }

        candidates = parsedCandidates;
        jumbo = parsedJumbo;
        return true;
    }

    private static string CreateCore(
        string prefix,
        string primary,
        string? fallback,
        string? finalFallback)
    {
        ArgumentException.ThrowIfNullOrEmpty(primary);

        var builder = new StringBuilder(prefix.Length + WireVersion.Length + primary.Length + 12);
        builder.Append(prefix);
        builder.Append(WireVersion);
        AppendCandidate(builder, primary);

        if (!string.IsNullOrEmpty(fallback)
            && !string.Equals(fallback, primary, StringComparison.Ordinal))
        {
            AppendCandidate(builder, fallback);
        }

        if (!string.IsNullOrEmpty(finalFallback)
            && !string.Equals(finalFallback, primary, StringComparison.Ordinal)
            && !string.Equals(finalFallback, fallback, StringComparison.Ordinal))
        {
            AppendCandidate(builder, finalFallback);
        }

        return builder.ToString();
    }

    private static void AppendCandidate(StringBuilder builder, string candidate)
    {
        Span<char> lengthBuffer = stackalloc char[10];
        _ = candidate.Length.TryFormat(
            lengthBuffer,
            out var lengthWritten,
            provider: CultureInfo.InvariantCulture);
        builder.Append(lengthBuffer[..lengthWritten]);
        builder.Append(':');
        builder.Append(candidate);
    }

    private static bool TryReadCandidate(
        ref ReadOnlySpan<char> payload,
        out ReadOnlySpan<char> candidate)
    {
        var separatorIndex = payload.IndexOf(':');
        if (separatorIndex <= 0
            || !int.TryParse(
                payload[..separatorIndex],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var candidateLength)
            || candidateLength <= 0)
        {
            candidate = default;
            return false;
        }

        payload = payload[(separatorIndex + 1)..];
        if (candidateLength > payload.Length)
        {
            candidate = default;
            return false;
        }

        candidate = payload[..candidateLength];
        payload = payload[candidateLength..];
        return true;
    }

    private static bool TryGetPayload(
        string? value,
        out ReadOnlySpan<char> payload,
        out bool jumbo)
    {
        if (value?.StartsWith(AppIconPrefix, StringComparison.Ordinal) == true)
        {
            payload = value.AsSpan(AppIconPrefix.Length);
            jumbo = false;
            return true;
        }

        if (value?.StartsWith(JumboAppIconPrefix, StringComparison.Ordinal) == true)
        {
            payload = value.AsSpan(JumboAppIconPrefix.Length);
            jumbo = true;
            return true;
        }

        payload = default;
        jumbo = false;
        return false;
    }
}
