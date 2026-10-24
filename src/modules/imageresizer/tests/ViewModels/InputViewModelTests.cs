// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
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
    public sealed class InputViewModelTests : IDisposable
    {
        private Func<string, string> _originalGetString;
        private string _originalSettingsPath;
        private AiAvailabilityState _originalAiAvailabilityState;
        private TestDirectory _directory;

        [TestInitialize]
        public void Initialize()
        {
            _originalAiAvailabilityState = App.AiAvailabilityState;
            _originalGetString = ResourceLoaderInstance.GetString;
            _originalSettingsPath = Settings.SettingsPath;
            ResourceLoaderInstance.GetString = key => key;
            _directory = new TestDirectory();
            Settings.SettingsPath = Path.Combine(_directory, "settings.json");
        }

        [TestCleanup]
        public void Cleanup()
        {
            App.AiAvailabilityState = _originalAiAvailabilityState;
            ResourceLoaderInstance.GetString = _originalGetString;
            Settings.SettingsPath = _originalSettingsPath;
            Dispose();
        }

        public void Dispose()
        {
            _directory?.Dispose();
            GC.SuppressFinalize(this);
        }

        [TestMethod]
        [DataRow("", false)]
        [DataRow("photo.jpg|gift.png|anim.gif.jpg|x.gif ", false)]
        [DataRow("anim.gif", true)]
        [DataRow("ANIM.GIF", true)]
        [DataRow(@"folder.GIF\photo.GiF", true)]
        [DataRow("a.jpg|b.gif|c.png", true)]
        [DataRow("a.gif|b.gif", true)]
        public void HasGifFilesUsesCaseInsensitiveFinalSuffix(string paths, bool expected)
        {
            var batch = new ResizeBatch();
            foreach (var path in paths.Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                batch.Files.Add(path);
            }

            var settings = new Settings();
            var viewModel = new InputViewModel(settings, new MainViewModel(batch, settings), Mock.Of<IMainView>(), batch);

            Assert.AreEqual(expected, viewModel.HasGifFiles);
        }

        [TestMethod]
        public void HasGifFilesSnapshotsSelectionAtConstruction()
        {
            var settings = new Settings();
            var batch = new ResizeBatch();
            batch.Files.Add("photo.jpg");
            var viewModel = new InputViewModel(settings, new MainViewModel(batch, settings), Mock.Of<IMainView>(), batch);

            batch.Files.Add("animation.gif");

            Assert.IsFalse(viewModel.HasGifFiles);
        }

        [TestMethod]
        public void SelectionChangesInvalidateCustomDecisionAndResizeCommand()
        {
            var settings = new Settings();
            var batch = new ResizeBatch();
            var viewModel = new InputViewModel(settings, new MainViewModel(batch, settings), Mock.Of<IMainView>(), batch);
            var notifications = new List<string>();
            var commandInvalidated = false;
            viewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
            viewModel.ResizeCommand.CanExecuteChanged += (_, _) => commandInvalidated = true;

            Assert.IsFalse(viewModel.IsCustomSizeSelected);
            foreach (var index in new[] { settings.Sizes.Count, 1 })
            {
                notifications.Clear();
                commandInvalidated = false;

                settings.SelectedSizeIndex = index;

                Assert.AreEqual(index == settings.Sizes.Count, viewModel.IsCustomSizeSelected);
                Assert.AreSame(index == settings.Sizes.Count ? settings.CustomSize : settings.Sizes[index], settings.SelectedSize);
                CollectionAssert.Contains(notifications, nameof(InputViewModel.IsCustomSizeSelected));
                CollectionAssert.Contains(notifications, nameof(InputViewModel.CanResize));
                Assert.IsTrue(commandInvalidated);
                Assert.IsTrue(viewModel.CanResize);
                Assert.IsTrue(viewModel.ResizeCommand.CanExecute(null));
            }

            notifications.Clear();
            commandInvalidated = false;
            settings.SelectedSizeIndex = 1;

            Assert.AreEqual(0, notifications.Count);
            Assert.IsFalse(commandInvalidated);
        }

        [TestMethod]
        [DataRow(AiAvailabilityState.NotSupported, true, false, false, false, false, true)]
        [DataRow(AiAvailabilityState.ModelNotReady, false, true, true, false, true, false)]
        [DataRow(AiAvailabilityState.Ready, false, true, false, true, true, true)]
        public void AiSelectionShouldReflectAvailabilityAndResetOnReturnToCustom(
            AiAvailabilityState availability,
            bool expectedCustomSelection,
            bool expectedDescriptionVisibility,
            bool expectedDownloadPrompt,
            bool expectedAiControls,
            bool expectedConfiguration,
            bool expectedCanResize)
        {
            App.AiAvailabilityState = availability;
            var settings = new Settings();
            settings.SelectedSizeIndex = settings.Sizes.Count;
            var batch = new ResizeBatch();
            var page = new object();
            var main = new MainViewModel(batch, settings) { CurrentPage = page };
            var view = new Mock<IMainView>(MockBehavior.Strict);
            var viewModel = new InputViewModel(settings, main, view.Object, batch);
            var notifications = new List<string>();
            var commandInvalidated = false;
            viewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
            viewModel.ResizeCommand.CanExecuteChanged += (_, _) => commandInvalidated = true;

            Assert.AreSame(settings.CustomSize, settings.SelectedSize);
            Assert.IsTrue(viewModel.IsCustomSizeSelected);
            Assert.IsFalse(viewModel.ShowAiSizeDescriptions);
            Assert.IsFalse(viewModel.ShowModelDownloadPrompt);
            Assert.IsFalse(viewModel.ShowAiControls);
            Assert.IsFalse(viewModel.ShowAiConfigurationSection);
            Assert.IsTrue(viewModel.CanResize);
            Assert.IsTrue(viewModel.ResizeCommand.CanExecute(null));

            foreach (var selectAi in new[] { true, false })
            {
                notifications.Clear();
                commandInvalidated = false;

                settings.SelectedSizeIndex = settings.Sizes.Count + (selectAi ? 1 : 0);

                if (selectAi && !expectedCustomSelection)
                {
                    Assert.AreSame(settings.AiSize, settings.SelectedSize);
                }
                else
                {
                    Assert.AreSame(settings.CustomSize, settings.SelectedSize);
                }

                Assert.AreEqual(!selectAi || expectedCustomSelection, viewModel.IsCustomSizeSelected);
                Assert.AreEqual(selectAi && expectedDescriptionVisibility, viewModel.ShowAiSizeDescriptions);
                Assert.AreEqual(selectAi && expectedDownloadPrompt, viewModel.ShowModelDownloadPrompt);
                Assert.AreEqual(selectAi && expectedAiControls, viewModel.ShowAiControls);
                Assert.AreEqual(selectAi && expectedConfiguration, viewModel.ShowAiConfigurationSection);
                Assert.AreEqual(!selectAi || expectedCanResize, viewModel.CanResize);
                Assert.AreEqual(!selectAi || expectedCanResize, viewModel.ResizeCommand.CanExecute(null));
                var expectedDescription = selectAi && !expectedCustomSelection ? "Input_AiUnknownSize" : string.Empty;
                Assert.AreEqual(expectedDescription, viewModel.CurrentResolutionDescription);
                Assert.AreEqual(expectedDescription, viewModel.NewResolutionDescription);
                foreach (var propertyName in new[]
                {
                    nameof(InputViewModel.IsCustomSizeSelected),
                    nameof(InputViewModel.CanResize),
                    nameof(InputViewModel.ShowAiSizeDescriptions),
                    nameof(InputViewModel.ShowModelDownloadPrompt),
                    nameof(InputViewModel.ShowAiControls),
                    nameof(InputViewModel.ShowAiConfigurationSection),
                })
                {
                    CollectionAssert.Contains(notifications, propertyName);
                }

                Assert.IsTrue(commandInvalidated);
                Assert.AreSame(page, main.CurrentPage);
                Assert.IsFalse(File.Exists(Settings.SettingsPath));
                view.VerifyNoOtherCalls();
            }
        }

        [TestMethod]
        public void ClearingBatchShouldKeepMultipleSelectionDescriptionsHidden()
        {
            App.AiAvailabilityState = AiAvailabilityState.Ready;
            var settings = new Settings();
            settings.SelectedSizeIndex = settings.Sizes.Count;
            var batch = new ResizeBatch();
            batch.Files.Add("first-must-not-be-opened.jpg");
            batch.Files.Add("second-must-not-be-opened.jpg");
            var page = new object();
            var main = new MainViewModel(batch, settings) { CurrentPage = page };
            var view = new Mock<IMainView>(MockBehavior.Strict);
            var viewModel = new InputViewModel(settings, main, view.Object, batch);

            batch.Files.Clear();
            settings.SelectedSizeIndex = settings.Sizes.Count + 1;

            Assert.AreSame(settings.AiSize, settings.SelectedSize);
            Assert.IsFalse(viewModel.ShowAiSizeDescriptions);
            Assert.AreEqual(string.Empty, viewModel.CurrentResolutionDescription);
            Assert.AreEqual(string.Empty, viewModel.NewResolutionDescription);
            Assert.AreSame(page, main.CurrentPage);
            Assert.IsFalse(File.Exists(Settings.SettingsPath));
            view.VerifyNoOtherCalls();
        }

        [TestMethod]
        [DataRow(-1, false)]
        [DataRow(100, false)]
        [DataRow(0, true)]
        public void CustomFallbackRemainsResizable(int index, bool clearPresets)
        {
            var settings = new Settings();
            if (clearPresets)
            {
                settings.Sizes.Clear();
            }

            settings.SelectedSizeIndex = index;
            var batch = new ResizeBatch();
            var viewModel = new InputViewModel(settings, new MainViewModel(batch, settings), Mock.Of<IMainView>(), batch);

            Assert.IsTrue(viewModel.IsCustomSizeSelected);
            Assert.AreSame(settings.CustomSize, settings.SelectedSize);
            Assert.IsTrue(viewModel.ResizeCommand.CanExecute(null));
        }

        [TestMethod]
        [DataRow(InputViewModel.Dimension.Width, 400.0, 400.0, 640.0)]
        [DataRow(InputViewModel.Dimension.Height, 300.0, 1024.0, 300.0)]
        [DataRow(InputViewModel.Dimension.Width, 0.0, 0.0, 640.0)]
        [DataRow(InputViewModel.Dimension.Width, 2.54, 2.54, 640.0)]
        [DataRow((InputViewModel.Dimension)99, 300.0, 1024.0, 640.0)]
        public void EnterCommitsOnlyRequestedDimension(InputViewModel.Dimension dimension, double value, double width, double height)
        {
            var settings = new Settings();
            settings.SelectedSizeIndex = settings.Sizes.Count;
            var batch = new ResizeBatch();
            var page = new object();
            var main = new MainViewModel(batch, settings) { CurrentPage = page };
            var view = new Mock<IMainView>(MockBehavior.Strict);
            var viewModel = new InputViewModel(settings, main, view.Object, batch);

            viewModel.EnterKeyPressedCommand.Execute(new InputViewModel.KeyPressParams { Dimension = dimension, Value = value });

            Assert.AreEqual(width, settings.CustomSize.Width);
            Assert.AreEqual(height, settings.CustomSize.Height);
            Assert.AreSame(page, main.CurrentPage);
            Assert.IsFalse(File.Exists(Settings.SettingsPath));
            view.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void ResizeSaveFailurePreservesPageAndDoesNotClose()
        {
            var settings = new Settings();
            var batch = new ResizeBatch();
            batch.Files.Add("must-not-be-processed.jpg");
            var page = new object();
            var main = new MainViewModel(batch, settings) { CurrentPage = page };
            var view = new Mock<IMainView>(MockBehavior.Strict);
            var viewModel = new InputViewModel(settings, main, view.Object, batch);
            var blocker = Path.Combine(_directory, "blocked-parent");
            File.WriteAllText(blocker, "owned blocker");
            var originalSettingsPath = Settings.SettingsPath;
            try
            {
                Settings.SettingsPath = Path.Combine(blocker, "settings.json");

                Assert.ThrowsExactly<IOException>(() => viewModel.ResizeCommand.Execute(null));

                Assert.AreSame(page, main.CurrentPage);
                view.VerifyNoOtherCalls();
                Assert.AreEqual("owned blocker", File.ReadAllText(blocker));
                Assert.AreEqual(1, Directory.GetFiles(_directory).Length);
                Assert.AreEqual(0, Directory.GetDirectories(_directory).Length);
            }
            finally
            {
                Settings.SettingsPath = originalSettingsPath;
            }
        }

        [TestMethod]
        [DataRow(1)]
        [DataRow(2)]
        public void CancelClosesWithoutSavingOrChangingPage(int invocationCount)
        {
            var settings = new Settings();
            settings.CustomSize.Width = 321;
            var batch = new ResizeBatch();
            var page = new object();
            var main = new MainViewModel(batch, settings) { CurrentPage = page };
            var view = new Mock<IMainView>(MockBehavior.Strict);
            view.Setup(item => item.Close());
            var viewModel = new InputViewModel(settings, main, view.Object, batch);

            for (var invocation = 1; invocation <= invocationCount; invocation++)
            {
                viewModel.CancelCommand.Execute(null);

                view.Verify(item => item.Close(), Times.Exactly(invocation));
                Assert.AreSame(page, main.CurrentPage);
                Assert.IsFalse(File.Exists(Settings.SettingsPath));
            }

            view.VerifyNoOtherCalls();
        }
    }
}
