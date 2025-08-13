// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Apps.Programs;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

/// <summary>
/// Helper class to create test data for unit tests
/// </summary>
public static class TestDataHelper
{
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

    private class MockPackage : IPackage
    {
        public string Name { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string FamilyName { get; set; } = string.Empty;

        public bool IsFramework { get; set; }

        public bool IsDevelopmentMode { get; set; }

        public string InstalledLocation { get; set; } = string.Empty;
    }
}
