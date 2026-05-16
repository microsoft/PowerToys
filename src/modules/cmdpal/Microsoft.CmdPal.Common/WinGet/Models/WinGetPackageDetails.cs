// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.WinGet.Models;

public sealed record WinGetPackageDetails(
    string? Name,
    string? Version,
    string? Summary,
    string? Description,
    string? Publisher,
    string? PublisherUrl,
    string? PublisherSupportUrl,
    string? Author,
    string? License,
    string? LicenseUrl,
    string? PackageUrl,
    string? ReleaseNotes,
    string? ReleaseNotesUrl,
    string? IconUrl,
    IReadOnlyList<WinGetNamedLink> DocumentationLinks,
    IReadOnlyList<string> Tags);
