// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Windows.ApplicationModel;
using Windows.Foundation.Metadata;
using Package = Windows.ApplicationModel.Package;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

public class PackageWrapper : IPackage
{
    private const int ErrorAppDataNotFoundHResult = unchecked((int)0x80071130);

    private static readonly Lazy<bool> IsPackageDotInstallationPathAvailable = new(() =>
        ApiInformation.IsPropertyPresent(typeof(Package).FullName, nameof(Package.InstalledLocation.Path)));

    public string Name { get; } = string.Empty;

    public string FullName { get; } = string.Empty;

    public string FamilyName { get; } = string.Empty;

    public bool IsFramework { get; }

    public bool IsDevelopmentMode { get; }

    public bool IsNonRemovable { get; }

    public string InstalledLocation { get; } = string.Empty;

    public PackageWrapper()
    {
    }

    public PackageWrapper(string name, string fullName, string familyName, bool isFramework, bool isDevelopmentMode, string installedLocation, bool isNonRemovable = false)
    {
        Name = name;
        FullName = fullName;
        FamilyName = familyName;
        IsFramework = isFramework;
        IsDevelopmentMode = isDevelopmentMode;
        InstalledLocation = installedLocation;
        IsNonRemovable = isNonRemovable;
    }

    public static PackageWrapper GetWrapperFromPackage(Package package)
    {
        ArgumentNullException.ThrowIfNull(package);

        string path;
        try
        {
            path = IsPackageDotInstallationPathAvailable.Value ? GetInstalledPath(package) : package.InstalledLocation.Path;
        }
        catch (Exception e) when (
            e is ArgumentException or FileNotFoundException or DirectoryNotFoundException ||
            IsFastCacheDataNotFound(e))
        {
            path = string.Empty;
        }

        var packageId = package.Id;
        return new PackageWrapper(
            packageId.Name,
            packageId.FullName,
            packageId.FamilyName,
            package.IsFramework,
            package.IsDevelopmentMode,
            path,
            GetIsNonRemovable(() => package.SignatureKind));
    }

    /// <summary>Reads system-package status, conservatively disabling uninstall when the package-cache data is unavailable.</summary>
    internal static bool GetIsNonRemovable(Func<PackageSignatureKind> getSignatureKind)
    {
        ArgumentNullException.ThrowIfNull(getSignatureKind);

        try
        {
            return getSignatureKind() == PackageSignatureKind.System;
        }
        catch (COMException e) when (IsFastCacheDataNotFound(e))
        {
            // Signature kind only controls whether the Uninstall command is offered. If Windows'
            // package cache is temporarily unavailable, keep the app and choose the safe default.
            return true;
        }
    }

    /// <summary>Reads framework status, treating a failed property read as an app package so refreshes are not suppressed.</summary>
    internal static bool GetIsFramework(Func<bool> getIsFramework)
    {
        ArgumentNullException.ThrowIfNull(getIsFramework);

        try
        {
            return getIsFramework();
        }
        catch (Exception)
        {
            // Unknown package type must not suppress an application refresh.
            return false;
        }
    }

    private static bool IsFastCacheDataNotFound(Exception exception)
    {
        return exception.HResult == ErrorAppDataNotFoundHResult;
    }

    // This is a separate method so the reference to .InstalledPath won't be loaded in API versions which do not support this API (e.g. older then Build 19041)
    private static string GetInstalledPath(Package package)
    {
        return package.InstalledLocation.Path;
    }
}
