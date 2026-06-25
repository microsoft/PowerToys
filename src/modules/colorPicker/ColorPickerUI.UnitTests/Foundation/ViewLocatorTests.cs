// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using ColorPicker.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ColorPicker.UnitTests.Foundation
{
    [TestClass]
    public class ViewLocatorTests
    {
        private sealed class FakeViewModel
        {
        }

        private sealed class OtherViewModel
        {
        }

        [TestMethod]
        public void Resolve_invokes_the_factory_registered_for_the_runtime_type()
        {
            // The factory returns null (UIElement is a reference type) so the test
            // does NOT instantiate a WinUI control — the MSTest host has no XAML
            // runtime. We assert on dispatch behavior (which factory ran, how often).
            var locator = new ViewLocator();
            int fakeCalls = 0;
            int otherCalls = 0;
            locator.Register<FakeViewModel>(() =>
            {
                fakeCalls++;
                return null;
            });
            locator.Register<OtherViewModel>(() =>
            {
                otherCalls++;
                return null;
            });

            locator.Resolve(new FakeViewModel());
            locator.Resolve(new FakeViewModel());

            Assert.AreEqual(2, fakeCalls, "the FakeViewModel factory must run once per Resolve");
            Assert.AreEqual(0, otherCalls, "no other factory may run");
        }

        [TestMethod]
        [ExpectedException(typeof(KeyNotFoundException))]
        public void Resolve_throws_for_an_unregistered_viewmodel_type()
        {
            var locator = new ViewLocator();
            locator.Resolve(new FakeViewModel());
        }
    }
}
