// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.Registry.Constants;

/// <summary>
/// This class contains names for important registry keys
/// </summary>
internal static class KeyName
{
    /// <summary>
    /// The first name part of each base key without the underscore
    /// </summary>
    internal const string FirstPart = "HKEY";

    /// <summary>
    /// The first name part of each base key follow by a underscore
    /// </summary>
    internal const string FirstPartUnderscore = "HKEY_";

    /// <summary>
    /// The short name for the base key HKEY_CLASSES_ROOT (see <see cref="Win32.Registry.ClassesRoot"/>)
    /// </summary>
    internal const string ClassRootShort = "HKCR";

    /// <summary>
    /// The short name for the base key HKEY_CURRENT_CONFIG (see <see cref="Win32.Registry.CurrentConfig"/>)
    /// </summary>
    internal const string CurrentConfigShort = "HKCC";

    /// <summary>
    /// The short name for the base key HKEY_CURRENT_USER (see <see cref="Win32.Registry.CurrentUser"/>)
    /// </summary>
    internal const string CurrentUserShort = "HKCU";

    /// <summary>
    /// The short name for the base key HKEY_LOCAL_MACHINE (see <see cref="Win32.Registry.LocalMachine"/>)
    /// </summary>
    internal const string LocalMachineShort = "HKLM";

    /// <summary>
    /// The short name for the base key HKEY_PERFORMANCE_DATA (see <see cref="Win32.Registry.PerformanceData"/>)
    /// </summary>
    internal const string PerformanceDataShort = "HKPD";

    /// <summary>
    /// The short name for the base key HKEY_USERS (see <see cref="Win32.Registry.Users"/>)
    /// </summary>
    internal const string UsersShort = "HKU";
}
