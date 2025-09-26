// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Helpers.MarkdownImageProviders;

internal sealed class ImageHints
{
    public static ImageHints Empty { get; } = new();

    public double? DesiredPixelWidth { get; init; }

    public double? DesiredPixelHeight { get; init; }

    public double? MaxPixelWidth { get; init; }

    public double? MaxPixelHeight { get; init; }

    public bool? DownscaleOnly { get; init; }

    public string? FitMode { get; init; } // fit=fit

    public static ImageHints ParseHintsFromUri(Uri? uri)
    {
        if (uri is null || string.IsNullOrEmpty(uri.Query))
        {
            return Empty;
        }

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = p.Split('=', 2);
            var k = Uri.UnescapeDataString(kv[0]);
            var v = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
            dict[k] = v;
        }

        return new ImageHints
        {
            DesiredPixelWidth = GetInt("--x-cmdpal-width"),
            DesiredPixelHeight = GetInt("--x-cmdpal-height"),
            MaxPixelWidth = GetInt("--x-cmdpal-maxwidth"),
            MaxPixelHeight = GetInt("--x-cmdpal-maxheight"),
            DownscaleOnly = GetBool("--x-cmdpal-downscaleOnly") ?? (GetBool("--x-cmdpal-upscale") is bool u ? !u : (bool?)null),
            FitMode = dict.TryGetValue("--x-cmdpal-fit", out var f) ? f : null,
        };

        int? GetInt(params string[] keys)
        {
            foreach (var k in keys)
            {
                if (dict.TryGetValue(k, out var v) && int.TryParse(v, out var n))
                {
                    return n;
                }
            }

            return null;
        }

        bool? GetBool(params string[] keys)
        {
            foreach (var k in keys)
            {
                if (dict.TryGetValue(k, out var v) && (v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1"))
                {
                    return true;
                }
                else if (dict.TryGetValue(k, out var v2) && (v2.Equals("false", StringComparison.OrdinalIgnoreCase) || v2 == "0"))
                {
                    return false;
                }
            }

            return null;
        }
    }
}
