// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace Microsoft.CmdPal.Ext.WindowsPalettes;

internal enum WupCommandKind
{
    Invoke,
    Children,
}

internal readonly record struct WupCommandIdentity(
    string ExtensionId,
    string ItemId,
    WupCommandKind Kind);

internal static class WupCommandId
{
    private const string Prefix = "wup-item:v1:";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static string Create(string extensionId, string itemId, WupCommandKind kind)
    {
        ArgumentException.ThrowIfNullOrEmpty(extensionId);
        ArgumentException.ThrowIfNullOrEmpty(itemId);

        var kindToken = kind == WupCommandKind.Children ? "c" : "i";
        return $"{Prefix}{kindToken}:{Encode(extensionId)}:{Encode(itemId)}";
    }

    internal static bool TryParse(string id, out WupCommandIdentity identity)
    {
        identity = default;
        if (string.IsNullOrEmpty(id) || !id.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var payload = id.AsSpan(Prefix.Length);
        if (payload.Length < 5 || payload[1] != ':')
        {
            return false;
        }

        var kind = payload[0] switch
        {
            'i' => WupCommandKind.Invoke,
            'c' => WupCommandKind.Children,
            _ => (WupCommandKind?)null,
        };
        if (kind is null)
        {
            return false;
        }

        var separatorIndex = payload[2..].IndexOf(':');
        if (separatorIndex < 1)
        {
            return false;
        }

        separatorIndex += 2;
        if (!TryDecode(payload[2..separatorIndex], out var extensionId) ||
            !TryDecode(payload[(separatorIndex + 1)..], out var itemId) ||
            string.IsNullOrEmpty(extensionId) ||
            string.IsNullOrEmpty(itemId))
        {
            return false;
        }

        identity = new(extensionId, itemId, kind.Value);
        return true;
    }

    private static string Encode(string value) =>
        Convert.ToBase64String(StrictUtf8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static bool TryDecode(ReadOnlySpan<char> value, out string decoded)
    {
        decoded = string.Empty;
        if (value.IsEmpty || value.Length % 4 == 1)
        {
            return false;
        }

        var base64 = value.ToString().Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
        try
        {
            decoded = StrictUtf8.GetString(Convert.FromBase64String(base64));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}