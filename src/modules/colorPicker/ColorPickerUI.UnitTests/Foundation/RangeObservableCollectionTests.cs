// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Specialized;
using ColorPicker.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ColorPicker.UnitTests.Foundation
{
    [TestClass]
    public class RangeObservableCollectionTests
    {
        private static readonly int[] _inputItems = new[] { 1, 2, 3 };
        private static readonly int[] _expectedItems = new[] { 1, 2, 3 };

        [TestMethod]
        public void AddRange_adds_all_items_and_raises_a_single_reset()
        {
            var sut = new RangeObservableCollection<int>();
            int notifications = 0;
            sut.CollectionChanged += (_, e) =>
            {
                notifications++;
                Assert.AreEqual(NotifyCollectionChangedAction.Reset, e.Action);
            };

            sut.AddRange(_inputItems);

            CollectionAssert.AreEqual(_expectedItems, sut);
            Assert.AreEqual(1, notifications);
        }
    }
}
