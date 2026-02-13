// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.UITest
{
    /// <summary>
    /// Centralized configuration for all environment variables used in UI tests.
    /// </summary>
    public static class EnvironmentConfig
    {
        private static readonly Lazy<bool> _isInPipeline = new(() =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("platform")));

        private static readonly Lazy<bool> _useInstallerForTest = new(() =>
        {
            string? envValue = Environment.GetEnvironmentVariable("useInstallerForTest") ??
                              Environment.GetEnvironmentVariable("USEINSTALLERFORTEST");
            return !string.IsNullOrEmpty(envValue) && bool.TryParse(envValue, out bool result) && result;
        });

        private static readonly Lazy<string?> _platform = new(() =>
            Environment.GetEnvironmentVariable("platform"));

        /// <summary>
        /// Gets a value indicating whether the tests are running in a CI/CD pipeline.
        /// Determined by the presence of the "platform" environment variable.
        /// </summary>
        public static bool IsInPipeline => _isInPipeline.Value;

        /// <summary>
        /// Gets a value indicating whether to use installer paths for testing.
        /// Checks both "useInstallerForTest" and "USEINSTALLERFORTEST" environment variables.
        /// </summary>
        public static bool UseInstallerForTest => _useInstallerForTest.Value;

        /// <summary>
        /// Gets the platform name from the environment variable.
        /// Typically used in CI/CD pipelines to identify the build platform.
        /// </summary>
        public static string? Platform => _platform.Value;
    }
}
