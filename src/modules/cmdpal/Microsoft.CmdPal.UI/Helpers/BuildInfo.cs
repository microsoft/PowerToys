// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using System.Runtime.CompilerServices;

namespace Microsoft.CmdPal.UI.Helpers;

internal static class BuildInfo
{
#if DEBUG
    public const string Configuration = "Debug";
#else
    public const string Configuration = "Release";
#endif

    // Runtime AOT detection
    public static bool IsNativeAot => !RuntimeFeature.IsDynamicCodeSupported;

    // From assembly metadata (build-time values)
    public static bool PublishTrimmed => GetBoolMetadata("PublishTrimmed", false);

    // From assembly metadata (build-time values)
    public static bool PublishAot => GetBoolMetadata("PublishAot", false);

    public static bool IsCiBuild => GetBoolMetadata("CIBuild", false);

    private static string? GetMetadata(string key) =>
        Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == key)?.Value;

    private static bool GetBoolMetadata(string key, bool defaultValue) =>
        bool.TryParse(GetMetadata(key), out var result) ? result : defaultValue;
}
