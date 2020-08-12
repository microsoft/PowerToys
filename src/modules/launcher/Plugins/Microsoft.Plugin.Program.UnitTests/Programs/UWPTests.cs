using Castle.Core.Internal;
using Microsoft.Plugin.Program.Programs;
using Moq;
using NUnit.Framework;
using System.Collections.Generic;

namespace Microsoft.Plugin.Program.UnitTests.Programs
{
    [TestFixture]
    public class UWPTests
    {
        static readonly PackageWrapper developmentModeApp = new PackageWrapper(
            "DevelopmentApp",
            "DevelopmentApp",
            "DevelopmentApp",
            false,
            true,
            "AppxManifests/DevelopmentApp"
        );

        static readonly PackageWrapper frameworkApp = new PackageWrapper(
            "FrameworkApp",
            "FrameworkApp",
            "FrameworkApp",
            true,
            false,
            "AppxManifests/FrameworkApp"
        );

        static readonly PackageWrapper packagedApp = new PackageWrapper(
            "PackagedApp",
            "PackagedApp",
            "PackagedApp",
            false,
            false,
            "AppxManifests/PackagedApp"
        );

        [Test]
        public void AllShouldReturnPackagesWithDevelopmentModeWhenCalled()
        {
            // Arrange
            Main._settings = new ProgramPluginSettings();
            List<IPackage> packages = new List<IPackage>() { developmentModeApp, packagedApp };
            var mock = new Mock<IPackageManager>();
            mock.Setup(x => x.FindPackagesForCurrentUser()).Returns(packages);
            UWP.PackageManagerWrapper = mock.Object;

            // Act
            var applications = UWP.All();

            // Assert
            Assert.AreEqual(applications.Length, 2);
            Assert.IsTrue(applications.FindAll(x => x.Name == "DevelopmentApp").Length > 0);
            Assert.IsTrue(applications.FindAll(x => x.Name == "PackagedApp").Length > 0);
        }

        [Test]
        public void AllShouldNotReturnPackageFrameworksWhenCalled()
        {
            // Arrange
            Main._settings = new ProgramPluginSettings();
            List<IPackage> packages = new List<IPackage>() { frameworkApp, packagedApp };
            var mock = new Mock<IPackageManager>();
            mock.Setup(x => x.FindPackagesForCurrentUser()).Returns(packages);
            UWP.PackageManagerWrapper = mock.Object;

            // Act
            var applications = UWP.All();

            // Assert
            Assert.AreEqual(applications.Length, 1);
            Assert.IsTrue(applications.FindAll(x => x.Name == "PackagedApp").Length > 0);
        }

        [Test]
        public void PowerToysRunShouldNotAddInvalidAppWhenIndexingUWPApplications()
        {
            // Arrange
            PackageWrapper invalidPackagedApp = new PackageWrapper();
            Main._settings = new ProgramPluginSettings();
            List<IPackage> packages = new List<IPackage>() { invalidPackagedApp };
            var mock = new Mock<IPackageManager>();
            mock.Setup(x => x.FindPackagesForCurrentUser()).Returns(packages);
            UWP.PackageManagerWrapper = mock.Object;

            // Act
            var applications = UWP.All();

            // Assert
            Assert.AreEqual(applications.Length, 0);
        }
    }
}
