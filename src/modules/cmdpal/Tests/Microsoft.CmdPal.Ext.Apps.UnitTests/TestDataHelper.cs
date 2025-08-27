// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Apps.Programs;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

/// <summary>
/// Helper class to create test data for unit tests.
/// </summary>
public static class TestDataHelper
{
    /// <summary>
    /// Creates a test Win32 program with the specified parameters.
    /// </summary>
    /// <param name="name">The name of the application.</param>
    /// <param name="fullPath">The full path to the application executable.</param>
    /// <param name="enabled">A value indicating whether the application is enabled.</param>
    /// <param name="valid">A value indicating whether the application is valid.</param>
    /// <returns>A new Win32Program instance with the specified parameters.</returns>
    public static Win32Program CreateTestWin32Program(
        string name = "Test App",
        string fullPath = "C:\\TestApp\\app.exe",
        bool enabled = true,
        bool valid = true)
    {
        return new Win32Program
        {
            Name = name,
            FullPath = fullPath,
            Enabled = enabled,
            Valid = valid,
            UniqueIdentifier = $"win32_{name}",
            Description = $"Test description for {name}",
            ExecutableName = "app.exe",
            ParentDirectory = "C:\\TestApp",
            AppType = Win32Program.ApplicationType.Win32Application,
        };
    }

    /// <summary>
    /// Creates a test UWP application with the specified parameters.
    /// </summary>
    /// <param name="displayName">The display name of the application.</param>
    /// <param name="userModelId">The user model ID of the application.</param>
    /// <param name="enabled">A value indicating whether the application is enabled.</param>
    /// <returns>A new IUWPApplication instance with the specified parameters.</returns>
    public static IUWPApplication CreateTestUWPApplication(
        string displayName = "Test UWP App",
        string userModelId = "TestPublisher.TestUWPApp_1.0.0.0_neutral__8wekyb3d8bbwe",
        bool enabled = true)
    {
        return new MockUWPApplication
        {
            DisplayName = displayName,
            UserModelId = userModelId,
            Enabled = enabled,
            UniqueIdentifier = $"uwp_{userModelId}",
            Description = $"Test UWP description for {displayName}",
            AppListEntry = "default",
            BackgroundColor = "#000000",
            EntryPoint = "TestApp.App",
            CanRunElevated = false,
            LogoPath = string.Empty,
            Package = CreateMockUWPPackage(displayName, userModelId),
        };
    }

    /// <summary>
    /// Creates a mock UWP package for testing purposes.
    /// </summary>
    /// <param name="displayName">The display name of the package.</param>
    /// <param name="userModelId">The user model ID of the package.</param>
    /// <returns>A new UWP package instance.</returns>
    private static UWP CreateMockUWPPackage(string displayName, string userModelId)
    {
        var mockPackage = new MockPackage
        {
            Name = displayName,
            FullName = userModelId,
            FamilyName = $"{displayName}_8wekyb3d8bbwe",
            InstalledLocation = $"C:\\Program Files\\WindowsApps\\{displayName}",
        };

        return new UWP(mockPackage)
        {
            Location = mockPackage.InstalledLocation,
            LocationLocalized = mockPackage.InstalledLocation,
        };
    }

    /// <summary>
    /// Mock implementation of IPackage for testing purposes.
    /// </summary>
    private sealed class MockPackage : IPackage
    {
        /// <summary>
        /// Gets or sets the name of the package.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the full name of the package.
        /// </summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the family name of the package.
        /// </summary>
        public string FamilyName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the package is a framework package.
        /// </summary>
        public bool IsFramework { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the package is in development mode.
        /// </summary>
        public bool IsDevelopmentMode { get; set; }

        /// <summary>
        /// Gets or sets the installed location of the package.
        /// </summary>
        public string InstalledLocation { get; set; } = string.Empty;
    }
}
