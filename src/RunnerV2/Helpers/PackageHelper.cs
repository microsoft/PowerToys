// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Interop;
using ManagedCommon;
using RunnerV2.Extensions;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Management.Deployment;

namespace RunnerV2.Helpers
{
    /// <summary>
    /// Provides helper methods for working with UWP packages.
    /// </summary>
    internal static partial class PackageHelper
    {
        /// <summary>
        /// Gets the registered UWP package based on the display name and version check.
        /// </summary>
        /// <param name="packageDisplayName">The display name of the package.</param>
        /// <param name="checkVersion">If true, the package version will be checked against the executing assembly version.</param>
        /// <returns>If a package is found the corresponding <see cref="Package"/> object. If none is found <c>null</c>.</returns>
        internal static Package? GetRegisteredPackage(string packageDisplayName, bool checkVersion)
        {
            PackageManager packageManager = new();
            foreach (var package in packageManager.FindPackagesForUser(null))
            {
                if (package.Id.FullName.Contains(packageDisplayName) && (!checkVersion || package.Id.Version.ToVersion() == Assembly.GetExecutingAssembly().GetName().Version))
                {
                    return package;
                }
            }

            return null;
        }

        internal static string[] FindMsixFiles(string directoryPath, bool recursive)
        {
            if (!Directory.Exists(directoryPath))
            {
                Logger.LogError("Tried to search msix files in " + directoryPath + ", but it does not exist.");
                return [];
            }

            List<string> matchedFiles = [];

            try
            {
                foreach (string file in Directory.GetFiles(directoryPath, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                {
                    if (File.Exists(file) && msixPackagePattern().IsMatch(Path.GetFileName(file)))
                    {
                        matchedFiles.Add(file);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError("An error occured while searching for MSIX files.", e);
            }

            return [.. matchedFiles];
        }

        /// <summary>
        /// Installs the specified appx package along with its dependencies.
        /// </summary>
        /// <param name="packagePath">Path to the package</param>
        /// <param name="dependencies">Array of dependency package paths</param>
        /// <returns>True if the installation was successful, false otherwise</returns>
        internal static bool InstallPackage(string packagePath, string[] dependencies, bool isSparsePackage = false)
        {
            Logger.LogInfo("Starting package install of package \"" + packagePath + "\"");
            PackageManager packageManager = new();
            List<Uri> uris = [];

            if (IsPackageSatisfied(packagePath))
            {
                return true;
            }

            foreach (string dependency in dependencies)
            {
                try
                {
                    if (IsPackageSatisfied(dependency))
                    {
                        Logger.LogInfo("Dependency \"" + dependency + "\" is already satisfied.");
                        continue;
                    }
                    else
                    {
                        uris.Add(new Uri(packagePath));
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Could not process dependency package at path \"" + dependency + "\"", ex);
                }
            }

            try
            {
                IAsyncOperationWithProgress<DeploymentResult, DeploymentProgress> deploymentOperation = isSparsePackage
                    ? packageManager.AddPackageByUriAsync(new Uri(packagePath), new AddPackageOptions { ExternalLocationUri = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)! + "\\WinUi3Apps"), ForceUpdateFromAnyVersion = true })
                    : packageManager.AddPackageAsync(new Uri(packagePath), uris, DeploymentOptions.ForceApplicationShutdown);
                deploymentOperation.Get();

                switch (deploymentOperation.Status)
                {
                    case AsyncStatus.Error:
                        Logger.LogError($"Registering {packagePath} failed. ErrorCode: {deploymentOperation.ErrorCode}, ErrorText: {deploymentOperation.GetResults().ErrorText}");
                        break;
                    case AsyncStatus.Canceled:
                        Logger.LogError($"Registering {packagePath} was canceled.");
                        break;
                    case AsyncStatus.Completed:
                        Logger.LogInfo($"Registering {packagePath} succeded.");
                        break;
                    default:
                        Logger.LogDebug($"Registering {packagePath} package started.");
                        break;
                }

                return true;
            }
            catch (Exception e)
            {
                Logger.LogError($"Exception thrown while trying to register package: {packagePath}", e);
            }

            return false;
        }

        /// <summary>
        /// Checks if the package specified by the given path is already installed and satisfies the required version.
        /// </summary>
        /// <param name="packagePath">Path to the package.</param>
        /// <returns>True if the package is already installed and satisfies the required version, false otherwise.</returns>
        private static bool IsPackageSatisfied(string packagePath)
        {
            if (!GetPackageNameAndVersionFromAppx(packagePath, out string name, out PackageVersion version))
            {
                Logger.LogError("Could not get package name and version from dependency package at path \"" + packagePath + "\"");
                return false;
            }

            PackageManager packageManager = new();

            foreach (var package in packageManager.FindPackagesForUser(null))
            {
                if (package.Id.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                    package.Id.Version.ToVersion() >= version.ToVersion())
                {
                    Logger.LogInfo($@"Package ""{name}"" is already statisfied with version: {package.Id.Version}. Target version: {version}. PackagePath: {packagePath}");
                    return true;
                }
            }

            Logger.LogInfo($@"Package ""{name}"" with version {version} is not satisfied. PackagePath: {packagePath}");
            return false;
        }

        /// <summary>
        /// Gets the package name and version from the specified appx package file.
        /// </summary>
        /// <param name="packagePath">Path to the package file.</param>
        /// <param name="name">Output parameter for the package name.</param>
        /// <param name="packageVersion">Output parameter for the package version.</param>
        /// <returns>True if the package name and version were successfully retrieved, false otherwise.</returns>
        private static bool GetPackageNameAndVersionFromAppx(string packagePath, out string name, out PackageVersion packageVersion)
        {
            bool retValue = PowerToys.Interop.Package.GetPackageNameAndVersionFromAppx(packagePath, out name, out PowerToys.Interop.PACKAGE_VERSION packageVersionInterop);
            packageVersion = new PackageVersion
            {
                Major = packageVersionInterop.Major,
                Minor = packageVersionInterop.Minor,
                Build = packageVersionInterop.Build,
                Revision = packageVersionInterop.Revision,
            };
            return retValue;
        }

        [GeneratedRegex("(^.+\\.(appx|msix|msixbundle)$)", RegexOptions.IgnoreCase)]
        private static partial Regex msixPackagePattern();
    }
}
