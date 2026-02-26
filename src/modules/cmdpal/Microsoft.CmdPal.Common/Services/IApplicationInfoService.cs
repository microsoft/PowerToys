// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.Services;

/// <summary>
/// Provides access to application-wide information such as version, packaging flavor, and directory paths.
/// </summary>
public interface IApplicationInfoService
{
    /// <summary>
    /// Gets the application version as a string in the format "Major.Minor.Build.Revision".
    /// </summary>
    string AppVersion { get; }

    /// <summary>
    /// Gets the packaging flavor of the application.
    /// </summary>
    AppPackagingFlavor PackagingFlavor { get; }

    /// <summary>
    /// Gets the directory path where application logs are stored.
    /// </summary>
    string LogDirectory { get; }

    /// <summary>
    /// Gets the directory path where application configuration files are stored.
    /// </summary>
    string ConfigDirectory { get; }

    /// <summary>
    /// Gets a value indicating whether the application is running with administrator privileges.
    /// </summary>
    bool IsElevated { get; }

    /// <summary>
    /// Gets a formatted summary of application information suitable for logging.
    /// </summary>
    /// <returns>A formatted string containing application information.</returns>
    string GetApplicationInfoSummary();

    /// <summary>
    /// Sets the log directory delegate to be used for retrieving the log directory path.
    /// This allows deferred initialization of the logger path.
    /// </summary>
    /// <param name="getLogDirectory">Delegate to retrieve the log directory path.</param>
    void SetLogDirectory(Func<string> getLogDirectory);

    /// <summary>
    /// Sets the language override configured by the user.
    /// </summary>
    /// <param name="languageTag">The IETF BCP 47 language tag (e.g. "cs-CZ"), or empty for system default.</param>
    void SetLanguageOverride(string languageTag);
}
