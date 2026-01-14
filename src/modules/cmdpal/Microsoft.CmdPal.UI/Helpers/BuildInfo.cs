// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;

namespace Microsoft.CmdPal.UI.Helpers;

internal static class BuildInfo
{
#if DEBUG
    public const string Configuration = "Debug";
#else
    public const string Configuration = "Release";
#endif

    // Runtime AOT detection
    public static bool IsNativeAot => !RuntimeFeature.IsDynamicCodeSupported;

    // build-time values
    public static bool PublishTrimmed
    {
        get
        {
#if BUILD_INFO_PUBLISH_TRIMMED
          return true;
#else
            return false;
#endif
        }
    }

    // build-time values
    public static bool PublishAot
    {
        get
        {
#if BUILD_INFO_PUBLISH_AOT
            return true;
#else
            return false;
#endif
        }
    }

    public static bool IsCiBuild
    {
        get
        {
#if BUILD_INFO_CIBUILD
            return true;
#else
            return false;
#endif
        }
    }
}
