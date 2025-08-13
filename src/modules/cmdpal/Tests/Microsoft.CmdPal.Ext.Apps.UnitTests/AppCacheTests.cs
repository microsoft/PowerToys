// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AppCacheTests
{
    [TestMethod]
    public void MockAppCache_ShouldReload_ReturnsFalseByDefault()
    {
        // Arrange
        var mockCache = new MockAppCache();

        // Act
        var shouldReload = mockCache.ShouldReload();

        // Assert
        Assert.IsFalse(shouldReload);
    }

    [TestMethod]
    public void MockAppCache_SetShouldReload_UpdatesReloadFlag()
    {
        // Arrange
        var mockCache = new MockAppCache();

        // Act
        mockCache.SetShouldReload(true);

        // Assert
        Assert.IsTrue(mockCache.ShouldReload());
    }

    [TestMethod]
    public void MockAppCache_ResetReloadFlag_SetsToFalse()
    {
        // Arrange
        var mockCache = new MockAppCache();
        mockCache.SetShouldReload(true);

        // Act
        mockCache.ResetReloadFlag();

        // Assert
        Assert.IsFalse(mockCache.ShouldReload());
    }

    [TestMethod]
    public void MockAppCache_Win32s_InitiallyEmpty()
    {
        // Arrange
        var mockCache = new MockAppCache();

        // Act & Assert
        Assert.IsNotNull(mockCache.Win32s);
        Assert.AreEqual(0, mockCache.Win32s.Count);
    }

    [TestMethod]
    public void MockAppCache_UWPs_InitiallyEmpty()
    {
        // Arrange
        var mockCache = new MockAppCache();

        // Act & Assert
        Assert.IsNotNull(mockCache.UWPs);
        Assert.AreEqual(0, mockCache.UWPs.Count);
    }

    [TestMethod]
    public void MockAppCache_CanAddWin32Programs()
    {
        // Arrange
        var mockCache = new MockAppCache();
        var testApp = TestDataHelper.CreateTestWin32Program("Notepad", "C:\\Windows\\System32\\notepad.exe");

        // Act
        mockCache.Win32s.Add(testApp);

        // Assert
        Assert.AreEqual(1, mockCache.Win32s.Count);
        Assert.AreEqual("Notepad", mockCache.Win32s.First().Name);
    }

    [TestMethod]
    public void MockAppCache_CanAddUWPApplications()
    {
        // Arrange
        var mockCache = new MockAppCache();
        var testApp = TestDataHelper.CreateTestUWPApplication("Calculator");

        // Act
        mockCache.UWPs.Add(testApp);

        // Assert
        Assert.AreEqual(1, mockCache.UWPs.Count);
        Assert.AreEqual("Calculator", mockCache.UWPs.First().DisplayName);
    }

    [TestMethod]
    public void MockAppCache_Dispose_DoesNotThrow()
    {
        // Arrange
        var mockCache = new MockAppCache();

        // Act & Assert - should not throw
        mockCache.Dispose();
    }
}
