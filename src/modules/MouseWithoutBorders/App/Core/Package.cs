// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// <summary>
//     Package format/conversion.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
namespace MouseWithoutBorders.Core;

internal static class Package
{
    internal const byte PACKAGE_SIZE = 32;
    internal const byte PACKAGE_SIZE_EX = 64;
    private const byte WP_PACKAGE_SIZE = 6;
    internal static PackageMonitor PackageSent;
    internal static PackageMonitor PackageReceived;
    internal static int PackageID;
}
