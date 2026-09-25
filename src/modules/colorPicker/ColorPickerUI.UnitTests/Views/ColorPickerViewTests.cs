// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ColorPicker.ViewModelContracts;
using ColorPicker.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ColorPicker.UnitTests.Views
{
    [TestClass]
    public class ColorPickerViewTests
    {
        [DataTestMethod]
        [DataRow(nameof(IMainViewModel.ColorName))]
        [DataRow(nameof(IMainViewModel.ShowColorName))]
        public void InvalidatesDesiredSize_returns_true_for_sizing_properties(string propertyName)
        {
            Assert.IsTrue(ColorPickerView.InvalidatesDesiredSize(propertyName));
        }

        [DataTestMethod]
        [DataRow(nameof(IMainViewModel.ColorText))]
        [DataRow(nameof(IMainViewModel.ColorBrush))]
        [DataRow(null)]
        public void InvalidatesDesiredSize_returns_false_for_other_properties(string propertyName)
        {
            Assert.IsFalse(ColorPickerView.InvalidatesDesiredSize(propertyName));
        }
    }
}
