// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class ProfileEditorViewModelTests
    {
        [TestMethod]
        public void CreateProfile_DefaultProfileId_ReturnsEmptyUntilSaved()
        {
            var viewModel = new ProfileEditorViewModel(
                new ObservableCollection<MonitorInfo>(),
                "New profile");

            var profile = viewModel.CreateProfile();

            Assert.AreEqual(Guid.Empty, profile.Id);
        }

        [TestMethod]
        public void CreateProfile_ExistingProfileId_PreservesId()
        {
            var profileId = Guid.Parse("9d5a932a-1170-4fdc-998d-ac6be7eea8c3");
            var viewModel = new ProfileEditorViewModel(
                new ObservableCollection<MonitorInfo>(),
                "Existing profile",
                profileId);

            var profile = viewModel.CreateProfile();

            Assert.AreEqual(profileId, profile.Id);
        }
    }
}
