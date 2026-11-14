// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ImageResizer.Helpers;
using ImageResizer.Models;
using ImageResizer.Properties;
using ImageResizer.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ImageResizer.ViewModels
{
    [TestClass]
    [DoNotParallelize]
    public class MainViewModelTests
    {
        private static readonly string[] SelectionPaths =
        {
            @"C:\selection\first.jpg",
            @"C:\selection\second.GiF",
            @"C:\selection\third.png",
        };

        private Func<string, string> _originalGetString;

        [TestInitialize]
        public void Initialize()
        {
            _originalGetString = ResourceLoaderInstance.GetString;
            ResourceLoaderInstance.GetString = key => key;
        }

        [TestCleanup]
        public void Cleanup()
        {
            ResourceLoaderInstance.GetString = _originalGetString;
        }

        [TestMethod]
        [DataRow(false, 1)]
        [DataRow(false, 3)]
        [DataRow(true, 0)]
        [DataRow(true, 1)]
        [DataRow(true, 3)]
        public async Task LoadPreservesSelectionOrderAndOnlyPicksWhenEmpty(bool usePicker, int count)
        {
            var paths = SelectionPaths.Take(count).ToArray();
            var settings = new Settings();
            var batch = new ResizeBatch();
            var view = new Mock<IMainView>(MockBehavior.Strict);
            if (usePicker)
            {
                view.Setup(item => item.OpenPictureFilesAsync()).ReturnsAsync((IEnumerable<string>)paths);
            }
            else
            {
                foreach (var path in paths)
                {
                    batch.Files.Add(path);
                }
            }

            var main = new MainViewModel(batch, settings);

            await main.LoadAsync(view.Object);

            CollectionAssert.AreEqual(paths, batch.Files.ToArray());
            Assert.IsInstanceOfType(main.CurrentPage, typeof(InputViewModel));
            var input = (InputViewModel)main.CurrentPage;
            Assert.AreSame(settings, input.Settings);
            Assert.AreEqual(count == 3, input.HasGifFiles);
            if (usePicker)
            {
                view.Verify(item => item.OpenPictureFilesAsync(), Times.Once);
            }

            view.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task PickerFailurePropagatesWithoutReplacingPage()
        {
            var settings = new Settings();
            var batch = new ResizeBatch();
            var page = new object();
            var main = new MainViewModel(batch, settings) { CurrentPage = page };
            var error = new InvalidOperationException("Picker unavailable");
            var view = new Mock<IMainView>(MockBehavior.Strict);
            view.Setup(item => item.OpenPictureFilesAsync()).Returns(Task.FromException<IEnumerable<string>>(error));

            try
            {
                await main.LoadAsync(view.Object);
                Assert.Fail("Expected picker failure to propagate.");
            }
            catch (InvalidOperationException actual)
            {
                Assert.AreSame(error, actual);
            }

            Assert.AreSame(page, main.CurrentPage);
            Assert.AreEqual(0, batch.Files.Count);
            view.Verify(item => item.OpenPictureFilesAsync(), Times.Once);
            view.VerifyNoOtherCalls();
        }
    }
}
