// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Windows.ApplicationModel;

namespace Microsoft.CmdPal.Common.Helpers;

/// <summary>
/// Helper class for retrieving application version information safely.
/// </summary>
internal static partial class VersionHelper
{
    /// <summary>
    /// Gets the application version as a string in the format "Major.Minor.Build.Revision".
    /// Falls back to assembly version if packaged version is unavailable, and returns a default value if both fail.
    /// </summary>
    /// <returns>The application version string, or a fallback value if retrieval fails.</returns>
    public static string GetAppVersionSafe(ILogger logger)
    {
        if (TryGetPackagedVersion(out var version, logger))
        {
            return version;
        }

        if (TryGetAssemblyVersion(out version, logger))
        {
            return version;
        }

        return "?";
    }

    /// <summary>
    /// Attempts to retrieve the application version from the package manifest.
    /// </summary>
    /// <param name="version">The version string if successful, or an empty string if unsuccessful.</param>
    /// <returns>True if the version was retrieved successfully; otherwise, false.</returns>
    private static bool TryGetPackagedVersion(out string version, ILogger logger)
    {
        version = string.Empty;
        try
        {
            // Package.Current throws InvalidOperationException if the app is not packaged
            var v = Package.Current.Id.Version;
            version = $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (Exception ex)
        {
            Log_FailedToGetVersion(logger, ex);
            return false;
        }
    }

    /// <summary>
    /// Attempts to retrieve the application version from the executable file.
    /// </summary>
    /// <param name="version">The version string if successful, or an empty string if unsuccessful.</param>
    /// <returns>True if the version was retrieved successfully; otherwise, false.</returns>
    private static bool TryGetAssemblyVersion(out string version, ILogger logger)
    {
        version = string.Empty;
        try
        {
            var processPath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(processPath))
            {
                return false;
            }

            var info = FileVersionInfo.GetVersionInfo(processPath);
            version = $"{info.FileMajorPart}.{info.FileMinorPart}.{info.FileBuildPart}.{info.FilePrivatePart}";
            return true;
        }
        catch (Exception ex)
        {
            Log_FailedToGetVersionFromExe(logger, ex);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to get version from the package")]
    static partial void Log_FailedToGetVersion(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to get version from the executable")]
    static partial void Log_FailedToGetVersionFromExe(ILogger logger, Exception ex);
}
