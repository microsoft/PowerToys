// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;

using Castle.Core.Internal;
using Microsoft.Plugin.Program.Programs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Plugin.Common.Win32;

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
            Assert.IsTrue(Array.FindAll(applications, x => x.Name == "DevelopmentApp").Length > 0);
            Assert.IsTrue(Array.FindAll(applications, x => x.Name == "PackagedApp").Length > 0);
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
            Assert.IsTrue(Array.FindAll(applications, x => x.Name == "PackagedApp").Length > 0);
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

        [TestMethod]
        public void UwpResultActionShouldHandleEmptyUserModelIdGracefully()
        {
            // Arrange
            string displayName = "PackagedApp";
            string emptyUserModelId = string.Empty;
            string logoUri = "Assets\\Logo.png";
            string description = "Description";
            string backgroundColor = "transparent";
            string entryPoint = string.Empty;

            StringMatcher.Instance = new StringMatcher();

            var manifestApp = new Mock<IAppxManifestApplication>();
            manifestApp.Setup(x => x.GetAppUserModelId(out emptyUserModelId)).Returns(HRESULT.S_OK);
            manifestApp.Setup(x => x.GetStringValue("DisplayName", out displayName)).Returns(HRESULT.S_OK);
            manifestApp.Setup(x => x.GetStringValue("Description", out description)).Returns(HRESULT.S_OK);
            manifestApp.Setup(x => x.GetStringValue("BackgroundColor", out backgroundColor)).Returns(HRESULT.S_OK);
            manifestApp.Setup(x => x.GetStringValue("EntryPoint", out entryPoint)).Returns(HRESULT.S_OK);
            manifestApp.Setup(x => x.GetStringValue("Square44x44Logo", out logoUri)).Returns(HRESULT.S_OK);

            var package = new UWP(PackagedApp)
            {
                Location = PackagedApp.InstalledLocation,
                LocationLocalized = PackagedApp.InstalledLocation,
                Version = UWP.PackageVersion.Windows10,
            };

            var application = new UWPApplication(manifestApp.Object, package)
            {
                DisplayName = displayName,
                Description = description,
                UserModelId = string.Empty,
            };
            using var showMessageCalled = new ManualResetEventSlim();
            var api = new Mock<IPublicAPI>();
            api.Setup(x => x.ShowMsg(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Callback<string, string, string, bool>((_, _, _, _) => showMessageCalled.Set());

            // Assert
            Assert.AreEqual(displayName, application.DisplayName);
            Assert.AreEqual(description, application.Description);

            // Act
            var result = application.Result(displayName, string.Empty, api.Object);

            // Assert
            Assert.IsNotNull(result);
            var actionResult = result.Action(new ActionContext());
            Assert.IsTrue(actionResult);
            Assert.IsTrue(showMessageCalled.Wait(TimeSpan.FromSeconds(5)));
            api.Verify(
                x => x.ShowMsg(
                    It.IsAny<string>(),
                    It.Is<string>(message => message.Contains(displayName, StringComparison.Ordinal)),
                    string.Empty,
                    It.IsAny<bool>()),
                Times.Once);
        }
    }
}
