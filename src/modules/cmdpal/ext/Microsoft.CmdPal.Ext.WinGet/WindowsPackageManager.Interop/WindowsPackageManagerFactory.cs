// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Management.Deployment;

namespace WindowsPackageManager.Interop;

/// <summary>
/// Factory class for creating WinGet COM objects.
/// Details about each method can be found in the source IDL:
/// https://github.com/microsoft/winget-cli/blob/master/src/Microsoft.Management.Deployment/PackageManager.idl
/// </summary>
public abstract class WindowsPackageManagerFactory
{
    private readonly ClsidContext _clsidContext;

    public WindowsPackageManagerFactory(ClsidContext clsidContext)
    {
        _clsidContext = clsidContext;
    }

    /// <summary>
    /// Creates an instance of the class <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// Type <typeparamref name="T"/> must be one of the types defined in the winget COM API.
    /// Implementations of this method can assume that <paramref name="clsid"/> and <paramref name="iid"/>
    /// are the right GUIDs for the class in the given context.
    /// </remarks>
    protected abstract T CreateInstance<T>(Guid clsid, Guid iid);

    public PackageManager CreatePackageManager() => CreateInstance<PackageManager>();

    public FindPackagesOptions CreateFindPackagesOptions() => CreateInstance<FindPackagesOptions>();

    public CreateCompositePackageCatalogOptions CreateCreateCompositePackageCatalogOptions() => CreateInstance<CreateCompositePackageCatalogOptions>();

    public InstallOptions CreateInstallOptions() => CreateInstance<InstallOptions>();

    public UninstallOptions CreateUninstallOptions() => CreateInstance<UninstallOptions>();

    public PackageMatchFilter CreatePackageMatchFilter() => CreateInstance<PackageMatchFilter>();

    /// <summary>
    /// Creates an instance of the class <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// This is a helper for calling the derived class's <see cref="CreateInstance{T}(Guid, Guid)"/>
    /// method with the appropriate GUIDs.
    /// </remarks>
    private T CreateInstance<T>()
    {
        var clsid = ClassesDefinition.GetClsid<T>(_clsidContext);
        var iid = ClassesDefinition.GetIid<T>();
        return CreateInstance<T>(clsid, iid);
    }
}
