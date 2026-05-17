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
using System.Threading;

namespace MouseWithoutBorders.Core;

internal static class Package
{
    internal const byte PACKAGE_SIZE = 32;
    internal const byte PACKAGE_SIZE_EX = 64;
    private const byte WP_PACKAGE_SIZE = 6;

    private static int _packageID;

    internal static int PackageID
        => _packageID;

    internal static int IncrementPackageID()
    {
        return Interlocked.Increment(ref _packageID);
    }

    internal static int SetPackageID(int value)
    {
        return Interlocked.Exchange(ref _packageID, value);
    }

#pragma warning disable SA1500 // Braces for multi line statements must not share line
#pragma warning disable SA1513 // Closing brace must be followed by blank line
    internal static PackageMonitor PackageSent
    {
        get;
        set;
    } = new(0);
#pragma warning restore SA1513
#pragma warning restore SA1500

#pragma warning disable SA1500 // Braces for multi line statements must not share line
#pragma warning disable SA1513 // Closing brace must be followed by blank line
    internal static PackageMonitor PackageReceived
    {
        get;
        set;
    } = new(0);
#pragma warning restore SA1513
#pragma warning restore SA1500
}
