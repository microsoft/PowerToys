// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using HostsUILib.Helpers;
using HostsUILib.Settings;
using HostsUILib.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Hosts.Tests
{
    [TestClass]
    public class MainViewModelHostTest
    {
        [TestMethod]
        public void DisabledByPolicy_DoesNotReadHostsFile()
        {
            var hostsService = new Mock<IHostsService>();
            var viewModel = CreateViewModel(hostsService, new FakeElevationHelper(isElevated: true), isDisabledByPolicy: true);

            viewModel.ReadHosts();

            Assert.IsTrue(viewModel.IsDisabledByPolicy);
            Assert.IsFalse(viewModel.ShowElevationPrompt);
            hostsService.Verify(s => s.ReadAsync(), Times.Never);
        }

        [TestMethod]
        public void OptionalHostMembers_CanBeOmitted()
        {
            var viewModel = CreateViewModel(new Mock<IHostsService>(), new FakeElevationHelper(isElevated: false), isDisabledByPolicy: false);

            Assert.IsFalse(viewModel.CanOpenSettings);
            Assert.IsFalse(viewModel.CanQuit);
            Assert.IsFalse(viewModel.ShowElevationPrompt);

            viewModel.OpenSettings();
            viewModel.Quit();
            viewModel.RequestElevation();
        }

        [TestMethod]
        public void NotElevated_HostCanElevate_ShowsPromptAndRequestsElevation()
        {
            var elevationHelper = new FakeElevationHelper(isElevated: false, canRequestElevation: true);
            var viewModel = CreateViewModel(new Mock<IHostsService>(), elevationHelper, isDisabledByPolicy: false);

            Assert.IsTrue(viewModel.ShowElevationPrompt);

            viewModel.RequestElevationCommand.Execute(null);

            Assert.AreEqual(1, elevationHelper.RequestCount);
        }

        [TestMethod]
        public void Elevated_HidesElevationPrompt()
        {
            var viewModel = CreateViewModel(new Mock<IHostsService>(), new FakeElevationHelper(isElevated: true, canRequestElevation: true), isDisabledByPolicy: false);

            Assert.IsFalse(viewModel.ShowElevationPrompt);
        }

        [TestMethod]
        public void HostDelegates_AreInvoked()
        {
            var settingsOpened = 0;
            var quit = 0;
            var viewModel = new MainViewModel(
                new Mock<IHostsService>().Object,
                new Mock<IUserSettings>().Object,
                new Mock<IDuplicateService>().Object,
                null,
                () => settingsOpened++,
                new FakeElevationHelper(isElevated: true),
                () => quit++,
                () => false,
                null);

            Assert.IsTrue(viewModel.CanOpenSettings);
            Assert.IsTrue(viewModel.CanQuit);

            viewModel.OpenSettingsCommand.Execute(null);
            viewModel.Quit();

            Assert.AreEqual(1, settingsOpened);
            Assert.AreEqual(1, quit);
        }

        private static MainViewModel CreateViewModel(Mock<IHostsService> hostsService, IElevationHelper elevationHelper, bool isDisabledByPolicy)
        {
            return new MainViewModel(
                hostsService.Object,
                new Mock<IUserSettings>().Object,
                new Mock<IDuplicateService>().Object,
                null,
                null,
                elevationHelper,
                null,
                () => isDisabledByPolicy,
                null);
        }

        private sealed class FakeElevationHelper(bool isElevated, bool canRequestElevation = false) : IElevationHelper
        {
            public int RequestCount { get; private set; }

            public bool IsElevated => isElevated;

            public bool CanRequestElevation => canRequestElevation;

            public void RequestElevation() => RequestCount++;
        }
    }
}
