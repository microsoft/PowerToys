// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Castle.Core.Internal;
using Microsoft.Plugin.Program.Programs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.Plugin.Program.UnitTests.Programs
{
    [TestClass]
    public class UWPTests
    {
        private static readonly PackageWrapper DevelopmentModeApp = new PackageWrapper(
            "DevelopmentApp",
            "DevelopmentApp",
            "DevelopmentApp",
            false,
            true,
            "AppxManifests/DevelopmentApp");

        private static readonly PackageWrapper FrameworkApp = new PackageWrapper(
            "FrameworkApp",
            "FrameworkApp",
            "FrameworkApp",
            true,
            false,
            "AppxManifests/FrameworkApp");

        private static readonly PackageWrapper PackagedApp = new PackageWrapper(
            "PackagedApp",
            "PackagedApp",
            "PackagedApp",
            false,
            false,
            "AppxManifests/PackagedApp");

        [TestMethod]
        public void AllShouldReturnPackagesWithDevelopmentModeWhenCalled()
        {
            // Arrange
            Main.Settings = new ProgramPluginSettings();
            List<IPackage> packages = new List<IPackage>() { DevelopmentModeApp, PackagedApp };
            var mock = new Mock<IPackageManager>();
            mock.Setup(x => x.FindPackagesForCurrentUser()).Returns(packages);
            UWP.PackageManagerWrapper = mock.Object;

            // Act
            var applications = UWP.All();

            // Assert
            Assert.AreEqual(2, applications.Length);
            Assert.IsTrue(applications.FindAll(x => x.Name == "DevelopmentApp").Length > 0);
            Assert.IsTrue(applications.FindAll(x => x.Name == "PackagedApp").Length > 0);
        }

        [TestMethod]
        public void AllShouldNotReturnPackageFrameworksWhenCalled()
        {
            // Arrange
            Main.Settings = new ProgramPluginSettings();
            List<IPackage> packages = new List<IPackage>() { FrameworkApp, PackagedApp };
            var mock = new Mock<IPackageManager>();
            mock.Setup(x => x.FindPackagesForCurrentUser()).Returns(packages);
            UWP.PackageManagerWrapper = mock.Object;

            // Act
            var applications = UWP.All();

            // Assert
            Assert.AreEqual(1, applications.Length);
            Assert.IsTrue(applications.FindAll(x => x.Name == "PackagedApp").Length > 0);
        }

        [TestMethod]
        public void PowerToysRunShouldNotAddInvalidAppWhenIndexingUWPApplications()
        {
            // Arrange
            PackageWrapper invalidPackagedApp = new PackageWrapper();
            Main.Settings = new ProgramPluginSettings();
            List<IPackage> packages = new List<IPackage>() { invalidPackagedApp };
            var mock = new Mock<IPackageManager>();
            mock.Setup(x => x.FindPackagesForCurrentUser()).Returns(packages);
            UWP.PackageManagerWrapper = mock.Object;

            // Act
            var applications = UWP.All();

            // Assert
            Assert.AreEqual(0, applications.Length);
        }
    }
}
