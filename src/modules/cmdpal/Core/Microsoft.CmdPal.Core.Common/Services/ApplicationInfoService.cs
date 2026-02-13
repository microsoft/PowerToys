// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.CmdPal.Core.Common.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.ApplicationModel;

namespace Microsoft.CmdPal.Core.Common.Services;

/// <summary>
/// Implementation of IApplicationInfoService providing application-wide information.
/// </summary>
public sealed class ApplicationInfoService : IApplicationInfoService
{
    private readonly Lazy<string> _configDirectory = new(() => Utilities.BaseSettingsPath("Microsoft.CmdPal"));
    private readonly Lazy<bool> _isElevated;
    private readonly Lazy<string> _logDirectory;
    private readonly Lazy<AppPackagingFlavor> _packagingFlavor;
    private Func<string>? _getLogDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationInfoService"/> class.
    /// The log directory delegate can be set later via <see cref="SetLogDirectory(Func{string})"/>.
    /// </summary>
    public ApplicationInfoService()
    {
        _packagingFlavor = new Lazy<AppPackagingFlavor>(DeterminePackagingFlavor);
        _isElevated = new Lazy<bool>(DetermineElevationStatus);
        _logDirectory = new Lazy<string>(() => _getLogDirectory?.Invoke() ?? "Not available");
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationInfoService"/> class with an optional log directory provider.
    /// </summary>
    /// <param name="getLogDirectory">Optional delegate to retrieve the log directory path. If not provided, the log directory will be unavailable.</param>
    public ApplicationInfoService(Func<string>? getLogDirectory)
        : this()
    {
        _getLogDirectory = getLogDirectory;
    }

    /// <summary>
    /// Sets the log directory delegate to be used for retrieving the log directory path.
    /// This allows deferred initialization of the logger path.
    /// </summary>
    /// <param name="getLogDirectory">Delegate to retrieve the log directory path.</param>
    public void SetLogDirectory(Func<string> getLogDirectory)
    {
        ArgumentNullException.ThrowIfNull(getLogDirectory);
        _getLogDirectory = getLogDirectory;
    }

    public string AppVersion => VersionHelper.GetAppVersionSafe();

    public AppPackagingFlavor PackagingFlavor => _packagingFlavor.Value;

    public string LogDirectory => _logDirectory.Value;

    public string ConfigDirectory => _configDirectory.Value;

    public bool IsElevated => _isElevated.Value;

    public string GetApplicationInfoSummary()
    {
        return $"""
                Application:
                  App version:           {AppVersion}
                  Packaging flavor:      {PackagingFlavor}
                  Is elevated:           {(IsElevated ? "yes" : "no")}

                Environment:
                  OS version:            {RuntimeInformation.OSDescription}
                  OS architecture:       {RuntimeInformation.OSArchitecture}
                  Runtime identifier:    {RuntimeInformation.RuntimeIdentifier}
                  Framework:             {RuntimeInformation.FrameworkDescription}
                  Process architecture:  {RuntimeInformation.ProcessArchitecture}
                  Culture:               {CultureInfo.CurrentCulture.Name}
                  UI culture:            {CultureInfo.CurrentUICulture.Name}

                Paths:
                  Log directory:         {LogDirectory}
                  Config directory:      {ConfigDirectory}
                """;
    }

    private static AppPackagingFlavor DeterminePackagingFlavor()
    {
        // Try to determine if running as packaged
        try
        {
            // If this doesn't throw, we're packaged
            _ = Package.Current.Id.Version;
            return AppPackagingFlavor.Packaged;
        }
        catch (InvalidOperationException)
        {
            // Not packaged, check if portable
            // For now, we don't support portable yet, so return Unpackaged
            // In the future, check for a marker file or environment variable
            return AppPackagingFlavor.Unpackaged;
        }
        catch (Exception ex)
        {
            CoreLogger.LogError("Failed to determine packaging flavor", ex);
            return AppPackagingFlavor.Unpackaged;
        }
    }

    private static bool DetermineElevationStatus()
    {
        try
        {
            var isElevated = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            return isElevated;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
