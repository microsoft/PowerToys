// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class ShellViewModelTests
    {
        [TestMethod]
        public void GetPageDisplayName_ReturnsFullName_ForKnownType()
        {
            string actual = ShellViewModel.GetPageDisplayName(typeof(ShellViewModelTests));

            Assert.AreEqual(typeof(ShellViewModelTests).FullName, actual);
        }

        [TestMethod]
        public void GetPageDisplayName_ReturnsPlaceholder_ForNullType()
        {
            // NavigationFailedEventArgs.SourcePageType can be null when navigation fails
            // before the runtime can resolve the requested page type. The handler must
            // tolerate this without throwing NullReferenceException.
            string actual = ShellViewModel.GetPageDisplayName(null);

            Assert.AreEqual("<unknown>", actual);
        }
    }
}
