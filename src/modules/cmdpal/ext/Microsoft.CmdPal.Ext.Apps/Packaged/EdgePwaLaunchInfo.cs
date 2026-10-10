// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.Ext.Apps.Packaged;

/// <summary>Identifies equivalent Edge PWA launches without depending on generated package names.</summary>
/// <remarks>All fields compare ordinally. Browser parameters, profile context and URLs must retain their original spelling.</remarks>
internal sealed record EdgePwaLaunchInfo
{
    private const string AppIdPrefix = "--app-id=";

    /// <summary>Gets the hosted package's manifest publisher.</summary>
    public string PackagePublisher { get; init; } = string.Empty;

    /// <summary>Gets the host runtime package name, including its browser channel.</summary>
    public string HostPackageName { get; init; } = string.Empty;

    /// <summary>Gets the host runtime package's publisher.</summary>
    public string HostPackagePublisher { get; init; } = string.Empty;

    /// <summary>Gets the hosted application's runtime identifier.</summary>
    public string HostId { get; init; } = string.Empty;

    /// <summary>Gets the complete manifest parameters passed to the host.</summary>
    public string Parameters { get; init; } = string.Empty;

    /// <summary>Gets Edge's complete private launch description, including its profile and any start URL.</summary>
    public string LaunchContext { get; init; } = string.Empty;

    /// <summary>Gets whether the metadata follows the supported Edge PWA launch format.</summary>
    [JsonIgnore]
    public bool IsSupported => !string.IsNullOrWhiteSpace(PackagePublisher)
        && !string.IsNullOrWhiteSpace(HostPackagePublisher)
        && HostPackageName is "Microsoft.MicrosoftEdge" or "Microsoft.MicrosoftEdge.Stable"
            or "Microsoft.MicrosoftEdge.Beta" or "Microsoft.MicrosoftEdge.Dev" or "Microsoft.MicrosoftEdge.Canary"
        && HostId == "PWA"
        && !string.IsNullOrWhiteSpace(Parameters)
        && Parameters.StartsWith(AppIdPrefix, StringComparison.Ordinal)
        && Parameters.Length > AppIdPrefix.Length
        && !char.IsWhiteSpace(Parameters[AppIdPrefix.Length])
        && HasKnownLaunchContext();

    private bool HasKnownLaunchContext()
    {
        // Edge's private format is compared intact rather than splitting arguments or URLs on semicolons.
        var prefix = $"parameters?{Parameters};profile-directory?";
        return LaunchContext is not null
            && LaunchContext.StartsWith(prefix, StringComparison.Ordinal)
            && LaunchContext.Length > prefix.Length
            && !char.IsWhiteSpace(LaunchContext[prefix.Length])
            && LaunchContext[prefix.Length] != ';';
    }
}
