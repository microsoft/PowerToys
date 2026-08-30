// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Windows.ApplicationModel;

namespace RunnerV2.Extensions
{
    internal static class PackageVersionExtensions
    {
        public static Version ToVersion(this PackageVersion packageVersion)
        {
            return new Version(packageVersion.Major, packageVersion.Minor, packageVersion.Build, packageVersion.Revision);
        }
    }
}
