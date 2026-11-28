// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Windows.ApplicationModel.Resources;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// The engine's localizable words, from <c>Strings\en-us\Resources.resw</c>. Each call carries its English,
/// used when the resources can't be read. Never used on the keyboard hook's thread.
/// </summary>
internal static class EngineText
{
    private static readonly Lazy<Func<string, string>?> Loader = new(CreateLoader);

    public static string Get(string key, string english)
    {
        try
        {
            string? text = Loader.Value?.Invoke(key);
            return string.IsNullOrEmpty(text) ? english : text;
        }
        catch
        {
            return english;
        }
    }

    private static Func<string, string>? CreateLoader()
    {
        try
        {
            return new ResourceLoader("PowerToys.TextExpander.pri").GetString;
        }
        catch
        {
            return null;
        }
    }
}
