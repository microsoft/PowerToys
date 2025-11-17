// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.WinGet.WindowsPackageManager.Interop;
using Microsoft.Management.Deployment;

namespace WindowsPackageManager.Interop;

internal static class ClassesDefinition
{
    private static Dictionary<Type, ClassModel> Classes { get; } = new()
    {
        [typeof(PackageManager)] = new()
        {
            ProjectedClassType = typeof(PackageManager),
            InterfaceType = typeof(IPackageManager),
            Clsids = new Dictionary<ClsidContext, Guid>()
            {
                [ClsidContext.Prod] = new Guid("C53A4F16-787E-42A4-B304-29EFFB4BF597"),
                [ClsidContext.Dev] = new Guid("74CB3139-B7C5-4B9E-9388-E6616DEA288C"),
            },
        },

        [typeof(FindPackagesOptions)] = new()
        {
            ProjectedClassType = typeof(FindPackagesOptions),
            InterfaceType = typeof(IFindPackagesOptions),
            Clsids = new Dictionary<ClsidContext, Guid>()
            {
                [ClsidContext.Prod] = new Guid("572DED96-9C60-4526-8F92-EE7D91D38C1A"),
                [ClsidContext.Dev] = new Guid("1BD8FF3A-EC50-4F69-AEEE-DF4C9D3BAA96"),
            },
        },

        [typeof(CreateCompositePackageCatalogOptions)] = new()
        {
            ProjectedClassType = typeof(CreateCompositePackageCatalogOptions),
            InterfaceType = typeof(ICreateCompositePackageCatalogOptions),
            Clsids = new Dictionary<ClsidContext, Guid>()
            {
                [ClsidContext.Prod] = new Guid("526534B8-7E46-47C8-8416-B1685C327D37"),
                [ClsidContext.Dev] = new Guid("EE160901-B317-4EA7-9CC6-5355C6D7D8A7"),
            },
        },

        [typeof(InstallOptions)] = new()
        {
            ProjectedClassType = typeof(InstallOptions),
            InterfaceType = typeof(IInstallOptions),
            Clsids = new Dictionary<ClsidContext, Guid>()
            {
                [ClsidContext.Prod] = new Guid("1095F097-EB96-453B-B4E6-1613637F3B14"),
                [ClsidContext.Dev] = new Guid("44FE0580-62F7-44D4-9E91-AA9614AB3E86"),
            },
        },

        [typeof(UninstallOptions)] = new()
        {
            ProjectedClassType = typeof(UninstallOptions),
            InterfaceType = typeof(IUninstallOptions),
            Clsids = new Dictionary<ClsidContext, Guid>()
            {
                [ClsidContext.Prod] = new Guid("E1D9A11E-9F85-4D87-9C17-2B93143ADB8D"),
                [ClsidContext.Dev] = new Guid("AA2A5C04-1AD9-46C4-B74F-6B334AD7EB8C"),
            },
        },

        [typeof(PackageMatchFilter)] = new()
        {
            ProjectedClassType = typeof(PackageMatchFilter),
            InterfaceType = typeof(IPackageMatchFilter),
            Clsids = new Dictionary<ClsidContext, Guid>()
            {
                [ClsidContext.Prod] = new Guid("D02C9DAF-99DC-429C-B503-4E504E4AB000"),
                [ClsidContext.Dev] = new Guid("3F85B9F4-487A-4C48-9035-2903F8A6D9E8"),
            },
        },
    };

    /// <summary>
    /// Get CLSID based on the provided context for the specified type
    /// </summary>
    /// <typeparam name="T">Projected class type</typeparam>
    /// <param name="context">Context</param>
    /// <returns>CLSID for the provided context and type, or throw an exception if not found.</returns>
    /// <exception cref="InvalidOperationException">Throws an exception if type is not a project class.</exception>
    public static Guid GetClsid<T>(ClsidContext context)
    {
        ValidateType<T>();
        return Classes[typeof(T)].GetClsid(context);
    }

    /// <summary>
    /// Get IID corresponding to the COM object
    /// </summary>
    /// <typeparam name="T">Projected class type</typeparam>
    /// <returns>IID or throw an exception if not found.</returns>
    /// <exception cref="InvalidOperationException">Throws an exception if type is not a project class.</exception>
    public static Guid GetIid<T>()
    {
        ValidateType<T>();
        return Classes[typeof(T)].GetIid();
    }

    /// <summary>
    /// Validate that the provided type is defined.
    /// </summary>
    /// <param name="type">Projected class type</param>
    /// <exception cref="InvalidOperationException">Throws an exception if type is not a project class.</exception>
    private static void ValidateType<TType>()
    {
        if (!Classes.ContainsKey(typeof(TType)))
        {
            throw new InvalidOperationException($"{typeof(TType).Name} is not a projected class type.");
        }
    }
}
