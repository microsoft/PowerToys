// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Creates and parses semantic Shell-item icon requests for use in
/// <see cref="IconData.Icon"/> and <see cref="IconInfo"/>.
/// </summary>
/// <remarks>
/// The host resolves the item to the icon identity supplied by the Windows Shell.
/// This lets different paths that share one Shell icon reuse the same materialized icon.
/// The payload is length-prefixed so any UTF-16 path or Shell parsing name is valid.
/// </remarks>
public static class ShellItemIconProtocol
{
    private const string ShellItemIconPrefix = "|ShellItemIcon|";
    private const string JumboShellItemIconPrefix = "|JumboShellItemIcon|";
    private const string WireVersion = "v1;";

    private static readonly string[] ProtocolPrefixValues = [ShellItemIconPrefix, JumboShellItemIconPrefix];

    /// <summary>
    /// Gets the protocol prefixes claimed by Shell-item icon requests.
    /// </summary>
    public static ReadOnlySpan<string> ProtocolPrefixes => ProtocolPrefixValues;

    /// <summary>
    /// Creates a standard Shell-item icon request.
    /// </summary>
    /// <param name="itemPath">The filesystem path or Shell parsing name whose icon should be resolved.</param>
    /// <returns>A protocol string that can be passed to <see cref="IconData"/> or <see cref="IconInfo"/>.</returns>
    public static string Create(string itemPath) => CreateCore(ShellItemIconPrefix, itemPath);

    /// <summary>
    /// Creates a large Shell-item icon request.
    /// </summary>
    /// <param name="itemPath">The filesystem path or Shell parsing name whose icon should be resolved.</param>
    /// <returns>A protocol string that can be passed to <see cref="IconData"/> or <see cref="IconInfo"/>.</returns>
    public static string CreateJumbo(string itemPath) => CreateCore(JumboShellItemIconPrefix, itemPath);

    /// <summary>
    /// Determines whether a value is claimed by the Shell-item icon protocol.
    /// </summary>
    /// <remarks>
    /// This identifies the protocol prefix even when the payload is malformed, so
    /// a supporting host will not reinterpret a malformed request as another icon kind.
    /// </remarks>
    public static bool IsProtocol(string? value) =>
        value?.StartsWith(ShellItemIconPrefix, StringComparison.Ordinal) == true
        || value?.StartsWith(JumboShellItemIconPrefix, StringComparison.Ordinal) == true;

    /// <summary>
    /// Parses a versioned Shell-item icon request.
    /// </summary>
    /// <param name="value">The protocol string to parse.</param>
    /// <param name="itemPath">The filesystem path or Shell parsing name carried by the request.</param>
    /// <param name="jumbo"><see langword="true"/> when the request asks for a large icon.</param>
    /// <returns><see langword="true"/> when the request is well formed and uses a supported version.</returns>
    /// <remarks>Path lengths are encoded as UTF-16 code-unit counts.</remarks>
    public static bool TryParse(string? value, out string itemPath, out bool jumbo)
    {
        itemPath = string.Empty;
        jumbo = false;

        if (!TryGetPayload(value, out var payload, out var parsedJumbo)
            || !payload.StartsWith(WireVersion, StringComparison.Ordinal))
        {
            return false;
        }

        payload = payload[WireVersion.Length..];
        var separatorIndex = payload.IndexOf(':');
        if (separatorIndex <= 0
            || !int.TryParse(
                payload[..separatorIndex],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var itemPathLength)
            || itemPathLength <= 0)
        {
            return false;
        }

        payload = payload[(separatorIndex + 1)..];
        if (payload.Length != itemPathLength)
        {
            return false;
        }

        itemPath = payload.ToString();
        jumbo = parsedJumbo;
        return true;
    }

    private static string CreateCore(string prefix, string itemPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(itemPath);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{prefix}{WireVersion}{itemPath.Length}:{itemPath}");
    }

    private static bool TryGetPayload(
        string? value,
        out ReadOnlySpan<char> payload,
        out bool jumbo)
    {
        if (value?.StartsWith(ShellItemIconPrefix, StringComparison.Ordinal) == true)
        {
            payload = value.AsSpan(ShellItemIconPrefix.Length);
            jumbo = false;
            return true;
        }

        if (value?.StartsWith(JumboShellItemIconPrefix, StringComparison.Ordinal) == true)
        {
            payload = value.AsSpan(JumboShellItemIconPrefix.Length);
            jumbo = true;
            return true;
        }

        payload = default;
        jumbo = false;
        return false;
    }
}
