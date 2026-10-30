// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

/// <summary>Captures package identity and installation data for manifest and resource reads.</summary>
internal sealed class PackageMetadata
{
    /// <summary>Gets the manifest package identity name used in PRI resource URIs.</summary>
    public string Name { get; }

    public string FullName { get; }

    public string FamilyName { get; }

    /// <summary>Gets the package installation directory used for manifest and resource reads.</summary>
    public string InstalledLocation { get; init; }

    public bool IsNonRemovable { get; }

    /// <summary>Initializes a new instance of the <see cref="PackageMetadata"/> class. Copies package identity, installation location, and uninstall eligibility into discovery metadata.</summary>
    public PackageMetadata(IPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        Name = package.Name;
        FullName = package.FullName;
        FamilyName = package.FamilyName;
        InstalledLocation = package.InstalledLocation;
        IsNonRemovable = package.IsNonRemovable;
    }
}
