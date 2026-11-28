// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>Snippets files for the tests, with each snippet given as trigger=replacement for brevity.</summary>
internal static class SnippetJson
{
    /// <summary>Gets a snippets file with <paramref name="snippets"/>, in order, each written as trigger=replacement.</summary>
    public static string Of(params string[] snippets)
        => "{ \"version\": 1, \"snippets\": [" + string.Join(", ", snippets.Select(Entry)) + "] }";

    /// <summary>Gets <see cref="Of"/> as a file's bytes.</summary>
    public static byte[] Bytes(params string[] snippets) => Encoding.UTF8.GetBytes(Of(snippets));

    /// <summary>Gets the snippets in the file at <paramref name="path"/>, each as trigger=replacement.</summary>
    public static string[] In(string path)
        => SnippetFile.Read(System.IO.File.ReadAllBytes(path)).Snippets.Select(entry => entry.Trigger + "=" + entry.Replacement).ToArray();

    /// <summary>Gets <see cref="In"/> on one line, each snippet separated by " | ", for comparing.</summary>
    public static string Listed(string path) => string.Join(" | ", In(path));

    private static string Entry(string snippet)
    {
        int split = snippet.IndexOf('=', StringComparison.Ordinal);
        return $"{{ \"trigger\": \"{JsonEncodedText.Encode(snippet[..split])}\", \"replacement\": \"{JsonEncodedText.Encode(snippet[(split + 1)..])}\" }}";
    }
}
