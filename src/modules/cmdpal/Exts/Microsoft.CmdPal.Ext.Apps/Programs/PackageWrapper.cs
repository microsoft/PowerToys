// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Windows.Foundation.Metadata;
using Package = Windows.ApplicationModel.Package;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

public class PackageWrapper : IPackage
{
    public string Name { get; } = string.Empty;

    public string FullName { get; } = string.Empty;

    public string FamilyName { get; } = string.Empty;

    public bool IsFramework { get; }

    public bool IsDevelopmentMode { get; }

    public string InstalledLocation { get; } = string.Empty;

    public PackageWrapper()
    {
    }

    public PackageWrapper(string name, string fullName, string familyName, bool isFramework, bool isDevelopmentMode, string installedLocation)
    {
        Name = name;
        FullName = fullName;
        FamilyName = familyName;
        IsFramework = isFramework;
        IsDevelopmentMode = isDevelopmentMode;
        InstalledLocation = installedLocation;
    }

    private static readonly Lazy<bool> IsPackageDotInstallationPathAvailable = new(() =>
        ApiInformation.IsPropertyPresent(typeof(Package).FullName, nameof(Package.InstalledLocation.Path)));

    public static PackageWrapper GetWrapperFromPackage(Package package)
    {
        ArgumentNullException.ThrowIfNull(package);

        string path;
        try
        {
            path = IsPackageDotInstallationPathAvailable.Value ? GetInstalledPath(package) : package.InstalledLocation.Path;
        }
        catch (Exception e) when (e is ArgumentException || e is FileNotFoundException || e is DirectoryNotFoundException)
        {
            return new PackageWrapper(
                package.Id.Name,
                package.Id.FullName,
                package.Id.FamilyName,
                package.IsFramework,
                package.IsDevelopmentMode,
                string.Empty);
        }

        return new PackageWrapper(
                package.Id.Name,
                package.Id.FullName,
                package.Id.FamilyName,
                package.IsFramework,
                package.IsDevelopmentMode,
                path);
    }

    // This is a separate method so the reference to .InstalledPath won't be loaded in API versions which do not support this API (e.g. older then Build 19041)
    private static string GetInstalledPath(Package package)
        => package.InstalledLocation.Path;
}
