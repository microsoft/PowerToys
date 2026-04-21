// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common;

/// <summary>
/// Represents the packaging flavor of the application.
/// </summary>
public enum AppPackagingFlavor
{
    /// <summary>
    /// Application is packaged as a Windows MSIX package.
    /// </summary>
    Packaged,

    /// <summary>
    /// Application is running unpackaged (native executable).
    /// </summary>
    Unpackaged,

    /// <summary>
    /// Application is running as unpackaged portable (self-contained distribution).
    /// </summary>
    UnpackagedPortable,
}
