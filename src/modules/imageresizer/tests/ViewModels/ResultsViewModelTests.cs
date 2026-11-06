// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using ImageResizer.Models;
using ImageResizer.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ImageResizer.ViewModels
{
    [TestClass]
    public class ResultsViewModelTests
    {
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void CloseRetainsPresentedErrorsWithoutRetry(bool empty)
        {
            var errors = empty
                ? Array.Empty<ResizeError>()
                : new[] { new ResizeError("first.jpg", "Cannot decode"), new ResizeError("second.png", "Cannot write") };
            var view = new Mock<IMainView>(MockBehavior.Strict);
            view.Setup(item => item.Close());
            var results = new ResultsViewModel(view.Object, errors);

            CollectionAssert.AreEqual(errors.Select(item => item.File).ToArray(), results.Errors.Select(item => item.File).ToArray());
            CollectionAssert.AreEqual(errors.Select(item => item.Error).ToArray(), results.Errors.Select(item => item.Error).ToArray());

            results.CloseCommand.Execute(null);

            view.Verify(item => item.Close(), Times.Once);
            view.VerifyNoOtherCalls();
            CollectionAssert.AreEqual(errors, results.Errors.ToArray());
        }
    }
}
