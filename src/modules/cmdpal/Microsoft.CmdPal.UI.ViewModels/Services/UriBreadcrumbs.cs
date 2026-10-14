// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Services;

internal static class UriBreadcrumbs
{
    public static bool TryParse(Uri uri, string expectedScheme, out string[] breadcrumbs)
    {
        breadcrumbs = [];

        if (!uri.IsAbsoluteUri ||
            !uri.Scheme.Equals(expectedScheme, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !uri.IsDefaultPort ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            uri.AbsolutePath.Contains("//", StringComparison.Ordinal))
        {
            return false;
        }

        // Split while the path is still escaped so an encoded separator stays in
        // its original segment. Decode each segment exactly once afterwards.
        var escapedSegments = uri.GetComponents(UriComponents.Path, UriFormat.UriEscaped)
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        var parsedBreadcrumbs = new string[escapedSegments.Length + 1];
        parsedBreadcrumbs[0] = uri.Host;

        for (var i = 0; i < escapedSegments.Length; i++)
        {
            parsedBreadcrumbs[i + 1] = Uri.UnescapeDataString(escapedSegments[i]);
        }

        if (Array.Exists(
            parsedBreadcrumbs,
            static breadcrumb =>
                breadcrumb.Contains('/') ||
                breadcrumb.Contains('\\') ||
                breadcrumb.Any(char.IsControl)))
        {
            return false;
        }

        breadcrumbs = parsedBreadcrumbs;
        return true;
    }
}
